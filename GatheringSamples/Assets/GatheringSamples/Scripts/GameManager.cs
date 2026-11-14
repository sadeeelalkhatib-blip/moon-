using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace GatheringSamples
{
    public enum EVAState
    {
        Briefing,
        Playing,
        Paused,
        Guide,
        Success,
        Failure
    }

    public class SampleRecord
    {
        public SiteDef Site;
        public string ToolSummary;
        public float MissionTime;
    }

    /// <summary>
    /// Boots the runtime-built lunar surface and owns the complete EVA loop:
    /// movement, oxygen, mission clock, boundaries, tool use, sample verification,
    /// HUD state, success/failure and the field guide.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public WorldBuilder World { get; private set; }
        public AstronautRig Astronaut { get; private set; }
        public CameraRig CameraRig { get; private set; }
        public AudioManager Audio { get; private set; }
        public GameUI UI { get; private set; }

        public EVAState State { get; private set; } = EVAState.Briefing;
        public float Oxygen => oxygen;
        public float MissionElapsed => missionElapsed;
        public float TimeRemaining => Mathf.Max(0f, Sim.MissionDuration - missionElapsed);
        public string SelectedToolId => selectedTool;
        public string FailureReason => failureReason;
        public int CollectedCount => samples.Count;
        public int WrongToolAttempts => wrongToolAttempts;
        public IReadOnlyList<SampleRecord> Samples => samples;
        public Transform Player => Astronaut != null ? Astronaut.transform : null;

        private sealed class SiteRuntime
        {
            public SiteDef Def;
            public StationView View;
            public int Stage;
            public float CollectProgress;
            public bool Collecting;
            public bool Verified;
        }

        private readonly Dictionary<string, SiteRuntime> siteRuntimes = new Dictionary<string, SiteRuntime>();
        private readonly List<SampleRecord> samples = new List<SampleRecord>();

        private SiteRuntime activeSite;
        private SiteRuntime collectingSite;
        private string selectedTool = "scoop";
        private float oxygen = Sim.OxygenMax;
        private float missionElapsed;
        private float boundaryTimer;
        private float footstepTimer;
        private float verticalVelocity;
        private Vector3 planarVelocity;
        private bool wasGrounded;
        private bool isMoving;
        private bool isLoping;
        private bool jumpRequested;
        private bool warnedLowOxygen;
        private bool warnedCriticalOxygen;
        private bool warnedLowTime;
        private int wrongToolAttempts;
        private string failureReason = string.Empty;
        private EVAState guideReturnState = EVAState.Briefing;
        private bool built;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Application.targetFrameRate = 60;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Time.timeScale = 1f;
            BuildGame();
        }

        private void BuildGame()
        {
            World = new GameObject("World Builder").AddComponent<WorldBuilder>();
            World.Build();

            Vector3 spawn = new Vector3(0f, WorldBuilder.TerrainHeight(0f, 7.5f) + 0.18f, 7.5f);
            GameObject playerObject = new GameObject("Astronaut");
            playerObject.transform.position = spawn;
            playerObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Astronaut = playerObject.AddComponent<AstronautRig>();
            Astronaut.Build();
            Astronaut.SetTool(selectedTool);

            GameObject cameraObject = new GameObject("Camera Rig");
            CameraRig = cameraObject.AddComponent<CameraRig>();
            CameraRig.Build(Astronaut.transform);
            CameraRig.Yaw = Mathf.PI;
            CameraRig.SnapToTarget();

            GameObject audioObject = new GameObject("Audio Manager");
            Audio = audioObject.AddComponent<AudioManager>();
            Audio.Init();
            Audio.StartAmbient();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemObject = new GameObject("Event System");
                eventSystemObject.AddComponent<EventSystem>();
                eventSystemObject.AddComponent<InputSystemUIInputModule>();
            }

            ResetSiteRuntimes();

            GameObject uiObject = new GameObject("Game UI");
            UI = uiObject.AddComponent<GameUI>();
            UI.Build(this);

            SetWorldLabelCameras();

            State = EVAState.Briefing;
            UI.ShowBriefing(true);
            UI.ShowPause(false);
            UI.ShowGuide(false);
            UI.ShowEnd(false, false, string.Empty, string.Empty);
            UI.SetSelectedTool(selectedTool);
            UI.UpdateHud(TimeRemaining, oxygen, missionElapsed, samples.Count, Sim.SampleCount, BuildManifestText(), BuildObjectiveText(), selectedTool);
            RefreshBoundaryWarning();
            RefreshInteractionPrompt();
            built = true;
        }

        private void ResetSiteRuntimes()
        {
            siteRuntimes.Clear();
            foreach (KeyValuePair<string, StationView> pair in World.Stations)
            {
                SiteRuntime runtime = new SiteRuntime
                {
                    Def = pair.Value.Site,
                    View = pair.Value
                };
                runtime.View.SetState("pending");
                siteRuntimes[pair.Key] = runtime;
            }
        }

        private void SetWorldLabelCameras()
        {
            if (CameraRig == null || CameraRig.Cam == null) return;
            WorldLabel[] labels = FindObjectsByType<WorldLabel>(FindObjectsSortMode.None);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] != null && labels[i].Canvas != null)
                {
                    labels[i].Canvas.worldCamera = CameraRig.Cam;
                }
            }
        }

        private void Update()
        {
            if (!built) return;

            HandleGlobalInput();
            float deltaTime = Mathf.Min(Time.deltaTime, 0.05f);

            if (State == EVAState.Briefing)
            {
                CameraRig.Yaw += deltaTime * 0.045f;
                World.UpdateWorld(deltaTime, Astronaut.transform.position, false);
                RefreshUI();
                return;
            }

            if (State != EVAState.Playing)
            {
                World.UpdateWorld(deltaTime * 0.25f, Astronaut.transform.position, false);
                return;
            }

            UpdatePlayer(deltaTime);
            if (State != EVAState.Playing) return;

            UpdateMission(deltaTime);
            if (State != EVAState.Playing) return;

            UpdateInteraction(deltaTime);
            if (State != EVAState.Playing) return;

            World.UpdateWorld(deltaTime, Astronaut.transform.position, isMoving);
            RefreshUI();
        }

        private void HandleGlobalInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (State == EVAState.Guide)
                {
                    CloseGuide();
                }
                else if (State == EVAState.Paused)
                {
                    Resume();
                }
                else if (State == EVAState.Playing)
                {
                    Pause();
                }

                return;
            }

            if ((keyboard.tabKey.wasPressedThisFrame || keyboard.gKey.wasPressedThisFrame) &&
                (State == EVAState.Briefing || State == EVAState.Playing || State == EVAState.Paused ||
                 State == EVAState.Guide || State == EVAState.Success || State == EVAState.Failure))
            {
                ToggleFieldGuide();
                return;
            }

            if (State == EVAState.Briefing)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame ||
                    keyboard.nKey.wasPressedThisFrame)
                {
                    BeginMission();
                    return;
                }
            }

            HandleCameraInput();

            if (State != EVAState.Playing) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SelectTool(Content.Tools[0].id);
            if (keyboard.digit2Key.wasPressedThisFrame) SelectTool(Content.Tools[1].id);
            if (keyboard.digit3Key.wasPressedThisFrame) SelectTool(Content.Tools[2].id);
            if (keyboard.digit4Key.wasPressedThisFrame) SelectTool(Content.Tools[3].id);
            if (keyboard.digit5Key.wasPressedThisFrame) SelectTool(Content.Tools[4].id);
            if (keyboard.digit6Key.wasPressedThisFrame) SelectTool(Content.Tools[5].id);

            if (keyboard.eKey.wasPressedThisFrame) TryInteract();
            if (keyboard.spaceKey.wasPressedThisFrame) jumpRequested = true;
        }

        private void HandleCameraInput()
        {
            if (State == EVAState.Guide) return;

            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse.rightButton.isPressed || mouse.middleButton.isPressed ||
                (mouse.leftButton.isPressed && !overUi))
            {
                Vector2 delta = mouse.delta.ReadValue();
                CameraRig.Orbit(delta.x, delta.y);
            }

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                CameraRig.Zoom(-scroll * 0.0045f);
            }
        }

        private void UpdatePlayer(float deltaTime)
        {
            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;

            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }

            bool wantsToMove = input.sqrMagnitude > 0.001f;
            if (collectingSite != null && wantsToMove)
            {
                CancelCollection("Collection interrupted — hold position while sampling.");
            }

            if (input.sqrMagnitude > 1f) input.Normalize();

            Vector3 forward = new Vector3(Mathf.Sin(CameraRig.Yaw), 0f, Mathf.Cos(CameraRig.Yaw));
            Vector3 right = Vector3.Cross(forward, Vector3.up);
            Vector3 moveDirection = forward * input.y + right * input.x;
            isMoving = moveDirection.sqrMagnitude > 0.001f;
            isLoping = isMoving && keyboard != null &&
                       (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);

            float speed = isLoping ? Sim.LopeSpeed : Sim.WalkSpeed;

            if (isMoving)
            {
                moveDirection.Normalize();
                Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
                Astronaut.transform.rotation = Quaternion.Slerp(
                    Astronaut.transform.rotation, targetRotation, 1f - Mathf.Exp(-Sim.TurnRate * deltaTime));
            }
            else if (collectingSite != null)
            {
                Vector3 toSite = new Vector3(
                    collectingSite.Def.position.x, Astronaut.transform.position.y, collectingSite.Def.position.y)
                    - Astronaut.transform.position;
                Vector3 flat = new Vector3(toSite.x, 0f, toSite.z);
                if (flat.sqrMagnitude > 0.01f)
                {
                    Astronaut.transform.rotation = Quaternion.Slerp(
                        Astronaut.transform.rotation,
                        Quaternion.LookRotation(flat.normalized, Vector3.up),
                        1f - Mathf.Exp(-Sim.TurnRate * deltaTime));
                }
            }

            CharacterController controller = Astronaut.Controller;
            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = -2f;
                if (jumpRequested && collectingSite == null)
                {
                    verticalVelocity = Sim.JumpVelocity;
                    if (Audio != null) Audio.Hop();
                }
            }
            else
            {
                verticalVelocity -= Sim.Gravity * deltaTime;
            }
            jumpRequested = false;

            // Smooth planar acceleration/deceleration so starting and stopping a walk
            // blend naturally instead of snapping between zero and full speed.
            Vector3 targetVelocity = isMoving ? moveDirection * speed : Vector3.zero;
            float velocityBlend = 1f - Mathf.Exp(-Sim.MoveAcceleration * deltaTime);
            planarVelocity = Vector3.Lerp(planarVelocity, targetVelocity, velocityBlend);
            if (!isMoving && planarVelocity.sqrMagnitude < 0.0004f) planarVelocity = Vector3.zero;

            Vector3 motion = planarVelocity * deltaTime;
            motion.y = verticalVelocity * deltaTime;
            controller.Move(motion);

            bool grounded = controller.isGrounded;
            if (grounded && !wasGrounded && verticalVelocity < -2.5f && Audio != null)
            {
                Audio.Land();
            }
            wasGrounded = grounded;

            if (isMoving && grounded)
            {
                footstepTimer -= deltaTime;
                if (footstepTimer <= 0f)
                {
                    footstepTimer = isLoping ? 0.36f : 0.52f;
                    if (Audio != null) Audio.Footstep();
                }
            }
            else
            {
                footstepTimer = 0f;
            }

            float actualSpeed = planarVelocity.magnitude;
            Astronaut.UpdateRig(
                deltaTime,
                actualSpeed > 0.08f,
                actualSpeed,
                collectingSite != null,
                collectingSite != null ? collectingSite.CollectProgress : 0f);
        }

        private void UpdateMission(float deltaTime)
        {
            missionElapsed = Mathf.Min(Sim.MissionDuration, missionElapsed + deltaTime);

            float drain = Sim.O2BaseDrain;
            if (isMoving) drain += Sim.O2MoveDrain;
            if (isMoving && isLoping) drain += Sim.O2LopeDrain;
            oxygen = Mathf.Max(0f, oxygen - drain * deltaTime);

            if (!warnedLowOxygen && oxygen <= Sim.LowOxygen)
            {
                warnedLowOxygen = true;
                if (Audio != null) Audio.Warning();
                Toast("Oxygen below 30%. Keep the return to LUNA-1 in mind.", new Color(1f, 0.72f, 0.32f));
            }

            if (!warnedCriticalOxygen && oxygen <= Sim.CriticalOxygen)
            {
                warnedCriticalOxygen = true;
                if (Audio != null) Audio.Critical();
                Toast("CRITICAL OXYGEN. Return to LUNA-1 now.", new Color(1f, 0.38f, 0.28f));
            }

            if (!warnedLowTime && TimeRemaining <= Sim.LowTime)
            {
                warnedLowTime = true;
                if (Audio != null) Audio.Warning();
                Toast("Mission clock: two minutes remaining.", new Color(1f, 0.72f, 0.32f));
            }

            if (oxygen <= 0f)
            {
                Fail("Oxygen depleted. Your PLSS reserves ran out before you could return to LUNA-1.");
                return;
            }

            if (missionElapsed >= Sim.MissionDuration)
            {
                Fail("The mission clock expired. EVA operations were terminated.");
                return;
            }

            Vector2 playerPosition = new Vector2(Astronaut.transform.position.x, Astronaut.transform.position.z);
            float radius = playerPosition.magnitude;
            if (radius > Sim.BoundaryRadius)
            {
                boundaryTimer += deltaTime;
                if (boundaryTimer >= Sim.BoundaryGrace)
                {
                    Fail("Mission boundary breached. The EVA was terminated for safety.");
                }
            }
            else
            {
                boundaryTimer = 0f;
            }
        }

        private void UpdateInteraction(float deltaTime)
        {
            if (collectingSite != null)
            {
                float distance = HorizontalDistance(Astronaut.transform.position, collectingSite.Def.position);
                if (distance > Sim.InteractionRadius + 0.85f)
                {
                    CancelCollection("Collection interrupted — stay close to the sample.");
                    collectingSite = null;
                }
                else
                {
                    collectingSite.CollectProgress = Mathf.Clamp01(
                        collectingSite.CollectProgress + deltaTime / Sim.CollectDuration);
                    if (collectingSite.CollectProgress >= 1f)
                    {
                        CompleteSite(collectingSite);
                    }
                }
            }

            SiteRuntime nearest = FindNearestSite();
            activeSite = collectingSite != null ? collectingSite : nearest;
        }

        private SiteRuntime FindNearestSite()
        {
            SiteRuntime best = null;
            float bestDistance = float.MaxValue;
            Vector3 player = Astronaut.transform.position;

            foreach (KeyValuePair<string, SiteRuntime> pair in siteRuntimes)
            {
                SiteRuntime runtime = pair.Value;
                float distance = HorizontalDistance(player, runtime.Def.position);
                if (distance <= Sim.InteractionRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = runtime;
                }
            }

            return best;
        }

        public void BeginMission()
        {
            if (State != EVAState.Briefing) return;

            State = EVAState.Playing;
            oxygen = Sim.OxygenMax;
            missionElapsed = 0f;
            boundaryTimer = 0f;
            footstepTimer = 0f;
            verticalVelocity = 0f;
            planarVelocity = Vector3.zero;
            warnedLowOxygen = false;
            warnedCriticalOxygen = false;
            warnedLowTime = false;
            wrongToolAttempts = 0;
            failureReason = string.Empty;
            samples.Clear();

            foreach (KeyValuePair<string, SiteRuntime> pair in siteRuntimes)
            {
                SiteRuntime runtime = pair.Value;
                runtime.Stage = 0;
                runtime.CollectProgress = 0f;
                runtime.Collecting = false;
                runtime.Verified = false;
                runtime.View.SetState("pending");
            }

            collectingSite = null;
            activeSite = null;
            selectedTool = "scoop";
            Astronaut.SetTool(selectedTool);
            Astronaut.ResetPose();
            if (Astronaut.Controller != null) Astronaut.Controller.enabled = true;

            UI.ShowBriefing(false);
            UI.ShowEnd(false, false, string.Empty, string.Empty);
            UI.ShowPause(false);
            UI.SetSelectedTool(selectedTool);
            if (Audio != null)
            {
                Audio.StartAmbient();
                Audio.Select();
            }

            Toast("EVA started. Verify five samples, then return to LUNA-1.", new Color(0.55f, 0.86f, 1f));
            RefreshUI();
        }

        public void SelectTool(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (Content.Tool(id) == null) return;
            if (State != EVAState.Playing && State != EVAState.Briefing) return;

            if (collectingSite != null)
            {
                CancelCollection("Sampling cancelled — you changed tools.");
            }

            if (string.Equals(selectedTool, id)) return;
            selectedTool = id;
            Astronaut.SetTool(id);
            if (UI != null) UI.SetSelectedTool(id);
            if (Audio != null) Audio.Select();
        }

        public void TryInteract()
        {
            if (State != EVAState.Playing || Astronaut == null) return;

            Vector2 playerPosition = new Vector2(Astronaut.transform.position.x, Astronaut.transform.position.z);
            float landerDistance = playerPosition.magnitude;

            if (landerDistance <= Sim.LanderRadius)
            {
                if (samples.Count >= Sim.SampleCount)
                {
                    CompleteMission();
                }
                else
                {
                    Toast("LUNA-1: " + samples.Count + "/" + Sim.SampleCount +
                          " samples verified. The lander will accept the manifest when it is complete.",
                        new Color(1f, 0.72f, 0.32f));
                    if (Audio != null) Audio.Warning();
                }
                return;
            }

            if (collectingSite != null)
            {
                CancelCollection("Collection cancelled.");
                return;
            }

            if (activeSite == null)
            {
                Toast("Move closer to a sample station to use a tool.", new Color(1f, 0.72f, 0.32f));
                if (Audio != null) Audio.Warning();
                return;
            }

            TryUseTool();
        }

        private void TryUseTool()
        {
            SiteRuntime runtime = activeSite;
            if (runtime == null) return;

            if (runtime.Verified)
            {
                Toast(runtime.Def.name + " is already sealed and logged.", new Color(0.55f, 0.86f, 1f));
                if (Audio != null) Audio.Warning();
                return;
            }

            if (runtime.Collecting)
            {
                CancelCollection("Collection cancelled.");
                return;
            }

            if (runtime.Stage >= runtime.Def.requiredTools.Length)
            {
                StartCollection(runtime);
                return;
            }

            string needed = runtime.Def.requiredTools[runtime.Stage];
            if (!string.Equals(selectedTool, needed, StringComparison.OrdinalIgnoreCase))
            {
                wrongToolAttempts++;
                if (Audio != null) Audio.Warning();
                Toast(BuildWrongToolMessage(runtime, needed), new Color(1f, 0.72f, 0.32f));
                return;
            }

            SpendOxygen(Sim.O2ToolCost);
            if (State != EVAState.Playing) return;

            if (Audio != null) Audio.Tool();
            if (World != null) World.DustBurst(8);

            runtime.Stage++;
            runtime.View.SetState("partial");

            if (runtime.Stage < runtime.Def.requiredTools.Length)
            {
                string used = ToolName(runtime.Def.requiredTools[runtime.Stage - 1]);
                string next = ToolName(runtime.Def.requiredTools[runtime.Stage]);
                runtime.View.Label.SetText(runtime.Def.code + " · " + runtime.Def.name + " · " +
                                           used.ToUpperInvariant() + " USED");
                Toast(used + " applied. Now use the " + next + ".", new Color(0.95f, 0.82f, 0.5f));
            }
            else
            {
                StartCollection(runtime);
            }
        }

        private void StartCollection(SiteRuntime runtime)
        {
            runtime.Collecting = true;
            runtime.CollectProgress = 0f;
            collectingSite = runtime;
            activeSite = runtime;
            if (Audio != null) Audio.Select();
            Toast("Collecting " + runtime.Def.name + ". Keep still and let the tool do the work.",
                new Color(0.55f, 0.86f, 1f));
        }

        private void CompleteSite(SiteRuntime runtime)
        {
            if (State != EVAState.Playing) return;

            SpendOxygen(Sim.O2CollectCost);
            if (State != EVAState.Playing) return;

            runtime.Collecting = false;
            runtime.CollectProgress = 1f;
            runtime.Verified = true;
            runtime.View.SetState("verified");
            if (collectingSite == runtime) collectingSite = null;

            ContainerDef container = Content.Container(runtime.Def.container);
            samples.Add(new SampleRecord
            {
                Site = runtime.Def,
                ToolSummary = BuildToolSummary(runtime.Def),
                MissionTime = missionElapsed
            });

            if (Audio != null) Audio.Collect();
            if (World != null) World.DustBurst(20);

            Toast("Sealed: " + runtime.Def.name + " → " +
                  (container != null ? container.name : runtime.Def.container),
                new Color(0.56f, 0.89f, 0.65f));

            if (samples.Count >= Sim.SampleCount)
            {
                if (Audio != null) Audio.Success();
                Toast("All five samples verified. Return to LUNA-1 and press E to stow them.",
                    new Color(0.56f, 0.89f, 0.65f));
            }

            RefreshUI();
        }

        private void CancelCollection(string message)
        {
            if (collectingSite == null) return;

            SiteRuntime runtime = collectingSite;
            runtime.Collecting = false;
            runtime.CollectProgress = 0f;
            if (runtime.Stage > 0) runtime.View.SetState("partial");
            collectingSite = null;

            if (Audio != null) Audio.Warning();
            Toast(message, new Color(1f, 0.72f, 0.32f));
        }

        private void SpendOxygen(float amount)
        {
            oxygen = Mathf.Max(0f, oxygen - amount);
            if (oxygen <= 0f && State == EVAState.Playing)
            {
                Fail("Oxygen depleted. Your PLSS reserves ran out before you could return to LUNA-1.");
            }
        }

        private string BuildWrongToolMessage(SiteRuntime runtime, string needed)
        {
            if (runtime.Def.type == "outcrop")
            {
                if (runtime.Stage == 0 && selectedTool == "chisel")
                {
                    return "Start the break with the hammer first, then control it with the chisel.";
                }
                if (runtime.Stage > 0 && selectedTool == "hammer")
                {
                    return "The surface is already started. Switch to the chisel to free the sample.";
                }
            }

            return "Wrong tool. Use the " + ToolName(needed) + ". " + runtime.Def.hint;
        }

        private static string BuildToolSummary(SiteDef site)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < site.requiredTools.Length; i++)
            {
                if (i > 0) builder.Append(" → ");
                builder.Append(ToolName(site.requiredTools[i]));
            }
            return builder.ToString();
        }

        private static string ToolName(string id)
        {
            ToolDef tool = Content.Tool(id);
            return tool != null ? tool.name : id;
        }

        private void CompleteMission()
        {
            if (State != EVAState.Playing || samples.Count < Sim.SampleCount) return;

            State = EVAState.Success;
            if (Astronaut.Controller != null) Astronaut.Controller.enabled = false;
            if (Audio != null)
            {
                Audio.StopAmbient();
                Audio.Success();
            }

            float bestTime = PlayerPrefs.GetFloat("GatheringSamples.BestTime", float.MaxValue);
            if (missionElapsed < bestTime)
            {
                PlayerPrefs.SetFloat("GatheringSamples.BestTime", missionElapsed);
                PlayerPrefs.Save();
            }

            UI.ShowSuccess(
                "All five samples were verified, documented, and stowed aboard LUNA-1. The EVA is complete.",
                BuildStatsText());
        }

        private void Fail(string reason)
        {
            if (State != EVAState.Playing) return;

            failureReason = reason;
            State = EVAState.Failure;
            if (collectingSite != null)
            {
                collectingSite.Collecting = false;
                collectingSite.CollectProgress = 0f;
                collectingSite = null;
            }
            if (Astronaut.Controller != null) Astronaut.Controller.enabled = false;
            if (Audio != null)
            {
                Audio.StopAmbient();
                Audio.Fail();
            }

            UI.ShowFailure(reason, BuildStatsText());
        }

        public void Pause()
        {
            if (State != EVAState.Playing) return;
            CancelCollection("Collection interrupted — mission paused.");
            State = EVAState.Paused;
            UI.ShowPause(true);
            if (Audio != null) Audio.Click();
        }

        public void Resume()
        {
            if (State != EVAState.Paused) return;
            State = EVAState.Playing;
            UI.ShowPause(false);
            if (Audio != null) Audio.Click();
        }

        public void ToggleFieldGuide()
        {
            if (State == EVAState.Guide)
            {
                CloseGuide();
                return;
            }

            if (State != EVAState.Briefing && State != EVAState.Playing &&
                State != EVAState.Paused && State != EVAState.Success && State != EVAState.Failure)
            {
                return;
            }

            if (State == EVAState.Playing)
            {
                CancelCollection("Collection interrupted — field guide opened.");
            }

            guideReturnState = State;
            State = EVAState.Guide;
            UI.ShowPause(false);
            UI.ShowGuide(true);
            if (Audio != null) Audio.Select();
        }

        public void CloseGuide()
        {
            if (State != EVAState.Guide) return;

            State = guideReturnState;
            UI.ShowGuide(false);
            if (State == EVAState.Paused) UI.ShowPause(true);
            if (Audio != null) Audio.Click();
        }

        public void RestartMission()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
            {
                SceneManager.LoadScene(active.buildIndex);
            }
            else
            {
                SceneManager.LoadScene(active.name);
            }
        }

        private void RefreshUI()
        {
            if (UI == null) return;

            UI.UpdateHud(
                TimeRemaining,
                oxygen,
                missionElapsed,
                samples.Count,
                Sim.SampleCount,
                BuildManifestText(),
                BuildObjectiveText(),
                selectedTool);

            RefreshBoundaryWarning();
            RefreshInteractionPrompt();
        }

        private void RefreshBoundaryWarning()
        {
            if (UI == null || Astronaut == null) return;

            Vector2 player = new Vector2(Astronaut.transform.position.x, Astronaut.transform.position.z);
            float radius = player.magnitude;
            string warning = null;
            Color warningColor = new Color(1f, 0.72f, 0.32f);

            if (radius > Sim.BoundaryRadius)
            {
                int seconds = Mathf.Max(0, Mathf.CeilToInt(Sim.BoundaryGrace - boundaryTimer));
                warning = "BOUNDARY BREACH — RETURN TO MISSION AREA (" + seconds + "s)";
                warningColor = new Color(1f, 0.32f, 0.24f);
            }
            else if (radius > Sim.SoftBoundaryRadius)
            {
                warning = "APPROACHING MISSION BOUNDARY — TURN BACK";
                warningColor = new Color(1f, 0.62f, 0.24f);
            }
            else if (oxygen <= Sim.CriticalOxygen)
            {
                warning = "CRITICAL OXYGEN — RETURN TO LUNA-1";
                warningColor = new Color(1f, 0.32f, 0.24f);
            }
            else if (oxygen <= Sim.LowOxygen)
            {
                warning = "LOW OXYGEN — MONITOR YOUR RESERVES";
                warningColor = new Color(1f, 0.62f, 0.24f);
            }
            else if (TimeRemaining <= Sim.LowTime)
            {
                warning = "MISSION CLOCK — " + FormatClock(TimeRemaining) + " REMAINING";
                warningColor = new Color(1f, 0.62f, 0.24f);
            }

            UI.SetWarning(warning, warningColor);

            float vignette = 0f;
            if (oxygen < Sim.LowOxygen)
            {
                vignette = Mathf.Lerp(0.16f, 0.62f, Mathf.InverseLerp(Sim.LowOxygen, 0f, oxygen));
            }
            UI.SetVignetteAlpha(vignette);
        }

        private void RefreshInteractionPrompt()
        {
            if (UI == null || Astronaut == null) return;

            Vector2 player = new Vector2(Astronaut.transform.position.x, Astronaut.transform.position.z);
            float landerDistance = player.magnitude;

            if (landerDistance <= Sim.LanderRadius && samples.Count >= Sim.SampleCount)
            {
                UI.SetInteraction(
                    "E · Stow samples and complete the EVA",
                    "All five samples verified · LUNA-1 lander",
                    -1f, false, true);
                return;
            }

            if (collectingSite != null)
            {
                UI.SetInteraction(
                    "COLLECTING · " + collectingSite.Def.name,
                    "Keep still — " + Mathf.RoundToInt(collectingSite.CollectProgress * 100f) + "%",
                    collectingSite.CollectProgress, false, true);
                return;
            }

            if (activeSite != null)
            {
                if (activeSite.Verified)
                {
                    UI.SetInteraction(
                        "VERIFIED · " + activeSite.Def.name,
                        "Sample sealed and logged · " + activeSite.Def.typeLabel,
                        -1f, false, true);
                    return;
                }

                string needed = activeSite.Stage < activeSite.Def.requiredTools.Length
                    ? activeSite.Def.requiredTools[activeSite.Stage]
                    : activeSite.Def.requiredTools[activeSite.Def.requiredTools.Length - 1];
                bool correct = string.Equals(selectedTool, needed, StringComparison.OrdinalIgnoreCase);
                string title = correct
                    ? "E · Use " + ToolName(needed) + " on " + activeSite.Def.name
                    : "SELECT " + ToolName(needed).ToUpperInvariant() + " · " + activeSite.Def.name;

                UI.SetInteraction(title, activeSite.Def.hint, -1f, !correct, true);
                return;
            }

            if (landerDistance <= Sim.LanderRadius)
            {
                UI.SetInteraction(
                    "LUNA-1 LANDER",
                    samples.Count + "/" + Sim.SampleCount + " samples verified · Return with all five",
                    -1f, false, true);
                return;
            }

            UI.SetInteraction(string.Empty, string.Empty, -1f, false, false);
        }

        private string BuildManifestText()
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < Content.Sites.Length; i++)
            {
                SiteDef site = Content.Sites[i];
                SiteRuntime runtime;
                if (!siteRuntimes.TryGetValue(site.id, out runtime)) continue;

                string status = runtime.Verified
                    ? "<color=#8FE3A5>VERIFIED</color>"
                    : runtime.Stage > 0
                        ? "<color=#FFC56B>IN PROGRESS</color>"
                        : "<color=#6E7C8C>PENDING</color>";

                builder.Append("<color=#5E7186>")
                    .Append(site.code)
                    .Append("</color>  ")
                    .Append(ShortSampleName(site))
                    .Append("  ")
                    .Append(status);

                if (i < Content.Sites.Length - 1) builder.Append('\n');
            }
            return builder.ToString();
        }

        private string BuildObjectiveText()
        {
            if (samples.Count < Sim.SampleCount)
            {
                return "Collect and verify all five samples. Match the tool to the material at each station.";
            }
            return "All samples verified. Return to LUNA-1 and press E to stow the samples.";
        }

        private string BuildStatsText()
        {
            int attempts = samples.Count + wrongToolAttempts;
            int accuracy = attempts > 0 ? Mathf.RoundToInt(samples.Count * 100f / attempts) : 100;
            return "Samples verified     " + samples.Count + " / " + Sim.SampleCount + "\n" +
                   "Mission time         " + FormatClock(missionElapsed) + "\n" +
                   "Oxygen remaining     " + Mathf.CeilToInt(oxygen) + "%\n" +
                   "Tool accuracy        " + accuracy + "%  (" + wrongToolAttempts + " misuse attempts)";
        }

        private static string ShortSampleName(SiteDef site)
        {
            switch (site.type)
            {
                case "loose_regolith": return "Regolith";
                case "individual_rock": return "Float Rock";
                case "outcrop": return "Outcrop";
                case "stratified_soil": return "Stratigraphy";
                case "coarse_material": return "Coarse Lag";
                default: return site.name;
            }
        }

        private static float HorizontalDistance(Vector3 worldPosition, Vector2 sitePosition)
        {
            float dx = worldPosition.x - sitePosition.x;
            float dz = worldPosition.z - sitePosition.y;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static string FormatClock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return (total / 60).ToString("00") + ":" + (total % 60).ToString("00");
        }

        private void Toast(string message, Color color)
        {
            if (UI != null) UI.Toast(message, color);
        }
    }
}
