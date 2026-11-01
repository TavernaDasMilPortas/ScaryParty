using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.UI.Piaria.Core
{
    public enum ScreenState
    {
        Hidden,
        Opening,
        Active,
        Closing
    }

    public enum AsyncOperationState
    {
        Idle,
        Pending,
        Success,
        Failure
    }

    public interface IScreenView
    {
        string ScreenId { get; }
        VisualElement RootElement { get; }
        ScreenState State { get; }
        void Open(Action onComplete = null);
        void Close(Action onComplete = null);
    }

    /// <summary>
    /// Roteador e gerenciador de ciclo de vida de telas da interface P.I.A.R.I.A.
    /// Garante transições coordenadas, bloqueio contra cliques atravessados durante animações,
    /// e navegação reversível entre Menu, Lobby e Gameplay.
    /// </summary>
    public class UIScreenRouter : MonoBehaviour
    {
        public static UIScreenRouter Instance { get; private set; }

        private readonly Dictionary<string, IScreenView> _screens = new Dictionary<string, IScreenView>();
        private IScreenView _activeScreen;
        private readonly Stack<string> _history = new Stack<string>();

        public IScreenView ActiveScreen => _activeScreen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_activeScreen != null && _activeScreen.State != ScreenState.Hidden)
            {
                if (UnityEngine.Cursor.lockState != CursorLockMode.None) UnityEngine.Cursor.lockState = CursorLockMode.None;
                if (!UnityEngine.Cursor.visible) UnityEngine.Cursor.visible = true;
            }
        }

        public void RegisterScreen(IScreenView screen)
        {
            if (screen == null || string.IsNullOrEmpty(screen.ScreenId)) return;
            _screens[screen.ScreenId] = screen;
        }

        public void UnregisterScreen(IScreenView screen)
        {
            if (screen == null || string.IsNullOrEmpty(screen.ScreenId)) return;
            if (_screens.ContainsKey(screen.ScreenId))
            {
                _screens.Remove(screen.ScreenId);
            }
        }

        public void NavigateTo(string screenId, bool recordHistory = true, Action onComplete = null)
        {
            if (!_screens.TryGetValue(screenId, out var targetScreen))
            {
                Debug.LogWarning($"[UIScreenRouter] Tela não encontrada: {screenId}");
                return;
            }

            if (_activeScreen == targetScreen) return;

            var previousScreen = _activeScreen;
            if (recordHistory && previousScreen != null)
            {
                _history.Push(previousScreen.ScreenId);
            }

            if (previousScreen != null)
            {
                previousScreen.Close(() =>
                {
                    _activeScreen = targetScreen;
                    targetScreen.Open(onComplete);
                });
            }
            else
            {
                _activeScreen = targetScreen;
                targetScreen.Open(onComplete);
            }
        }

        public bool NavigateBack(Action onComplete = null)
        {
            if (_history.Count > 0)
            {
                string prevId = _history.Pop();
                NavigateTo(prevId, recordHistory: false, onComplete: onComplete);
                return true;
            }
            return false;
        }

        public void CloseCurrent(Action onComplete = null)
        {
            if (_activeScreen != null)
            {
                _activeScreen.Close(() =>
                {
                    _activeScreen = null;
                    onComplete?.Invoke();
                });
            }
            else
            {
                onComplete?.Invoke();
            }
        }
    }
}
