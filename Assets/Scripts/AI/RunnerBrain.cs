using UnityEngine;

namespace Run.AI
{
    /// <summary>
    /// The computer runner's decision-making, step by step: when to (re)plan, which take-off to
    /// commit to, and how sloppy to be about it. Plain C#, so the checks can run the exact same
    /// logic as the game against a hand-made world.
    /// </summary>
    /// <remarks>
    /// The caller copies the world into <see cref="Planner"/>, then calls <see cref="Think"/> once
    /// per physics step while the runner can jump. When it returns true the caller performs the
    /// jump and confirms it with <see cref="Jumped"/>.
    /// </remarks>
    public sealed class RunnerBrain
    {
        [System.Serializable]
        public struct Skill
        {
            [Tooltip("Seconds before a newly placed trap is noticed.")]
            [Min(0f)] public float ReactionTime;

            [Tooltip("Typical take-off timing error in seconds (standard deviation).")]
            [Min(0f)] public float TimingJitter;

            [Tooltip("Chance per jump of a clear mistake: going badly early or late.")]
            [Range(0f, 1f)] public float MistakeChance;

            [Tooltip("How far off a mistake is, in seconds.")]
            [Min(0f)] public float MistakeSize;
        }

        public readonly JumpPlanner Planner = new JumpPlanner();

        /// <summary>How far ahead (in seconds of running) the runner looks for danger.</summary>
        public float LookAhead = 1.2f;

        /// <summary>Re-check the current plan this often, in physics steps, to follow speed changes.</summary>
        public int ReplanEverySteps = 8;

        public Skill CurrentSkill;

        private System.Random _random = new System.Random();
        private int _step;
        private bool _hasPlan;
        private int _targetStep;      // the ideal take-off, kept stable across re-plans
        private int _errorSteps;      // this jump's timing error, rolled once per danger
        private int _takeoffStep;     // _targetStep + _errorSteps
        private float _takeoffHold;
        private int _replanAt;
        private JumpPlan _plan;

        public bool HasPlan => _hasPlan;

        /// <summary>The plan behind the current (or last) decision, for logging and gizmos.</summary>
        public JumpPlan LastPlan => _plan;

        /// <summary>Physics steps until the committed take-off (negative once due).</summary>
        public int StepsUntilTakeoff => _takeoffStep - _step;

        public int TimingErrorSteps => _errorSteps;

        /// <summary>Starts a fresh run; a non-zero <paramref name="seed"/> makes the mistakes repeatable.</summary>
        public void Reset(Skill skill, int seed)
        {
            CurrentSkill = skill;
            _random = seed != 0 ? new System.Random(seed) : new System.Random();
            _step = 0;
            _hasPlan = false;
        }

        /// <summary>
        /// One physics step of thinking. Returns true when the runner should jump right now,
        /// holding the button for <paramref name="hold"/> seconds.
        /// </summary>
        /// <param name="canJump">False while airborne; any plan is dropped until the next landing.</param>
        /// <param name="worldChanged">Something new appeared (e.g. a trap was noticed): re-plan now.</param>
        public bool Think(RunnerState state, float speed, float dt, bool canJump, bool worldChanged, out float hold)
        {
            _step++;
            hold = 0f;
            if (!canJump)
            {
                _hasPlan = false;
                return false;
            }

            if (!_hasPlan || worldChanged || _step >= _replanAt)
            {
                Replan(state, speed, dt);
            }

            if (_hasPlan && _step >= _takeoffStep)
            {
                hold = _takeoffHold;
                return true;
            }
            return false;
        }

        /// <summary>The jump requested by <see cref="Think"/> happened; wait for the next danger.</summary>
        public void Jumped() => _hasPlan = false;

        private void Replan(RunnerState state, float speed, float dt)
        {
            if (!Planner.TryPlan(state, speed, dt, LookAhead, out JumpPlan plan))
            {
                _hasPlan = false;
                return;
            }

            // Keep the earlier target while it is still safe. Re-aiming at the middle of what
            // is left of the window every time would keep sliding the jump later and later.
            int windowStart = _step + plan.WindowStart;
            int windowEnd = _step + plan.WindowEnd;
            bool keepTarget = _hasPlan && plan.Clean && _targetStep >= windowStart && _targetStep <= windowEnd;
            if (!keepTarget)
            {
                _targetStep = _step + plan.DelaySteps;
            }
            if (!_hasPlan)
            {
                _errorSteps = RollTimingError(dt);
            }

            _takeoffStep = Mathf.Max(_step, _targetStep + _errorSteps);
            _takeoffHold = Planner.HoldForDelay(plan, _takeoffStep - _step);
            _hasPlan = true;
            _replanAt = _step + ReplanEverySteps;
            _plan = plan;
        }

        /// <summary>How sloppy this jump will be: usually a little, occasionally a lot.</summary>
        private int RollTimingError(float dt)
        {
            float seconds = Gaussian() * CurrentSkill.TimingJitter;
            if (_random.NextDouble() < CurrentSkill.MistakeChance)
            {
                seconds += (_random.NextDouble() < 0.5 ? -1f : 1f) * CurrentSkill.MistakeSize;
            }
            return Mathf.RoundToInt(seconds / dt);
        }

        private float Gaussian()
        {
            // Box-Muller: two uniform numbers in, one normally distributed number out.
            double u1 = 1.0 - _random.NextDouble();
            double u2 = _random.NextDouble();
            return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Cos(2.0 * System.Math.PI * u2));
        }
    }
}
