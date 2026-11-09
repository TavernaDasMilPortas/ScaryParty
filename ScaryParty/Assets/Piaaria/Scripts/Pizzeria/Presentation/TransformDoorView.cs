using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum DoorMotionMode
    {
        Rotation,
        Translation
    }

    public enum DoorVisualTarget
    {
        Closed,
        Hover,
        Open
    }

    [DisallowMultipleComponent]
    public class TransformDoorView : MonoBehaviour
    {
        [Header("Modo de Movimento")]
        public DoorMotionMode motionMode = DoorMotionMode.Rotation;
        public Vector3 localAxis = Vector3.up;
        [Tooltip("Multiplicador de direção: +1 para padrão, -1 para inverter (ex: portas duplas)")]
        public float sign = 1f;

        [Header("Amplitudes (Graus para Rotação, Metros para Translação)")]
        [Tooltip("Amplitude de entreabertura para hover")]
        public float hoverAmount = 20f;
        [Tooltip("Amplitude total para abertura (ex: 90 graus para geladeira, 0.55m para forno)")]
        public float openAmount = 90f;

        [Header("Durações (Segundos)")]
        [Range(0.05f, 2f)] public float durationOpen = 0.25f;
        [Range(0.05f, 2f)] public float durationClose = 0.25f;
        [Range(0.05f, 2f)] public float durationHover = 0.15f;

        [Header("Curva de Interpolação")]
        public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Pose Fechada de Autoria")]
        [SerializeField] private bool _hasCustomClosedPose;
        [SerializeField] private Vector3 _closedPosition;
        [SerializeField] private Quaternion _closedRotation = Quaternion.identity;

        private DoorVisualTarget _currentTarget = DoorVisualTarget.Closed;
        private float _currentFraction = 0f;
        private float _startFraction = 0f;
        private float _targetFraction = 0f;
        private float _transitionTimer = 0f;
        private float _transitionDuration = 0f;
        private bool _isTransitioning = false;

        public DoorVisualTarget CurrentTarget => _currentTarget;
        public bool IsTransitioning => _isTransitioning;
        public float CurrentFraction => _currentFraction;

        private void Awake()
        {
            if (!_hasCustomClosedPose)
            {
                CaptureClosedPose();
            }
        }

        private void OnEnable()
        {
            // Reconcilia com a pose atual sem salto
            ApplyFraction(_currentFraction);
        }

        private void OnDisable()
        {
            _isTransitioning = false;
        }

        [ContextMenu("Capturar Pose Fechada Atual")]
        public void CaptureClosedPose()
        {
            _closedPosition = transform.localPosition;
            _closedRotation = transform.localRotation;
            _hasCustomClosedPose = true;
        }

        [ContextMenu("Restaurar Pose Fechada")]
        public void RestoreClosedPose()
        {
            if (_hasCustomClosedPose)
            {
                transform.localPosition = _closedPosition;
                transform.localRotation = _closedRotation;
            }
            _currentFraction = 0f;
            _startFraction = 0f;
            _targetFraction = 0f;
            _currentTarget = DoorVisualTarget.Closed;
            _isTransitioning = false;
        }

        public void SetTarget(DoorVisualTarget target, bool immediate = false)
        {
            if (!_hasCustomClosedPose)
            {
                CaptureClosedPose();
            }

            _currentTarget = target;
            float newTargetFraction = ResolveFractionForTarget(target);

            if (immediate)
            {
                _currentFraction = newTargetFraction;
                _startFraction = newTargetFraction;
                _targetFraction = newTargetFraction;
                _isTransitioning = false;
                ApplyFraction(_currentFraction);
                return;
            }

            if (Mathf.Approximately(_currentFraction, newTargetFraction))
            {
                _currentFraction = newTargetFraction;
                _targetFraction = newTargetFraction;
                _isTransitioning = false;
                ApplyFraction(_currentFraction);
                return;
            }

            _startFraction = _currentFraction;
            _targetFraction = newTargetFraction;
            _transitionTimer = 0f;

            float baseDuration = target switch
            {
                DoorVisualTarget.Open => durationOpen,
                DoorVisualTarget.Hover => durationHover,
                _ => durationClose
            };

            float distanceFraction = Mathf.Abs(_targetFraction - _startFraction);
            _transitionDuration = Mathf.Max(0.02f, baseDuration * distanceFraction);
            _isTransitioning = true;
        }

        private float ResolveFractionForTarget(DoorVisualTarget target)
        {
            return target switch
            {
                DoorVisualTarget.Closed => 0f,
                DoorVisualTarget.Hover => openAmount > 0.001f ? (hoverAmount / openAmount) : 0.1f,
                DoorVisualTarget.Open => 1f,
                _ => 0f
            };
        }

        private void Update()
        {
            if (!_isTransitioning) return;

            _transitionTimer += Time.deltaTime;
            float progress = Mathf.Clamp01(_transitionTimer / _transitionDuration);
            float curved = animationCurve != null ? animationCurve.Evaluate(progress) : Mathf.SmoothStep(0f, 1f, progress);

            _currentFraction = Mathf.Lerp(_startFraction, _targetFraction, curved);
            ApplyFraction(_currentFraction);

            if (progress >= 1f)
            {
                _currentFraction = _targetFraction;
                _isTransitioning = false;
                ApplyFraction(_currentFraction);
            }
        }

        private void ApplyFraction(float fraction)
        {
            Vector3 axis = localAxis.sqrMagnitude > 0.0001f ? localAxis.normalized : Vector3.up;

            if (motionMode == DoorMotionMode.Rotation)
            {
                float angle = fraction * openAmount * sign;
                transform.localRotation = _closedRotation * Quaternion.AngleAxis(angle, axis);
            }
            else
            {
                float displacement = fraction * openAmount * sign;
                transform.localPosition = _closedPosition + axis * displacement;
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Preview: Fechado (0°)")]
        public void PreviewClosed() => SetTarget(DoorVisualTarget.Closed, immediate: true);

        [ContextMenu("Preview: Hover")]
        public void PreviewHover() => SetTarget(DoorVisualTarget.Hover, immediate: true);

        [ContextMenu("Preview: Aberto")]
        public void PreviewOpen() => SetTarget(DoorVisualTarget.Open, immediate: true);
#endif
    }
}
