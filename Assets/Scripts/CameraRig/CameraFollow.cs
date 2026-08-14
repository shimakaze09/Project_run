using UnityEngine;
using Run.Core;

namespace Run.CameraRig
{
    /// <summary>
    /// Frames the run: follows the shared scroll line at a fixed height, centring a little
    /// ahead of it, and never scrolling backwards.
    /// </summary>
    /// <remarks>
    /// The camera deliberately tracks <see cref="WorldScroller.ScrollX"/> rather than the
    /// player's transform. The scroll line advances on its own while playing, so the view
    /// keeps moving even when the player is physically blocked — a stuck player slides off
    /// the left edge and the run ends instead of soft-locking. Because the player closes
    /// any gap via its own catch-up, the two stay locked together in normal play with no
    /// drift. Without a scroller present the camera falls back to plain target-following.
    /// </remarks>
    [RequireComponent(typeof(Camera))]
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private string _targetTag = "Player";

        [Tooltip("How far ahead of the scroll line the camera centres, in world units.")]
        [SerializeField] private float _leadDistance = 3f;
        [Tooltip("Fixed world-space Y the camera holds.")]
        [SerializeField] private float _fixedY = 1f;
        [Tooltip("Smoothing applied only when following the player directly (no scroller present).")]
        [SerializeField, Min(0f)] private float _smoothTime = 0.15f;

        private const float CameraZ = -10f;

        private float _smoothVelocityX;

        private void Start()
        {
            if (_target == null)
            {
                var found = GameObject.FindGameObjectWithTag(_targetTag);
                if (found != null)
                {
                    _target = found.transform;
                }
            }

            transform.position = new Vector3(transform.position.x, _fixedY, CameraZ);
        }

        private void LateUpdate()
        {
            var scroller = WorldScroller.Instance;
            float nextX;

            if (scroller != null)
            {
                // The scroll line is already perfectly smooth (a pure integration of run
                // speed), so it is tracked exactly. Smoothing it would only add a
                // speed-proportional lag — which would grow as the run speeds up and shift
                // the player out of their intended screen lane.
                nextX = scroller.ScrollX + _leadDistance;
            }
            else if (_target != null)
            {
                // Fallback: following the transform directly, which does jitter, so smooth it.
                nextX = Mathf.SmoothDamp(
                    transform.position.x, _target.position.x + _leadDistance, ref _smoothVelocityX, _smoothTime);
            }
            else
            {
                return;
            }

            nextX = Mathf.Max(nextX, transform.position.x); // an endless runner never pans back
            transform.position = new Vector3(nextX, _fixedY, CameraZ);
        }

        /// <summary>Re-bases the camera by <paramref name="dx"/> on X (floating origin).</summary>
        public void ShiftX(float dx)
        {
            Vector3 position = transform.position;
            position.x += dx;
            transform.position = position;
        }
    }
}
