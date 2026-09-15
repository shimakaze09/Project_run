using System;

namespace Run.Core
{
    public enum PowerUpType
    {
        None,
        Shield,
        SpeedBoost,
        DoubleJump
    }

    public static class PowerUpEvents
    {
        public static event Action<PowerUpType, float> OnPowerUpActivated;
        public static event Action<PowerUpType> OnPowerUpExpired;

        public static void TriggerActivated(PowerUpType type, float duration)
        {
            OnPowerUpActivated?.Invoke(type, duration);
        }

        public static void TriggerExpired(PowerUpType type)
        {
            OnPowerUpExpired?.Invoke(type);
        }
    }
}