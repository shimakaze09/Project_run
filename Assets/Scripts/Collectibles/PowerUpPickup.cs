using UnityEngine;
using Run.Common;
using Run.Core;

namespace Run.Collectibles
{
    [RequireComponent(typeof(Collider2D))]
    public class PowerUpPickup : MonoBehaviour, IWorldShiftable
    {
        [SerializeField] private PowerUpType type = PowerUpType.Shield;
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
                // Fire the event to notify the HUD
                PowerUpEvents.TriggerActivated(type, duration);

                // Recycle/disable the pickup
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// </summary>
        public void OnWorldShift(float dx)
        {
        }
    }
}