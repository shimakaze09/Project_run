using UnityEngine;
using Run.Core;
using Run.Player;

namespace Run.Effects
{
    /// <summary>Visual-only animation; never scales or moves the player's collider.</summary>
    public sealed class RunnerVisuals : MonoBehaviour
    {
        private PlayerController _player;
        private Rigidbody2D _body;
        private Transform _visual;
        private Vector3 _baseScale;
        private float _dustTime;
        private float _squash;
        private GameManager _game;
        private bool _milestone;

        private void Start()
        {
            _player = GetComponent<PlayerController>();
            _body = GetComponent<Rigidbody2D>();
            _visual = transform.Find("Visual");
            if (_visual != null) _baseScale = _visual.localScale;
            _game = GameManager.Instance;
            if (_player != null) { _player.Jumped += OnJump; _player.Landed += OnLand; }
            if (_game != null) { _game.StateChanged += OnState; _game.DistanceChanged += OnDistance; }
        }

        private void OnJump() { _squash = -0.25f; GameFeel.Instance?.Emit(BurstKind.Jump, transform.position + Vector3.down * 0.5f); }
        private void OnLand() { _squash = 0.22f; GameFeel.Instance?.Emit(BurstKind.Land, transform.position + Vector3.down * 0.5f); }
        private void OnState(GameState state)
        {
            if (state == GameState.Playing) GameFeel.Instance?.Emit(BurstKind.Start, transform.position);
            if (state == GameState.GameOver) { GameFeel.Instance?.Emit(BurstKind.Death, transform.position); _squash = 0.3f; }
        }
        private void OnDistance(float distance)
        {
            if (!_milestone && _game.LastRunDistance > 0f && _game.LastRunProgress >= 1f)
            {
                _milestone = true;
                GameFeel.Instance?.Emit(BurstKind.Milestone, transform.position + Vector3.up * 0.5f);
            }
        }
        private void Update()
        {
            if (_visual == null || _game == null || _body == null) return;
            bool running = _game.State == GameState.Playing && _player.IsGrounded;
            _squash = Mathf.MoveTowards(_squash, 0f, Time.deltaTime * 1.5f);
            float stride = running ? Mathf.Sin(Time.time * 24f) * 0.04f : 0f;
            _visual.localScale = Vector3.Scale(_baseScale, new Vector3(1f + _squash - stride, 1f - _squash + stride, 1f));
            _visual.localRotation = Quaternion.Euler(0f, 0f, Mathf.Clamp(-_body.linearVelocity.y, -12f, 12f));
            _dustTime -= Time.deltaTime;
            if (running && _dustTime <= 0f)
            {
                _dustTime = 0.09f;
                GameFeel.Instance?.Emit(BurstKind.Dust, transform.position + new Vector3(-0.3f, -0.48f, 0f));
            }
        }
        private void OnDestroy()
        {
            if (_player != null) { _player.Jumped -= OnJump; _player.Landed -= OnLand; }
            if (_game != null) { _game.StateChanged -= OnState; _game.DistanceChanged -= OnDistance; }
        }
    }
}
