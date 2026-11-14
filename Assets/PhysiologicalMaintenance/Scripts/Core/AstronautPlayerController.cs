using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace PhysiologicalMaintenance
{
    [DisallowMultipleComponent]
    public sealed class AstronautPlayerController : MonoBehaviour
    {
        private const float WalkSpeed = 4.2f;
        private const float SprintMultiplier = 1.2f;
        private const float LookSensitivity = 0.08f;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction interactAction;
        private InputAction pauseAction;
        private InputAction sprintAction;
        private InputAction cursorToggleAction;
        private CharacterController characterController;
        private Camera followCamera;
        private PhysiologicalMaintenanceController controller;
        private Transform modelRoot;
        private Transform head;
        private Transform torso;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform leftFoot;
        private Transform rightFoot;
        private Animator avatarAnimator;
        private bool usingImportedRig;
        private EquipmentStation activeStation;
        private Vector3 stationAnchor;
        private bool usingMachine;
        private bool uiInteractionOpen;
        private float cameraPitch;
        private float cameraYaw;
        private float exerciseCycle;

        public void Setup(Camera mainCamera, PhysiologicalMaintenanceController missionController)
        {
            followCamera = mainCamera;
            controller = missionController;
            characterController = GetComponent<CharacterController>();
            if (characterController == null)
                characterController = gameObject.AddComponent<CharacterController>();

            CreateInputActions();
            BuildAstronautVisual();
            transform.position = new Vector3(0f, 0.16f, -4.5f);
            cameraYaw = 0f;
            cameraPitch = 10f;
            SetGameplayCursor(true);
        }

        private void Update()
        {
            if (controller == null)
                return;

            if (pauseAction != null && pauseAction.WasPressedThisFrame() &&
                controller.CurrentMissionState != MissionState.NotStarted && !controller.Snapshot.MissionEnded)
            {
                controller.ToggleSimulationPause();
                SetGameplayCursor(!controller.SimulationPaused);
            }

            if (cursorToggleAction != null && cursorToggleAction.WasPressedThisFrame() &&
                controller.CurrentMissionState == MissionState.InProgress)
            {
                bool shouldLock = Cursor.lockState != CursorLockMode.Locked;
                SetGameplayCursor(shouldLock);
            }

            bool gameplayActive = controller.CurrentMissionState == MissionState.InProgress;
            if (uiInteractionOpen)
            {
                UpdateStationHighlights();
                UpdateCameraFollow();
                return;
            }

            if (gameplayActive)
            {
                HandleCameraLook();
                HandleMovement();
                UpdateStationHighlights();
                TryInteract();
                UpdateExerciseVisuals(Time.deltaTime);
            }
            else if (controller.CurrentMissionState == MissionState.Paused)
            {
                SetGameplayCursor(false);
            }
            else if (controller.Snapshot != null && controller.Snapshot.MissionEnded)
            {
                SetGameplayCursor(false);
            }
            UpdateCameraFollow();
        }

        private void HandleCameraLook()
        {
            if (followCamera == null)
                return;

            if (Cursor.lockState != CursorLockMode.Locked || lookAction == null)
                return;

            Vector2 lookDelta = lookAction.ReadValue<Vector2>();
            cameraYaw += lookDelta.x * LookSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch - lookDelta.y * LookSensitivity, -35f, 35f);

            Quaternion yawRotation = Quaternion.Euler(0f, cameraYaw, 0f);
            transform.rotation = yawRotation;
        }

        private void HandleMovement()
        {
            if (characterController == null)
                return;

            if (controller.CurrentMissionState != MissionState.InProgress || usingMachine)
                return;

            Vector2 moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
            if (input.sqrMagnitude > 1f)
                input.Normalize();

            Vector3 moveDirection = transform.TransformDirection(input);
            moveDirection.y = 0f;
            float speed = WalkSpeed * (sprintAction != null && sprintAction.IsPressed() ? SprintMultiplier : 1f);
            CharacterController cc = characterController;
            Vector3 motion = moveDirection * speed;
            motion.y = -0.5f;
            Vector3 requestedPosition = transform.position + motion * Time.deltaTime;
            requestedPosition.x = Mathf.Clamp(requestedPosition.x, -9.25f, 9.25f);
            requestedPosition.z = Mathf.Clamp(requestedPosition.z, -5.75f, 5.75f);
            if (requestedPosition.y < -0.5f)
                requestedPosition.y = 0.16f;
            cc.Move(requestedPosition - transform.position);

            if (modelRoot != null)
            {
                float walkStrength = input.sqrMagnitude > 0.05f ? 1f : 0f;
                float stride = Time.time * (walkStrength > 0f ? 9f : 0f);
                if (leftLeg != null)
                    leftLeg.localRotation = Quaternion.Euler(Mathf.Sin(stride) * 28f * walkStrength, 0f, 0f);
                if (rightLeg != null)
                    rightLeg.localRotation = Quaternion.Euler(-Mathf.Sin(stride) * 28f * walkStrength, 0f, 0f);
                if (leftArm != null)
                    leftArm.localRotation = Quaternion.Euler(-Mathf.Sin(stride) * 18f * walkStrength, 0f, 0f);
                if (rightArm != null)
                    rightArm.localRotation = Quaternion.Euler(Mathf.Sin(stride) * 18f * walkStrength, 0f, 0f);
            }
        }

        private void TryInteract()
        {
            if (interactAction == null || !interactAction.WasPressedThisFrame())
                return;

            if (usingMachine)
            {
                controller.StopExercise();
                usingMachine = false;
                activeStation = null;
                return;
            }

            EquipmentStation nearest = FindNearestStation();
            if (nearest == null)
                return;

            activeStation = nearest;
            stationAnchor = nearest.transform.position + nearest.transform.forward * 1.65f;
            controller.OpenExerciseInstructions(nearest);
        }

        public bool StartExerciseAtStation()
        {
            if (activeStation == null || controller == null || controller.CurrentMissionState != MissionState.InProgress)
                return false;

            controller.StartExercise();
            if (controller.Snapshot.Session == null || !controller.Snapshot.Session.IsRunning)
                return false;

            usingMachine = true;
            return true;
        }

        public void CancelStationInteraction()
        {
            if (usingMachine)
                controller?.StopExercise();
            usingMachine = false;
            activeStation = null;
        }

        private void UpdateStationHighlights()
        {
            EquipmentStation nearest = usingMachine || uiInteractionOpen ? activeStation : FindNearestStation();
            EquipmentStation[] stations = FindObjectsByType<EquipmentStation>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
                if (stations[i] != null)
                    stations[i].SetHighlighted(stations[i] == nearest);
        }

        private EquipmentStation FindNearestStation()
        {
            var stations = FindObjectsByType<EquipmentStation>(FindObjectsSortMode.None);
            EquipmentStation best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < stations.Length; i++)
            {
                EquipmentStation station = stations[i];
                if (station == null)
                    continue;

                float distance = Vector3.Distance(transform.position, station.transform.position);
                if (distance <= station.InteractionRadius && distance < bestDistance)
                {
                    bestDistance = distance;
                    best = station;
                }
            }
            return best;
        }

        private void UpdateExerciseVisuals(float deltaTime)
        {
            if (controller == null)
                return;

            SimulationSnapshot snapshot = controller.Snapshot;
            bool training = snapshot != null && snapshot.Session != null && snapshot.Session.IsRunning && activeStation != null;
            if (!training)
            {
                usingMachine = false;
                if (modelRoot != null)
                {
                    modelRoot.localPosition = Vector3.Lerp(modelRoot.localPosition, Vector3.zero, deltaTime * 5f);
                    if (head != null)
                        head.localRotation = Quaternion.Lerp(head.localRotation, Quaternion.identity, deltaTime * 9f);
                    if (torso != null)
                        torso.localRotation = Quaternion.Lerp(torso.localRotation, Quaternion.identity, deltaTime * 9f);
                }
                return;
            }

            exerciseCycle += deltaTime * (1f / Mathf.Max(0.25f, snapshot.Session.Definition.RepetitionCycleSeconds));
            Vector3 anchorTarget = stationAnchor + Vector3.up * 0.16f;
            Vector3 toTarget = anchorTarget - transform.position;
            if (toTarget.magnitude > 0.15f)
            {
                Vector3 move = toTarget.normalized * Mathf.Min(toTarget.magnitude, 2.0f * deltaTime);
                characterController.Move(move);
            }

            Quaternion stationYaw = Quaternion.LookRotation(-activeStation.transform.forward, Vector3.up);
            transform.rotation = Quaternion.Lerp(transform.rotation, stationYaw, deltaTime * 7f);
            cameraYaw = transform.eulerAngles.y;

            float squatWave = Mathf.Sin(exerciseCycle * Mathf.PI * 2f);
            float rowsWave = Mathf.Sin(exerciseCycle * Mathf.PI * 2f + Mathf.PI * 0.5f);
            float cycleWave = Mathf.Sin(exerciseCycle * Mathf.PI * 2f);

            if (controller.SelectedExerciseType == ExerciseType.Squat)
            {
                if (torso != null)
                    torso.localRotation = Quaternion.Euler(10f + squatWave * 18f, 0f, 0f);
                if (leftLeg != null)
                    leftLeg.localRotation = Quaternion.Euler(15f + squatWave * 35f, 0f, 0f);
                if (rightLeg != null)
                    rightLeg.localRotation = Quaternion.Euler(-15f - squatWave * 35f, 0f, 0f);
                if (leftArm != null)
                    leftArm.localRotation = Quaternion.Euler(-20f - squatWave * 20f, 0f, 0f);
                if (rightArm != null)
                    rightArm.localRotation = Quaternion.Euler(-20f + squatWave * 20f, 0f, 0f);
            }
            else if (controller.SelectedExerciseType == ExerciseType.Row)
            {
                if (torso != null)
                    torso.localRotation = Quaternion.Euler(7f + rowsWave * 12f, 0f, 0f);
                if (leftArm != null)
                    leftArm.localRotation = Quaternion.Euler(-35f + rowsWave * 35f, 0f, 0f);
                if (rightArm != null)
                    rightArm.localRotation = Quaternion.Euler(35f - rowsWave * 35f, 0f, 0f);
                if (leftLeg != null)
                    leftLeg.localRotation = Quaternion.Euler(5f, 0f, 0f);
                if (rightLeg != null)
                    rightLeg.localRotation = Quaternion.Euler(5f, 0f, 0f);
            }
            else
            {
                if (torso != null)
                    torso.localRotation = Quaternion.Euler(8f, 0f, 0f);
                if (leftLeg != null)
                    leftLeg.localRotation = Quaternion.Euler(18f + cycleWave * 18f, 0f, 0f);
                if (rightLeg != null)
                    rightLeg.localRotation = Quaternion.Euler(-18f - cycleWave * 18f, 0f, 0f);
                if (leftArm != null)
                    leftArm.localRotation = Quaternion.Euler(18f + cycleWave * 12f, 0f, 0f);
                if (rightArm != null)
                    rightArm.localRotation = Quaternion.Euler(-18f - cycleWave * 12f, 0f, 0f);
            }
        }

        private void UpdateCameraFollow()
        {
            if (followCamera == null)
                return;

            Vector3 lookTarget = transform.position + Vector3.up * 1.48f;
            Quaternion orbitRotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            Vector3 cameraDirection = orbitRotation * Vector3.back;
            const float cameraDistance = 5f;
            Vector3 desiredPosition = lookTarget + cameraDirection * cameraDistance;
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, -9.35f, 9.35f);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, 0.45f, 4.55f);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, -5.95f, 5.95f);

            RaycastHit[] hits = Physics.SphereCastAll(lookTarget, 0.2f, cameraDirection, cameraDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float closestDistance = cameraDistance;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform hitTransform = hits[i].transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform) || hits[i].distance < 0.28f)
                    continue;
                closestDistance = Mathf.Min(closestDistance, Mathf.Max(0.35f, hits[i].distance - 0.12f));
            }
            desiredPosition = lookTarget + cameraDirection * closestDistance;
            desiredPosition.x = Mathf.Clamp(desiredPosition.x, -9.35f, 9.35f);
            desiredPosition.y = Mathf.Clamp(desiredPosition.y, 0.45f, 4.55f);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, -5.95f, 5.95f);

            Vector3 cameraPosition = Vector3.Lerp(followCamera.transform.position, desiredPosition, Time.deltaTime * 8f);
            followCamera.transform.position = cameraPosition;
            followCamera.transform.rotation = Quaternion.Lerp(followCamera.transform.rotation,
                Quaternion.LookRotation(lookTarget - followCamera.transform.position, Vector3.up),
                Time.deltaTime * 8f);
        }

        public string InteractionPrompt
        {
            get
            {
                if (usingMachine)
                    return "E  —  stop exercise and return to walking";

                EquipmentStation station = FindNearestStation();
                return station != null
                    ? "Press E to use the " + station.FriendlyName + "!"
                    : "WASD: walk     Mouse: look around     E: use equipment     ESC: pause";
            }
        }

        private void CreateInputActions()
        {
            // Mirrors the project's existing InputSystem_Actions Player map (Move, Look,
            // Interact and Sprint) while keeping this runtime-created player self-contained.
            moveAction = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            lookAction = new InputAction("Look", InputActionType.Value, "<Pointer>/delta", expectedControlType: "Vector2");
            interactAction = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
            pauseAction = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
            cursorToggleAction = new InputAction("CursorControl", InputActionType.Button, "<Keyboard>/f1");

            moveAction.Enable();
            lookAction.Enable();
            interactAction.Enable();
            pauseAction.Enable();
            sprintAction.Enable();
            cursorToggleAction.Enable();
        }

        public void UpdateCursorForMissionState()
        {
            bool gameplay = controller != null && controller.CurrentMissionState == MissionState.InProgress;
            SetGameplayCursor(gameplay && !uiInteractionOpen);
        }

        public void SetUIInteraction(bool open)
        {
            uiInteractionOpen = open;
            UpdateCursorForMissionState();
        }

        public void ResetForMission()
        {
            usingMachine = false;
            uiInteractionOpen = false;
            activeStation = null;
            stationAnchor = Vector3.zero;
            transform.position = new Vector3(0f, 0.16f, -4.5f);
            transform.rotation = Quaternion.identity;
            cameraYaw = 0f;
            cameraPitch = 10f;
            exerciseCycle = 0f;
            if (modelRoot != null)
                modelRoot.localPosition = Vector3.zero;
            SetGameplayCursor(true);
        }

        private static void SetGameplayCursor(bool gameplay)
        {
            Cursor.lockState = gameplay ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !gameplay;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                SetGameplayCursor(false);
                return;
            }

            bool gameplay = controller != null && controller.CurrentMissionState == MissionState.InProgress && !uiInteractionOpen;
            SetGameplayCursor(gameplay);
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
            interactAction?.Dispose();
            pauseAction?.Dispose();
            sprintAction?.Dispose();
            cursorToggleAction?.Dispose();
        }

        private void BuildAstronautVisual()
        {
            if (modelRoot != null)
            {
                Destroy(modelRoot.gameObject);
                modelRoot = null;
            }

            var importedCharacter = Resources.Load<GameObject>("PhysiologicalMaintenance/Characters/ManBaseCharacter");
            if (importedCharacter != null)
            {
                GameObject instance = Instantiate(importedCharacter, transform);
                instance.name = "Astronaut Model";
                modelRoot = instance.transform;
                modelRoot.localPosition = Vector3.zero;
                modelRoot.localRotation = Quaternion.identity;
                avatarAnimator = instance.GetComponentInChildren<Animator>();
                usingImportedRig = avatarAnimator != null;

                var renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i].name.Contains("Body") || renderers[i].name.Contains("Torso") || renderers[i].name.Contains("Shirt") || renderers[i].name.Contains("Shorts") || renderers[i].name.Contains("Shoe") || renderers[i].name.Contains("Boot") || renderers[i].name.Contains("Belt"))
                    {
                        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        material.color = new Color(0.90f, 0.78f, 0.70f);
                        renderers[i].sharedMaterial = material;
                    }
                }

                var hips = instance.transform.Find("Root/Hips") ?? instance.transform.Find("Armature/Hips");
                if (hips != null)
                    torso = hips;
                else
                    torso = instance.transform;

                head = FindChildByName(instance.transform, "Head") ?? FindChildByName(instance.transform, "head");
                leftArm = FindChildByName(instance.transform, "LeftArm") ?? FindChildByName(instance.transform, "Left_Arm") ?? FindChildByName(instance.transform, "upper_arm.L");
                rightArm = FindChildByName(instance.transform, "RightArm") ?? FindChildByName(instance.transform, "Right_Arm") ?? FindChildByName(instance.transform, "upper_arm.R");
                leftLeg = FindChildByName(instance.transform, "LeftLeg") ?? FindChildByName(instance.transform, "Left_Leg") ?? FindChildByName(instance.transform, "thigh.L");
                rightLeg = FindChildByName(instance.transform, "RightLeg") ?? FindChildByName(instance.transform, "Right_Leg") ?? FindChildByName(instance.transform, "thigh.R");
                leftFoot = FindChildByName(instance.transform, "LeftFoot") ?? FindChildByName(instance.transform, "Left_Foot") ?? FindChildByName(instance.transform, "foot.L");
                rightFoot = FindChildByName(instance.transform, "RightFoot") ?? FindChildByName(instance.transform, "Right_Foot") ?? FindChildByName(instance.transform, "foot.R");
                return;
            }

            var root = new GameObject("Astronaut Model");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;
            modelRoot = root.transform;

            var bodyMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bodyMaterial.color = new Color(0.90f, 0.78f, 0.70f);
            var shirtMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            shirtMaterial.color = new Color(0.025f, 0.03f, 0.04f);
            var shortsMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            shortsMaterial.color = new Color(0.08f, 0.09f, 0.11f);
            var shoeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            shoeMaterial.color = new Color(0.12f, 0.14f, 0.17f);

            torso = CreatePrimitivePart(root.transform, PrimitiveType.Capsule, new Vector3(0f, 1.25f, 0f), new Vector3(0.6f, 0.8f, 0.34f), shirtMaterial, "Black Athletic Shirt");
            head = CreatePrimitivePart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 2.15f, 0f), Vector3.one * 0.28f, bodyMaterial, "Head");
            var chest = CreatePrimitivePart(root.transform, PrimitiveType.Cube, new Vector3(0f, 1.2f, 0.22f), new Vector3(0.45f, 0.72f, 0.14f), shirtMaterial, "Shirt Front");
            chest.localScale = new Vector3(0.7f, 0.8f, 0.16f);

            leftArm = CreatePrimitivePart(root.transform, PrimitiveType.Capsule, new Vector3(-0.42f, 1.4f, 0f), new Vector3(0.18f, 0.75f, 0.18f), bodyMaterial, "Left Arm");
            rightArm = CreatePrimitivePart(root.transform, PrimitiveType.Capsule, new Vector3(0.42f, 1.4f, 0f), new Vector3(0.18f, 0.75f, 0.18f), bodyMaterial, "Right Arm");
            leftLeg = CreatePrimitivePart(root.transform, PrimitiveType.Capsule, new Vector3(-0.18f, 0.48f, 0f), new Vector3(0.2f, 0.84f, 0.2f), bodyMaterial, "Left Leg");
            rightLeg = CreatePrimitivePart(root.transform, PrimitiveType.Capsule, new Vector3(0.18f, 0.48f, 0f), new Vector3(0.2f, 0.84f, 0.2f), bodyMaterial, "Right Leg");

            var leftShort = CreatePrimitivePart(root.transform, PrimitiveType.Cube, new Vector3(-0.18f, 0.12f, 0f), new Vector3(0.28f, 0.24f, 0.18f), shortsMaterial, "Left Short");
            var rightShort = CreatePrimitivePart(root.transform, PrimitiveType.Cube, new Vector3(0.18f, 0.12f, 0f), new Vector3(0.28f, 0.24f, 0.18f), shortsMaterial, "Right Short");
            leftShort.localPosition = new Vector3(-0.18f, 0.07f, 0f);
            rightShort.localPosition = new Vector3(0.18f, 0.07f, 0f);
            CreatePrimitivePart(root.transform, PrimitiveType.Cube, new Vector3(-0.18f, 0.035f, 0.07f),
                new Vector3(0.27f, 0.12f, 0.42f), shoeMaterial, "Left Trainer");
            CreatePrimitivePart(root.transform, PrimitiveType.Cube, new Vector3(0.18f, 0.035f, 0.07f),
                new Vector3(0.27f, 0.12f, 0.42f), shoeMaterial, "Right Trainer");
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name || child.name.Contains(name))
                    return child;
                Transform nested = FindChildByName(child, name);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static Transform CreatePrimitivePart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, string name)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            if (part.GetComponent<Collider>() != null)
                Destroy(part.GetComponent<Collider>());
            return part.transform;
        }
    }
}
