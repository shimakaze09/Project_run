using System;

namespace Run.Core
{
    /// <summary>The kinds of timed power-up a pickup can grant (see <see cref="PowerUpEvents"/>).</summary>
    public enum PowerUpType
    {
        /// <summary>Sentinel for "nothing active" - never itself granted by a pickup.</summary>
        None,
        Shield,
        SpeedBoost,
        DoubleJump
    }

    /// <summary>
    /// Fires when a power-up starts or ends. <see cref="Run.Player.PlayerController"/>,
    /// <see cref="Run.UI.HudController"/> and <see cref="Run.Audio.AudioManager"/> each subscribe
    /// independently, so a pickup never needs to know any of them exist.
    /// </summary>
    public static class PowerUpEvents
    {
        /// <summary>Raised when a power-up is granted, with its duration in seconds.</summary>
        public static event Action<PowerUpType, float> OnPowerUpActivated;

        /// <summary>Raised when a power-up's effect ends.</summary>
        public static event Action<PowerUpType> OnPowerUpExpired;

        /// <summary>The most recently activated power-up still considered active.</summary>
        public static PowerUpType Active { get; private set; } = PowerUpType.None;

        /// <summary>
        /// Grants <paramref name="type"/> for <paramref name="duration"/> seconds. Ignored for
        /// <see cref="PowerUpType.None"/> or a non-positive duration, since either would leave
        /// subscribers (like the HUD, which uses None to mean nothing is active) in a
        /// contradictory state.
        /// </summary>
        public static void TriggerActivated(PowerUpType type, float duration)
        {
            if (type == PowerUpType.None || duration <= 0f)
            {
                return;
            }

            Active = type;
            OnPowerUpActivated?.Invoke(type, duration);
        }

        /// <summary>Ends <paramref name="type"/>'s effect.</summary>
        public static void TriggerExpired(PowerUpType type)
        {
            if (Active == type)
            {
                Active = PowerUpType.None;
            }

            OnPowerUpExpired?.Invoke(type);
        }
    }
}
