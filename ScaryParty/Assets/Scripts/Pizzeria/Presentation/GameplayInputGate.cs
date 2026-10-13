using UnityEngine;
using StarterAssets;

namespace ScaryParty.Pizzeria.Presentation
{
    public static class GameplayInputGate
    {
        private static int _lockCount = 0;

        public static bool IsLocked => _lockCount > 0;

        public static void PushLock()
        {
            _lockCount++;
            ApplyState();
        }

        public static void PopLock()
        {
            _lockCount = Mathf.Max(0, _lockCount - 1);
            ApplyState();
        }

        public static void PushModal() => PushLock();
        public static void PopModal() => PopLock();

        public static void SetLock(bool locked)
        {
            if (locked) PushLock();
            else PopLock();
        }

        public static void Reset()
        {
            _lockCount = 0;
            ApplyState();
        }

        private static void ApplyState()
        {
            bool locked = IsLocked;

            var starterInputs = Object.FindFirstObjectByType<StarterAssetsInputs>();
            if (starterInputs != null)
            {
                starterInputs.cursorInputForLook = !locked;
                starterInputs.cursorLocked = !locked;
                if (locked)
                {
                    starterInputs.move = Vector2.zero;
                    starterInputs.look = Vector2.zero;
                }
            }

            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
    }
}
