using UnityEngine;
using Run.Player;

namespace Run.CameraRig
{
    /// <summary>Visual-only positional shake, applied after CameraFollow and removed before its next tick.</summary>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)]
    public sealed class CameraShake : MonoBehaviour
    {
        [SerializeField] private PlayerHealth _health;
        [Tooltip("Hearts lost in one event required to shake. Current hazards cost one heart.")]
        [SerializeField, Min(1)] private int _heavyDamageThreshold = 1;
        [Tooltip("Maximum positional offset in world units; zero disables shake.")]
        [SerializeField, Min(0f)] private float _intensity = 0.12f;
        [SerializeField, Min(0f)] private float _duration = 0.22f;
        [Tooltip("Decay exponent. Higher values settle faster.")]
        [SerializeField, Min(0.01f)] private float _decayRate = 2f;

        private PlayerHealth _subscribedHealth;
        private int _previousHearts;
        private float _elapsed;
        private bool _shaking;
        private Vector3 _offset;

        private void OnEnable() => BindHealth();
        private void Start() => BindHealth();

        private void BindHealth()
        {
            if (_health == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) _health = player.GetComponent<PlayerHealth>();
            }
            if (_subscribedHealth != null) _subscribedHealth.HealthChanged -= OnHealthChanged;
            _subscribedHealth = _health;
            if (_subscribedHealth == null) return;
            _previousHearts = _subscribedHealth.CurrentHearts;
            _subscribedHealth.HealthChanged += OnHealthChanged;
        }

        private void OnHealthChanged(int hearts)
        {
            int lost = _previousHearts - hearts;
            _previousHearts = hearts;
            if (lost >= Mathf.Max(1, _heavyDamageThreshold)) TriggerShake();
        }

        /// <summary>Hook for a major explosion's UnityEvent or gameplay callback; no damage side effects.</summary>
        public void TriggerMajorExplosion() => TriggerShake();

        private void TriggerShake()
        {
            if (!isActiveAndEnabled || _intensity <= 0f || _duration <= 0f) return;
            // Restart rather than stack so bursts can never grow beyond the configured intensity.
            _elapsed = 0f;
            _shaking = true;
        }

        private void Update()
        {
            RemoveOffset();
            if (_subscribedHealth == null) BindHealth();
        }

        private void LateUpdate() => Simulate(Time.deltaTime);

        /// <summary>Advance the visual offset. A zero delta removes the offset while paused.</summary>
        public void Simulate(float deltaTime)
        {
            RemoveOffset();
            if (!_shaking || deltaTime <= 0f) return;
            _elapsed += deltaTime;
            if (_duration <= 0f || _elapsed >= _duration)
            {
                _shaking = false;
                return;
            }
            float amplitude = Mathf.Max(0f, _intensity) *
                Mathf.Pow(1f - _elapsed / _duration, Mathf.Max(0.01f, _decayRate));
            // Deterministic local oscillation: does not consume Unity's gameplay random stream.
            _offset = new Vector3(Mathf.Sin(_elapsed * 113f), Mathf.Cos(_elapsed * 157f), 0f)
                * (amplitude * 0.70710678f);
            transform.position += _offset;
        }

        private void RemoveOffset()
        {
            transform.position -= _offset;
            _offset = Vector3.zero;
        }

        private void OnDisable()
        {
            if (_subscribedHealth != null) _subscribedHealth.HealthChanged -= OnHealthChanged;
            _subscribedHealth = null;
            _shaking = false;
            RemoveOffset();
        }
    }
}
