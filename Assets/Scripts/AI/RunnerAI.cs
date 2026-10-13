using System.Collections.Generic;
using UnityEngine;
using Run.Core;
using Run.EnvironmentMode;
using Run.Hazards;
using Run.Player;

namespace Run.AI
{
    /// <summary>
    /// The computer runner. In Environment mode it takes over the player's jump button and plays
    /// the run itself; <see cref="RunnerBrain"/> and <see cref="JumpPlanner"/> do the thinking.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It drives <see cref="PlayerController"/> like any other input source, so it obeys the same
    /// physics, coyote time and power-ups as a human. Its job here is the Unity side: copying
    /// the level near the runner into the planner as rectangles, and pressing jump on the
    /// physics step the brain picked.
    /// </para>
    /// <para>
    /// To be beatable it is deliberately imperfect, tuned per difficulty: it only notices a
    /// newly placed trap after a reaction delay, and every jump has a small random timing error
    /// (plus the odd bigger mistake). On ordinary level pieces the safe window is wide, so this
    /// rarely matters — it is the environment player's tight trap combos that punish it.
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PlayerController))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10)] // decide before PlayerController applies this physics step
    public sealed class RunnerAI : MonoBehaviour, IRunnerInput
    {
        [Header("Skill per difficulty (Easy = an easier runner to stop)")]
        [SerializeField] private RunnerBrain.Skill _easy = new RunnerBrain.Skill
            { ReactionTime = 0.45f, TimingJitter = 0.04f, MistakeChance = 0.08f, MistakeSize = 0.1f };
        [SerializeField] private RunnerBrain.Skill _normal = new RunnerBrain.Skill
            { ReactionTime = 0.3f, TimingJitter = 0.025f, MistakeChance = 0.03f, MistakeSize = 0.08f };
        [SerializeField] private RunnerBrain.Skill _hard = new RunnerBrain.Skill
            { ReactionTime = 0.18f, TimingJitter = 0.01f, MistakeChance = 0.01f, MistakeSize = 0.06f };

        [Header("Planning")]
        [Tooltip("How far ahead (in seconds of running) the runner looks for danger.")]
        [SerializeField, Min(0.3f)] private float _lookAhead = 1.2f;
        [Tooltip("Re-check the current plan this often (physics steps), to follow speed changes.")]
        [SerializeField, Min(1)] private int _replanEverySteps = 8;
        [Tooltip("Random seed for the runner's mistakes; 0 picks a new one every run.")]
        [SerializeField] private int _seed;

        [Header("Debug")]
        [Tooltip("Draw the planned jump arc as a gizmo.")]
        [SerializeField] private bool _drawPlan = true;
        [Tooltip("Log every jump decision (and the last one before a death) to the Console.")]
        [SerializeField] private bool _logDecisions;

        private enum Role { Ignore, Solid, Hazard, Mover }

        private PlayerController _player;
        private Rigidbody2D _body;
        private CapsuleCollider2D _collider;
        private GameManager _game;
        private readonly RunnerBrain _brain = new RunnerBrain();
        private readonly List<Collider2D> _overlaps = new List<Collider2D>();
        private readonly Dictionary<Collider2D, Role> _roles = new Dictionary<Collider2D, Role>();
        private readonly List<Vector2> _trace = new List<Vector2>();
        private int _groundMask;
        private bool _active;
        private int _noticedTraps;
        private float _airTime;
        private float _holdUntil;
        private string _lastDecision;

        /// <summary>True while the computer is playing the run.</summary>
        public bool IsActive => _active;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _body = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();
            int ground = LayerMask.NameToLayer("Ground");
            _groundMask = ground >= 0 ? 1 << ground : 0;
        }

        private void Start()
        {
            _game = GameManager.Instance;
            if (_game != null)
            {
                _game.StateChanged += OnGameStateChanged;
                OnGameStateChanged(_game.State);
            }
        }

        private void OnDestroy()
        {
            if (_game != null)
            {
                _game.StateChanged -= OnGameStateChanged;
            }
        }

        private void OnGameStateChanged(GameState state)
        {
            if (state != GameState.Playing)
            {
                if (state == GameState.GameOver && _active && _logDecisions)
                {
                    Debug.Log($"[RunnerAI] Stopped by {_game.LastDeathCause} at x={transform.position.x:F2}. " +
                              $"Last jump: {_lastDecision ?? "none"}");
                }
                _holdUntil = 0f;
                _trace.Clear();
                return;
            }

            // The run's mode and difficulty are fixed from here until the next restart.
            _active = _game.RunMode == GameMode.Environment;
            _player.SetInputSource(_active ? this : null);
            var skill = DifficultySettings.Current == Difficulty.Easy ? _easy
                : DifficultySettings.Current == Difficulty.Hard ? _hard : _normal;
            _brain.LookAhead = _lookAhead;
            _brain.ReplanEverySteps = _replanEverySteps;
            _brain.Reset(skill, _seed);
            _noticedTraps = 0;
        }

        // ---- IRunnerInput -----------------------------------------------------------------

        // The take-off itself goes through PlayerController.RequestJump on the physics step the
        // brain picked (see FixedUpdate), so a slow frame rate can't make it late. The "press"
        // is therefore never used; the hold is what shapes the jump's height.
        public bool JumpPressed() => false;

        public bool JumpHeld() => _active && Time.time < _holdUntil;

        // ---- Thinking ---------------------------------------------------------------------

        private void FixedUpdate()
        {
            if (!_active || _game == null || _game.State != GameState.Playing || _player.IsDead)
            {
                return;
            }

            float dt = Time.fixedDeltaTime;
            bool grounded = _player.TouchingGround();
            _airTime = grounded ? 0f : _airTime + dt;
            bool canJump = (grounded || _player.CanJump) && _body.linearVelocity.y <= 0.5f;

            int noticed = CountNoticedTraps();
            bool worldChanged = noticed != _noticedTraps;
            _noticedTraps = noticed;

            // The scroll speed; the planner adds the controller's catch-up sprint on top itself.
            float speed = _player.RunSpeed;
            if (canJump)
            {
                BuildWorld(speed);
            }

            bool wasPlanning = _brain.HasPlan;
            if (!_brain.Think(CurrentState(grounded), speed, dt, canJump, worldChanged, out float hold))
            {
                if (_brain.HasPlan && (!wasPlanning || worldChanged)) RecordPlan(speed, dt);
                return;
            }

            // Hold first, so this very step's gravity already treats the button as held.
            _holdUntil = Time.time + hold;
            if (_player.RequestJump())
            {
                if (_logDecisions) Debug.Log($"[RunnerAI] jumped at x={_collider.bounds.center.x:F2}, {_lastDecision}");
                _brain.Jumped();
            }
            else
            {
                _holdUntil = 0f; // not allowed this step; the plan stays and retries
            }
        }

        private void RecordPlan(float speed, float dt)
        {
            JumpPlan plan = _brain.LastPlan;
            if (_logDecisions)
            {
                float x = _collider.bounds.center.x;
                float perStep = speed * dt;
                _lastDecision = $"planned x={x + _brain.StepsUntilTakeoff * perStep:F2} " +
                    $"(safe x={x + plan.WindowStart * perStep:F2}..{x + plan.WindowEnd * perStep:F2}, " +
                    $"error {_brain.TimingErrorSteps} steps, lag {_player.ScrollLag:F2}" +
                    $"{(plan.Clean ? "" : ", no clean option")})";
            }
            if (_drawPlan)
            {
                _brain.Planner.TracePlan(plan, speed, dt, _trace);
            }
        }

        private RunnerState CurrentState(bool grounded)
        {
            return new RunnerState
            {
                Position = _collider.bounds.center,
                VelocityY = _body.linearVelocity.y,
                Grounded = grounded,
                AirTime = _airTime,
                Lag = _player.ScrollLag
            };
        }

        /// <summary>Copies the level around the runner into the planner as rectangles.</summary>
        private void BuildWorld(float speed)
        {
            JumpPlanner planner = _brain.Planner;
            planner.ClearWorld();
            planner.Model = new JumpModel
            {
                Size = _collider.size,
                Gravity = _player.Gravity,
                JumpVelocity = _player.JumpVelocity,
                FallMultiplier = _player.FallGravityMultiplier,
                LowJumpMultiplier = _player.LowJumpMultiplier,
                CoyoteTime = _player.CoyoteTime,
                CatchUpGain = _player.CatchUpGain,
                MaxCatchUp = _player.MaxCatchUpSpeed,
                CatchUpDeadzone = _player.CatchUpDeadzone
            };

            Vector2 at = _collider.bounds.center;
            float killY = _player.FallDeathY;
            planner.KillY = float.IsInfinity(killY) ? at.y - 10f : killY;

            float ahead = (speed + _player.MaxCatchUpSpeed) * _lookAhead + 12f;
            float bottom = Mathf.Min(planner.KillY, at.y - 2f);
            var area = Rect.MinMaxRect(at.x - 3f, bottom, at.x + ahead, at.y + 10f);
            var filter = new ContactFilter2D { useTriggers = true };
            Physics2D.OverlapBox(area.center, area.size, 0f, filter, _overlaps);

            foreach (var other in _overlaps)
            {
                switch (RoleOf(other))
                {
                    case Role.Solid:
                        planner.AddSolid(ToRect(other.bounds));
                        break;
                    case Role.Hazard:
                        planner.AddHazard(ToRect(other.bounds));
                        break;
                    case Role.Mover:
                        planner.AddMover(ToRect(other.bounds), other.GetComponent<PatrolEnemy>().Motion);
                        break;
                }
            }

            // Placed traps are added from the registry instead of physics, because ghosts have
            // no colliders yet — but only once this runner has had time to notice them.
            float now = Time.time;
            foreach (var trap in PlacedTrap.Active)
            {
                if (now - trap.PlacedAt < _brain.CurrentSkill.ReactionTime) continue;
                if (trap.Drone != null) planner.AddMover(trap.Bounds, trap.Drone.Motion);
                else planner.AddHazard(trap.Bounds); // a crate is avoided like a hazard
            }
        }

        private Role RoleOf(Collider2D other)
        {
            if (_roles.TryGetValue(other, out Role role))
            {
                return role;
            }

            if (other == _collider || other.GetComponent<PlacedTrap>() != null)
                role = Role.Ignore;
            else if (other.isTrigger)
                role = other.GetComponent<PatrolEnemy>() != null ? Role.Mover
                    : other.GetComponent<Hazard>() != null ? Role.Hazard : Role.Ignore;
            else
                role = (_groundMask & (1 << other.gameObject.layer)) != 0 ? Role.Solid : Role.Ignore;

            _roles[other] = role;
            return role;
        }

        private int CountNoticedTraps()
        {
            int count = 0;
            float now = Time.time;
            foreach (var trap in PlacedTrap.Active)
            {
                if (now - trap.PlacedAt >= _brain.CurrentSkill.ReactionTime) count++;
            }
            return count;
        }

        private static Rect ToRect(Bounds bounds) =>
            Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!_drawPlan || !_active || _trace.Count < 2) return;
            Gizmos.color = new Color(0.43f, 0.94f, 0.78f);
            for (int i = 1; i < _trace.Count; i++)
            {
                Gizmos.DrawLine(_trace[i - 1], _trace[i]);
            }
        }
#endif
    }
}
