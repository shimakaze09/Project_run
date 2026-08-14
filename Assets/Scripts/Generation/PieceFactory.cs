using System.Collections.Generic;
using UnityEngine;
using Run.Common;
using Run.Hazards;
using Run.Collectibles;

namespace Run.Generation
{
    /// <summary>The distinct building blocks the level generator can place.</summary>
    public enum PieceKind
    {
        Ground,
        Platform,
        Spike,
        Crate,
        Coin,
        Enemy
    }

    /// <summary>
    /// Creates, configures, and pools the placeholder building blocks that make up the
    /// world. Each <see cref="PieceKind"/> has its own silhouette (from <see cref="Shapes"/>),
    /// colour, and collider so entities read clearly at a glance.
    /// </summary>
    /// <remarks>
    /// The factory is the single place that knows how each kind is assembled. Callers ask
    /// for finished pieces through the <c>Spawn*</c> methods and later hand them back via
    /// <see cref="Release"/>; pooling keeps endless play allocation-free. Solid pieces
    /// (ground, platforms, crates) live on the "Ground" layer so the player can stand on —
    /// and wall-jump off — them; triggers (spikes, coins, enemies) stay off it. A piece's
    /// collider dimensions are set once at build time in unit space and thereafter scaled
    /// by the transform, so placement only ever touches the transform.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PieceFactory : MonoBehaviour
    {
        [Header("Palette")]
        [SerializeField] private Color _groundColor = new Color(0.36f, 0.26f, 0.20f);
        [SerializeField] private Color _platformColor = new Color(0.48f, 0.34f, 0.24f);
        [SerializeField] private Color _crateColor = new Color(0.82f, 0.58f, 0.24f);
        [SerializeField] private Color _spikeColor = new Color(0.90f, 0.35f, 0.20f);
        [SerializeField] private Color _enemyColor = new Color(0.70f, 0.22f, 0.72f);
        [SerializeField] private Color _coinColor = new Color(0.98f, 0.82f, 0.28f);

        [Header("Sorting Orders")]
        [SerializeField] private int _groundOrder = 0;
        [SerializeField] private int _propOrder = 5;

        private readonly Dictionary<PieceKind, Stack<GameObject>> _pool =
            new Dictionary<PieceKind, Stack<GameObject>>();

        private Transform _poolRoot;
        private int _groundLayer;

        private void Awake()
        {
            _poolRoot = new GameObject("PiecePool").transform;
            _poolRoot.SetParent(transform, false);
            _poolRoot.gameObject.SetActive(false);   // pooled pieces rest here, inactive

            _groundLayer = LayerMask.NameToLayer("Ground");
            if (_groundLayer < 0)
            {
                Debug.LogWarning("[PieceFactory] No \"Ground\" layer found. Run Tools ▸ Endless Runner ▸ " +
                                 "Build Scene once so the player can stand on generated ground.");
            }
        }

        // ---- Public spawn API ------------------------------------------------------

        /// <summary>Spawns a solid ground block whose top surface sits at <paramref name="topY"/>.</summary>
        public GameObject SpawnGround(Transform parent, float centerX, float topY, float width, float height)
        {
            var piece = Get(PieceKind.Ground);
            Place(piece, parent, centerX, topY - height * 0.5f, width, height);
            return piece;
        }

        /// <summary>Spawns a solid, standable platform block (top surface at <paramref name="topY"/>).</summary>
        public GameObject SpawnPlatform(Transform parent, float centerX, float topY, float width, float height)
        {
            var piece = Get(PieceKind.Platform);
            Place(piece, parent, centerX, topY - height * 0.5f, width, height);
            return piece;
        }

        /// <summary>Spawns a solid rounded crate resting with its top at <paramref name="topY"/>.</summary>
        public GameObject SpawnCrate(Transform parent, float centerX, float topY, float size)
        {
            var piece = Get(PieceKind.Crate);
            Place(piece, parent, centerX, topY - size * 0.5f, size, size);
            return piece;
        }

        /// <summary>Spawns a single lethal spike tooth sitting on a surface at <paramref name="surfaceY"/>.</summary>
        public GameObject SpawnSpikeTooth(Transform parent, float centerX, float surfaceY, float width, float height)
        {
            var piece = Get(PieceKind.Spike);
            Place(piece, parent, centerX, surfaceY + height * 0.5f, width, height);
            return piece;
        }

        /// <summary>Spawns a collectible coin centred at the given position.</summary>
        public GameObject SpawnCoin(Transform parent, float centerX, float centerY, float size = 0.5f)
        {
            var piece = Get(PieceKind.Coin);
            Place(piece, parent, centerX, centerY, size, size);
            piece.GetComponent<Coin>().Prime();
            return piece;
        }

        /// <summary>Spawns a patrolling enemy standing on a surface, pacing within [minX, maxX].</summary>
        public GameObject SpawnEnemy(Transform parent, float surfaceY, float size, float minX, float maxX, float speed)
        {
            var piece = Get(PieceKind.Enemy);
            float centerX = (minX + maxX) * 0.5f;
            Place(piece, parent, centerX, surfaceY + size * 0.5f, size, size);
            piece.GetComponent<PatrolEnemy>().Configure(minX, maxX, speed);
            return piece;
        }

        /// <summary>Returns a spawned piece to its pool for reuse.</summary>
        public void Release(PieceKind kind, GameObject piece)
        {
            if (piece == null)
            {
                return;
            }

            piece.SetActive(false);
            piece.transform.SetParent(_poolRoot, false);
            GetStack(kind).Push(piece);
        }

        // ---- Pooling ---------------------------------------------------------------

        private GameObject Get(PieceKind kind)
        {
            var stack = GetStack(kind);
            if (stack.Count > 0)
            {
                var reused = stack.Pop();
                reused.SetActive(true);
                return reused;
            }

            return Build(kind);
        }

        private Stack<GameObject> GetStack(PieceKind kind)
        {
            if (!_pool.TryGetValue(kind, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[kind] = stack;
            }

            return stack;
        }

        private GameObject Build(PieceKind kind)
        {
            var piece = new GameObject(kind.ToString());
            var renderer = piece.AddComponent<SpriteRenderer>();

            switch (kind)
            {
                case PieceKind.Ground:
                    renderer.sprite = Shapes.Rectangle;
                    renderer.color = _groundColor;
                    renderer.sortingOrder = _groundOrder;
                    AddUnitBox(piece, isTrigger: false);
                    SetGroundLayer(piece);
                    break;

                case PieceKind.Platform:
                    renderer.sprite = Shapes.Rectangle;
                    renderer.color = _platformColor;
                    renderer.sortingOrder = _groundOrder;
                    AddUnitBox(piece, isTrigger: false);
                    SetGroundLayer(piece);
                    break;

                case PieceKind.Crate:
                    renderer.sprite = Shapes.RoundedSquare;
                    renderer.color = _crateColor;
                    renderer.sortingOrder = _groundOrder + 1;
                    AddUnitBox(piece, isTrigger: false);
                    SetGroundLayer(piece);
                    break;

                case PieceKind.Spike:
                    renderer.sprite = Shapes.Triangle;
                    renderer.color = _spikeColor;
                    renderer.sortingOrder = _propOrder;
                    // Trigger inset into the lower-central mass so only the "meat" of the tooth kills.
                    var spikeBox = piece.AddComponent<BoxCollider2D>();
                    spikeBox.isTrigger = true;
                    spikeBox.size = new Vector2(0.55f, 0.7f);
                    spikeBox.offset = new Vector2(0f, -0.1f);
                    piece.AddComponent<Hazard>();
                    break;

                case PieceKind.Enemy:
                    renderer.sprite = Shapes.Circle;
                    renderer.color = _enemyColor;
                    renderer.sortingOrder = _propOrder;
                    var enemyCircle = piece.AddComponent<CircleCollider2D>();
                    enemyCircle.isTrigger = true;
                    enemyCircle.radius = 0.44f;
                    piece.AddComponent<PatrolEnemy>();
                    break;

                case PieceKind.Coin:
                    renderer.sprite = Shapes.Square;
                    renderer.color = _coinColor;
                    renderer.sortingOrder = _propOrder;
                    var coinBox = piece.AddComponent<BoxCollider2D>();
                    coinBox.isTrigger = true;
                    coinBox.size = new Vector2(0.9f, 0.9f);
                    piece.AddComponent<Coin>();
                    break;
            }

            return piece;
        }

        // ---- Helpers ---------------------------------------------------------------

        private static void AddUnitBox(GameObject piece, bool isTrigger)
        {
            var box = piece.AddComponent<BoxCollider2D>();
            box.isTrigger = isTrigger;
            box.size = Vector2.one;   // unit box; transform scale makes it match the sprite
            box.offset = Vector2.zero;
        }

        private static void Place(GameObject piece, Transform parent, float centerX, float centerY, float width, float height)
        {
            var t = piece.transform;
            t.SetParent(parent, false);
            t.localRotation = Quaternion.identity;
            t.localScale = new Vector3(width, height, 1f);
            t.localPosition = new Vector3(centerX, centerY, 0f);
        }

        private void SetGroundLayer(GameObject piece)
        {
            if (_groundLayer >= 0)
            {
                piece.layer = _groundLayer;
            }
        }
    }
}
