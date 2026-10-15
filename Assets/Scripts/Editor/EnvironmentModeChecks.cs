#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using Run.AI;
using Run.Core;
using Run.EnvironmentMode;
using Run.Hazards;

namespace Run.EditorTools
{
    /// <summary>
    /// Repeatable checks for Environment mode: the computer runner against hand-made courses at
    /// every speed the game reaches, drone prediction, trap building and environment scoring.
    /// Saved player data and settings are restored in a finally block.
    /// </summary>
    public static class EnvironmentModeChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float Step = 0.02f;
        private static readonly float[] Speeds = { 6f, 10f, 13f, 16f, 20.8f }; // Easy start .. Hard max

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        [MenuItem("Tools/Endless Runner/Verify Environment Mode")]
        public static void RunMenu() { Debug.Log(RunChecks()); }

        public static string RunChecks()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
            var result = new StringBuilder();
            CheckCourses(result);
            CheckSeamWindow(result);
            CheckDronePrediction(result);
            CheckTraps(result);
            CheckScoring(result);
            return result.ToString();
        }

        private static JumpModel Model()
        {
            // The PlayerController defaults: 3.2 high in 0.38 s, 1.7x fall, 2.2x low jump.
            float gravity = 2f * 3.2f / (0.38f * 0.38f);
            return new JumpModel
            {
                Size = new Vector2(0.85f, 1f), Gravity = gravity, JumpVelocity = gravity * 0.38f,
                FallMultiplier = 1.7f, LowJumpMultiplier = 2.2f, CoyoteTime = 0.1f,
                CatchUpGain = 3f, MaxCatchUp = 6f, CatchUpDeadzone = 0.05f
            };
        }

        private static readonly string[] Courses =
            { "flat", "gap", "spikes", "staircase", "floating platforms", "gap then spikes", "elevated", "seams then gap", "drone" };

        /// <summary>Builds one of the courses (copies of the authored chunks) into a planner.</summary>
        private static void BuildCourse(JumpPlanner planner, int course, float time)
        {
            planner.ClearWorld();
            switch (course)
            {
                case 0: planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0)); break;
                case 1:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 20, 0));
                    planner.AddSolid(Rect.MinMaxRect(24.5f, -10, 200, 0));
                    break;
                case 2:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0));
                    planner.AddHazard(Rect.MinMaxRect(20f, 0.04f, 20.47f, 0.6f));
                    break;
                case 3:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0));
                    planner.AddSolid(Rect.MinMaxRect(20f, 0, 21.43f, 0.85f));
                    planner.AddSolid(Rect.MinMaxRect(21.43f, 0, 22.86f, 1.7f));
                    planner.AddSolid(Rect.MinMaxRect(22.86f, 0, 24.29f, 0.85f));
                    break;
                case 4:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 23.89f, 0));
                    planner.AddSolid(Rect.MinMaxRect(26.53f, 0.09f, 28.43f, 0.59f));
                    planner.AddSolid(Rect.MinMaxRect(31.07f, 0.09f, 32.97f, 0.59f));
                    planner.AddSolid(Rect.MinMaxRect(35.6f, 0.09f, 37.5f, 0.59f));
                    planner.AddSolid(Rect.MinMaxRect(40.15f, -10, 200, 0));
                    break;
                case 5:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 20, 0));
                    planner.AddSolid(Rect.MinMaxRect(24.5f, -10, 200, 0));
                    planner.AddHazard(Rect.MinMaxRect(30f, 0f, 31.5f, 0.6f));
                    break;
                case 6:
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0));
                    planner.AddSolid(Rect.MinMaxRect(20f, 0, 23.5f, 1.33f));
                    break;
                case 7:
                    // Many chunk seams in a row, then a short gap and a raised block (the case
                    // that once shrank an easy window to a few steps).
                    for (int i = 0; i < 6; i++) planner.AddSolid(Rect.MinMaxRect(-10 + i * 5f, -10, -5 + i * 5f, 0));
                    planner.AddSolid(Rect.MinMaxRect(22.7f, -10, 30, 0));
                    planner.AddSolid(Rect.MinMaxRect(30, -10, 200, 0));
                    planner.AddSolid(Rect.MinMaxRect(33f, 0, 36.5f, 1.33f));
                    break;
                case 8:
                    // A drone pacing 18..24 on flat ground, at its position `time` seconds in.
                    planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0));
                    var motion = new PatrolMotion { X = 21f, MinX = 18f, MaxX = 24f, Speed = 2.5f, Direction = -1 };
                    float x = motion.PredictX(time);
                    int direction = motion.PredictX(time + 0.01f) >= x ? 1 : -1;
                    var now = new PatrolMotion { X = x, MinX = 18f, MaxX = 24f, Speed = 2.5f, Direction = direction };
                    planner.AddMover(new Rect(x - 0.4f, 0.05f, 0.8f, 0.8f), now);
                    break;
            }
        }

        /// <summary>Flies the brain through a course; returns null on a clean run, else what went wrong.</summary>
        private static string RunCourse(int course, float speed)
        {
            var brain = new RunnerBrain();
            brain.Reset(new RunnerBrain.Skill(), 1); // no deliberate mistakes: test the planning itself
            brain.Planner.Model = Model();
            brain.Planner.KillY = -6f;
            var state = new RunnerState { Position = new Vector2(5f, 0.5f), Grounded = true, Footing = true };
            int holdEnd = -1;

            for (int step = 0; step < 4000 && state.Position.x < 50f; step++)
            {
                BuildCourse(brain.Planner, course, step * Step);
                bool canJump = (state.Grounded || state.AirTime < 0.1f) && state.VelocityY <= 0.5f;
                if (brain.Think(state, speed, Step, canJump, false, out float hold))
                {
                    state.VelocityY = brain.Planner.Model.JumpVelocity;
                    state.Grounded = false;
                    holdEnd = step + Mathf.RoundToInt(hold / Step);
                    brain.Jumped();
                }

                var outcome = brain.Planner.Step(ref state, speed, Step, 0f, step < holdEnd);
                if (outcome != JumpPlanner.Outcome.Safe)
                {
                    return $"{outcome} at x={state.Position.x:F1}";
                }
            }
            return null;
        }

        private static void CheckCourses(StringBuilder result)
        {
            for (int course = 0; course < Courses.Length; course++)
            {
                foreach (float speed in Speeds)
                {
                    string failure = RunCourse(course, speed);
                    Check(failure == null, $"Runner failed '{Courses[course]}' at speed {speed}: {failure}.");
                }
            }

            // A 20-unit spike bed can't be cleared: the planner must admit there is no clean jump.
            var planner = new JumpPlanner { Model = Model(), KillY = -6f };
            planner.AddSolid(Rect.MinMaxRect(-10, -10, 200, 0));
            planner.AddHazard(Rect.MinMaxRect(20f, 0f, 40f, 0.6f));
            var start = new RunnerState { Position = new Vector2(12f, 0.5f), Grounded = true };
            bool planned = planner.TryPlan(start, 10f, Step, 1.2f, out JumpPlan plan);
            Check(!planned || !plan.Clean, "An impossible spike bed must not get a clean plan.");

            result.AppendLine($"PASS computer runner clears {Courses.Length} courses (incl. drone) at speeds 6-20.8; impossible bed has no clean plan");
        }

        private static void CheckSeamWindow(StringBuilder result)
        {
            // Chunk seams must not cut a take-off window short (see the footing fix).
            var planner = new JumpPlanner { Model = Model(), KillY = -6.3f };
            planner.AddSolid(Rect.MinMaxRect(85.94f, -10f, 92.00f, 0f));
            planner.AddSolid(Rect.MinMaxRect(92.00f, -10f, 96.02f, 0f));
            planner.AddSolid(Rect.MinMaxRect(98.74f, -10f, 104.39f, 0f));
            planner.AddSolid(Rect.MinMaxRect(104.39f, -10f, 113.06f, 0f));
            planner.AddSolid(Rect.MinMaxRect(106.97f, 0f, 110.49f, 1.33f));
            var start = new RunnerState { Position = new Vector2(88f, 0.5f), Grounded = true };
            Check(planner.TryPlan(start, 11.4f, Step, 1.2f, out JumpPlan plan) && plan.Clean, "Seam course must have a clean plan.");
            int width = plan.WindowEnd - plan.WindowStart + 1;
            Check(width >= 15, $"Seam course window too narrow ({width} steps).");
            result.AppendLine($"PASS chunk seams keep the full take-off window ({width} steps for a 2.7-unit gap)");
        }

        private static void CheckDronePrediction(StringBuilder result)
        {
            float worst = 0f;
            foreach (float startX in new[] { 2f, 3.3f, 5f })
            {
                foreach (int startDirection in new[] { -1, 1 })
                {
                    float x = startX;
                    int direction = startDirection;
                    var motion = new PatrolMotion { X = startX, MinX = 2f, MaxX = 5f, Speed = 2.5f, Direction = startDirection };
                    for (int i = 1; i <= 400; i++)
                    {
                        // The same rule PatrolEnemy.Update applies each frame.
                        x += direction * 2.5f * Step;
                        if (x <= 2f) { x = 2f; direction = 1; }
                        else if (x >= 5f) { x = 5f; direction = -1; }
                        worst = Mathf.Max(worst, Mathf.Abs(motion.PredictX(i * Step) - x));
                    }
                }
            }
            Check(worst <= 2.5f * Step + 0.001f, $"Drone prediction drifted by {worst:F3}.");
            result.AppendLine($"PASS drone prediction within one step ({worst:F3} units) over 8 s");
        }

        private static void CheckTraps(StringBuilder result)
        {
            var root = new GameObject("Environment Trap Checks");
            try
            {
                int ground = LayerMask.NameToLayer("Ground");
                var crate = TrapFactory.Build(TrapKind.Crate, root.transform);
                var spikes = TrapFactory.Build(TrapKind.Spikes, root.transform);
                var drone = TrapFactory.Build(TrapKind.Drone, root.transform);
                Check(!crate.gameObject.activeSelf && !spikes.gameObject.activeSelf && !drone.gameObject.activeSelf,
                    "Built traps must wait inactive in the pool.");
                Check(crate.gameObject.layer == ground && !crate.GetComponent<Collider2D>().isTrigger,
                    "A crate must be solid ground.");
                Check(spikes.GetComponent<Hazard>() != null && spikes.GetComponent<Collider2D>().isTrigger,
                    "Spikes must be a hazard trigger.");
                Check(drone.Drone != null && drone.GetComponent<Collider2D>().isTrigger, "A drone must patrol and hurt.");
                Check(TrapFactory.FootprintWidth(TrapKind.Drone) > TrapFactory.BodySize(TrapKind.Drone).x,
                    "A drone must claim its whole patrol.");
                result.AppendLine("PASS trap building: inactive pooled crate (solid), spikes (hazard), drone (patrol)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void CheckScoring(StringBuilder result)
        {
            string[] intKeys = { "run.coins", "run.bestDistanceScore", "run.environment.bestStopDistance" };
            const string lastKey = "run.lastRunDistance";
            bool[] had = Array.ConvertAll(intKeys, PlayerPrefs.HasKey);
            int[] values = Array.ConvertAll(intKeys, key => PlayerPrefs.GetInt(key));
            bool hadLast = PlayerPrefs.HasKey(lastKey);
            float last = PlayerPrefs.GetFloat(lastKey);
            GameMode mode = GameModeSettings.Current;
            var root = new GameObject("Environment Scoring Checks");
            root.SetActive(false);
            try
            {
                PlayerPrefs.SetInt("run.coins", 7);
                PlayerPrefs.SetInt("run.bestDistanceScore", 300);
                PlayerPrefs.SetFloat(lastKey, 120f);
                PlayerPrefs.DeleteKey("run.environment.bestStopDistance");
                GameModeSettings.Set(GameMode.Environment);

                var gm = root.AddComponent<GameManager>();
                typeof(GameManager).GetMethod("Awake", Private).Invoke(gm, null);
                typeof(GameManager).GetField("_distanceSource", Private).SetValue(gm, root.transform);
                Check(gm.EnvironmentBestStop == 0, "No environment record yet.");

                gm.BeginRun();
                Check(gm.RunMode == GameMode.Environment, "Run must start in Environment mode.");
                root.transform.position = new Vector3(80f, 0f, 0f);
                gm.AddCoins(3);
                Check(gm.Coins == 7 && PlayerPrefs.GetInt("run.coins") == 7, "The computer runner's coins must not reach the wallet.");
                gm.NotifyPlayerDied(DeathCause.Hazard);
                Check(gm.EnvironmentBestStop == 80 && gm.IsNewEnvironmentBest, "First stop must set the record.");
                Check(PlayerPrefs.GetInt("run.bestDistanceScore") == 300 && Mathf.Approximately(PlayerPrefs.GetFloat(lastKey), 120f),
                    "Environment runs must not touch runner records.");
                typeof(GameManager).GetMethod("OnDestroy", Private).Invoke(gm, null);
                UnityEngine.Object.DestroyImmediate(gm);

                // Next session: a longer stop is not a record (stopping sooner is better).
                gm = root.AddComponent<GameManager>();
                typeof(GameManager).GetMethod("Awake", Private).Invoke(gm, null);
                typeof(GameManager).GetField("_distanceSource", Private).SetValue(gm, root.transform);
                Check(gm.EnvironmentBestStop == 80, "The environment record must persist.");
                root.transform.position = Vector3.zero;
                gm.BeginRun();
                root.transform.position = new Vector3(150f, 0f, 0f);
                gm.NotifyPlayerDied(DeathCause.FellIntoPit);
                Check(gm.EnvironmentBestStop == 80 && !gm.IsNewEnvironmentBest, "A later stop must not beat the record.");
                typeof(GameManager).GetMethod("OnDestroy", Private).Invoke(gm, null);
                result.AppendLine("PASS scoring: wallet and runner records untouched; fastest stop 80 kept over a later 150");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                for (int i = 0; i < intKeys.Length; i++)
                {
                    if (had[i]) PlayerPrefs.SetInt(intKeys[i], values[i]); else PlayerPrefs.DeleteKey(intKeys[i]);
                }
                if (hadLast) PlayerPrefs.SetFloat(lastKey, last); else PlayerPrefs.DeleteKey(lastKey);
                GameModeSettings.Set(mode);
                PlayerPrefs.Save();
            }
        }
    }
}
#endif
