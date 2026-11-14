using System;
using System.Collections.Generic;
using UnityEngine;

namespace GatheringSamples
{
    public class AstronautRig : MonoBehaviour
    {
        public Transform Root { get; private set; }
        public Animator Animator { get; private set; }
        public GameObject Model { get; private set; }
        public Transform Hand { get; private set; }
        public Transform Head { get; private set; }
        public Transform ToolPivot { get; private set; }
        public CharacterController Controller { get; private set; }

        private Transform bobGroup;
        private Transform modelHolder;
        private readonly Dictionary<string, GameObject> tools = new Dictionary<string, GameObject>();
        private string selectedTool = "scoop";
        private float walkPhase;
        private bool hasAnimator;

        public void Build()
        {
            Root = transform;
            Controller = gameObject.GetComponent<CharacterController>();
            if (Controller == null) Controller = gameObject.AddComponent<CharacterController>();
            Controller.height = Sim.PlayerHeight;
            Controller.radius = 0.36f;
            Controller.center = new Vector3(0f, Sim.PlayerHeight * 0.5f, 0f);
            Controller.slopeLimit = 45f;
            Controller.stepOffset = 0.35f;
            Controller.skinWidth = 0.03f;

            bobGroup = new GameObject("Bob Group").transform;
            bobGroup.SetParent(Root, false);

            modelHolder = new GameObject("Model Holder").transform;
            modelHolder.SetParent(bobGroup, false);

            GameObject prefab = Resources.Load<GameObject>("Astronaut/Astronaut");
            if (prefab != null)
            {
                Model = Instantiate(prefab, modelHolder);
                Model.name = "Astronaut Model";
            }
            else
            {
                Model = CreateFallbackModel(modelHolder);
            }

            Model.transform.localPosition = Vector3.zero;
            Model.transform.localRotation = Quaternion.identity;
            Bounds bounds = CalculateBounds(Model);
            float height = bounds.size.y;
            float scale = height > 0.1f ? Sim.PlayerHeight / height : 1f;
            // Renderer bounds are world-space; convert the centre and lowest point into
            // modelHolder space before using them as a local offset. This puts the feet
            // exactly on the root (the CharacterController base) regardless of terrain.
            Vector3 localCenter = modelHolder.InverseTransformPoint(bounds.center);
            Vector3 localMin = modelHolder.InverseTransformPoint(bounds.min);
            Model.transform.localPosition = new Vector3(-localCenter.x, -localMin.y, -localCenter.z);
            modelHolder.localScale = Vector3.one * scale;

            Animator = Model.GetComponentInChildren<Animator>();
            if (Animator == null) Animator = Model.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            if (Animator.avatar == null)
            {
                Avatar avatar = Resources.Load<Avatar>("Astronaut/Astronaut");
                if (avatar != null) Animator.avatar = avatar;
            }

            RuntimeAnimatorController controller = Resources.Load<RuntimeAnimatorController>("AstronautController");
            if (controller != null && Animator.avatar != null)
            {
                Animator.runtimeAnimatorController = controller;
                hasAnimator = true;
            }

            if (Animator.avatar != null && Animator.avatar.isHuman)
            {
                Hand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
                Head = Animator.GetBoneTransform(HumanBodyBones.Head);
            }
            if (Hand == null)
            {
                Hand = FindBone(Model.transform, "RightHand", "Hand_R", "hand_r", "Right Hand");
            }
            if (Head == null)
            {
                Head = FindBone(Model.transform, "Head", "head");
            }

            ToolPivot = new GameObject("Tool Pivot").transform;
            ToolPivot.SetParent(Root, false);
            ToolPivot.localScale = Vector3.one * 1.3f;
            BuildTools();
            SetTool(selectedTool);
            BuildHelmetLamp();
        }

        public void SetTool(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            selectedTool = id;
            foreach (KeyValuePair<string, GameObject> pair in tools)
            {
                pair.Value.SetActive(pair.Key == id);
            }
        }

        public void ResetPose()
        {
            if (bobGroup != null)
            {
                bobGroup.localPosition = Vector3.zero;
                bobGroup.localRotation = Quaternion.identity;
            }
            walkPhase = 0f;
            if (ToolPivot != null) ToolPivot.gameObject.SetActive(true);
        }

        public void UpdateRig(float deltaTime, bool moving, float speed, bool usingTool, float collectProgress)
        {
            if (hasAnimator && Animator != null && Animator.runtimeAnimatorController != null)
            {
                Animator.SetFloat("Speed", speed);
                Animator.speed = moving
                    ? Mathf.Clamp(speed * 0.45f, 0.65f, 1.4f)
                    : 0.18f;
            }

            walkPhase += deltaTime * (moving ? speed * 2.4f : 1.2f);
            float bob = moving
                ? Mathf.Abs(Mathf.Sin(walkPhase)) * 0.035f
                : Mathf.Sin(walkPhase) * 0.006f;
            float crouch = usingTool ? -0.14f * Mathf.Sin(Mathf.Clamp01(collectProgress) * Mathf.PI) : 0f;
            Vector3 targetPosition = new Vector3(0f, bob + crouch, 0f);
            float positionBlend = 1f - Mathf.Exp(-9f * deltaTime);
            bobGroup.localPosition = Vector3.Lerp(bobGroup.localPosition, targetPosition, positionBlend);

            float targetLean = moving ? -0.055f : 0f;
            float toolLean = usingTool ? -0.2f * Mathf.Sin(Mathf.Clamp01(collectProgress) * Mathf.PI) : 0f;
            Quaternion targetRotation = Quaternion.Euler((targetLean + toolLean) * Mathf.Rad2Deg, 0f, 0f);
            float rotationBlend = 1f - Mathf.Exp(-7f * deltaTime);
            bobGroup.localRotation = Quaternion.Slerp(bobGroup.localRotation, targetRotation, rotationBlend);

            if (ToolPivot != null)
            {
                if (Hand != null)
                {
                    Vector3 handPosition = Hand.position;
                    ToolPivot.position = handPosition
                        + Root.right * 0.055f
                        + Root.up * 0.02f
                        + Root.forward * 0.03f;
                    ToolPivot.rotation = Hand.rotation * Quaternion.Euler(-8f, 0f, 86f);
                }
                else
                {
                    ToolPivot.localPosition = new Vector3(0.38f, 1.12f, 0.24f);
                    ToolPivot.localRotation = Quaternion.Euler(-8f, 0f, 86f);
                }

                bool hideTool = usingTool && collectProgress > 0.55f;
                ToolPivot.gameObject.SetActive(!hideTool);
            }
        }

        private void BuildHelmetLamp()
        {
            Transform lampParent = Head != null ? Head : Model.transform;
            GameObject lampObject = new GameObject("Helmet Lamp");
            lampObject.transform.SetParent(lampParent, false);
            lampObject.transform.localPosition = Vector3.zero;
            Light lamp = lampObject.AddComponent<Light>();
            lamp.type = LightType.Spot;
            lamp.color = new Color(1f, 0.95f, 0.84f);
            lamp.range = 12f;
            lamp.spotAngle = 42f;
            lamp.intensity = 2.1f;
            lamp.shadows = LightShadows.None;

            GameObject targetObject = new GameObject("Helmet Lamp Target");
            targetObject.transform.SetParent(lampParent, false);
            targetObject.transform.localPosition = new Vector3(0f, 0f, 1f);
            lampObject.transform.LookAt(targetObject.transform);
        }

        private void BuildTools()
        {
            Material metal = RuntimeAssets.CreateLit("tool-metal", new Color(0.72f, 0.75f, 0.78f), 0.85f, 0.28f);
            Material dark = RuntimeAssets.CreateLit("tool-dark", new Color(0.16f, 0.18f, 0.21f), 0.65f, 0.42f);
            Material accent = RuntimeAssets.CreateLit("tool-accent", new Color(0.85f, 0.5f, 0.17f), 0.25f, 0.55f);
            Material tube = RuntimeAssets.CreateLit("tool-tube", new Color(0.84f, 0.85f, 0.83f), 0.4f, 0.35f);

            tools["scoop"] = BuildScoop(metal, dark, accent);
            tools["tongs"] = BuildTongs(metal, dark, accent);
            tools["hammer"] = BuildHammer(metal, dark, accent);
            tools["chisel"] = BuildChisel(metal, dark, accent);
            tools["rake"] = BuildRake(metal, dark, accent);
            tools["drive-tube"] = BuildDriveTube(metal, dark, accent, tube);

            foreach (KeyValuePair<string, GameObject> pair in tools)
            {
                pair.Value.transform.SetParent(ToolPivot, false);
                pair.Value.transform.localPosition = Vector3.zero;
                pair.Value.transform.localRotation = Quaternion.identity;
            }
        }

        private GameObject NewTool(string name)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(ToolPivot, false);
            return root;
        }

        private static void AddPart(GameObject root, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            RuntimeAssets.CreatePrimitive(type, root.transform, "Part", position, scale, material);
        }

        private GameObject BuildScoop(Material metal, Material dark, Material accent)
        {
            GameObject root = NewTool("Scoop");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.04f, 0.31f, 0.04f), dark);
            AddPart(root, PrimitiveType.Sphere, new Vector3(0f, -0.26f, 0f), new Vector3(0.22f, 0.22f, 0.22f), metal);
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.29f, 0f), new Vector3(0.056f, 0.065f, 0.056f), accent);
            return root;
        }

        private GameObject BuildTongs(Material metal, Material dark, Material accent)
        {
            GameObject root = NewTool("Tongs");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(0.04f, 0.23f, 0.04f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject arm = RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, root.transform, "Arm",
                    new Vector3(side * 0.045f, -0.2f, 0f), new Vector3(0.018f, 0.34f, 0.028f), metal);
                arm.transform.localRotation = Quaternion.Euler(0f, 0f, side * 9.2f);
                GameObject tip = RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, root.transform, "Tip",
                    new Vector3(side * 0.075f, -0.4f, 0f), new Vector3(0.03f, 0.1f, 0.04f), metal);
                tip.transform.localRotation = Quaternion.Euler(0f, 0f, side * 17.2f);
            }
            GameObject pivot = RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, root.transform, "Pivot",
                new Vector3(0f, -0.05f, 0f), new Vector3(0.052f, 0.015f, 0.052f), accent);
            pivot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return root;
        }

        private GameObject BuildHammer(Material metal, Material dark, Material accent)
        {
            GameObject root = NewTool("Hammer");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, -0.05f, 0f), new Vector3(0.04f, 0.31f, 0.04f), accent);
            AddPart(root, PrimitiveType.Cube, new Vector3(0f, 0.27f, 0f), new Vector3(0.24f, 0.085f, 0.09f), metal);
            AddPart(root, PrimitiveType.Cube, new Vector3(-0.12f, 0.27f, 0f), new Vector3(0.045f, 0.1f, 0.1f), dark);
            return root;
        }

        private GameObject BuildChisel(Material metal, Material dark, Material accent)
        {
            GameObject root = NewTool("Chisel");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(0.044f, 0.24f, 0.044f), accent);
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, -0.31f, 0f), new Vector3(0.04f, 0.1f, 0.04f), metal);
            GameObject blade = RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cube, root.transform, "Blade",
                new Vector3(0f, -0.46f, 0f), new Vector3(0.065f, 0.13f, 0.065f), metal);
            blade.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            return root;
        }

        private GameObject BuildRake(Material metal, Material dark, Material accent)
        {
            GameObject root = NewTool("Rake");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.036f, 0.36f, 0.036f), dark);
            AddPart(root, PrimitiveType.Cube, new Vector3(0f, -0.34f, 0f), new Vector3(0.3f, 0.026f, 0.03f), metal);
            for (int i = -2; i <= 2; i++)
            {
                AddPart(
                    root, PrimitiveType.Cube,
                    new Vector3(i * 0.062f, -0.42f, 0f),
                    new Vector3(0.016f, 0.16f, 0.018f), metal);
            }
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.33f, 0f), new Vector3(0.052f, 0.07f, 0.052f), accent);
            return root;
        }

        private GameObject BuildDriveTube(Material metal, Material dark, Material accent, Material tube)
        {
            GameObject root = NewTool("Drive Tube");
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, -0.02f, 0f), new Vector3(0.11f, 0.23f, 0.11f), tube);
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.14f, 0.025f, 0.14f), dark);
            AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, -0.26f, 0f), new Vector3(0.15f, 0.035f, 0.15f), accent);
            GameObject handle = RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, root.transform, "Handle",
                new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.14f, 0.04f), dark);
            handle.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            return root;
        }

        private GameObject CreateFallbackModel(Transform parent)
        {
            Material suit = RuntimeAssets.CreateLit("fallback-suit", new Color(0.82f, 0.81f, 0.77f), 0.1f, 0.35f);
            Material visor = RuntimeAssets.CreateLit("fallback-visor", new Color(0.85f, 0.6f, 0.2f), 0.9f, 0.7f);
            Material pack = RuntimeAssets.CreateLit("fallback-pack", new Color(0.55f, 0.56f, 0.55f), 0.4f, 0.4f);

            GameObject root = new GameObject("Fallback Astronaut");
            root.transform.SetParent(parent, false);

            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Capsule, root.transform, "Body",
                new Vector3(0f, 0.95f, 0f), new Vector3(0.62f, 0.72f, 0.5f), suit);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, root.transform, "Helmet",
                new Vector3(0f, 1.63f, 0f), new Vector3(0.48f, 0.48f, 0.48f), suit);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, root.transform, "Visor",
                new Vector3(0f, 1.63f, 0.2f), new Vector3(0.36f, 0.3f, 0.16f), visor);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cube, root.transform, "Backpack",
                new Vector3(0f, 1.05f, -0.34f), new Vector3(0.52f, 0.66f, 0.24f), pack);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Capsule, root.transform, "Left Leg",
                new Vector3(-0.18f, 0.34f, 0f), new Vector3(0.2f, 0.36f, 0.2f), suit);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Capsule, root.transform, "Right Leg",
                new Vector3(0.18f, 0.34f, 0f), new Vector3(0.2f, 0.36f, 0.2f), suit);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Capsule, root.transform, "Left Arm",
                new Vector3(-0.42f, 1.02f, 0f), new Vector3(0.16f, 0.34f, 0.16f), suit);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Capsule, root.transform, "Right Arm",
                new Vector3(0.42f, 1.02f, 0f), new Vector3(0.16f, 0.34f, 0.16f), suit);

            return root;
        }

        private static Bounds CalculateBounds(GameObject model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(Vector3.zero, new Vector3(0.6f, 1.8f, 0.6f));
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return bounds;
        }

        private static Transform FindBone(Transform root, params string[] names)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                for (int j = 0; j < names.Length; j++)
                {
                    if (string.Equals(transforms[i].name, names[j], StringComparison.OrdinalIgnoreCase))
                    {
                        return transforms[i];
                    }
                }
            }

            for (int i = 0; i < transforms.Length; i++)
            {
                for (int j = 0; j < names.Length; j++)
                {
                    if (transforms[i].name.IndexOf(names[j], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return transforms[i];
                    }
                }
            }
            return null;
        }
    }
}
