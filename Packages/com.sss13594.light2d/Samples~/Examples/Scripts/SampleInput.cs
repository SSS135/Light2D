using UnityEngine;
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
// Keep the public TouchPhase surface on the legacy enum so the sample call sites read identically
// under both backends; the Input System phase type is referenced fully qualified where needed.
using TouchPhase = UnityEngine.TouchPhase;
#endif

namespace Light2D.Examples
{
    // Backend-agnostic input reader for the samples. When the consuming project has the Input
    // System package (LIGHT2D_SAMPLES_INPUT_SYSTEM, supplied by the asmdef versionDefines) and the
    // active input handling enables it (ENABLE_INPUT_SYSTEM), every query is served by the Input
    // System through null-guarded device accessors, so a headless or batch player loop with no
    // devices returns neutral values instead of throwing. Otherwise the queries fall back to the
    // legacy UnityEngine.Input class, keeping the samples working under Input Manager (Old) or Both
    // without an Input System package dependency. Touch phases are reported as UnityEngine.TouchPhase
    // in both backends so the sample call sites read identically.
    internal static class SampleInput
    {
        public static bool TouchSupported
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            get => Touchscreen.current != null;
#else
            get => Input.touchSupported;
#endif
        }

        public static int TouchCount
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            get => ActiveTouchCount();
#else
            get => Input.touchCount;
#endif
        }

        public static Vector3 MousePosition
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            get
            {
                Mouse mouse = Mouse.current;
                return mouse == null ? Vector3.zero : (Vector3)mouse.position.ReadValue();
            }
#else
            get => Input.mousePosition;
#endif
        }

        public static bool GetKey(KeyCode key)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            KeyControl control = KeyControlFor(key);
            return control != null && control.isPressed;
#else
            return Input.GetKey(key);
#endif
        }

        public static bool GetKeyDown(KeyCode key)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            KeyControl control = KeyControlFor(key);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetKeyDown(key);
#endif
        }

        public static bool GetMouseButton(int button)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = MouseButton(button);
            return control != null && control.isPressed;
#else
            return Input.GetMouseButton(button);
#endif
        }

        public static bool GetMouseButtonDown(int button)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = MouseButton(button);
            return control != null && control.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(button);
#endif
        }

        public static bool GetMouseButtonUp(int button)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            ButtonControl control = MouseButton(button);
            return control != null && control.wasReleasedThisFrame;
#else
            return Input.GetMouseButtonUp(button);
#endif
        }

        public static Vector2 GetTouchPosition(int index)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            TouchControl touch = ActiveTouch(index);
            return touch == null ? Vector2.zero : touch.position.ReadValue();
#else
            return Input.GetTouch(index).position;
#endif
        }

        public static TouchPhase GetTouchPhase(int index)
        {
#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
            TouchControl touch = ActiveTouch(index);
            return touch == null ? TouchPhase.Canceled : ToLegacyPhase(touch.phase.ReadValue());
#else
            return Input.GetTouch(index).phase;
#endif
        }

#if LIGHT2D_SAMPLES_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static int ActiveTouchCount()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return 0;

            var count = 0;
            var touches = screen.touches;
            for (var i = 0; i < touches.Count; i++)
            {
                if (touches[i].phase.ReadValue() != UnityEngine.InputSystem.TouchPhase.None)
                    count++;
            }

            return count;
        }

        private static TouchControl ActiveTouch(int index)
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null)
                return null;

            var count = 0;
            var touches = screen.touches;
            for (var i = 0; i < touches.Count; i++)
            {
                if (touches[i].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.None)
                    continue;
                if (count == index)
                    return touches[i];
                count++;
            }

            return null;
        }

        private static TouchPhase ToLegacyPhase(UnityEngine.InputSystem.TouchPhase phase)
        {
            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began: return TouchPhase.Began;
                case UnityEngine.InputSystem.TouchPhase.Moved: return TouchPhase.Moved;
                case UnityEngine.InputSystem.TouchPhase.Stationary: return TouchPhase.Stationary;
                case UnityEngine.InputSystem.TouchPhase.Ended: return TouchPhase.Ended;
                default: return TouchPhase.Canceled;
            }
        }

        private static ButtonControl MouseButton(int button)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
                return null;

            switch (button)
            {
                case 0: return mouse.leftButton;
                case 1: return mouse.rightButton;
                case 2: return mouse.middleButton;
                default: return null;
            }
        }

        private static KeyControl KeyControlFor(KeyCode key)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return null;

            switch (key)
            {
                case KeyCode.UpArrow: return keyboard.upArrowKey;
                case KeyCode.DownArrow: return keyboard.downArrowKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                case KeyCode.W: return keyboard.wKey;
                case KeyCode.A: return keyboard.aKey;
                case KeyCode.S: return keyboard.sKey;
                case KeyCode.D: return keyboard.dKey;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.F1: return keyboard.f1Key;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.C: return keyboard.cKey;
                default: return null;
            }
        }
#endif
    }
}
