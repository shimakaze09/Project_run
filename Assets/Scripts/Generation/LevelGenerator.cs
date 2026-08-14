using System.Collections.Generic;
using UnityEngine;
using Run.CameraRig;
using Run.Core;
using Run.Player;

namespace Run.Generation
{
    /// <summary>
    /// Streams an endless side-scrolling level by generating one chunk ("segment") at
    /// a time just ahead of the camera and recycling chunks once they fall behind it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The algorithm keeps a moving <c>frontier</c>: while the frontier is closer than
    /// <see cref="_spawnAhead"/> beyond the camera's right edge, it builds the next
    /// segment and advances the frontier by that segment's width. Segments whose end
    /// has scrolled past the camera's left edge are released back to the pool.
    /// </para>
    /// <para>
    /// Each segment is chosen from a weighted set of patterns whose mix shifts with a
    /// <c>difficulty</c> value that ramps up with distance. Crucially, every pattern is
    /// sized against the player's <em>current</em> jump reach
    /// (<see cref="PlayerController.MaxJumpSpan"/> / <see cref="PlayerController.MaxJumpHeight"/>),
    /// so no gap or step is ever unclearable even as the run speed rises.
    /// </para>
    /// <para>
    /// To add a new pattern: add an entry to the weight table in
    /// <see cref="ChoosePattern"/> and a matching <c>Build*</c> method that lays pieces
    /// out and returns the chunk width. That is the entire extension surface.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PieceFactory))]
    [DisallowMultipleComponent]
    public sealed class LevelGenerator : MonoBehaviour
    {
        [Header("World")]
        [Tooltip("World-space Y of the ground's top surface.")]
        [SerializeField] private float _groundTopY = 0f;
        [Tooltip("How thick ground blocks are drawn (kept well below the camera's bottom).")]
        [SerializeField, Min(1f)] private float _groundThickness = 10f;

        [Header("Streaming")]
        [Tooltip("Keep the world generated at least this far past the camera's right edge.")]
        [SerializeField, Min(0f)] private float _spawnAhead = 24f;
        [Tooltip("Recycle chunks once they are this far behind the camera's left edge.")]
        [SerializeField, Min(0f)] private float _despawnBehind = 20f;

        [Header("Difficulty")]
        [Tooltip("A fully flat, hazard-free runway of this length at the very start.")]
        [SerializeField, Min(0f)] private float _safeStartDistance = 28f;
        [Tooltip("Distance over which difficulty climbs from its start value to 1.")]
        [SerializeField, Min(1f)] private float _difficultyRampDistance = 600f;
        [SerializeField, Range(0f, 1f)] private float _startDifficulty = 0f;

        [Header("Safety Margins")]
        [Tooltip("Fraction of the player's jump span a gap is allowed to use.")]
        [SerializeField, Range(0.3f, 0.9f)] private float _gapSafety = 0.62f;
        [Tooltip("Fraction of the player's jump height a step-up is allowed to use.")]
        [SerializeField, Range(0.3f, 0.9f)] private float _heightSafety = 0.6f;
        [Tooltip("Clear runway guaranteed before any hazard so the player can react.")]
        [SerializeField, Min(1f)] private float _minReactionRunway = 4f;

        [Header("Flat Sections")]
        [SerializeField] private Vector2 _flatLengthRange = new Vector2(6f, 11f);

        [Header("Seed")]
        [SerializeField] private bool _useRandomSeed = true;
        [SerializeField] private int _seed = 12345;

        [Header("Floating Origin")]
        [Tooltip("Re-base the whole world back toward the origin once the player passes this X, " +
                 "keeping float coordinates small (and precise) on very long runs.")]
        [SerializeField, Min(50f)] private float _rebaseDistance = 1000f;

        private PieceFactory _factory;
        private PlayerController _player;
        private Camera _camera;
        private Transform _levelRoot;
        private System.Random _random;

        private readonly Queue<Segment> _active = new Queue<Segment>();
        private readonly Stack<Segment> _freeSegments = new Stack<Segment>();

        private float _frontierX;   // world X where the next segment will start
        private float _originX;     // where generation began (difficulty distance is measured from here)

        private void Awake()
        {
            _factory = GetComponent<PieceFactory>();
            _camera = Camera.main;
            _levelRoot = new GameObject("Level").transform;
            _random = _useRandomSeed ? new System.Random() : new System.Random(_seed);
        }

        private void Start()
        {
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            if (playerGo != null)
            {
                _player = playerGo.GetComponent<PlayerController>();
            }

            // Begin a little to the left of the player so there is always ground underfoot.
            _originX = (playerGo != null ? playerGo.transform.position.x : 0f) - 6f;
            _frontierX = _originX;

            EnsureGeneratedAhead();
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            EnsureGeneratedAhead();
            RecycleBehind();
            MaintainFloatingOrigin();
        }

        // ---- Streaming -------------------------------------------------------------

        private void EnsureGeneratedAhead()
        {
            float limit = CameraRightEdge() + _spawnAhead;

            // The guard simply caps how many segments we build in a single frame; it is a
            // safety net against pathological config, never hit in normal play.
            int guard = 0;
            while (_frontierX < limit && guard++ < 64)
            {
                var segment = RentSegment();
                segment.Reset(_frontierX);
                segment.Width = BuildSegment(segment, _frontierX);

                _frontierX += segment.Width;
                _active.Enqueue(segment);
            }
        }

        private void RecycleBehind()
        {
            float limit = CameraLeftEdge() - _despawnBehind;
            while (_active.Count > 0 && _active.Peek().EndX < limit)
            {
                var segment = _active.Dequeue();
                segment.Release(_factory);
                _freeSegments.Push(segment);
            }
        }

        /// <summary>
        /// Floating origin: once the player passes <see cref="_rebaseDistance"/>, shift the
        /// entire world (player, camera, every live piece, and the generator's anchors) back
        /// toward the origin by the same amount. Relative positions are unchanged — nothing
        /// moves on screen — but world coordinates stay small, so float precision never
        /// degrades no matter how long the run lasts.
        /// </summary>
        private void MaintainFloatingOrigin()
        {
            if (_player == null || _player.transform.position.x <= _rebaseDistance)
            {
                return;
            }

            float dx = -_rebaseDistance;

            foreach (var segment in _active)
            {
                segment.Shift(dx);
            }
            _frontierX += dx;
            _originX += dx;

            _player.ShiftX(dx);

            if (_camera != null)
            {
                var follow = _camera.GetComponent<CameraFollow>();
                if (follow != null)
                {
                    follow.ShiftX(dx);
                }
                else
                {
                    Vector3 camPos = _camera.transform.position;
                    camPos.x += dx;
                    _camera.transform.position = camPos;
                }
            }

            WorldScroller.Instance?.ShiftX(dx);
            GameManager.Instance?.ShiftDistanceOrigin(dx);
        }

        private Segment RentSegment() => _freeSegments.Count > 0 ? _freeSegments.Pop() : new Segment();

        private float CameraRightEdge()
        {
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            return _camera.transform.position.x + halfWidth;
        }

        private float CameraLeftEdge()
        {
            float halfWidth = _camera.orthographicSize * _camera.aspect;
            return _camera.transform.position.x - halfWidth;
        }

        // ---- Difficulty & reachability --------------------------------------------

        private float CurrentDifficulty(float atX)
        {
            float distance = Mathf.Max(0f, atX - _originX);
            float t = Mathf.Clamp01(distance / _difficultyRampDistance);
            return Mathf.Clamp01(Mathf.Lerp(_startDifficulty, 1f, t));
        }

        private bool InSafeZone(float atX) => (atX - _originX) < _safeStartDistance;

        /// <summary>Largest horizontal gap the player can safely clear right now.</summary>
        private float MaxGap()
        {
            float span = _player != null ? _player.MaxJumpSpan : 4.5f;
            return Mathf.Max(1.5f, span * _gapSafety);
        }

        /// <summary>Tallest step-up the player can safely reach right now.</summary>
        private float MaxStepUp()
        {
            float height = _player != null ? _player.MaxJumpHeight : 3.2f;
            return Mathf.Max(0.5f, height * _heightSafety);
        }

        // ---- Pattern selection -----------------------------------------------------

        private float BuildSegment(Segment segment, float startX)
        {
            if (InSafeZone(startX))
            {
                return BuildFlat(segment, startX, forceSafe: true);
            }

            float difficulty = CurrentDifficulty(startX);
            switch (ChoosePattern(difficulty))
            {
                case 0:  return BuildFlat(segment, startX, forceSafe: false);
                case 1:  return BuildGap(segment, startX, difficulty);
                case 2:  return BuildElevated(segment, startX, difficulty);
                case 3:  return BuildStaircase(segment, startX, difficulty);
                case 4:  return BuildSpikes(segment, startX, difficulty);
                case 5:  return BuildEnemy(segment, startX, difficulty);
                case 6:  return BuildFloatingPlatforms(segment, startX, difficulty);
                default: return BuildFlat(segment, startX, forceSafe: false);
            }
        }

        /// <summary>
        /// Picks a pattern index by weighted chance. Weights interpolate with difficulty
        /// so breathers thin out and hazards grow more common deeper into a run.
        /// </summary>
        private int ChoosePattern(float difficulty)
        {
            // Index:            flat  gap   elev  stair spike enemy float
            float[] weights =
            {
                Mathf.Lerp(0.30f, 0.12f, difficulty), // 0 flat (breather)
                Mathf.Lerp(0.22f, 0.24f, difficulty), // 1 gap
                Mathf.Lerp(0.18f, 0.13f, difficulty), // 2 elevated
                Mathf.Lerp(0.12f, 0.11f, difficulty), // 3 staircase
                Mathf.Lerp(0.08f, 0.18f, difficulty), // 4 spikes
                Mathf.Lerp(0.06f, 0.14f, difficulty), // 5 enemy
                Mathf.Lerp(0.04f, 0.08f, difficulty), // 6 floating platforms
            };

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                total += weights[i];
            }

            double roll = _random.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (roll < weights[i])
                {
                    return i;
                }

                roll -= weights[i];
            }

            return 0;
        }

        // ---- Patterns --------------------------------------------------------------
        // Every pattern begins and ends with solid ground at the base surface so chunks
        // always connect seamlessly, and every gap/step is clamped to the player's reach.

        private float BuildFlat(Segment segment, float startX, bool forceSafe)
        {
            float length = RandRange(_flatLengthRange.x, _flatLengthRange.y);
            AddGroundRun(segment, startX, length);

            if (!forceSafe && _random.NextDouble() < 0.5)
            {
                AddCoinLine(segment, startX + length * 0.5f, _groundTopY + 1.6f, RandInt(2, 4));
            }

            return length;
        }

        private float BuildGap(Segment segment, float startX, float difficulty)
        {
            float takeoff = RandRange(3f, 5f);
            float gap = Mathf.Min(MaxGap(), RandRange(2f, 2f + difficulty * 3f));
            float landing = RandRange(4f, 6f);

            AddGroundRun(segment, startX, takeoff);
            AddGroundRun(segment, startX + takeoff + gap, landing);
            AddCoinArc(segment, startX + takeoff, startX + takeoff + gap, _groundTopY + 1.5f, 1.2f);

            return takeoff + gap + landing;
        }

        private float BuildElevated(Segment segment, float startX, float difficulty)
        {
            float length = RandRange(8f, 12f);
            AddGroundRun(segment, startX, length); // continuous ground beneath — a gentle obstacle

            float stepHeight = Mathf.Min(MaxStepUp(), RandRange(1f, 1f + difficulty * 1.5f));
            float plateauWidth = RandRange(3f, 5f);
            float maxOffset = Mathf.Max(2f, length - plateauWidth - 2f);
            float plateauStart = startX + RandRange(2f, maxOffset);

            AddPlatform(segment, plateauStart + plateauWidth * 0.5f, _groundTopY + stepHeight, plateauWidth, stepHeight);
            AddCoinLine(segment, plateauStart + 0.5f, _groundTopY + stepHeight + 1f, Mathf.Max(1, Mathf.RoundToInt(plateauWidth - 1f)));

            return length;
        }

        private float BuildStaircase(Segment segment, float startX, float difficulty)
        {
            int steps = RandInt(2, 4);
            float stepHeight = Mathf.Min(MaxStepUp() * 0.8f, 0.7f + difficulty * 0.5f);
            float stepWidth = RandRange(1.2f, 1.8f);
            float body = (steps * 2 - 1) * stepWidth; // ascend + single peak + descend
            const float pad = 3f;
            float length = pad * 2f + body;

            AddGroundRun(segment, startX, length);

            float x = startX + pad;
            for (int i = 1; i <= steps; i++)     // ascending
            {
                AddStep(segment, x, i * stepHeight, stepWidth);
                x += stepWidth;
            }
            for (int i = steps - 1; i >= 1; i--) // descending
            {
                AddStep(segment, x, i * stepHeight, stepWidth);
                x += stepWidth;
            }

            return length;
        }

        private float BuildSpikes(Segment segment, float startX, float difficulty)
        {
            float length = RandRange(10f, 14f);
            AddGroundRun(segment, startX, length);

            const float spikeHeight = 0.8f;
            int clusters = RandInt(1, 1 + Mathf.RoundToInt(difficulty * 2f));
            float x = startX + _minReactionRunway;

            for (int i = 0; i < clusters && x < startX + length - _minReactionRunway; i++)
            {
                // Clamp cluster width so it can always be hopped in a single jump, like a gap.
                float rawWidth = RandInt(1, 1 + Mathf.RoundToInt(difficulty * 2f)) * 0.9f;
                float clusterWidth = Mathf.Min(rawWidth, MaxGap() * 0.8f);

                AddSpikes(segment, x, clusterWidth, spikeHeight);
                AddCoinArc(segment, x, x + clusterWidth, _groundTopY + 1.4f, 1f);

                x += clusterWidth + RandRange(_minReactionRunway, _minReactionRunway + 2f);
            }

            return length;
        }

        private float BuildEnemy(Segment segment, float startX, float difficulty)
        {
            float length = RandRange(9f, 13f);
            AddGroundRun(segment, startX, length);

            const float patrolPadding = 2.5f;
            float minX = startX + patrolPadding;
            float maxX = startX + length - patrolPadding;

            if (maxX - minX >= 2f)
            {
                float speed = 1.5f + difficulty * 2f;
                AddEnemy(segment, minX, maxX, 0.9f, speed);
                // High coins reward jumping the enemy rather than waiting it out.
                AddCoinLine(segment, startX + length * 0.5f - 1f, _groundTopY + 2.2f, 3);
            }

            return length;
        }

        private float BuildFloatingPlatforms(Segment segment, float startX, float difficulty)
        {
            float lead = RandRange(3f, 4f);
            AddGroundRun(segment, startX, lead);

            int platforms = RandInt(2, 3);
            float gap = Mathf.Min(MaxGap(), 2.2f + difficulty * 1.5f);
            float platformWidth = RandRange(1.6f, 2.4f);
            float platformY = _groundTopY + RandRange(0.5f, 1.4f);

            float x = startX + lead;
            for (int i = 0; i < platforms; i++)
            {
                x += gap;
                AddPlatform(segment, x + platformWidth * 0.5f, platformY, platformWidth, 0.5f);
                AddCoin(segment, x + platformWidth * 0.5f, platformY + 1f);
                x += platformWidth;
            }

            x += gap;
            float landing = RandRange(5f, 7f);
            AddGroundRun(segment, x, landing);

            return (x + landing) - startX;
        }

        // ---- Piece placement helpers ----------------------------------------------

        private void AddGroundRun(Segment segment, float startX, float length)
        {
            float center = startX + length * 0.5f;
            segment.Add(PieceKind.Ground, _factory.SpawnGround(_levelRoot, center, _groundTopY, length, _groundThickness));
        }

        private void AddPlatform(Segment segment, float centerX, float topY, float width, float height)
        {
            segment.Add(PieceKind.Platform, _factory.SpawnPlatform(_levelRoot, centerX, topY, width, height));
        }

        private void AddStep(Segment segment, float startX, float top, float width)
        {
            // A step is a solid column from the base surface up to its top face.
            segment.Add(PieceKind.Platform, _factory.SpawnPlatform(_levelRoot, startX + width * 0.5f, _groundTopY + top, width, top));
        }

        private void AddSpikes(Segment segment, float startX, float width, float height)
        {
            // Fill the cluster with evenly sized triangular teeth so it reads as spikes.
            const float preferredToothWidth = 0.7f;
            int teeth = Mathf.Max(1, Mathf.RoundToInt(width / preferredToothWidth));
            float toothWidth = width / teeth;

            for (int i = 0; i < teeth; i++)
            {
                float centerX = startX + toothWidth * (i + 0.5f);
                segment.Add(PieceKind.Spike, _factory.SpawnSpikeTooth(_levelRoot, centerX, _groundTopY, toothWidth * 0.95f, height));
            }
        }

        private void AddEnemy(Segment segment, float minX, float maxX, float size, float speed)
        {
            segment.Add(PieceKind.Enemy, _factory.SpawnEnemy(_levelRoot, _groundTopY, size, minX, maxX, speed));
        }

        private void AddCoin(Segment segment, float x, float y)
        {
            segment.Add(PieceKind.Coin, _factory.SpawnCoin(_levelRoot, x, y));
        }

        private void AddCoinLine(Segment segment, float startX, float y, int count)
        {
            for (int i = 0; i < count; i++)
            {
                AddCoin(segment, startX + i * 1f, y);
            }
        }

        private void AddCoinArc(Segment segment, float x0, float x1, float baseY, float arcHeight)
        {
            int count = Mathf.Max(2, Mathf.RoundToInt(x1 - x0));
            for (int i = 0; i <= count; i++)
            {
                float t = i / (float)count;
                float x = Mathf.Lerp(x0, x1, t);
                float y = baseY + Mathf.Sin(t * Mathf.PI) * arcHeight;
                AddCoin(segment, x, y);
            }
        }

        // ---- Seeded random ---------------------------------------------------------

        private float RandRange(float min, float max) => (float)(min + _random.NextDouble() * (max - min));

        private int RandInt(int minInclusive, int maxInclusive) => _random.Next(minInclusive, maxInclusive + 1);
    }
}
