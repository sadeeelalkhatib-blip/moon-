using System.Collections;
using UnityEngine;

namespace MoonCinematic
{
    /// <summary>
    /// Randomly fires short bursts of sparks from a damaged spacecraft section,
    /// with a synchronized flicker point light (works during cinematic playback).
    /// </summary>
    [DisallowMultipleComponent]
    public class SparkFlicker : MonoBehaviour
    {
        public ParticleSystem sparkParticles;
        public Light sparkLight;

        [Min(0.05f)] public float minIdleTime = 2.0f;
        [Min(0.05f)] public float maxIdleTime = 7.0f;
        [Min(0.05f)] public float minOnTime = 0.25f;
        [Min(0.05f)] public float maxOnTime = 0.9f;

        void OnEnable()
        {
            if (sparkParticles != null) sparkParticles.Clear(true);
            if (sparkLight != null) sparkLight.enabled = false;
            StartCoroutine(FlickerRoutine());
        }

        IEnumerator FlickerRoutine()
        {
            if (sparkParticles == null && sparkLight == null) yield break;
            yield return new WaitForSeconds(Random.Range(0.8f, 2.5f));

            while (true)
            {
                yield return new WaitForSeconds(Random.Range(minIdleTime, maxIdleTime));
                if (sparkParticles != null) sparkParticles.Play(true);
                if (sparkLight != null) sparkLight.enabled = true;
                yield return new WaitForSeconds(Random.Range(minOnTime, maxOnTime));
                if (sparkParticles != null) sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                if (sparkLight != null) sparkLight.enabled = false;
            }
        }
    }
}
