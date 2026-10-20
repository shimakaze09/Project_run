using UnityEngine;
using UnityEngine.InputSystem;
using Run.Core;

namespace Run.Common
{
    public class PowerUpTester : MonoBehaviour
    {
        // Independent of any pickup's own configured duration - there's no single "canonical"
        // value to match, since level designers can set a different duration per pickup instance.
        private const float ShieldTestDuration = 5f;
        private const float SpeedBoostTestDuration = 8f;

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                PowerUpEvents.TriggerActivated(PowerUpType.Shield, ShieldTestDuration);
            }

            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                PowerUpEvents.TriggerActivated(PowerUpType.SpeedBoost, SpeedBoostTestDuration);
            }
        }
    }
}