using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Run.Common
{
    /// <summary>
    /// A tiny, dependency-free input facade shared by the player and the game loop.
    /// </summary>
    /// <remarks>
    /// The project may be configured for the new Input System, the legacy Input
    /// Manager, or both. This helper compiles against whichever backend is active so
    /// the gameplay code never has to care. "Confirm" is the single action that
    /// starts the run, jumps, and restarts — keyboard (Space / W / Up), mouse, and
    /// touch all map to it.
    /// </remarks>
    public static class RunInput
    {
        /// <summary>True on the frame the confirm/jump action is first pressed.</summary>
        public static bool ConfirmPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.upArrowKey.wasPressedThisFrame ||
                 keyboard.wKey.wasPressedThisFrame))
            {
                return true;
            }

            if (!IsPointerOverUI())
            {
                var mouse = Mouse.current;
                if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    return true;
                }

                var touch = Touchscreen.current;
                if (touch != null && touch.primaryTouch.press.wasPressedThisFrame)
                {
                    return true;
                }
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W))
            {
                return true;
            }

            if (!IsPointerOverUI() && Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// True when the pointer is over an interactive UI element (a Button, for instance).
        /// Stops a click that resumes/restarts/quits — or picks a difficulty — from also
        /// registering as a jump or run-start, since both read the same physical mouse press.
        /// </summary>
        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>True while the confirm/jump action is held (used for variable jump height).</summary>
        public static bool JumpHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.isPressed ||
                 keyboard.upArrowKey.isPressed ||
                 keyboard.wKey.isPressed))
            {
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.isPressed)
            {
                return true;
            }

            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKey(KeyCode.Space) ||
                Input.GetKey(KeyCode.UpArrow) ||
                Input.GetKey(KeyCode.W) ||
                Input.GetMouseButton(0))
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>True on the frame the pause toggle (Escape, or the Android back button) is pressed.</summary>
        public static bool PausePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>Horizontal steering in the range [-1, 1] (only used when auto-run is disabled).</summary>
        public static float Horizontal()
        {
            float axis = 0f;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) axis -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) axis += 1f;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            axis = Input.GetAxisRaw("Horizontal");
#endif
            return axis;
        }
    }
}
