using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Core
{
    public interface IModalView
    {
        bool CanCloseWithEscape { get; }
        VisualElement RootElement { get; }
        void Close();
    }

    /// <summary>
    /// Gerenciador de pilha de modais para UI Toolkit.
    /// Controla tokens de foco, bloqueio de cursor e câmera via GameplayInputGate,
    /// e garante que ESC feche apenas o modal superior permitido.
    /// </summary>
    public class UIModalStack : MonoBehaviour
    {
        public static UIModalStack Instance { get; private set; }

        private readonly Stack<IModalView> _modalStack = new Stack<IModalView>();
        private int _inputTokensAcquired = 0;

        public bool HasActiveModal => _modalStack.Count > 0;
        public int ActiveModalCount => _modalStack.Count;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void PushModal(IModalView modal)
        {
            if (modal == null) return;
            if (_modalStack.Contains(modal)) return;

            _modalStack.Push(modal);
            AcquireInputLock();
        }

        public void PopModal(IModalView modal)
        {
            if (modal == null || _modalStack.Count == 0) return;

            // Se for o do topo, desempilha diretamente
            if (_modalStack.Peek() == modal)
            {
                _modalStack.Pop();
                ReleaseInputLock();
                return;
            }

            // Se não for o topo, reconstrói a pilha preservando a ordem
            var temp = new Stack<IModalView>();
            bool removed = false;
            while (_modalStack.Count > 0)
            {
                var cur = _modalStack.Pop();
                if (cur == modal)
                {
                    removed = true;
                    break;
                }
                temp.Push(cur);
            }

            while (temp.Count > 0)
            {
                _modalStack.Push(temp.Pop());
            }

            if (removed)
            {
                ReleaseInputLock();
            }
        }

        private void AcquireInputLock()
        {
            _inputTokensAcquired++;
            ScaryParty.Pizzeria.Presentation.GameplayInputGate.PushModal();
        }

        private void ReleaseInputLock()
        {
            if (_inputTokensAcquired > 0)
            {
                _inputTokensAcquired--;
                ScaryParty.Pizzeria.Presentation.GameplayInputGate.PopModal();
            }
        }

        public void ClearAll()
        {
            while (_modalStack.Count > 0)
            {
                var modal = _modalStack.Pop();
                try { modal.Close(); } catch { }
            }

            while (_inputTokensAcquired > 0)
            {
                _inputTokensAcquired--;
                ScaryParty.Pizzeria.Presentation.GameplayInputGate.PopModal();
            }
        }

        private void Update()
        {
            if (_modalStack.Count == 0) return;

            bool escPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
                escPressed = Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            escPressed = Input.GetKeyDown(KeyCode.Escape);
#endif

            if (escPressed)
            {
                var top = _modalStack.Peek();
                if (top != null && top.CanCloseWithEscape)
                {
                    top.Close();
                }
            }
        }

        private void OnDisable()
        {
            ClearAll();
        }
    }
}
