using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Run.CameraRig;
using Run.Core;
using Run.Player;

namespace Run.Generation
{
    /// <summary>Streams authored chunk prefabs into a separate loaded level scene.</summary>
    [DisallowMultipleComponent]
    public sealed class LevelGenerator : MonoBehaviour
    {
        [Header("Prefabs (empty uses Resources/Levels)")]
        [SerializeField] private LevelChunk[] _levelPrefabs;
        [SerializeField, Min(0f)] private float _spawnAhead = 24f;
        [SerializeField, Min(0f)] private float _despawnBehind = 20f;
        [SerializeField, Min(0f)] private float _safeStartDistance = 28f;
        [SerializeField, Min(1f)] private float _difficultyRampDistance = 600f;
        [SerializeField, Range(0f, 1f)] private float _startDifficulty;
        [SerializeField] private bool _useRandomSeed = true;
        [SerializeField] private int _seed = 12345;
        [SerializeField, Min(50f)] private float _rebaseDistance = 1000f;

        private PlayerController _player;
        private Camera _camera;
        private Transform _levelRoot;
        private Scene _levelScene;
        private System.Random _random;
        private readonly Queue<LevelChunk> _active = new Queue<LevelChunk>();
        private readonly Dictionary<LevelChunk, Stack<LevelChunk>> _pool =
            new Dictionary<LevelChunk, Stack<LevelChunk>>();
        private float _frontierX;
        private float _originX;
        private LevelChunk _safePrefab;

        private void Start()
        {
            _camera = Camera.main;
            var playerGo = GameObject.FindGameObjectWithTag("Player");
            _player = playerGo != null ? playerGo.GetComponent<PlayerController>() : null;
            _random = _useRandomSeed ? new System.Random() : new System.Random(_seed);

            // The player's chosen challenge level shifts where the hazard ramp starts and
            // how far it stretches, so Easy opens on gentler chunks and climbs slowly while
            // Hard starts partway up the ramp and reaches full difficulty sooner.
            _startDifficulty = DifficultySettings.StartDifficulty;
            _difficultyRampDistance *= DifficultySettings.RampDistanceMultiplier;
            if (_levelPrefabs == null || _levelPrefabs.Length == 0)
                _levelPrefabs = Resources.LoadAll<LevelChunk>("Levels");
            System.Array.Sort(_levelPrefabs, (a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));
            foreach (var prefab in _levelPrefabs)
                if (prefab != null && prefab.SafeStart && prefab.Width > 0f) { _safePrefab = prefab; break; }
            if (_camera == null || _safePrefab == null)
            {
                Debug.LogError("[LevelGenerator] Requires a camera and a positive-width SafeStart level prefab.");
                enabled = false;
                return;
            }
            _levelScene = SceneManager.CreateScene("Runner Level");
            _levelRoot = new GameObject("Level Prefabs").transform;
            SceneManager.MoveGameObjectToScene(_levelRoot.gameObject, _levelScene);
            _originX = (playerGo != null ? playerGo.transform.position.x : 0f) - 6f;
            _frontierX = _originX;
            EnsureGeneratedAhead();
        }

        private void Update()
        {
            if (_camera == null || _levelRoot == null) return;
            EnsureGeneratedAhead();
            RecycleBehind();
            MaintainFloatingOrigin();
        }

        private LevelChunk ChoosePrefab()
        {
            if (_frontierX - _originX < _safeStartDistance) return _safePrefab;
            float difficulty = Mathf.Lerp(_startDifficulty, 1f,
                Mathf.Clamp01((_frontierX - _originX) / Mathf.Max(1f, _difficultyRampDistance)));
            float total = 0f;
            foreach (var prefab in _levelPrefabs) total += Weight(prefab, difficulty);
            double roll = _random.NextDouble() * total;
            foreach (var prefab in _levelPrefabs)
            {
                float weight = Weight(prefab, difficulty);
                if (roll < weight) return prefab;
                roll -= weight;
            }
            return _safePrefab;
        }

        private float Weight(LevelChunk prefab, float difficulty)
        {
            if (prefab == null || prefab.SafeStart || prefab.Width <= 0f) return 0f;
            if (_player != null && (prefab.RequiredJumpSpan > _player.MaxJumpSpan ||
                prefab.RequiredJumpHeight > _player.MaxJumpHeight)) return 0f;
            return Mathf.Max(0f, Mathf.Lerp(prefab.StartWeight, prefab.EndWeight, difficulty));
        }

        private void EnsureGeneratedAhead()
        {
            float limit = _camera.transform.position.x + _camera.orthographicSize * _camera.aspect + _spawnAhead;
            for (int guard = 0; _frontierX < limit && guard < 64; guard++)
            {
                var prefab = ChoosePrefab();
                Stack<LevelChunk> free;
                LevelChunk chunk = _pool.TryGetValue(prefab, out free) && free.Count > 0
                    ? free.Pop() : Instantiate(prefab, _levelRoot);
                chunk.Source = prefab;
                chunk.Place(_frontierX);
                _frontierX += chunk.Width;
                _active.Enqueue(chunk);
            }
        }

        private void RecycleBehind()
        {
            float limit = _camera.transform.position.x - _camera.orthographicSize * _camera.aspect - _despawnBehind;
            while (_active.Count > 0 && _active.Peek().EndX < limit)
            {
                var chunk = _active.Dequeue();
                chunk.Recycle();
                Stack<LevelChunk> free;
                if (!_pool.TryGetValue(chunk.Source, out free))
                    _pool[chunk.Source] = free = new Stack<LevelChunk>();
                free.Push(chunk);
            }
        }
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
            Run.Effects.GameFeel.Instance?.Shift(dx);
            Run.Effects.VisualAtmosphere.Instance?.Shift(dx);
        }

    }
}
