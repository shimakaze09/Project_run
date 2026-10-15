using UnityEngine;
using UnityEngine.InputSystem;
using Run.Core;

namespace Run.Common
{
    public class PowerUpTester : MonoBehaviour
    {
        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                PowerUpEvents.TriggerActivated(PowerUpType.Shield, 5.0f);
            }

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