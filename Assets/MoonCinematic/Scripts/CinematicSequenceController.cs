using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;

namespace MoonCinematic
{
    /// <summary>
    /// Drives the cinematic presentation on top of the camera Timeline:
    /// fade from black, letterbox is handled by anchored UI bars,
    /// opening story text lines and space ambience audio.
    /// Starts everything on scene play (Play On Awake).
    /// </summary>
    [DisallowMultipleComponent]
    public class CinematicSequenceController : MonoBehaviour
    {
        [Header("Timeline")]
        public PlayableDirector director;

        [Header("UI References")]
        public Canvas uiCanvas;
        public Text storyText;
        public Image fadeOverlay;
        public Image topBar;
        public Image bottomBar;

        [Header("Audio")]
        public AudioSource ambienceSource;

        [Header("Timing")]
        public float fadeInDuration = 2.2f;
        public float fadeOutStart = 43.4f;
        public float fadeOutDuration = 1.6f;

        [Header("Presentation")]
        [Tooltip("Insert spacing between letters for a cinematic look.")]
        public bool letterSpacing = true;
        [Tooltip("Seconds for each story line to fade in and out.")]
        public float lineFade = 0.8f;

        [Header("End Card")]
        public Text endCardTitle;
        public Text endCardSub;
        [Tooltip("When the end card starts fading in (after the picture has fully faded to black).")]
        public float endCardStart = 45.8f;
        public float endCardFadeDuration = 1.6f;
        [Tooltip("Seconds the fully visible end card holds before everything is stopped.")]
        public float endCardHoldDuration = 6.5f;
        public string endCardTitleText = "MISSION FAILED";
        public string endCardSubText = "MOON EXPEDITION - LUNAR SURFACE";

        float _lastTextAlpha = 0f;
        Camera[] _cinematicCams;
        bool _cinematicEnded;
        bool _audioStopped;

        [System.Serializable]
        public class StoryLine
        {
            [TextArea(2, 4)]
            public string text;
            public float startTime;
            public float endTime;
        }

        public List<StoryLine> lines = new List<StoryLine>
        {
            new StoryLine { text = "The mission was supposed to be routine.", startTime = 1.6f,  endTime = 8.4f },
            new StoryLine { text = "You and your team were sent to the Moon to collect samples for scientific research.", startTime = 8.6f,  endTime = 15.0f },
            new StoryLine { text = "But during the journey, a critical failure caused the spacecraft to lose control.", startTime = 15.2f, endTime = 21.2f },
            new StoryLine { text = "The spacecraft crashed into the Moon.", startTime = 21.4f, endTime = 28.2f },
            new StoryLine { text = "Your teammates didn't survive.", startTime = 28.4f, endTime = 34.4f },
            new StoryLine { text = "You are the only one left alive.", startTime = 34.6f, endTime = 39.4f },
            new StoryLine { text = "Stranded on the surface of the Moon, with a damaged spacecraft and limited resources, you must find a way to survive.", startTime = 39.6f, endTime = 45.0f }
        };

        void Awake()
        {
            if (uiCanvas != null && !uiCanvas.gameObject.activeSelf) uiCanvas.gameObject.SetActive(true);
            if (fadeOverlay != null) fadeOverlay.color = new Color(0f, 0f, 0f, 1f);
            if (storyText != null) storyText.color = new Color(storyText.color.r, storyText.color.g, storyText.color.b, 0f);

            if (director != null && director.playableAsset != null)
            {
                director.Play();
            }
            if (ambienceSource != null)
            {
                ambienceSource.loop = true;
                if (!ambienceSource.playOnAwake) ambienceSource.Play();
            }

            // end card starts hidden so it can never flash on the first frame
            if (endCardTitle != null)
            {
                endCardTitle.text = "";
                var ct = endCardTitle.color; ct.a = 0f; endCardTitle.color = ct;
                endCardTitle.gameObject.SetActive(false);
            }
            if (endCardSub != null)
            {
                endCardSub.text = "";
                var cs = endCardSub.color; cs.a = 0f; endCardSub.color = cs;
                endCardSub.gameObject.SetActive(false);
            }

            // guarantee the Game view renders in the very first frame: the Timeline
            // activation tracks take over after this, but never allow a state where
            // ZERO renderable cameras (enabled Camera component on an active GameObject
            // = "No cameras rendering")
            if (_cinematicCams == null)
            {
                _cinematicCams = FindObjectsOfType<Camera>(true)
                    .Where(c => c.name.StartsWith("Camera_", System.StringComparison.Ordinal))
                    .OrderBy(c => c.name).ToArray();
            }
            if (_cinematicCams != null && !_cinematicCams.Any(c => c != null && c.enabled && c.gameObject.activeInHierarchy))
            {
                var first = _cinematicCams.FirstOrDefault();
                if (first != null)
                {
                    first.gameObject.SetActive(true);
                    first.enabled = true;
                    Debug.LogWarning("[Cinematic] no camera renderable on scene start; activated " + first.name);
                }
            }
        }

        void Update()
        {
            float t = Time.time;
            EnforceCameraPresence();
            UpdateFade(t);
            UpdateStoryText(t);
            if (!_cinematicEnded)
            {
                UpdateAudioFade(t);
                UpdateEndCard(t);
                if (t >= endCardStart + endCardFadeDuration + endCardHoldDuration + 0.4f)
                    EndCinematic();
            }
        }

        /// <summary>Ambience fades out exactly while the picture fades to black, then
        /// the audio source is stopped — no audio dribbles on after the cut to black.</summary>
        void UpdateAudioFade(float t)
        {
            if (ambienceSource == null) return;
            float fadeEnd = fadeOutStart + fadeOutDuration + 0.4f;
            if (t < fadeOutStart) { ambienceSource.volume = 1f; return; }
            if (!_audioStopped)
            {
                ambienceSource.volume = 1f - Mathf.Clamp01(
                    (t - fadeOutStart) / Mathf.Max(0.1f, fadeEnd - fadeOutStart));
                if (t >= fadeEnd)
                {
                    ambienceSource.Stop(); // no audio after the black-out
                    _audioStopped = true;
                }
            }
        }

        /// <summary>Big cinematic end card ("MISSION FAILED") that fades in over the
        /// black screen after the final shot, holds for several seconds so the viewer
        /// clearly understands the trailer is over, then everything stops cleanly.</summary>
        void UpdateEndCard(float t)
        {
            if (endCardTitle == null || t < endCardStart) return;

            if (!endCardTitle.gameObject.activeSelf) endCardTitle.gameObject.SetActive(true);
            float a = Mathf.Clamp01((t - endCardStart) / Mathf.Max(0.1f, endCardFadeDuration));

            string titleBody = letterSpacing ? AddLetterSpacing(endCardTitleText) : endCardTitleText;
            if (endCardTitle.text != titleBody) endCardTitle.text = titleBody;
            var ct = endCardTitle.color; ct.a = a; endCardTitle.color = ct;

            if (endCardSub != null)
            {
                if (!endCardSub.gameObject.activeSelf) endCardSub.gameObject.SetActive(true);
                string subBody = letterSpacing ? AddLetterSpacing(endCardSubText) : endCardSubText;
                if (endCardSub.text != subBody) endCardSub.text = subBody;
                var cs = endCardSub.color; cs.a = a; endCardSub.color = cs;
            }
        }

        void EndCinematic()
        {
            _cinematicEnded = true;
            if (director != null && director.state == PlayState.Playing) director.Stop();
            if (ambienceSource != null && ambienceSource.isPlaying)
            {
                ambienceSource.Stop();
                _audioStopped = true;
            }
            // the end card stays at full alpha on black — the trailer is over
            Debug.Log("[Cinematic] end card finished; cinematic fully stopped (no further audio/camera activity).");
        }

        /// <summary>Last-resort safety net: if the Timeline activation ever leaves the
        /// scene without a single renderable camera, activate the one whose shot window
        /// covers the current director time. An ActivationTrack switches cameras by
        /// setting the bound GameObject's active state, so the fallback does the same.
        /// The ActivationTrack remains the authoritative switcher; this only fires
        /// when it would fail open.</summary>
        void EnforceCameraPresence()
        {
            if (_cinematicEnded) return; // black end card phase — nothing must render behind
            if (_cinematicCams == null || _cinematicCams.Length == 0) return;
            if (_cinematicCams.Any(c => c != null && c.enabled && c.gameObject.activeInHierarchy)) return;

            double t = director != null ? director.time : Time.time;
            string target = t < 8 ? "Camera_01" : t < 15 ? "Camera_02" : t < 22 ? "Camera_03"
                : t < 30 ? "Camera_04" : t < 37 ? "Camera_05" : "Camera_06";
            var cam = _cinematicCams.FirstOrDefault(c => c != null && c.name.StartsWith(target, System.StringComparison.Ordinal))
                      ?? _cinematicCams.FirstOrDefault(c => c != null);
            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                Debug.LogWarning("[Cinematic] fallback camera activated at t=" + t.ToString("F1") + ": " + cam.name);
            }
        }

        void UpdateFade(float t)
        {
            if (fadeOverlay != null)
            {
                float alpha;
                if (t < fadeInDuration) alpha = 1f - Mathf.Clamp01(t / fadeInDuration);
                else if (t >= fadeOutStart) alpha = Mathf.Clamp01((t - fadeOutStart) / fadeOutDuration);
                else alpha = 0f;
                var c = fadeOverlay.color;
                c.a = alpha;
                fadeOverlay.color = c;
            }
        }

        void UpdateStoryText(float t)
        {
            if (storyText == null) return;

            // a single Text is used; only one message is shown at any time and the
            // string is cleared as soon as its fade-out completes, so messages can
            // never overlap or linger on screen.
            StoryLine current = null;
            foreach (var l in lines)
            {
                if (t >= l.startTime && t <= l.endTime) { current = l; break; }
            }

            if (current == null)
            {
                // fade the remaining visibility down, then hard-clear so nothing is left
                float a = Mathf.MoveTowards(_lastTextAlpha, 0f, Time.deltaTime / Mathf.Max(0.1f, lineFade));
                _lastTextAlpha = a;
                var cf = storyText.color;
                cf.a = a;
                storyText.color = cf;
                if (a <= 0.001f && storyText.text != "") storyText.text = "";
                return;
            }

            float target = Mathf.Clamp01((t - current.startTime) / lineFade)
                         * Mathf.Clamp01((current.endTime - t) / lineFade);
            string body = letterSpacing ? AddLetterSpacing(current.text) : current.text;
            if (storyText.text != body) storyText.text = body;
            var c = storyText.color;
            c.a = target;
            storyText.color = c;
            _lastTextAlpha = target;
        }

        /// <summary>Letter spacing between individual characters was removed on
        /// purpose: on-screen messages previously read like "W E L C O M E  T O  T H E
        ///  M O O N". Text is now shown as-is (normal single spaces between words),
        /// while wording, font, size, color, alignment, timing and fades are kept.</summary>
        static string AddLetterSpacing(string s)
        {
            return s;
        }
    }
}
