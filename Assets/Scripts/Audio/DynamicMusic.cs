using UnityEngine;
using Run.Core;

namespace Run.Audio
{
    /// <summary>
    /// Plays several versions of the same track in sync and fades between them
    /// as the run goes on. Only one version is audible at a time.
    /// </summary>
    public sealed class DynamicMusic : MonoBehaviour
    {
        [Header("Tracks - same length and tempo, calm to intense")]
        [SerializeField] private AudioClip[] _versions = new AudioClip[3];

        [Header("Seconds survived before each version starts")]
        [SerializeField] private float[] _startTimes = { 0f, 60f, 120f };

        [Header("How long the crossfade takes (seconds)")]
        [SerializeField] private float _fadeDuration = 3f;

        private AudioSource[] _sources;
        private int _currentVersion;
        private float _timeSurvived;

        private void Awake()
        {
            // Create one AudioSource per version.
            _sources = new AudioSource[_versions.Length];
            for (int i = 0; i < _versions.Length; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.clip = _versions[i];
                source.loop = true;
                source.playOnAwake = false;
                source.volume = 0f;
                _sources[i] = source;
            }
        }

        private void Start()
        {
            // Start every version at the exact same moment so they stay in sync.
            double startAt = AudioSettings.dspTime + 0.1;
            foreach (AudioSource source in _sources)
            {
                if (source.clip != null)
                {
                    source.PlayScheduled(startAt);
                }
            }
        }

        private void Update()
        {
            // Time.deltaTime is 0 while paused, so pausing stops the timer too.
            _timeSurvived += Time.deltaTime;

            // Step up to the next version once its start time is reached.
            while (_currentVersion + 1 < _startTimes.Length &&
                   _currentVersion + 1 < _sources.Length &&
                   _timeSurvived >= _startTimes[_currentVersion + 1])
            {
                _currentVersion++;
            }

            // Fade the current version up to the player's music volume, fade the rest out.
            float fadeStep = Time.unscaledDeltaTime / Mathf.Max(_fadeDuration, 0.01f);
            for (int i = 0; i < _sources.Length; i++)
            {
                float target = (i == _currentVersion) ? VolumePreferences.MusicVolume : 0f;
                _sources[i].volume = Mathf.MoveTowards(_sources[i].volume, target, fadeStep);
            }
        }
    }
}