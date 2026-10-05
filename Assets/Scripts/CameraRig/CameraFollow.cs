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

        [Header("Environment Mode")]
        [Tooltip("Lead distance in Environment mode: the view sits further ahead so traps can be planned.")]
        [SerializeField] private float _environmentLeadDistance = 7f;
        [Tooltip("Orthographic size in Environment mode (larger shows more of the level).")]
        [SerializeField, Min(1f)] private float _environmentOrthographicSize = 6.5f;
        [Tooltip("Fixed world-space Y in Environment mode, keeping the ground at a similar screen height.")]
        [SerializeField] private float _environmentFixedY = 2f;

        private const float CameraZ = -10f;

        private float _smoothVelocityX;
        private Camera _camera;
        private float _defaultOrthographicSize;
        private float _lead;
        private float _y;
        private bool _snap;

        private void OnEnable() => GameModeSettings.Changed += OnGameModeChanged;

        private void OnDisable() => GameModeSettings.Changed -= OnGameModeChanged;

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

            ApplyFraming();
            transform.position = new Vector3(transform.position.x, _y, CameraZ);
        }

        private void OnGameModeChanged(GameMode mode)
        {
            // The mode only changes on the Ready screen; reframe right away so the player sees
            // the view they're about to play with.
            var game = GameManager.Instance;
            if (game == null || game.State == GameState.Ready)
            {
                ApplyFraming();
            }
        }

        /// <summary>Environment mode zooms out and looks further ahead; Runner mode keeps the classic framing.</summary>
        private void ApplyFraming()
        {
            // Set up lazily: the editor checks drive Start/LateUpdate directly, without Awake.
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
                _defaultOrthographicSize = _camera.orthographicSize;
            }

            // Only while playing, so edit-mode tools always see the classic framing.
            bool environment = Application.isPlaying && GameModeSettings.IsEnvironment;
            _lead = environment ? _environmentLeadDistance : _leadDistance;
            _y = environment ? _environmentFixedY : _fixedY;
            _camera.orthographicSize = environment ? _environmentOrthographicSize : _defaultOrthographicSize;
            _snap = true; // allow one jump backwards when switching back to the closer framing
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
                nextX = scroller.ScrollX + _lead;
            }
            else if (_target != null)
            {
                // Fallback: following the transform directly, which does jitter, so smooth it.
                nextX = Mathf.SmoothDamp(
                    transform.position.x, _target.position.x + _lead, ref _smoothVelocityX, _smoothTime);
            }
            else
            {
                return;
            }

            if (!_snap)
            {
                nextX = Mathf.Max(nextX, transform.position.x); // an endless runner never pans back
            }
            _snap = false;
            transform.position = new Vector3(nextX, _y, CameraZ);
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
