using UnityEngine;
using Run.Player;
using Run.Core;

namespace Run.Audio
{
    /// <summary>
    /// Central audio hub. Plays background music and one-shot SFX, and
    /// listens to existing gameplay events so most sounds trigger automatically
    /// without other scripts needing to call this directly.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _backgroundMusic;
        [SerializeField] private AudioClip _jumpClip;
        [SerializeField] private AudioClip _coinClip;
        [SerializeField] private AudioClip _damageClip;
        [SerializeField] private AudioClip _powerUpClip;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (_musicSource != null && _backgroundMusic != null)
            {
                _musicSource.clip = _backgroundMusic;
                _musicSource.loop = true;
                _musicSource.Play();
            }

            var player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.Jumped += PlayJump;
            }
}

        private void OnEnable()
        {
            PowerUpEvents.OnPowerUpActivated += OnPowerUpActivated;
        }

        private void OnDisable()
        {
            PowerUpEvents.OnPowerUpActivated -= OnPowerUpActivated;
        }

        private void OnPowerUpActivated(PowerUpType type, float duration)
        {
            PlaySfx(_powerUpClip);
        }

        /// <summary>Plays the jump sound effect.</summary>
        public void PlayJump() => PlaySfx(_jumpClip);

        /// <summary>Plays the coin collection sound effect.</summary>
        public void PlayCoin() => PlaySfx(_coinClip);

        /// <summary>Plays the damage/hit sound effect.</summary>
        public void PlayDamage() => PlaySfx(_damageClip);

        /// <summary>Plays a one-shot sound effect on the shared SFX source, if both are available.</summary>
        private void PlaySfx(AudioClip clip)
        {
            if (clip != null && _sfxSource != null)
            {
                _sfxSource.PlayOneShot(clip);
            }
        }
    }
}