using UnityEngine;
using UnityEngine.InputSystem;

namespace Eggverse
{
    /// <summary>
    /// Thin wrapper over the Input System's keyboard so the rest of the game never
    /// has to null-check devices. The project runs with the new input backend only.
    /// </summary>
    public static class EggInput
    {
        static Keyboard K => Keyboard.current;

        public static Vector2 Move
        {
            get
            {
                var k = K;
                if (k == null) return Vector2.zero;
                float x = 0f, y = 0f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static bool ConfirmPressed
        {
            get
            {
                var k = K;
                return k != null && (k.spaceKey.wasPressedThisFrame ||
                                     k.enterKey.wasPressedThisFrame ||
                                     k.numpadEnterKey.wasPressedThisFrame);
            }
        }

        public static bool InteractPressed
        {
            get
            {
                var k = K;
                return k != null && k.eKey.wasPressedThisFrame;
            }
        }

        public static bool CancelPressed
        {
            get
            {
                var k = K;
                return k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame);
            }
        }

        public static bool LiftoffPressed
        {
            get
            {
                var k = K;
                return k != null && k.qKey.wasPressedThisFrame;
            }
        }

        public static bool PartyPressed
        {
            get
            {
                var k = K;
                return k != null && k.tabKey.wasPressedThisFrame;
            }
        }

        public static bool MutePressed
        {
            get
            {
                var k = K;
                return k != null && k.digit0Key.wasPressedThisFrame;
            }
        }

        /// <summary>The N key: "new run" on the title card, "name it" after a catch.</summary>
        public static bool NKeyPressed
        {
            get
            {
                var k = K;
                return k != null && k.nKey.wasPressedThisFrame;
            }
        }

        public static bool MapPressed
        {
            get
            {
                var k = K;
                return k != null && k.mKey.wasPressedThisFrame;
            }
        }

        public static bool UpPressed
        {
            get
            {
                var k = K;
                return k != null && (k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame);
            }
        }

        public static bool DownPressed
        {
            get
            {
                var k = K;
                return k != null && (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame);
            }
        }

        public static bool LeftPressed
        {
            get
            {
                var k = K;
                return k != null && (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame);
            }
        }

        public static bool RightPressed
        {
            get
            {
                var k = K;
                return k != null && (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame);
            }
        }

        /// <summary>Number keys 1-6, returned as a zero-based index, or -1.</summary>
        public static int DigitPressed
        {
            get
            {
                var k = K;
                if (k == null) return -1;
                if (k.digit1Key.wasPressedThisFrame) return 0;
                if (k.digit2Key.wasPressedThisFrame) return 1;
                if (k.digit3Key.wasPressedThisFrame) return 2;
                if (k.digit4Key.wasPressedThisFrame) return 3;
                if (k.digit5Key.wasPressedThisFrame) return 4;
                if (k.digit6Key.wasPressedThisFrame) return 5;
                return -1;
            }
        }
    }
}
