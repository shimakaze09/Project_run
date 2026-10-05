using System;
using UnityEngine;

namespace Run.Core
{
    /// <summary>Who the person at the keyboard plays as.</summary>
    public enum GameMode
    {
        /// <summary>The classic run: you jump, the level tries to stop you.</summary>
        Runner,

        /// <summary>You are the level: a computer runner jumps while you place traps ahead of it.</summary>
        Environment
    }

    /// <summary>
    /// Persists the chosen <see cref="GameMode"/> across sessions (and across the scene reload
    /// <see cref="GameManager.Restart"/> triggers), mirroring <see cref="DifficultySettings"/>.
    /// </summary>
    /// <remarks>
    /// The mode can change freely on the Ready screen. Gameplay systems should read
    /// <see cref="GameManager.RunMode"/> once a run is live, which is captured when the run starts
    /// and can no longer change underneath them.
    /// </remarks>
    public static class GameModeSettings
    {
        private const string PrefsKey = "run.gameMode";

        /// <summary>Raised whenever <see cref="Set"/> changes the active mode.</summary>
        public static event Action<GameMode> Changed;

        public static GameMode Current { get; private set; } =
            (GameMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefsKey, (int)GameMode.Runner), 0, 1);

        public static bool IsEnvironment => Current == GameMode.Environment;

        public static void Set(GameMode mode)
        {
            if (Current == mode)
            {
                return;
            }

            Current = mode;
            PlayerPrefs.SetInt(PrefsKey, (int)mode);
            PlayerPrefs.Save();
            Changed?.Invoke(Current);
        }
    }
}
