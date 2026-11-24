using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

namespace ScaryParty.Pizzeria.Presentation
{
    public static class GameplayInputGate
    {
        private static int _lockCount = 0;
        private static bool _wasEffectivelyLocked = false;

        public static float LastUnlockUnscaledTime { get; private set; } = -10f;
        public static bool IsLocked => _lockCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _lockCount = 0;
            _wasEffectivelyLocked = false;
            LastUnlockUnscaledTime = -10f;
        }

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

        /// <summary>
        /// Garante que o cursor permaneça destravado e visível enquanto um menu/modal estiver aberto,
        /// mesmo se um player spawnar depois ou o foco da janela retornar.
        /// </summary>
        public static void MaintainMenuCursorUnlocked()
        {
            if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (IsLocked) ApplyState();
            }
        }

        public static bool IsMenuOrLobbyScene()
        {
            try
            {
                string sceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(sceneName)) return false;
                return string.Equals(sceneName, "LobbyScene", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(sceneName, "ReadyScene", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void ApplyState()
        {
            bool locked = IsLocked;

            bool isMenuOpen = IsMenuOrLobbyScene() ||
                              (ScaryParty.UI.Piaria.Core.UIScreenRouter.Instance != null &&
                               ScaryParty.UI.Piaria.Core.UIScreenRouter.Instance.ActiveScreen is UnityEngine.Object screenObj &&
                               screenObj != null &&
                               ScaryParty.UI.Piaria.Core.UIScreenRouter.Instance.ActiveScreen.State != ScaryParty.UI.Piaria.Core.ScreenState.Hidden) ||
                              (ScaryParty.UI.Piaria.Core.UIModalStack.Instance != null && ScaryParty.UI.Piaria.Core.UIModalStack.Instance.HasActiveModal) ||
                              (ScaryParty.UI.Piaria.Presenters.MainMenuPresenter.Instance != null && ScaryParty.UI.Piaria.Presenters.MainMenuPresenter.Instance.State != ScaryParty.UI.Piaria.Core.ScreenState.Hidden) ||
                              (ScaryParty.UI.Piaria.Presenters.LobbyPresenter.Instance != null && ScaryParty.UI.Piaria.Presenters.LobbyPresenter.Instance.State != ScaryParty.UI.Piaria.Core.ScreenState.Hidden) ||
                              (ScaryParty.UI.Piaria.Presenters.ReadyRoomPresenter.Instance != null && ScaryParty.UI.Piaria.Presenters.ReadyRoomPresenter.Instance.State != ScaryParty.UI.Piaria.Core.ScreenState.Hidden);

            bool effectiveLocked = locked || isMenuOpen;

            var allInputs = UnityEngine.Object.FindObjectsByType<StarterAssetsInputs>(FindObjectsSortMode.None);
            if (allInputs != null)
            {
                foreach (var starterInputs in allInputs)
                {
                    if (starterInputs == null) continue;
                    var player = starterInputs.GetComponent<Unity.Netcode.NetworkObject>();
                    if (player != null && player.IsSpawned && !player.IsOwner) continue;
                    starterInputs.cursorInputForLook = !effectiveLocked;
                    starterInputs.cursorLocked = !effectiveLocked;
                    if (effectiveLocked)
                    {
                        starterInputs.move = Vector2.zero;
                        starterInputs.look = Vector2.zero;
                        starterInputs.jump = false;
                        starterInputs.sprint = false;
                        starterInputs.scrollWeapon = 0f;
                        starterInputs.aim = false;
                        starterInputs.fire = false;
                    }
                }
            }

            if (_wasEffectivelyLocked && !effectiveLocked)
            {
                LastUnlockUnscaledTime = SafeUnscaledTime();
            }
            _wasEffectivelyLocked = effectiveLocked;

            if (effectiveLocked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static float SafeUnscaledTime()
        {
            try { return Time.unscaledTime; }
            catch { return 0f; }
        }
    }
}
