using System.Collections.Generic;
using UnityEngine;

namespace GatheringSamples
{
    public class AudioManager : MonoBehaviour
    {
        private AudioSource source;
        private AudioSource ambientSource;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

        public void Init()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0.7f;

            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.spatialBlend = 0f;
            ambientSource.loop = true;
            ambientSource.volume = 0.045f;

            BuildClips();
        }

        public void StartAmbient()
        {
            if (ambientSource == null) return;
            if (!ambientSource.isPlaying)
            {
                ambientSource.clip = CreateAmbient();
                ambientSource.Play();
            }
        }

        public void StopAmbient()
        {
            if (ambientSource != null) ambientSource.Stop();
        }

        public void Click() => Play("click");
        public void Select() => Play("select");
        public void Warning() => Play("warning");
        public void Critical() => Play("critical");
        public void Tool() => Play("tool");
        public void Collect() => Play("collect");
        public void Success() => Play("success");
        public void Fail() => Play("fail");
        public void Footstep() => Play("footstep");
        public void Hop() => Play("hop");
        public void Land() => Play("land");

        private void Play(string id)
        {
            if (source == null) return;
            if (clips.TryGetValue(id, out AudioClip clip) && clip != null)
            {
                source.PlayOneShot(clip, 0.85f);
            }
        }

        private void BuildClips()
        {
            clips["click"] = Tone("click", 0.055f, 880f, 0.32f);
            clips["select"] = Arpeggio("select", new[] { 660f, 880f }, 0.06f, 0.26f);
            clips["warning"] = Tone("warning", 0.24f, 220f, 0.3f);
            clips["critical"] = Tone("critical", 0.36f, 165f, 0.34f);
            clips["tool"] = NoiseBurst("tool", 0.18f, 0.22f);
            clips["collect"] = Arpeggio("collect", new[] { 520f, 660f, 780f }, 0.09f, 0.28f);
            clips["success"] = Arpeggio("success", new[] { 523f, 659f, 784f, 1046f }, 0.14f, 0.3f);
            clips["fail"] = Arpeggio("fail", new[] { 220f, 165f, 110f }, 0.18f, 0.3f);
            clips["footstep"] = NoiseBurst("footstep", 0.09f, 0.16f);
            clips["hop"] = Arpeggio("hop", new[] { 300f, 440f, 540f }, 0.07f, 0.22f);
            clips["land"] = NoiseBurst("land", 0.14f, 0.2f);
        }

        private AudioClip Tone(string name, float duration, float frequency, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] data = new float[sampleCount];
            float attack = Mathf.Min(0.012f, duration * 0.2f);
            float release = Mathf.Min(0.08f, duration * 0.45f);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float envelope = Envelope(i, sampleCount, attack * sampleRate, release * sampleRate);
                float value = Mathf.Sin(time * frequency * Mathf.PI * 2f) * 0.72f;
                value += Mathf.Sin(time * frequency * 2f * Mathf.PI * 2f) * 0.18f;
                data[i] = value * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip Arpeggio(string name, float[] frequencies, float noteDuration, float volume)
        {
            int sampleRate = 44100;
            int noteSamples = Mathf.Max(1, Mathf.CeilToInt(noteDuration * sampleRate));
            int sampleCount = noteSamples * frequencies.Length;
            float[] data = new float[sampleCount];

            for (int note = 0; note < frequencies.Length; note++)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    int index = note * noteSamples + i;
                    float time = (float)i / sampleRate;
                    float envelope = Envelope(i, noteSamples, sampleRate * 0.006f, sampleRate * 0.05f);
                    float value = Mathf.Sin(time * frequencies[note] * Mathf.PI * 2f) * 0.72f;
                    value += Mathf.Sin(time * frequencies[note] * 2f * Mathf.PI * 2f) * 0.16f;
                    data[index] = value * envelope * volume;
                }
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip NoiseBurst(string name, float duration, float volume)
        {
            int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(duration * sampleRate));
            float[] data = new float[sampleCount];
            System.Random random = new System.Random(name.GetHashCode());
            float previous = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float envelope = Envelope(i, sampleCount, sampleRate * 0.006f, sampleRate * duration * 0.8f);
                float white = (float)random.NextDouble() * 2f - 1f;
                previous = Mathf.Lerp(previous, white, 0.22f);
                data[i] = previous * envelope * volume;
            }

            AudioClip clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateAmbient()
        {
            int sampleRate = 44100;
            int sampleCount = sampleRate * 4;
            float[] data = new float[sampleCount];
            System.Random random = new System.Random(31415);
            float filtered = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = (float)i / sampleRate;
                float hum = Mathf.Sin(time * 55f * Mathf.PI * 2f) * 0.2f;
                hum += Mathf.Sin(time * 82.5f * Mathf.PI * 2f) * 0.08f;
                float white = (float)random.NextDouble() * 2f - 1f;
                filtered = Mathf.Lerp(filtered, white, 0.006f);
                data[i] = (hum + filtered * 0.35f) * 0.5f;
            }

            AudioClip clip = AudioClip.Create("ambient", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Envelope(int index, int count, float attackSamples, float releaseSamples)
        {
            float attack = attackSamples > 0f ? Mathf.Clamp01(index / attackSamples) : 1f;
            float release = releaseSamples > 0f ? Mathf.Clamp01((count - index) / releaseSamples) : 1f;
            return Mathf.Min(attack, release);
        }
    }
}
