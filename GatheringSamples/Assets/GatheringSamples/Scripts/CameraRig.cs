using UnityEngine;

namespace GatheringSamples
{
    public class CameraRig : MonoBehaviour
    {
        public Camera Cam;
        public Transform Target;
        public float Yaw = Mathf.PI;
        public float Pitch = 0.24f;
        public float Distance = Sim.CameraDistance;

        public void Build(Transform target)
        {
            Target = target;

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            Cam = cameraObject.AddComponent<Camera>();
            Cam.fieldOfView = 55f;
            Cam.nearClipPlane = 0.08f;
            Cam.farClipPlane = 1200f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.004f, 0.007f, 0.012f);
            Cam.allowHDR = true;
            Cam.allowMSAA = true;

            if (Object.FindAnyObjectByType<AudioListener>() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            SnapToTarget();
        }

        public void Orbit(float deltaX, float deltaY)
        {
            Yaw -= deltaX * 0.005f;
            Pitch = Mathf.Clamp(Pitch + deltaY * 0.004f, 0.05f, 1.16f);
        }

        public void Zoom(float delta)
        {
            Distance = Mathf.Clamp(Distance + delta, Sim.CameraMinDistance, Sim.CameraMaxDistance);
        }

        public void ResetOrbit()
        {
            Yaw = Mathf.PI;
            Pitch = 0.24f;
            Distance = Sim.CameraDistance;
            SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (Target == null || Cam == null) return;
            Vector3 pivot = Target.position + Vector3.up * 1.35f;
            Vector3 forward = new Vector3(Mathf.Sin(Yaw), 0f, Mathf.Cos(Yaw));
            float horizontal = Mathf.Cos(Pitch) * Distance;
            float vertical = Mathf.Sin(Pitch) * Distance;
            Vector3 desired = pivot - forward * horizontal + Vector3.up * vertical;
            Cam.transform.position = desired;
            Cam.transform.LookAt(pivot + Vector3.up * 0.2f);
        }

        private void LateUpdate()
        {
            if (Target == null || Cam == null) return;

            Vector3 pivot = Target.position + Vector3.up * 1.35f;
            Vector3 forward = new Vector3(Mathf.Sin(Yaw), 0f, Mathf.Cos(Yaw));
            float horizontal = Mathf.Cos(Pitch) * Distance;
            float vertical = Mathf.Sin(Pitch) * Distance;
            Vector3 desired = pivot - forward * horizontal + Vector3.up * vertical;
            Vector3 direction = desired - pivot;
            float distance = direction.magnitude;

            if (distance > 0.01f)
            {
                if (Physics.SphereCast(
                    pivot, 0.25f, direction.normalized, out RaycastHit hit,
                    distance, ~0, QueryTriggerInteraction.Ignore))
                {
                    bool hitTarget = hit.transform == Target || hit.transform.IsChildOf(Target);
                    if (!hitTarget)
                    {
                        desired = pivot + direction.normalized * Mathf.Max(0.9f, hit.distance - 0.18f);
                    }
                }
            }

            Cam.transform.position = desired;
            Cam.transform.LookAt(pivot + Vector3.up * 0.2f);
        }
    }
}
