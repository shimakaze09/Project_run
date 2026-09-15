using UnityEngine;
using Run.Common;
using Run.Core;

namespace Run.Collectibles
{
    /// <summary>
    /// A pick-up that awards saved currency and gently bobs in place for visual life.
    /// </summary>
    /// <remarks>
    /// Coins are pooled, so <see cref="Prime"/> is called by the generator after each
    /// (re)placement to reset the collected flag and capture the bob origin. Collecting
    /// simply deactivates the object; the owning segment returns it to the pool later.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Coin : MonoBehaviour, IWorldShiftable
    {
        [SerializeField, Min(0)] private int _value = 1;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.12f;
        [SerializeField, Min(0f)] private float _bobFrequency = 2f;

        private Vector3 _bobOrigin;
        private float _phaseOffset;
        private bool _collected = true;   // inert until Prime() activates it

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        /// <summary>Resets pooled state and records the local position to bob around.</summary>
        public void Prime()
        {
            _collected = false;
            _bobOrigin = transform.localPosition;
            _phaseOffset = transform.position.x;   // desync the bob per coin
        }

        public void RestoreBobOrigin() { transform.localPosition = _bobOrigin; }

        private void Update()
        {
            if (_collected)
            {
                return;
            }

            float bob = Mathf.Sin((Time.time + _phaseOffset) * _bobFrequency) * _bobAmplitude;
            transform.localPosition = _bobOrigin + new Vector3(0f, bob, 0f);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected || !other.CompareTag("Player") || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            _collected = true;
            GameManager.Instance.AddCoins(_value);
            Run.Effects.GameFeel.Instance?.Emit(Run.Effects.BurstKind.Coin, transform.position);
            gameObject.SetActive(false);
        }

        /// <summary>Keeps the cached bob origin aligned after a floating-origin shift.</summary>
        public void OnWorldShift(float dx)
        {
            _bobOrigin.x += dx;
        }
    }
}
