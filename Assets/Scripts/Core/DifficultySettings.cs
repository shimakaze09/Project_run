using System;
using UnityEngine;

namespace Run.Core
{
    /// <summary>Player-selectable challenge level, chosen from the Ready screen before a run begins.</summary>
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// Persists the player's chosen <see cref="Difficulty"/> across sessions (and across the
    /// scene reload <see cref="GameManager.Restart"/> triggers) and exposes the tuning
    /// multipliers it maps to, so gameplay systems can scale themselves without knowing about
    /// each other.
    /// </summary>
    public static class DifficultySettings
    {
        private const string PrefsKey = "run.difficulty";

        /// <summary>Raised whenever <see cref="Set"/> changes the active difficulty.</summary>
        public static event Action<Difficulty> Changed;

        public static Difficulty Current { get; private set; } =
            (Difficulty)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)Difficulty.Normal), 0, 2);

        public static void Set(Difficulty difficulty)
        {
            if (Current == difficulty)
            {
                return;
            }

            Current = difficulty;
            PlayerPrefs.SetInt(PrefsKey, (int)difficulty);
            PlayerPrefs.Save();
            Changed?.Invoke(Current);
        }

        /// <summary>Scales the player's run speed — Easy runs slower, Hard runs faster.</summary>
        public static float SpeedMultiplier => Current switch
        {
            Difficulty.Easy => 0.6f,
            Difficulty.Hard => 1.6f,
            _ => 1f
        };

        /// <summary>Scales how fast run speed accelerates toward its ceiling over a run.</summary>
        public static float SpeedGainMultiplier => Current switch
        {
            Difficulty.Easy => 0.4f,
            Difficulty.Hard => 2.2f,
            _ => 1f
        };

        /// <summary>Scales how far the hazard-difficulty ramp stretches — bigger is slower to reach max.</summary>
        public static float RampDistanceMultiplier => Current switch
        {
            Difficulty.Easy => 3f,
            Difficulty.Hard => 0.35f,
            _ => 1f
        };

        /// <summary>Starting point on the hazard-difficulty ramp (0 = easiest chunks only).</summary>
        public static float StartDifficulty => Current switch
        {
            Difficulty.Easy => 0f,
            Difficulty.Hard => 0.65f,
            _ => 0.1f
        };
    }
}
