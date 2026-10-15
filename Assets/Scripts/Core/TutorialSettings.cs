using UnityEngine;

namespace Run.Core
{
    /// <summary>
    /// Whether the run that is about to load should be a guided tutorial run.
    /// </summary>
    /// <remarks>
    /// Set by the main menu's TUTORIAL button just before the gameplay scene loads, and read by
    /// <see cref="Run.UI.PauseMenu"/> (which adds the on-screen guide) when that scene starts.
    /// It is deliberately never saved: the tutorial only runs when the player asks for it, and
    /// the main menu clears it every time it opens.
    /// </remarks>
    public static class TutorialSettings
    {
        /// <summary>True while the current session should show the tutorial guide.</summary>
        public static bool Active { get; private set; }

        public static void Begin() => Active = true;

        public static void End() => Active = false;

        // Static values survive between Play sessions when Unity's "Enter Play Mode" options
        // skip the domain reload, so reset explicitly at startup.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStartup() => Active = false;
    }
}
