using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    [DisallowMultipleComponent]
    public class StationDoorPresenter : MonoBehaviour
    {
        [Header("Folhas / Portas Móveis")]
        [SerializeField] private TransformDoorView[] _doors;

        private bool _isHovered;
        private bool _isSessionOpen;
        private bool _isTransferActive;
        private bool _hoverSuppressed;

        public bool IsSessionOpen => _isSessionOpen;
        public bool IsTransferActive => _isTransferActive;
        public bool IsHovered => _isHovered;
        public bool IsHoverSuppressed => _hoverSuppressed;

        private void Awake()
        {
            if (_doors == null || _doors.Length == 0)
            {
                _doors = GetComponentsInChildren<TransformDoorView>(true);
            }
        }

        public void SetDoors(TransformDoorView[] doors)
        {
            _doors = doors;
        }

        public void OnHoverEnter()
        {
            _isHovered = true;
            EvaluateAndApplyTarget();
        }

        public void OnHoverExit()
        {
            _isHovered = false;
            // Retirar a mira limpa a supressão de hover
            _hoverSuppressed = false;
            EvaluateAndApplyTarget();
        }

        public void SetSessionOpen(bool open)
        {
            _isSessionOpen = open;
            if (!open)
            {
                // Encerramento de sessão suprime hover imediato até retirar a mira
                _hoverSuppressed = true;
            }
            EvaluateAndApplyTarget();
        }

        public void SetTransferActive(bool active)
        {
            _isTransferActive = active;
            if (!active)
            {
                // Encerramento de transferência de forno suprime hover imediato
                _hoverSuppressed = true;
            }
            EvaluateAndApplyTarget();
        }

        public void CloseAndSuppressHover()
        {
            _isSessionOpen = false;
            _isTransferActive = false;
            _hoverSuppressed = true;
            EvaluateAndApplyTarget();
        }

        public void SnapClosed()
        {
            _isSessionOpen = false;
            _isTransferActive = false;
            _isHovered = false;
            _hoverSuppressed = false;

            if (_doors == null) return;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null)
                {
                    _doors[i].SetTarget(DoorVisualTarget.Closed, immediate: true);
                }
            }
        }

        private void EvaluateAndApplyTarget()
        {
            DoorVisualTarget resolvedTarget;

            if (_isSessionOpen || _isTransferActive)
            {
                resolvedTarget = DoorVisualTarget.Open;
            }
            else if (_isHovered && !_hoverSuppressed)
            {
                resolvedTarget = DoorVisualTarget.Hover;
            }
            else
            {
                resolvedTarget = DoorVisualTarget.Closed;
            }

            if (_doors == null) return;
            for (int i = 0; i < _doors.Length; i++)
            {
                if (_doors[i] != null)
                {
                    _doors[i].SetTarget(resolvedTarget, immediate: false);
                }
            }
        }

        private void OnDisable()
        {
            _isHovered = false;
            _hoverSuppressed = false;
        }
    }
}
