using System.Collections.Generic;
using UnityEngine;
using Run.Effects;
using Run.Generation;
using Run.Hazards;

namespace Run.EnvironmentMode
{
    /// <summary>
    /// A trap the environment player dropped into the level. It starts as a harmless, pulsing
    /// ghost (the warning) and becomes real after its arming delay.
    /// </summary>
    /// <remarks>
    /// The trap is parented to the <see cref="LevelChunk"/> it sits on, so it scrolls, re-bases
    /// and recycles with that chunk. While it is a ghost its colliders are off; the computer
    /// runner still "sees" it through <see cref="Active"/>, after its own reaction delay.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlacedTrap : MonoBehaviour, IChunkAttachment
    {
        private const float PopDuration = 0.15f;
        private const float PopScale = 1.25f;

        private static readonly List<PlacedTrap> s_active = new List<PlacedTrap>();

        /// <summary>Every trap currently in the level, ghost or armed.</summary>
        public static IReadOnlyList<PlacedTrap> Active => s_active;

        // Statics survive Play-mode restarts when domain reload is off; start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => s_active.Clear();

        public TrapKind Kind { get; private set; }
        public Vector2 BodySize { get; private set; }

        /// <summary><see cref="Time.time"/> when the trap was placed (its ghost appeared).</summary>
        public float PlacedAt { get; private set; }
        public bool IsArmed { get; private set; }
        public LevelChunk Chunk { get; private set; }

        /// <summary>The patrol behaviour, for drones; null for other traps.</summary>
        public PatrolEnemy Drone { get; private set; }

        /// <summary>World rectangle of the trap's body right now (a drone's moves with it).</summary>
        public Rect Bounds
        {
            get
            {
                Vector2 centre = transform.position;
                return new Rect(centre - BodySize * 0.5f, BodySize);
            }
        }

        /// <summary>The stretch of ground this trap claims, including a drone's whole patrol.</summary>
        public Rect Footprint
        {
            get
            {
                float width = TrapFactory.FootprintWidth(Kind);
                Vector3 origin = Chunk != null ? Chunk.transform.position : Vector3.zero;
                return new Rect(origin.x + _homeLocalX - width * 0.5f, origin.y + _surfaceLocalY, width, BodySize.y);
            }
        }

        private Collider2D[] _colliders;
        private SpriteTint _tint;
        private Transform _artwork;
        private Collider2D _runner;
        private float _armAt;
        private float _popTime;
        private float _homeLocalX;
        private float _surfaceLocalY;
        private System.Action<PlacedTrap> _onReleased;

        /// <summary>One-time setup by <see cref="TrapFactory"/>.</summary>
        internal void Init(TrapKind kind, Vector2 bodySize)
        {
            Kind = kind;
            BodySize = bodySize;
            _colliders = GetComponents<Collider2D>();
            _tint = new SpriteTint(gameObject);
            _artwork = transform.Find("Artwork");
            Drone = GetComponent<PatrolEnemy>();
        }

        /// <summary>
        /// Drops the trap onto <paramref name="chunk"/> with its base on the ground at
        /// <paramref name="surface"/>. It arms after <paramref name="armDelay"/> seconds.
        /// </summary>
        public void Place(LevelChunk chunk, Vector2 surface, float armDelay, System.Action<PlacedTrap> onReleased)
        {
            Chunk = chunk;
            _onReleased = onReleased;
            transform.SetParent(chunk.transform, true);
            transform.position = new Vector3(surface.x, surface.y + BodySize.y * 0.5f, 0f);
            _homeLocalX = surface.x - chunk.transform.position.x;
            _surfaceLocalY = surface.y - chunk.transform.position.y;

            PlacedAt = Time.time;
            _armAt = PlacedAt + armDelay;
            IsArmed = false;
            _popTime = 0f;
            if (_artwork != null) _artwork.localScale = Vector3.one;
            SetColliders(false);
            _tint.Set(Color.white, 0f, 0.4f);

            gameObject.SetActive(true);
            if (Drone != null)
            {
                Drone.Configure(surface.x - TrapFactory.DronePatrolHalfRange,
                    surface.x + TrapFactory.DronePatrolHalfRange, TrapFactory.DroneSpeed);
            }
            chunk.Attach(this);
        }

        /// <summary>Removes the trap from the level and hands it back to its pool.</summary>
        public void Release()
        {
            if (Chunk != null)
            {
                Chunk.Detach(this);
                Chunk = null;
            }

            IsArmed = false;
            SetColliders(false);
            gameObject.SetActive(false);
            _onReleased?.Invoke(this);
        }

        public void OnChunkRecycled() => Release();

        public void OnWorldShift(float dx)
        {
            // The transform moves with the chunk; only the drone caches world-space bounds.
            if (Drone != null) Drone.OnWorldShift(dx);
        }

        private void OnEnable() => s_active.Add(this);

        private void OnDisable() => s_active.Remove(this);

        private void Update()
        {
            if (IsArmed)
            {
                // A quick scale "pop" on the artwork (not the colliders) sells the moment it turns real.
                if (_popTime > 0f && _artwork != null)
                {
                    _popTime = Mathf.Max(0f, _popTime - Time.deltaTime);
                    float scale = Mathf.Lerp(1f, PopScale, _popTime / PopDuration);
                    _artwork.localScale = new Vector3(scale, scale, 1f);
                }
                return;
            }

            if (Time.time >= _armAt)
            {
                Arm();
                return;
            }

            // Ghost: a fast pulse that reads as "something is about to appear here".
            float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time - PlacedAt) * 18f);
            _tint.Set(Color.white, 0.35f * pulse, Mathf.Lerp(0.25f, 0.6f, pulse));
        }

        private void Arm()
        {
            // Never materialise inside the runner: a solid crate would shove it out of the ground.
            if (OverlapsRunner())
            {
                GameFeel.Instance?.Emit(BurstKind.Mote, transform.position);
                Release();
                return;
            }

            IsArmed = true;
            SetColliders(true);
            _tint.Restore();
            _popTime = PopDuration;
            GameFeel.Instance?.Emit(BurstKind.Spark, transform.position);
        }

        private bool OverlapsRunner()
        {
            if (_runner == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                _runner = player != null ? player.GetComponent<Collider2D>() : null;
            }

            if (_runner == null) return false;
            Bounds runner = _runner.bounds;
            return Bounds.Overlaps(Rect.MinMaxRect(runner.min.x, runner.min.y, runner.max.x, runner.max.y));
        }

        private void SetColliders(bool enabled)
        {
            foreach (var collider in _colliders)
            {
                collider.enabled = enabled;
            }
        }
    }
}
