using System.Collections.Generic;
using UnityEngine;
using Run.Common;
using Run.Core;
using Run.Effects;
using Run.Generation;
using Run.Player;

namespace Run.EnvironmentMode
{
    /// <summary>One trap on the environment player's hotbar.</summary>
    [System.Serializable]
    public struct TrapCard
    {
        public TrapKind Kind;
        public string Label;
        [Min(0)] public int Cost;
    }

    /// <summary>
    /// The human's side of Environment mode: an energy bar and a hotbar of traps to drop into
    /// the level ahead of the computer runner, while the run keeps going.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Controls: 1–3 (or the mouse wheel) pick a trap, left click places it. The cursor shows a
    /// preview, green where the trap can go and red (with a reason on the HUD) where it can't.
    /// </para>
    /// <para>
    /// Fair-play rules keep it a contest instead of a guaranteed win: traps cost energy, they
    /// start as a harmless ghost for <c>_armDelay</c>, nothing can be placed inside the red
    /// warning zone in front of the runner, traps need flat ground and some space between them,
    /// and each level chunk holds only a few.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentPlayer : MonoBehaviour
    {
        private static readonly Color ValidTint = new Color(0.43f, 0.94f, 0.78f);
        private static readonly Color InvalidTint = new Color(0.95f, 0.45f, 0.35f);

        public static EnvironmentPlayer Instance { get; private set; }

        [Header("Energy")]
        [SerializeField, Min(1f)] private float _maxEnergy = 100f;
        [SerializeField, Min(0f)] private float _startEnergy = 40f;
        [SerializeField, Min(0f)] private float _energyPerSecond = 9f;
        [Tooltip("Energy refunded every time the runner loses a heart.")]
        [SerializeField, Min(0f)] private float _energyPerHit = 15f;

        [Header("Traps")]
        [SerializeField] private TrapCard[] _cards =
        {
            new TrapCard { Kind = TrapKind.Crate, Label = "CRATE", Cost = 20 },
            new TrapCard { Kind = TrapKind.Spikes, Label = "SPIKES", Cost = 30 },
            new TrapCard { Kind = TrapKind.Drone, Label = "DRONE", Cost = 45 }
        };
        [Tooltip("Seconds a placed trap stays a harmless ghost before it turns real.")]
        [SerializeField, Min(0f)] private float _armDelay = 0.4f;

        [Header("Fair Play")]
        [Tooltip("Seconds of warning, on top of the arm delay, that the red zone in front of the runner guarantees.")]
        [SerializeField, Min(0f)] private float _reactionMargin = 0.25f;
        [Tooltip("Minimum gap between two placed traps, in world units.")]
        [SerializeField, Min(0f)] private float _minTrapGap = 1f;
        [Tooltip("Most placed traps a single level chunk can hold.")]
        [SerializeField, Min(1)] private int _maxTrapsPerChunk = 2;

        private GameManager _game;
        private Camera _camera;
        private PlayerController _runner;
        private Collider2D _runnerBody;
        private PlayerHealth _runnerHealth;
        private int _groundMask;
        private int _lastHearts;
        private Transform _poolRoot;
        private readonly Dictionary<TrapKind, Stack<PlacedTrap>> _pool = new Dictionary<TrapKind, Stack<PlacedTrap>>();
        private readonly Dictionary<TrapKind, GameObject> _previews = new Dictionary<TrapKind, GameObject>();
        private readonly Dictionary<TrapKind, SpriteTint> _previewTints = new Dictionary<TrapKind, SpriteTint>();
        private SpriteRenderer _footprintBar;
        private SpriteRenderer _zone;
        private SpriteRenderer _zoneEdge;

        /// <summary>True while an Environment-mode run is live and the human is placing traps.</summary>
        public bool IsActive { get; private set; }
        public float Energy { get; private set; }
        public float MaxEnergy => _maxEnergy;
        public IReadOnlyList<TrapCard> Cards => _cards;
        public int SelectedIndex { get; private set; }

        /// <summary>Why the spot under the cursor can't take the selected trap, or null if it can.</summary>
        public string Hint { get; private set; }
        public int TrapsPlaced { get; private set; }

        /// <summary>Hearts the runner has lost this run (to any hazard, placed or not).</summary>
        public int HitsLanded { get; private set; }

        /// <summary>World X where the red no-place zone in front of the runner ends.</summary>
        public float NoPlaceX { get; private set; }

        /// <summary>The outcome of checking one spot for one trap.</summary>
        public struct Placement
        {
            public bool Valid;
            public string Reason;
            public LevelChunk Chunk;
            public Vector2 Surface;   // where the trap's base would sit
            public bool HasSurface;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            int ground = LayerMask.NameToLayer("Ground");
            _groundMask = ground >= 0 ? 1 << ground : Physics2D.DefaultRaycastLayers;
            BuildVisuals();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_game != null) _game.StateChanged -= OnGameStateChanged;
            if (_runnerHealth != null) _runnerHealth.HealthChanged -= OnRunnerHealthChanged;
        }

        private void Start()
        {
            _camera = Camera.main;
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _runner = player.GetComponent<PlayerController>();
                _runnerBody = player.GetComponent<Collider2D>();
                _runnerHealth = player.GetComponent<PlayerHealth>();
            }
            if (_runnerHealth != null)
            {
                _runnerHealth.HealthChanged += OnRunnerHealthChanged;
                _lastHearts = _runnerHealth.CurrentHearts;
            }

            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.StateChanged += OnGameStateChanged;
                OnGameStateChanged(_game.State);
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            bool wasActive = IsActive;
            IsActive = state == GameState.Playing && _game.RunMode == GameMode.Environment;
            if (IsActive && !wasActive)
            {
                Energy = Mathf.Min(_startEnergy, _maxEnergy);
                TrapsPlaced = 0;
                HitsLanded = 0;
                if (_runnerHealth != null) _lastHearts = _runnerHealth.CurrentHearts;
            }
            if (!IsActive)
            {
                Hint = null;
                ShowPreview(null, default);
                SetZoneVisible(false);
            }
        }

        private void OnRunnerHealthChanged(int hearts)
        {
            if (IsActive && hearts < _lastHearts)
            {
                HitsLanded += _lastHearts - hearts;
                AddEnergy(_energyPerHit * (_lastHearts - hearts));
            }
            _lastHearts = hearts;
        }

        /// <summary>Picks a hotbar slot (0-based); out-of-range indices are ignored.</summary>
        public void Select(int index)
        {
            if (index >= 0 && index < _cards.Length) SelectedIndex = index;
        }

        public bool CanAfford(int index) => index >= 0 && index < _cards.Length && Energy >= _cards[index].Cost;

        private void AddEnergy(float amount) => Energy = Mathf.Clamp(Energy + amount, 0f, _maxEnergy);

        private void Update()
        {
            if (!IsActive || _camera == null || _runner == null || _runnerBody == null || _cards.Length == 0)
            {
                return;
            }

            // Paused (the pause menu sets timeScale to 0): no building while the world is frozen.
            if (Time.timeScale <= 0f)
            {
                Hint = null;
                ShowPreview(null, default);
                return;
            }

            AddEnergy(_energyPerSecond * Time.deltaTime);
            HandleSelection();
            NoPlaceX = ComputeNoPlaceX();
            UpdateZone();

            if (!RunInput.TryGetPointer(out Vector2 screen) || RunInput.PointerOverUI())
            {
                Hint = null;
                ShowPreview(null, default);
                return;
            }

            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            TrapCard card = _cards[SelectedIndex];
            Placement placement = Evaluate(card, world);
            Hint = placement.Valid ? null : placement.Reason;
            ShowPreview(card, placement);

            if (placement.Valid && RunInput.PointerPressed())
            {
                Place(card, placement);
            }
        }

        /// <summary>
        /// Places hotbar trap <paramref name="cardIndex"/> at <paramref name="worldPoint"/> if the
        /// rules allow it (the same rules as a click). Returns the reason when it can't.
        /// </summary>
        public bool TryPlace(int cardIndex, Vector2 worldPoint, out string reason)
        {
            reason = null;
            if (!IsActive || cardIndex < 0 || cardIndex >= _cards.Length)
            {
                reason = "Not placing right now";
                return false;
            }

            NoPlaceX = ComputeNoPlaceX();
            Placement placement = Evaluate(_cards[cardIndex], worldPoint);
            if (!placement.Valid)
            {
                reason = placement.Reason;
                return false;
            }

            Place(_cards[cardIndex], placement);
            return true;
        }

        private void HandleSelection()
        {
            int number = RunInput.NumberKeyPressed();
            if (number > 0) Select(number - 1);

            float scroll = RunInput.ScrollDelta();
            if (scroll != 0f)
            {
                int step = scroll > 0f ? -1 : 1;
                SelectedIndex = (SelectedIndex + step + _cards.Length) % _cards.Length;
            }
        }

        /// <summary>
        /// The runner's warning zone: it covers arm delay + reaction margin at the run speed.
        /// (The steady scroll speed, not the brief catch-up sprint, so the zone doesn't jitter.)
        /// </summary>
        private float ComputeNoPlaceX()
        {
            Bounds runner = _runnerBody.bounds;
            return runner.max.x + _runner.RunSpeed * (_armDelay + _reactionMargin);
        }

        /// <summary>Checks whether <paramref name="card"/>'s trap could be placed under <paramref name="pointer"/>.</summary>
        public Placement Evaluate(TrapCard card, Vector2 pointer)
        {
            var result = new Placement();
            Vector2 size = TrapFactory.BodySize(card.Kind);
            float width = TrapFactory.FootprintWidth(card.Kind);

            // Find the floor under the cursor. If the cursor is inside solid ground, start from
            // that piece's top instead, so pointing at the dirt still means "on this ground".
            Vector2 origin = new Vector2(pointer.x, pointer.y + 0.75f);
            Collider2D inside = Physics2D.OverlapPoint(origin, _groundMask);
            if (inside != null) origin.y = inside.bounds.max.y + 0.05f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 25f, _groundMask);
            if (hit.collider == null) return Fail(result, "Needs ground");
            if (hit.collider.GetComponent<PlacedTrap>() != null) return Fail(result, "Can't stack traps");

            result.Chunk = hit.collider.GetComponentInParent<LevelChunk>();
            if (result.Chunk == null) return Fail(result, "Needs ground");
            result.Surface = new Vector2(pointer.x, hit.point.y);
            result.HasSurface = true;

            float left = pointer.x - width * 0.5f;
            float right = pointer.x + width * 0.5f;

            // Flat, solid ground across the whole footprint (a drone needs its whole patrol).
            if (!FloorAt(left + 0.05f, hit.point.y) || !FloorAt(right - 0.05f, hit.point.y) ||
                (width > 2f && !FloorAt(pointer.x, hit.point.y)))
                return Fail(result, "Needs flat ground");

            Vector2 roomCentre = new Vector2(pointer.x, hit.point.y + size.y * 0.5f + 0.05f);
            if (Physics2D.OverlapBox(roomCentre, new Vector2(width - 0.1f, size.y - 0.1f), 0f, _groundMask) != null)
                return Fail(result, "No room here");

            if (left < NoPlaceX) return Fail(result, "Too close to the runner");

            float cameraRight = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect;
            if (right > cameraRight) return Fail(result, "Too far ahead");

            foreach (var trap in PlacedTrap.Active)
            {
                Rect other = trap.Footprint;
                bool sameLevel = Mathf.Abs(other.yMin - hit.point.y) < 1.5f;
                float gap = Mathf.Max(other.xMin - right, left - other.xMax);
                if (sameLevel && gap < _minTrapGap) return Fail(result, "Too close to another trap");
            }

            if (result.Chunk.AttachmentCount >= _maxTrapsPerChunk) return Fail(result, "This section is full");
            if (Energy < card.Cost) return Fail(result, "Not enough energy");

            result.Valid = true;
            return result;
        }

        private static Placement Fail(Placement placement, string reason)
        {
            placement.Valid = false;
            placement.Reason = reason;
            return placement;
        }

        private bool FloorAt(float x, float surfaceY)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(x, surfaceY + 0.3f), Vector2.down, 0.6f, _groundMask);
            return hit.collider != null && Mathf.Abs(hit.point.y - surfaceY) < 0.03f &&
                   hit.collider.GetComponent<PlacedTrap>() == null;
        }

        private void Place(TrapCard card, Placement placement)
        {
            PlacedTrap trap = Take(card.Kind);
            trap.Place(placement.Chunk, placement.Surface, _armDelay, ReturnToPool);
            Energy -= card.Cost;
            TrapsPlaced++;
            GameFeel.Instance?.Emit(BurstKind.Start, placement.Surface);
        }

        // ---- Pooling ----------------------------------------------------------------------

        private PlacedTrap Take(TrapKind kind)
        {
            if (_pool.TryGetValue(kind, out var stack) && stack.Count > 0)
            {
                return stack.Pop();
            }
            return TrapFactory.Build(kind, _poolRoot);
        }

        private void ReturnToPool(PlacedTrap trap)
        {
            if (this == null || _poolRoot == null) return; // scene is unloading
            trap.transform.SetParent(_poolRoot, false);
            if (!_pool.TryGetValue(trap.Kind, out var stack))
            {
                _pool[trap.Kind] = stack = new Stack<PlacedTrap>();
            }
            stack.Push(trap);
        }

        // ---- Visuals ----------------------------------------------------------------------

        private void BuildVisuals()
        {
            _poolRoot = new GameObject("Trap Pool").transform;
            _poolRoot.SetParent(transform, false);

            var previews = new GameObject("Trap Previews").transform;
            previews.SetParent(transform, false);
            foreach (TrapKind kind in System.Enum.GetValues(typeof(TrapKind)))
            {
                var preview = TrapFactory.BuildPreview(kind, previews);
                foreach (var sprite in preview.GetComponentsInChildren<SpriteRenderer>()) sprite.sortingOrder += 20;
                preview.SetActive(false);
                _previews[kind] = preview;
                _previewTints[kind] = new SpriteTint(preview);
            }

            _footprintBar = MakeSprite("Footprint", previews, 24);
            _zone = MakeSprite("No-Place Zone", transform, 20);
            _zoneEdge = MakeSprite("No-Place Edge", transform, 21);
            _zone.color = new Color(InvalidTint.r, InvalidTint.g, InvalidTint.b, 0.045f);
            _zoneEdge.color = new Color(InvalidTint.r, InvalidTint.g, InvalidTint.b, 0.45f);
            SetZoneVisible(false);
        }

        private static SpriteRenderer MakeSprite(string name, Transform parent, int order)
        {
            var sprite = new GameObject(name).AddComponent<SpriteRenderer>();
            sprite.transform.SetParent(parent, false);
            sprite.sprite = Shapes.Rectangle;
            sprite.sortingOrder = order;
            sprite.enabled = false;
            return sprite;
        }

        private void ShowPreview(TrapCard? card, Placement placement)
        {
            foreach (var pair in _previews)
            {
                bool show = card.HasValue && pair.Key == card.Value.Kind;
                if (pair.Value.activeSelf != show) pair.Value.SetActive(show);
            }
            _footprintBar.enabled = card.HasValue && placement.HasSurface;
            if (!card.HasValue) return;

            TrapKind kind = card.Value.Kind;
            Vector2 size = TrapFactory.BodySize(kind);
            Color tint = placement.Valid ? ValidTint : InvalidTint;
            _previewTints[kind].Set(tint, 0.55f, placement.HasSurface ? 0.6f : 0.3f);

            Vector2 at = placement.Surface;
            if (!placement.HasSurface && RunInput.TryGetPointer(out Vector2 screen))
            {
                at = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            }
            _previews[kind].transform.position = new Vector3(at.x, at.y + size.y * 0.5f, 0f);

            if (_footprintBar.enabled)
            {
                // A thin bar on the ground shows how much space the trap claims.
                _footprintBar.transform.position = new Vector3(at.x, at.y + 0.03f, 0f);
                _footprintBar.transform.localScale = new Vector3(TrapFactory.FootprintWidth(kind), 0.06f, 1f);
                _footprintBar.color = new Color(tint.r, tint.g, tint.b, 0.8f);
            }
        }

        private void UpdateZone()
        {
            float halfHeight = _camera.orthographicSize;
            float left = _camera.transform.position.x - halfHeight * _camera.aspect;
            float width = Mathf.Max(0f, NoPlaceX - left);
            float y = _camera.transform.position.y;
            SetZoneVisible(width > 0f);
            _zone.transform.position = new Vector3(left + width * 0.5f, y, 0f);
            _zone.transform.localScale = new Vector3(width, halfHeight * 2f, 1f);
            _zoneEdge.transform.position = new Vector3(NoPlaceX, y, 0f);
            _zoneEdge.transform.localScale = new Vector3(0.06f, halfHeight * 2f, 1f);
        }

        private void SetZoneVisible(bool visible)
        {
            if (_zone == null) return;
            _zone.enabled = visible;
            _zoneEdge.enabled = visible;
        }
    }
}
