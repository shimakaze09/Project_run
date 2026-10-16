using UnityEngine;
using Run.Common;
using Run.Core;

namespace Run.Collectibles
{
    /// <summary>
    /// A pick-up that grants a timed power-up (see <see cref="PowerUpEvents"/>) on contact, and
    /// gently bobs in place for visual life.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class PowerUpPickup : MonoBehaviour
    {
        [SerializeField] private PowerUpType _powerUpType = PowerUpType.Shield;
        [SerializeField, Min(0f)] private float _duration = 5f;
        [SerializeField, Min(0f)] private float _bobAmplitude = 0.2f;
        [SerializeField, Min(0f)] private float _bobFrequency = 3f;

        private float _startY;
        private float _bobTimer;

        private void Reset()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnEnable()
        {
            _startY = transform.position.y;
            _bobTimer = 0f;
        }

        private void Update()
        {
            _bobTimer += Time.deltaTime * _bobFrequency;
            transform.position = new Vector3(
                transform.position.x,
                _startY + Mathf.Sin(_bobTimer) * _bobAmplitude,
                transform.position.z
            );
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Mirrors Coin's guard: without it, a dead player sliding into a pickup while the
            // Game Over screen is up (physics keeps running - only the Pause menu stops time)
            // would still collect it.
            if (!other.CompareTag(Tags.Player) || GameManager.Instance == null ||
                GameManager.Instance.State != GameState.Playing)
            {
                return;
            }

            PowerUpEvents.TriggerActivated(_powerUpType, _duration);
            gameObject.SetActive(false);
        }
    }
}
