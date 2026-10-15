using System;
using UnityEngine;

namespace Run.Core
{
    /// <summary>
    /// Persists the player's accessibility choices across sessions, mirroring
    /// <see cref="VolumePreferences"/>'s pattern so visual systems (see
    /// <see cref="Run.Generation.LevelChunk"/>, <see cref="Run.EnvironmentMode.TrapFactory"/>)
    /// can react to a change without knowing where it came from.
    /// </summary>
    public static class AccessibilityPreferences
    {
        private const string HighContrastKey = "run.accessibility.highContrast";

        private static bool _highContrast = PlayerPrefs.GetInt(HighContrastKey, 0) != 0;

        /// <summary>Raised whenever <see cref="SetHighContrast"/> changes the value.</summary>
        public static event Action Changed;

        /// <summary>
        /// When on, hazards (spikes, enemies, drones) are recoloured to a single bright warning
        /// colour and standable level geometry to a single dark one, trading the normal varied
        /// palette for maximum lightness contrast between the two - a cue that still reads for
        /// colourblind players, unlike the normal palette's similarly-toned hues.
        /// </summary>
        public static bool HighContrast => _highContrast;

        public static void SetHighContrast(bool value)
        {
            if (_highContrast == value)
            {
                return;
            }

            _highContrast = value;
            PlayerPrefs.SetInt(HighContrastKey, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
