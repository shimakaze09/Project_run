using UnityEngine;
using UnityEngine.InputSystem;
using Run.Core;

namespace Run.Common
{
    public class PowerUpTester : MonoBehaviour
    {
        private void Update()
        {
            // Ensure a keyboard is connected
            if (Keyboard.current == null) return;

            // Press '1' for a 5-second Shield
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                PowerUpEvents.TriggerActivated(PowerUpType.Shield, 5.0f);
            }

            // Press '2' for an 8-second Speed Boost
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                PowerUpEvents.TriggerActivated(PowerUpType.SpeedBoost, 8.0f);
            }
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                {
                    Debug.Log("Tester: Key 1 pressed, firing event!");
                    PowerUpEvents.TriggerActivated(PowerUpType.Shield, 5.0f);
                }
        }
    }
}