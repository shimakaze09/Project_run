using UnityEngine;
using Run.Player;

namespace Run.Core
{
    /// <summary>
    /// Single source of truth for how far the world has scrolled — the "scroll line" the
    /// player is expected to occupy at any moment.
    /// </summary>
    /// <remarks>
    /// <para>
    /// While a run is in progress the line advances at the player's run speed, purely by
    /// integration. It is deliberately <em>never</em> snapped to the player's actual
    /// position: doing so lets rendering jitter and collision losses ratchet the line
    /// forward, which reads as the player being slowly dragged left. Instead the line is
    /// an independent target, and <see cref="PlayerController"/> closes the gap by running
    /// faster whenever it trails (see its catch-up settings).
    /// </para>
    /// <para>
    /// Both the camera and the player read this one value, so they can never disagree
    /// about where "forward" is. Outside an active run the line simply holds the player's
    /// position.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WorldScroller : MonoBehaviour
    {
        /// <summary>The active scroller, or <c>null</c> before the scene has loaded one.</summary>
        public static WorldScroller Instance { get; private set; }

        [Tooltip("Scroll speed used while playing if no PlayerController can be found.")]
        [SerializeField, Min(0f)] private float _fallbackScrollSpeed = 7f;

        private PlayerController _player;

        /// <summary>World-space X the player is expected to occupy right now.</summary>
        public float ScrollX { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            var playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.GetComponent<PlayerController>();
                ScrollX = playerObject.transform.position.x;
            }
        }

        private void Update()
        {
            var game = GameManager.Instance;

            if (game != null && game.State == GameState.Playing)
            {
                float speed = _player != null ? _player.RunSpeed : _fallbackScrollSpeed;
                ScrollX += speed * Time.deltaTime;
            }
            else if (_player != null)
            {
                // Ready / Game Over: hold the line on the player so a new run starts framed.
                ScrollX = _player.transform.position.x;
            }
        }

        /// <summary>How far the given X trails the scroll line (0 when level with it or ahead).</summary>
        public float DistanceBehind(float worldX) => Mathf.Max(0f, ScrollX - worldX);

        /// <summary>Re-bases the scroll line by <paramref name="dx"/> on X (floating origin).</summary>
        public void ShiftX(float dx)
        {
            ScrollX += dx;
        }
    }
}
