using UnityEngine;

namespace GatheringSamples
{
    public class StationView : MonoBehaviour
    {
        public SiteDef Site;
        public LineRenderer Ring;
        public Renderer BeaconRenderer;
        public Light BeaconLight;
        public WorldLabel Label;
        public Material BeaconMaterial;
        public Material RingMaterial;
        public Material BeamMaterial;
        public string BaseLabel;
        public string State = "pending";
        public float PulsePhase;

        public void SetState(string state)
        {
            State = state;
            Color color = state == "verified"
                ? new Color(0.5f, 0.88f, 0.63f)
                : state == "partial"
                    ? new Color(1f, 0.76f, 0.36f)
                    : new Color(1f, 0.62f, 0.28f);

            if (BeaconMaterial != null)
            {
                BeaconMaterial.color = color;
                BeaconMaterial.SetColor("_BaseColor", color);
            }
            if (RingMaterial != null)
            {
                RingMaterial.color = color;
                RingMaterial.SetColor("_BaseColor", color);
            }
            if (BeamMaterial != null)
            {
                Color beam = new Color(color.r, color.g, color.b, 0.16f);
                BeamMaterial.color = beam;
                BeamMaterial.SetColor("_BaseColor", beam);
            }
            if (Ring != null)
            {
                Ring.startColor = color;
                Ring.endColor = color;
            }
            if (BeaconLight != null)
            {
                BeaconLight.color = color;
            }
            if (Label != null)
            {
                Label.SetAccent(color);
                Label.SetText(state == "verified" ? BaseLabel + " · VERIFIED" : BaseLabel);
            }
        }

        private void Update()
        {
            if (BeaconLight != null)
            {
                BeaconLight.intensity = 1.1f + Mathf.Sin(Time.time * 2.1f + PulsePhase) * 0.35f;
            }
        }
    }
}
