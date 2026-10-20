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
            if (IsUIFocused())
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame ||
                 keyboard.upArrowKey.wasPressedThisFrame ||
                 keyboard.wKey.wasPressedThisFrame))
            {
                return true;
            }

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
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.W) ||
                Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// True when the pointer is over an interactive UI element, or a UI element (a menu
        /// button navigated to with arrow keys, say) currently holds keyboard focus. Stops a
        /// click or Enter press that resumes/restarts/quits — or picks a difficulty — from also
        /// registering as a jump or run-start, since all of these read the same physical
        /// mouse/keyboard press the UI event system just consumed.
        /// </summary>
        private static bool IsUIFocused()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null &&
                (eventSystem.IsPointerOverGameObject() || eventSystem.currentSelectedGameObject != null);
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

        /// <summary>
        /// True on the frame the Enter key is first pressed. Menus call this directly and
        /// invoke whatever's selected themselves, rather than trusting the EventSystem's own
        /// Submit action end-to-end — one less link in the chain to go wrong.
        /// </summary>
        public static bool SubmitPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
            {
                return true;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
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

        /// <summary>True when the mouse or touch point is over a UI element (so world clicks should be ignored).</summary>
        public static bool PointerOverUI()
        {
            var eventSystem = EventSystem.current;
            return eventSystem != null && eventSystem.IsPointerOverGameObject();
        }

        /// <summary>Screen position of the mouse, or of the active touch. False if there is no pointer.</summary>
        public static bool TryGetPointer(out Vector2 screenPosition)
        {
#if ENABLE_INPUT_SYSTEM
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screenPosition = touch.primaryTouch.position.ReadValue();
                return true;
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                screenPosition = mouse.position.ReadValue();
                return true;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            screenPosition = Input.mousePosition;
            return true;
#endif
            screenPosition = Vector2.zero;
            return false;
        }

        /// <summary>True on the frame the left mouse button or a touch goes down (world placement clicks).</summary>
        public static bool PointerPressed()
        {
#if ENABLE_INPUT_SYSTEM
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
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0))
            {
                return true;
            }
#endif
            return false;
        }

        /// <summary>The number key (1–9) pressed this frame, or 0 if none.</summary>
        public static int NumberKeyPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return 0;
            }

            for (int n = 1; n <= 9; n++)
            {
                if (keyboard[Key.Digit1 + (n - 1)].wasPressedThisFrame ||
                    keyboard[Key.Numpad1 + (n - 1)].wasPressedThisFrame)
                {
                    return n;
                }
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            for (int n = 1; n <= 9; n++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + (n - 1)) || Input.GetKeyDown(KeyCode.Keypad1 + (n - 1)))
                {
                    return n;
                }
            }
#endif
            return 0;
        }

        /// <summary>Mouse-wheel movement this frame: positive when scrolled up, negative down.</summary>
        public static float ScrollDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = Mouse.current;
            return mouse != null ? mouse.scroll.ReadValue().y : 0f;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mouseScrollDelta.y;
#else
            return 0f;
#endif
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
