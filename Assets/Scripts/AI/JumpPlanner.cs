using System.Collections.Generic;
using UnityEngine;
using Run.Hazards;

namespace Run.AI
{
    /// <summary>The runner's body and jump physics, copied from <see cref="Run.Player.PlayerController"/>.</summary>
    public struct JumpModel
    {
        /// <summary>Collider size in world units.</summary>
        public Vector2 Size;

        /// <summary>Base downward acceleration (a positive number).</summary>
        public float Gravity;
        public float JumpVelocity;
        public float FallMultiplier;
        public float LowJumpMultiplier;
        public float CoyoteTime;

        // The controller's catch-up sprint: extra speed in proportion to how far the runner
        // trails the scroll line. Leave at zero to simulate a constant speed.
        public float CatchUpGain;
        public float MaxCatchUp;
        public float CatchUpDeadzone;
    }

    /// <summary>A snapshot of the runner at one moment of a simulation.</summary>
    public struct RunnerState
    {
        /// <summary>Collider centre.</summary>
        public Vector2 Position;
        public float VelocityY;
        public bool Grounded;

        /// <summary>
        /// Standing with its centre over solid ground: a full-strength take-off spot. Near an
        /// edge the real (round-bottomed) capsule is already starting to roll off.
        /// </summary>
        public bool Footing;

        /// <summary>Seconds since the runner last stood on ground (for coyote time).</summary>
        public float AirTime;

        /// <summary>How far the runner trails the scroll line (negative when ahead).</summary>
        public float Lag;
    }

    /// <summary>What the planner decided: leave the ground after a delay, holding jump for a while.</summary>
    public struct JumpPlan
    {
        /// <summary>Physics steps to keep running before jumping (0 = jump now).</summary>
        public int DelaySteps;

        /// <summary>How long to hold jump; shorter holds give lower, shorter hops.</summary>
        public float HoldTime;

        /// <summary>False when no safe jump exists and this is merely the least-bad option.</summary>
        public bool Clean;

        /// <summary>First and last delay (in steps) that still give a safe jump.</summary>
        public int WindowStart;
        public int WindowEnd;
    }

    /// <summary>
    /// Decides when the computer runner should jump by simulating its future, the same way a
    /// person imagines "if I keep running I'll hit that — when could I jump to clear it?".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The world is reduced to plain rectangles: solid ground and platforms, still hazards, and
    /// patrolling enemies whose movement is predicted with <see cref="PatrolMotion"/>. Every level
    /// piece in this game is an axis-aligned box, so this is exact enough and cheap enough to
    /// try dozens of jumps per decision. Nothing here touches Unity physics, which also makes the
    /// planner easy to test with hand-made worlds (see <c>EnvironmentModeChecks</c>).
    /// </para>
    /// <para>
    /// Planning works in three steps:
    /// 1. Run forward without jumping. If nothing goes wrong within the horizon, keep running.
    /// 2. Otherwise try a jump at each step before the danger, with several hold lengths, and
    ///    keep the ones that land safely past it. Consecutive safe steps form a timing window.
    /// 3. Pick the take-off in the first window that is furthest from both window edges and
    ///    lands with the most ground ahead, which leaves the most room for error.
    /// </para>
    /// </remarks>
    public sealed class JumpPlanner
    {
        /// <summary>Hold lengths to try, longest (highest, farthest) first.</summary>
        public static readonly float[] HoldOptions = { 0.5f, 0.26f, 0.15f, 0.06f };

        private const float Skin = 0.02f;

        /// <summary>Extra clearance kept from hazards, covering the gap between this box model and the real capsule.</summary>
        public float HazardMargin = 0.06f;

        /// <summary>Half-width used against ground; matches the controller's ground-check box.</summary>
        public float SolidHalfWidth = 0.3f;

        /// <summary>
        /// How far past an edge the runner's centre may be and still land. Smaller than
        /// <see cref="SolidHalfWidth"/>: a capsule caught on a corner slides off rather than landing.
        /// </summary>
        public float LandingHalfWidth = 0.1f;

        /// <summary>After landing past the danger, the runner needs at least this long on its feet.</summary>
        public float MinRunway = 0.04f;

        /// <summary>Ground ahead of a landing beyond this many seconds counts as "plenty".</summary>
        public float RunwayCap = 0.2f;

        /// <summary>World Y below which the runner is lost (the fall-death line).</summary>
        public float KillY = -10f;

        public JumpModel Model;

        private struct Mover
        {
            public Rect Bounds;
            public PatrolMotion Motion;
        }

        /// <summary>How one simulated step (or a whole trial) ended.</summary>
        public enum Outcome { Safe, Hit, Blocked, Fell }

        private struct Trial
        {
            public Outcome Outcome;
            public int Step;      // step at which the trial ended
            public float ClearX;  // a jump must land at or beyond this X to have cleared the danger
            public int Runway;    // steps on the ground after a safe landing (capped)
        }

        private readonly List<Rect> _solids = new List<Rect>();
        private readonly List<Rect> _hazards = new List<Rect>();
        private readonly List<Mover> _movers = new List<Mover>();
        private readonly List<RunnerState> _path = new List<RunnerState>();
        private float[] _holdAtStep = new float[0];
        private int[] _runwayAtStep = new int[0];

        public void ClearWorld()
        {
            _solids.Clear();
            _hazards.Clear();
            _movers.Clear();
        }

        public void AddSolid(Rect bounds) => _solids.Add(bounds);

        public void AddHazard(Rect bounds) => _hazards.Add(bounds);

        /// <summary>Adds a moving hazard; <paramref name="boundsNow"/> is where it is at time zero.</summary>
        public void AddMover(Rect boundsNow, PatrolMotion motion) =>
            _movers.Add(new Mover { Bounds = boundsNow, Motion = motion });

        public int SolidCount => _solids.Count;
        public int HazardCount => _hazards.Count + _movers.Count;

        /// <summary>Time from take-off to landing back at take-off height for a fully held jump.</summary>
        public float FullJumpAirTime
        {
            get
            {
                float g = Mathf.Max(0.01f, Model.Gravity);
                float rise = Model.JumpVelocity / g;
                float apex = Model.JumpVelocity * rise * 0.5f;
                float fall = Mathf.Sqrt(2f * apex / (g * Mathf.Max(1f, Model.FallMultiplier)));
                return rise + fall;
            }
        }

        /// <summary>
        /// Looks ahead from <paramref name="start"/>. Returns false when running on is fine (or
        /// when every jump is worse than not jumping); otherwise fills in when and how to jump.
        /// </summary>
        public bool TryPlan(RunnerState start, float runSpeed, float dt, float horizon, out JumpPlan plan)
        {
            plan = default;
            int horizonSteps = Mathf.Max(1, Mathf.CeilToInt(horizon / dt));
            Trial danger = RunWithoutJumping(SnapToFloor(start), runSpeed, dt, horizonSteps);
            if (danger.Outcome == Outcome.Safe)
            {
                return false;
            }

            // The latest possible take-off is the last moment the runner still has ground (or
            // coyote time) under it; for a pit that is the edge, not the point of no return.
            int last = Mathf.Min(danger.Step - 1, _path.Count - 1);
            while (last > 0 && !CanTakeOff(_path[last]))
            {
                last--;
            }

            // A take-off earlier than one full jump before that would land short of the danger,
            // so only the last stretch of the path is worth trying.
            int airSteps = Mathf.CeilToInt(FullJumpAirTime / dt) + 6;
            int first = Mathf.Max(0, last - airSteps);
            int jumpSteps = airSteps * 2 + horizonSteps;
            if (_holdAtStep.Length < _path.Count)
            {
                _holdAtStep = new float[_path.Count * 2];
                _runwayAtStep = new int[_path.Count * 2];
            }

            int windowStart = -1;
            int windowEnd = -1;
            int fallbackScore = Score(danger);
            int fallbackStep = -1;
            float fallbackHold = HoldOptions[0];

            for (int k = first; k <= last; k++)
            {
                RunnerState takeoff = _path[k];
                int bestRunway = -1;

                if (CanTakeOff(takeoff))
                {
                    // Keep the hold that lands with the most ground ahead; on ties the longer
                    // hold wins, since it also clears hazards by more. Only a take-off with
                    // solid footing counts as clean; coyote-time jumps are a last resort.
                    foreach (float hold in HoldOptions)
                    {
                        Trial trial = TryJump(takeoff, k, runSpeed, dt, hold, danger.ClearX, jumpSteps, null);
                        if (trial.Outcome == Outcome.Safe && takeoff.Footing)
                        {
                            if (trial.Runway > bestRunway)
                            {
                                bestRunway = trial.Runway;
                                _holdAtStep[k] = hold;
                                _runwayAtStep[k] = trial.Runway;
                            }
                            continue;
                        }

                        int score = Score(trial);
                        if (score > fallbackScore)
                        {
                            fallbackScore = score;
                            fallbackStep = k;
                            fallbackHold = hold;
                        }
                    }
                }

                if (bestRunway >= 0)
                {
                    if (windowStart < 0) windowStart = k;
                    windowEnd = k;
                }
                else if (windowStart >= 0)
                {
                    break; // the first window has closed; later ones are riskier
                }
            }

            if (windowStart >= 0)
            {
                // Best take-off: as far as possible from either edge of the window (room for
                // timing error) and landing with the most ground ahead (room for the next jump).
                int aim = windowStart;
                int bestQuality = int.MinValue;
                for (int k = windowStart; k <= windowEnd; k++)
                {
                    int quality = Mathf.Min(k - windowStart, windowEnd - k) + _runwayAtStep[k];
                    if (quality > bestQuality)
                    {
                        bestQuality = quality;
                        aim = k;
                    }
                }

                plan = new JumpPlan
                {
                    DelaySteps = aim,
                    HoldTime = _holdAtStep[aim],
                    Clean = true,
                    WindowStart = windowStart,
                    WindowEnd = windowEnd
                };
                return true;
            }

            // No clean jump. Take the least-bad one only if it beats simply running on.
            if (fallbackStep < 0)
            {
                return false;
            }

            plan = new JumpPlan
            {
                DelaySteps = fallbackStep,
                HoldTime = fallbackHold,
                Clean = false,
                WindowStart = fallbackStep,
                WindowEnd = fallbackStep
            };
            return true;
        }

        /// <summary>Hold length that keeps a jump at <paramref name="delaySteps"/> safe, using the last plan's results.</summary>
        public float HoldForDelay(JumpPlan plan, int delaySteps)
        {
            int step = Mathf.Clamp(delaySteps, plan.WindowStart, plan.WindowEnd);
            return plan.Clean && step >= 0 && step < _holdAtStep.Length ? _holdAtStep[step] : plan.HoldTime;
        }

        /// <summary>Records the planned route (run-up plus jump arc) of the last <see cref="TryPlan"/> call, for gizmos.</summary>
        public void TracePlan(JumpPlan plan, float runSpeed, float dt, List<Vector2> points)
        {
            points.Clear();
            int takeoff = Mathf.Clamp(plan.DelaySteps, 0, _path.Count - 1);
            for (int i = 0; i <= takeoff; i++)
            {
                points.Add(_path[i].Position);
            }
            // No danger line to clear: the trace simply stops a moment after the first landing.
            int steps = Mathf.CeilToInt(FullJumpAirTime / dt) * 2;
            TryJump(_path[takeoff], takeoff, runSpeed, dt, plan.HoldTime, float.NegativeInfinity, steps, points);
        }

        /// <summary>
        /// Advances <paramref name="state"/> by one physics step in the current world; used by the
        /// checks to fly the runner through a plan exactly as the planner imagines it.
        /// </summary>
        public Outcome Step(ref RunnerState state, float runSpeed, float dt, float time, bool holding)
        {
            float ignored = 0f;
            return Advance(ref state, runSpeed, dt, time, holding, ref ignored);
        }

        // Coyote time counts down in the controller too; only use part of it to stay safe.
        private bool CanTakeOff(RunnerState state) =>
            state.Grounded || state.AirTime < Model.CoyoteTime * 0.6f;

        /// <summary>
        /// The real capsule rests a hair above or below the floor's top (physics contact offsets).
        /// Snap a grounded start onto the floor so the simulation doesn't fall through it.
        /// </summary>
        private RunnerState SnapToFloor(RunnerState state)
        {
            if (!state.Grounded)
            {
                return state;
            }

            float halfH = Model.Size.y * 0.5f;
            float bottom = state.Position.y - halfH;
            float x = state.Position.x;
            float best = float.NegativeInfinity;
            bool footing = false;
            foreach (Rect solid in _solids)
            {
                if (solid.xMax <= x - SolidHalfWidth || solid.xMin >= x + SolidHalfWidth) continue;
                if (Mathf.Abs(solid.yMax - bottom) <= 0.15f && solid.yMax >= best)
                {
                    footing = solid.yMax > best ? x >= solid.xMin && x <= solid.xMax
                        : footing || (x >= solid.xMin && x <= solid.xMax);
                    best = solid.yMax;
                }
            }

            if (best > float.NegativeInfinity)
            {
                state.Position.y = best + halfH;
                state.VelocityY = Mathf.Min(0f, state.VelocityY);
                state.Footing = footing;
            }
            return state;
        }

        // Ranks last-resort options: a safe but shaky (coyote-time) jump beats getting hit, and
        // falling is fatal, so it ranks below being hit or blocked. Between equal outcomes, the
        // one that goes wrong later is better.
        private static int Score(Trial trial)
        {
            int severity;
            switch (trial.Outcome)
            {
                case Outcome.Safe: severity = 3; break;
                case Outcome.Hit: severity = 2; break;
                case Outcome.Blocked: severity = 1; break;
                default: severity = 0; break;
            }
            return severity * 100000 + trial.Step;
        }

        /// <summary>Runs straight on, recording every step in <see cref="_path"/>, until something goes wrong.</summary>
        private Trial RunWithoutJumping(RunnerState start, float runSpeed, float dt, int maxSteps)
        {
            _path.Clear();
            _path.Add(start);
            RunnerState state = start;
            float lastGroundX = start.Position.x;

            for (int i = 1; i <= maxSteps; i++)
            {
                float clearX = 0f;
                Outcome outcome = Advance(ref state, runSpeed, dt, i * dt, false, ref clearX);
                if (state.Grounded)
                {
                    lastGroundX = state.Position.x;
                }

                if (outcome != Outcome.Safe)
                {
                    // For a pit, the danger begins at the edge the runner fell from.
                    if (outcome == Outcome.Fell)
                    {
                        clearX = lastGroundX + SolidHalfWidth + Skin;
                    }
                    return new Trial { Outcome = outcome, Step = i, ClearX = clearX };
                }

                _path.Add(state);
            }

            return new Trial { Outcome = Outcome.Safe, Step = maxSteps };
        }

        /// <summary>
        /// Jumps from <paramref name="takeoff"/> (reached at step <paramref name="takeoffStep"/>).
        /// Safe means landing on ground at or beyond <paramref name="clearX"/>, unharmed, with
        /// at least <see cref="MinRunway"/> of solid footing to jump from again.
        /// </summary>
        private Trial TryJump(RunnerState takeoff, int takeoffStep, float runSpeed, float dt, float hold,
            float clearX, int maxSteps, List<Vector2> record)
        {
            RunnerState state = takeoff;
            state.VelocityY = Model.JumpVelocity;
            state.Grounded = false;
            state.Footing = false;
            bool landed = false;
            int runway = 0;
            int runwayCap = Mathf.Max(1, Mathf.RoundToInt(RunwayCap / dt));
            int minRunway = Mathf.Max(1, Mathf.RoundToInt(MinRunway / dt));

            for (int i = 1; i <= maxSteps; i++)
            {
                bool holding = (i - 1) * dt < hold;
                float ignored = 0f;
                Outcome outcome = Advance(ref state, runSpeed, dt, (takeoffStep + i) * dt, holding, ref ignored);
                record?.Add(state.Position);
                if (outcome != Outcome.Safe)
                {
                    return new Trial { Outcome = outcome, Step = takeoffStep + i };
                }

                if (!landed)
                {
                    landed = state.Grounded && state.Position.x >= clearX;
                    if (!landed) continue;
                }

                // Count how long the landing spot offers solid footing to jump from again. A
                // landing just short of a platform's start gets its footing a step later.
                if (state.Footing)
                {
                    runway++;
                    if (runway >= runwayCap)
                    {
                        return new Trial { Outcome = Outcome.Safe, Step = takeoffStep + i, Runway = runway };
                    }
                }
                else if (runway > 0 || !state.Grounded)
                {
                    // Footing ran out (end of a short platform): fine if there was time to jump.
                    return runway >= minRunway
                        ? new Trial { Outcome = Outcome.Safe, Step = takeoffStep + i, Runway = runway }
                        : new Trial { Outcome = Outcome.Fell, Step = takeoffStep + i };
                }
            }

            // Never landed past the danger in time: treat as a miss.
            return new Trial { Outcome = Outcome.Fell, Step = takeoffStep + maxSteps };
        }

        /// <summary>One physics step of the runner, mirroring PlayerController.FixedUpdate plus collisions.</summary>
        private Outcome Advance(ref RunnerState state, float runSpeed, float dt, float time, bool holding, ref float clearX)
        {
            // Same gravity rules as the controller: heavier while falling, and while rising
            // with jump released (which is what makes short holds give short hops).
            float gravity = Model.Gravity;
            if (state.VelocityY < 0f) gravity *= Model.FallMultiplier;
            else if (state.VelocityY > 0f && !holding) gravity *= Model.LowJumpMultiplier;
            float velocityY = state.VelocityY - gravity * dt;

            float halfW = SolidHalfWidth;
            float halfH = Model.Size.y * 0.5f;
            Vector2 p = state.Position;
            float bottom = p.y - halfH;
            float top = p.y + halfH;

            // Horizontal: the scroll line moves at run speed, and the runner sprints to close any
            // gap behind it. Stop against the first wall face in the way.
            float lag = state.Lag;
            float catchUp = lag > Model.CatchUpDeadzone ? Mathf.Min(lag * Model.CatchUpGain, Model.MaxCatchUp) : 0f;
            float x = p.x + (runSpeed + catchUp) * dt;
            bool blocked = false;
            foreach (Rect solid in _solids)
            {
                if (solid.yMax <= bottom + Skin || solid.yMin >= top - Skin) continue;
                if (p.x + halfW <= solid.xMin + Skin && x + halfW > solid.xMin)
                {
                    x = solid.xMin - halfW;
                    blocked = true;
                    clearX = solid.xMin + halfW + Skin;
                }
            }

            // Vertical: land on the highest floor crossed, or bump the lowest ceiling. Staying on
            // a floor uses the ground-check width; landing on one needs the centre nearly over it.
            float y = p.y + velocityY * dt;
            bool grounded = false;
            bool footing = false;
            if (velocityY <= 0f)
            {
                float support = state.Grounded ? halfW : LandingHalfWidth;
                foreach (Rect solid in _solids)
                {
                    if (solid.xMax <= x - support || solid.xMin >= x + support) continue;
                    if (bottom >= solid.yMax - Skin && y - halfH < solid.yMax)
                    {
                        y = solid.yMax + halfH;
                        velocityY = 0f;
                        grounded = true;
                    }
                }

                // Footing is checked against every piece at standing height, not just the one
                // landed on: where two ground pieces meet (every chunk seam) either one counts.
                if (grounded)
                {
                    float feet = y - halfH;
                    foreach (Rect solid in _solids)
                    {
                        if (Mathf.Abs(solid.yMax - feet) <= Skin && x >= solid.xMin && x <= solid.xMax)
                        {
                            footing = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                foreach (Rect solid in _solids)
                {
                    if (solid.xMax <= x - halfW || solid.xMin >= x + halfW) continue;
                    if (top <= solid.yMin + Skin && y + halfH > solid.yMin)
                    {
                        y = solid.yMin - halfH;
                        velocityY = 0f;
                    }
                }
            }

            state.Position = new Vector2(x, y);
            state.VelocityY = velocityY;
            state.Grounded = grounded;
            state.Footing = footing;
            state.AirTime = grounded ? 0f : state.AirTime + dt;
            state.Lag = lag + runSpeed * dt - (x - p.x);

            // Hazards use the full body plus a margin, since touching one costs a heart.
            float margin = HazardMargin;
            var body = new Rect(
                x - Model.Size.x * 0.5f - margin, y - halfH - margin,
                Model.Size.x + margin * 2f, Model.Size.y + margin * 2f);

            foreach (Rect hazard in _hazards)
            {
                if (body.Overlaps(hazard))
                {
                    clearX = hazard.xMax + Model.Size.x * 0.5f + margin;
                    return Outcome.Hit;
                }
            }

            foreach (Mover mover in _movers)
            {
                Rect at = mover.Bounds;
                at.x += mover.Motion.PredictX(time) - mover.Motion.X;
                if (body.Overlaps(at))
                {
                    clearX = at.xMax + Model.Size.x * 0.5f + margin;
                    return Outcome.Hit;
                }
            }

            if (y < KillY)
            {
                return Outcome.Fell;
            }

            return blocked ? Outcome.Blocked : Outcome.Safe;
        }
    }
}
