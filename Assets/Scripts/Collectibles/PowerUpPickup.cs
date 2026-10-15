using UnityEngine;
using Run.Common;
using Run.Core;

namespace Run.Collectibles
{
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour, IWorldShiftable
    {
        [SerializeField] private PowerUpType type = PowerUpType.Shield;
        [Tooltip("Active duration in seconds. Ignored for ExtraHeart.")]
        [SerializeField] private float duration = 5f;

        private float startY;
        private float bobTimer;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnEnable()
        {
            startY = transform.position.y;
            bobTimer = 0f;
        }

        private void Update()
        {
            // Bobbing effect along the Y-axis
            bobTimer += Time.deltaTime * 3f;
            transform.position = new Vector3(
                transform.position.x,
                startY + Mathf.Sin(bobTimer) * 0.2f,
                transform.position.z
            );
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                // ExtraHeart is instantaneous and has no countdown duration
                float activeDuration = (type == PowerUpType.ExtraHeart) ? 0f : duration;

                // Fire the event to notify HUD, PlayerHealth, and PlayerController
                PowerUpEvents.TriggerActivated(type, activeDuration);

                // Optional audio/particle cue if available
                Run.Audio.AudioManager.Instance?.PlayPickup();
                Run.Effects.GameFeel.Instance?.Emit(Run.Effects.BurstKind.Spark, transform.position);

                // Recycle/disable the pickup
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Keeps the pickup synchronized when the runner re-bases its floating origin coordinate space.
        /// </summary>
        public void OnWorldShift(float dx)
        {
            Vector3 pos = transform.position;
            pos.x += dx;
            transform.position = pos;
        }
    }
}