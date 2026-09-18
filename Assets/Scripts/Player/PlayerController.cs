using UnityEngine;
using Run.Common;
using Run.Core;
using Run.Effects;

namespace Run.Player
{
    /// <summary>
    /// Drives the player capsule: constant forward running plus a single, responsive,
    /// variable-height jump.
    /// </summary>
    /// <remarks>
    /// Gravity and the jump impulse are derived from a desired apex height and
    /// time-to-apex, so the arc is tuned by feel rather than raw numbers. Coyote time and
    /// a jump buffer keep the controls forgiving; heavier fall / low-jump gravity give the
    /// arc its snap. The controller publishes its jump reach so the level generator can
    /// guarantee the gaps and steps it spawns are clearable at the current run speed.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CapsuleCollider2D))]
    [DisallowMultipleComponent]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Run")]
        [Tooltip("When true the capsule runs forward automatically (endless-runner style).")]
        [SerializeField] private bool _autoRun = true;
        [SerializeField, Min(0f)] private float _runSpeed = 10f;
        [Tooltip("Speed the run ramps up to over the session. Set equal to Run Speed to disable ramping.")]
        [SerializeField, Min(0f)] private float _maxRunSpeed = 16f;
        [SerializeField, Min(0f)] private float _runSpeedGainPerSecond = 0.15f;

        [Header("Jump Feel")]
        [Tooltip("Peak height of a full jump, in world units.")]
        [SerializeField, Min(0.1f)] private float _jumpHeight = 3.2f;
        [Tooltip("Time taken to reach the apex of a full jump, in seconds.")]
        [SerializeField, Min(0.05f)] private float _timeToApex = 0.38f;
        [Tooltip("Extra gravity multiplier while falling (snappier descent).")]
        [SerializeField, Min(1f)] private float _fallGravityMultiplier = 1.7f;
        [Tooltip("Extra gravity multiplier while rising with jump released (variable height).")]
        [SerializeField, Min(1f)] private float _lowJumpMultiplier = 2.2f;

        [Header("Catch-Up")]
        [Tooltip("Extra speed per world unit the player trails the scroll line.")]
        [SerializeField, Min(0f)] private float _catchUpGain = 3f;
        [Tooltip("Ceiling on the catch-up bonus, so recovery reads as a sprint and not a teleport.")]
        [SerializeField, Min(0f)] private float _maxCatchUpSpeed = 6f;
        [Tooltip("Gap below which no catch-up is applied (avoids jitter around the line).")]
        [SerializeField, Min(0f)] private float _catchUpDeadzone = 0.05f;

        [Header("Forgiveness")]
        [Tooltip("Grace period after leaving a ledge during which a jump still registers.")]
        [SerializeField, Min(0f)] private float _coyoteTime = 0.10f;
        [Tooltip("Window before landing during which a pressed jump is remembered.")]
        [SerializeField, Min(0f)] private float _jumpBufferTime = 0.10f;

        [Header("Ground Check")]
        [Tooltip("Layers considered solid ground. Left empty, resolves to the \"Ground\" layer.")]
        [SerializeField] private LayerMask _groundLayers;
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.6f, 0.15f);
        [SerializeField, Min(0f)] private float _groundCheckOffset = 0.02f;

        [Header("Death Bounds")]
        [Tooltip("Distance below the camera's bottom edge at which a fall becomes fatal.")]
        [SerializeField, Min(0f)] private float _killMarginBelow = 1.5f;
        [Tooltip("Distance behind the camera's left edge at which being left behind is fatal.")]
        [SerializeField, Min(0f)] private float _killMarginBehind = 1.0f;

        [Header("Power-Ups")]
        [Tooltip("Run speed multiplier applied while a Speed Boost power-up is active.")]
        [SerializeField, Min(1f)] private float _speedBoostMultiplier = 1.3f;

        private Rigidbody2D _body;
        private CapsuleCollider2D _collider;
        private Camera _camera;

        private float _gravity;
        private float _jumpVelocity;
        private float _coyoteCounter;
        private float _bufferCounter;
        private bool _isGrounded;
        private bool _startArmed;   // Ready-state gate: requires a fresh press to begin
        private bool _isDead;
        private float _effectiveMaxRunSpeed;
        private float _effectiveRunSpeedGain;
        private float _baseRunSpeed;

        // Power-up state. Kept as independent flags (rather than a single "active type") so
        // Shield, Speed Boost, and Double Jump can be active at once if their windows overlap.
        private bool _shieldActive;
        private bool _speedBoostActive;
        private bool _doubleJumpActive;
        private bool _airJumpUsed;

        /// <summary>Current forward run speed (grows over the session up to the max; boosted while Speed Boost is active).</summary>
        public float RunSpeed => _baseRunSpeed * (_speedBoostActive ? _speedBoostMultiplier : 1f);

        /// <summary>Peak reachable jump height, in world units.</summary>
        public float MaxJumpHeight => _jumpHeight;

        /// <summary>Approximate horizontal distance clearable in a single full jump.</summary>
        public float MaxJumpSpan { get; private set; }
        public bool IsGrounded => _isGrounded;
        public event System.Action Jumped;
        public event System.Action Landed;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();
            _camera = Camera.main;
            EnsureVisual();

            if (_groundLayers.value == 0)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                _groundLayers = groundLayer >= 0 ? (1 << groundLayer) : Physics2D.DefaultRaycastLayers;
            }

            // Derive gravity and the jump impulse from the desired apex height/time:
            //   height = ½ · g · t²   →   g = 2·height / t²,   v0 = g · t.
            _gravity = (2f * _jumpHeight) / (_timeToApex * _timeToApex);
            _jumpVelocity = _gravity * _timeToApex;

            _body.gravityScale = _gravity / Mathf.Abs(Physics2D.gravity.y);
            _body.freezeRotation = true;
            _body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Auto-run presses the capsule into whatever it meets, so any surface friction
            // would let it cling to a wall face and hang there. Frictionless contact means
            // a blocked player always slides down under full gravity instead of sticking.
            _collider.sharedMaterial = PhysicsMaterials.Frictionless;

            ApplyDifficulty();
        }

        /// <summary>
        /// Re-reads <see cref="DifficultySettings"/> and recomputes the speed values derived
        /// from it. Called from <see cref="Awake"/> for a sane default, and again whenever the
        /// run actually starts (see <see cref="OnGameStateChanged"/>), since the Ready-screen
        /// difficulty picker can change the selection at any point up until then — a one-time
        /// read in <see cref="Awake"/> alone would miss any choice made after the scene loaded.
        /// </summary>
        private void ApplyDifficulty()
        {
            // The player's chosen challenge level scales both the base and the ramp
            // ceiling by the same factor, so Easy/Hard shift the whole speed curve
            // rather than just its starting point.
            float speedMultiplier = DifficultySettings.SpeedMultiplier;
            _baseRunSpeed = _runSpeed * speedMultiplier;
            _effectiveMaxRunSpeed = _maxRunSpeed * speedMultiplier;
            _effectiveRunSpeedGain = _runSpeedGainPerSecond * DifficultySettings.SpeedGainMultiplier;
            RecomputeJumpSpan();
        }

        private void Start()
        {
            var game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged += OnGameStateChanged;
            }
        }

        private void OnEnable()
        {
            PowerUpEvents.OnPowerUpActivated += OnPowerUpActivated;
            PowerUpEvents.OnPowerUpExpired += OnPowerUpExpired;
        }

        private void OnDisable()
        {
            PowerUpEvents.OnPowerUpActivated -= OnPowerUpActivated;
            PowerUpEvents.OnPowerUpExpired -= OnPowerUpExpired;
            GameManager.Instance?.StateChanged -= OnGameStateChanged;
        }

        private void OnGameStateChanged(GameState state)
        {
            // Re-read here rather than only at the moment PlayerController itself starts the
            // run, since anything can start it - the Ready-screen confirm press, or the
            // Difficulty Menu starting the run directly when its already-picked difficulty is
            // re-confirmed. Both must pick up whatever's currently selected.
            if (state == GameState.Playing)
            {
                ApplyDifficulty();
            }
        }

        private void OnPowerUpActivated(PowerUpType type, float duration)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    _shieldActive = true;
                    break;

                case PowerUpType.SpeedBoost:
                    _speedBoostActive = true;
                    RecomputeJumpSpan();
                    break;

                case PowerUpType.DoubleJump:
                    _doubleJumpActive = true;
                    break;
            }
        }

        private void OnPowerUpExpired(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.Shield:
                    _shieldActive = false;
                    break;

                case PowerUpType.SpeedBoost:
                    _speedBoostActive = false;
                    RecomputeJumpSpan();
                    break;

                case PowerUpType.DoubleJump:
                    _doubleJumpActive = false;
                    break;
            }
        }

        private void Update()
        {
            if (_isDead)
            {
                return;
            }

            var game = GameManager.Instance;

            UpdateGrounded();

            bool confirmPressed = RunInput.ConfirmPressed();

            // While waiting to start, the first *fresh* press begins the run instead of
            // jumping — and we require a release first so a click carried over from
            // entering play (or the last run) can't auto-start it.
            if (game != null && game.State == GameState.Ready)
            {
                if (!RunInput.JumpHeld())
                {
                    _startArmed = true;
                }
                if (_startArmed && confirmPressed)
                {
                    game.BeginRun();
                }
                return;
            }

            if (game == null || game.State != GameState.Playing)
            {
                return;
            }

            _bufferCounter = confirmPressed ? _jumpBufferTime : _bufferCounter - Time.deltaTime;
            if (_bufferCounter > 0f)
            {
                if (_coyoteCounter > 0f)
                {
                    Jump();
                }
                else if (_doubleJumpActive && !_airJumpUsed)
                {
                    // The extra jump is only available once per airborne stretch, regardless
                    // of how long the Double Jump power-up's remaining duration is.
                    _airJumpUsed = true;
                    Jump();
                }
            }

            CheckDeathBounds();
        }

        private void FixedUpdate()
        {
            if (_isDead)
            {
                return;
            }

            var game = GameManager.Instance;
            bool playing = game != null && game.State == GameState.Playing;

            // Difficulty ramp: gently raise the run speed, then refresh jump reach so the
            // generator's clamps stay in sync with how far the player can actually jump.
            if (playing && _effectiveMaxRunSpeed > _baseRunSpeed)
            {
                _baseRunSpeed = Mathf.Min(_effectiveMaxRunSpeed, _baseRunSpeed + _effectiveRunSpeedGain * Time.fixedDeltaTime);
                RecomputeJumpSpan();
            }

            float velocityX = playing ? (_autoRun ? RunSpeed : RunInput.Horizontal() * RunSpeed) : 0f;

            // Catch-up: the scroll line advances at run speed regardless of what happens to
            // the player, so any ground lost — to a collision, a bad landing, or being held
            // up by an obstacle — would otherwise be permanent. Running proportionally
            // faster while behind repays that debt and settles the player back into their
            // screen lane; the cap keeps the recovery readable rather than a snap.
            if (playing && _autoRun)
            {
                velocityX += CatchUpBonus();
            }

            float velocityY = _body.linearVelocity.y;

            // Give the arc its snap: heavier gravity while falling, and while rising with
            // the jump released (which shortens tapped jumps).
            if (velocityY < 0f)
            {
                velocityY += Physics2D.gravity.y * _body.gravityScale * (_fallGravityMultiplier - 1f) * Time.fixedDeltaTime;
            }
            else if (velocityY > 0f && !RunInput.JumpHeld())
            {
                velocityY += Physics2D.gravity.y * _body.gravityScale * (_lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
            }

            _body.linearVelocity = new Vector2(velocityX, velocityY);
        }

        private void Jump()
        {
            _body.linearVelocity = new Vector2(_body.linearVelocity.x, _jumpVelocity);
            _coyoteCounter = 0f;
            _bufferCounter = 0f;
            Jumped?.Invoke();
        }

        /// <summary>Extra forward speed applied while the player trails the scroll line.</summary>
        private float CatchUpBonus()
        {
            var scroller = WorldScroller.Instance;
            if (scroller == null)
            {
                return 0f;
            }

            float behind = scroller.DistanceBehind(_body.position.x);
            if (behind <= _catchUpDeadzone)
            {
                return 0f;
            }

            return Mathf.Min(behind * _catchUpGain, _maxCatchUpSpeed);
        }

        /// <summary>Refreshes the grounded flag and coyote timer.</summary>
        private void UpdateGrounded()
        {
            bool wasGrounded = _isGrounded;
            Bounds bounds = _collider.bounds;
            Vector2 feet = new Vector2(bounds.center.x, bounds.min.y - _groundCheckOffset);
            _isGrounded = Physics2D.OverlapBox(feet, _groundCheckSize, 0f, _groundLayers) != null;
            if (!wasGrounded && _isGrounded && _body.linearVelocity.y <= 0f &&
                GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
                Landed?.Invoke();

            if (_isGrounded && _body.linearVelocity.y <= 0.01f)
            {
                _coyoteCounter = _coyoteTime;
                _airJumpUsed = false;
            }
            else
            {
                _coyoteCounter -= Time.deltaTime;
            }
        }

        private void CheckDeathBounds()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                if (_camera == null)
                {
                    return;
                }
            }

            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;
            float cameraBottom = _camera.transform.position.y - halfHeight;
            float cameraLeft = _camera.transform.position.x - halfWidth;

            if (transform.position.y < cameraBottom - _killMarginBelow)
            {
                Kill(DeathCause.FellIntoPit);
            }
            else if (transform.position.x < cameraLeft - _killMarginBehind)
            {
                Kill(DeathCause.LeftBehind);
            }
        }

        /// <summary>
        /// Re-bases the player by <paramref name="dx"/> on X (floating origin). Sets both the
        /// transform (so this frame's reads see the shift) and the body (so physics stays in
        /// sync) to the same target — avoiding a one-frame divergence from the rest of the world.
        /// </summary>
        public void ShiftX(float dx)
        {
            Vector3 position = transform.position;
            position.x += dx;
            transform.position = position;
            _body.position = new Vector2(position.x, position.y);
        }

        /// <summary>Ends the run. Idempotent — extra calls in the same frame are ignored.</summary>
        public void Kill(DeathCause cause)
        {
            if (_isDead)
            {
                return;
            }

            // Shield absorbs exactly one hazard hit (not a fall or being left behind, which
            // are the player's own mistakes rather than something to block) and is consumed
            // immediately, regardless of how much of its timer was left.
            if (_shieldActive && cause == DeathCause.Hazard)
            {
                _shieldActive = false;
                PowerUpEvents.TriggerExpired(PowerUpType.Shield);
                GameFeel.Instance?.Emit(BurstKind.Spark, transform.position);
                return;
            }

            _isDead = true;
            _body.linearVelocity = Vector2.zero;
            _body.simulated = false;   // freeze in place; physics no longer applies
            GameManager.Instance?.NotifyPlayerDied(cause);
        }

        /// <summary>
        /// Ensures the sprite lives on a child "Visual" (kept separate from the physics
        /// body so animation/juice can be layered on later without touching the collider).
        /// </summary>
        private void EnsureVisual()
        {
            var visual = transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(transform, false);
            }

            var renderer = visual.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = visual.gameObject.AddComponent<SpriteRenderer>();
                renderer.sortingOrder = 10;
            }
            if (renderer.sprite == null)
            {
                renderer.sprite = Shapes.Capsule;
            }
        }

        private void RecomputeJumpSpan()
        {
            // Horizontal reach ≈ run speed × total air time (rise + fall). Rise uses base
            // gravity; the fall uses the heavier fall gravity, matching FixedUpdate.
            float riseTime = _timeToApex;
            float fallGravity = _gravity * _fallGravityMultiplier;
            float fallTime = Mathf.Sqrt((2f * _jumpHeight) / fallGravity);
            MaxJumpSpan = RunSpeed * (riseTime + fallTime);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var capsule = GetComponent<CapsuleCollider2D>();
            if (capsule == null)
            {
                return;
            }

            Bounds bounds = capsule.bounds;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector2(bounds.center.x, bounds.min.y - _groundCheckOffset), _groundCheckSize);
        }
#endif
    }
}
