using UnityEngine;
using DG.Tweening;
using Sirenix.OdinInspector;
using System.Collections.Generic;

namespace ProceduralGraphicSystem
{
    /// <summary>
    /// Componente helper para animar automaticamente um ProceduralGraphic
    /// alternando entre seus keyframes via DOTween.
    /// </summary>
    [RequireComponent(typeof(ProceduralGraphic))]
    public class ProceduralGraphicAnimator : MonoBehaviour
    {
        [Title("Animator Settings")]
        [Tooltip("Toca a animação automaticamente ao ativar o objeto.")]
        public bool playOnAwake = true;
        
        public enum AnimationMode 
        { 
            [InspectorName("Timeline (Loop pelo Time)")] LoopAllKeyframes,
            [InspectorName("Ping-Pong (Entre 2 Keyframes)")] PingPong 
        }
        
        public enum TransitionStyle 
        { 
            [InspectorName("Suave (Fluido)")] Fluid,
            [InspectorName("Brusco (Stepped)")] Stepped 
        }

        [EnumToggleButtons]
        public AnimationMode mode = AnimationMode.LoopAllKeyframes;

        [EnumToggleButtons]
        [Tooltip("Suave interpola os pontos. Brusco pula de um frame direto pro outro.")]
        public TransitionStyle style = TransitionStyle.Fluid;

        [ShowIf("mode", AnimationMode.PingPong)]
        [ValueDropdown("GetKeyframeNames")]
        [LabelText("Keyframe Inicial (A)")]
        public string keyframeA;

        [ShowIf("mode", AnimationMode.PingPong)]
        [ValueDropdown("GetKeyframeNames")]
        [LabelText("Keyframe Final (B)")]
        public string keyframeB;

        [ShowIf("mode", AnimationMode.PingPong)]
        [LabelText("Duração da ida (seg)")]
        public float duration = 0.5f;

        [ShowIf("mode", AnimationMode.LoopAllKeyframes)]
        [Tooltip("Duração para ir do tempo 0 até o último tempo configurado nos keyframes.")]
        [LabelText("Duração total do ciclo (seg)")]
        public float totalLoopDuration = 1f;

        private ProceduralGraphic _graphic;
        private Tweener _currentTween;
        private Sequence _pingPongSequence;
        private bool _stopRequested = false;

        private void Awake()
        {
            _graphic = GetComponent<ProceduralGraphic>();
        }

        private void OnEnable()
        {
            if (playOnAwake)
            {
                // Aguarda o fim do frame para garantir que o ProceduralGraphic chamou seu próprio Awake
                DOVirtual.DelayedCall(0f, () => {
                    if (this != null && gameObject.activeInHierarchy) Play();
                });
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        private IEnumerable<string> GetKeyframeNames()
        {
            var graphic = GetComponent<ProceduralGraphic>();
            if (graphic != null && graphic.Preset != null && graphic.Preset.Keyframes != null)
            {
                foreach(var k in graphic.Preset.Keyframes) yield return k.name;
            }
        }

        [Button("▶ Play Anim", ButtonSizes.Medium), HideInEditorMode]
        public void Play()
        {
            Stop();
            _stopRequested = false;
            if (_graphic.Preset == null || _graphic.Preset.Keyframes.Count == 0) return;

            if (mode == AnimationMode.PingPong)
            {
                if (string.IsNullOrEmpty(keyframeA) || string.IsNullOrEmpty(keyframeB))
                {
                    Debug.LogWarning("[ProceduralGraphicAnimator] Configure os Keyframes A e B para o PingPong.");
                    return;
                }

                _pingPongSequence = DOTween.Sequence();
                
                bool isStepped = (style == TransitionStyle.Stepped);
                float animDuration = isStepped ? 0f : duration;
                
                _pingPongSequence.Append(_graphic.DOTransition(keyframeA, 0.05f));
                
                _pingPongSequence.AppendCallback(() => {
                    _currentTween = _graphic.DOTransition(keyframeB, animDuration).SetEase(Ease.InOutSine);
                });
                _pingPongSequence.AppendInterval(duration);
                
                _pingPongSequence.AppendCallback(() => {
                    _currentTween = _graphic.DOTransition(keyframeA, animDuration).SetEase(Ease.InOutSine);
                });
                _pingPongSequence.AppendInterval(duration);
                
                _pingPongSequence.SetLoops(-1);
                _pingPongSequence.OnStepComplete(CheckStopRequestSequence);
            }
            else if (mode == AnimationMode.LoopAllKeyframes)
            {
                float maxTime = _graphic.Preset.Keyframes[_graphic.Preset.Keyframes.Count - 1].time;
                if (maxTime <= 0) maxTime = 1f;

                bool isStepped = (style == TransitionStyle.Stepped);

                _currentTween = _graphic.DOBlendTimeline(0f, maxTime, totalLoopDuration, isStepped)
                                        .SetEase(isStepped ? Ease.Linear : Ease.InOutSine)
                                        .SetLoops(-1, LoopType.Yoyo)
                                        .OnStepComplete(CheckStopRequestTween);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  API VIA CÓDIGO
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Toca a animação inteira da timeline apenas 1 vez (sem loop)
        /// </summary>
        public void PlayOnce()
        {
            Stop();
            _stopRequested = false;
            if (_graphic.Preset == null || _graphic.Preset.Keyframes.Count == 0) return;
            
            float maxTime = _graphic.Preset.Keyframes[_graphic.Preset.Keyframes.Count - 1].time;
            bool isStepped = (style == TransitionStyle.Stepped);

            _currentTween = _graphic.DOBlendTimeline(0f, maxTime, totalLoopDuration, isStepped)
                                    .SetEase(isStepped ? Ease.Linear : Ease.InOutSine);
        }

        /// <summary>
        /// Faz o loop entre dois frames específicos na timeline
        /// </summary>
        public void PlayTimelineRange(string startKeyframe, string endKeyframe, float customDuration = -1f)
        {
            Stop();
            _stopRequested = false;
            if (_graphic.Preset == null) return;
            
            var kfStart = _graphic.Preset.GetKeyframeByName(startKeyframe);
            var kfEnd = _graphic.Preset.GetKeyframeByName(endKeyframe);
            
            float dur = customDuration > 0f ? customDuration : totalLoopDuration;
            bool isStepped = (style == TransitionStyle.Stepped);

            _currentTween = _graphic.DOBlendTimeline(kfStart.time, kfEnd.time, dur, isStepped)
                                    .SetEase(isStepped ? Ease.Linear : Ease.InOutSine)
                                    .SetLoops(-1, LoopType.Yoyo)
                                    .OnStepComplete(CheckStopRequestTween);
        }

        /// <summary>
        /// Avisa a animação atual para parar de "loppar" assim que ela terminar o ciclo atual
        /// (garante que ela vai parar numa pose natural sem cortes secos)
        /// </summary>
        public void RequestStopAtEndOfLoop()
        {
            _stopRequested = true;
        }

        private void CheckStopRequestTween()
        {
            if (_stopRequested && _currentTween != null)
            {
                // Como é Yoyo, um ciclo completo (ida e volta) conta como 2 completed loops.
                if (_currentTween.CompletedLoops() % 2 == 0) 
                {
                    _currentTween.Kill();
                    _currentTween = null;
                }
            }
        }

        private void CheckStopRequestSequence()
        {
            if (_stopRequested && _pingPongSequence != null)
            {
                // Sequence inteira contém ida e volta, então cada loop é 1 ciclo
                _pingPongSequence.Kill();
                _pingPongSequence = null;
            }
        }

        [Button("⏹ Stop", ButtonSizes.Medium), HideInEditorMode]
        public void Stop()
        {
            if (_currentTween != null)
            {
                _currentTween.Kill();
                _currentTween = null;
            }
            if (_pingPongSequence != null)
            {
                _pingPongSequence.Kill();
                _pingPongSequence = null;
            }
        }
        
        [Button("⏹ Stop & Reset", ButtonSizes.Medium), HideInEditorMode]
        public void StopAndReset()
        {
            Stop();
            
            // Tenta voltar ao primeiro keyframe como pose base
            if (_graphic.Preset != null && _graphic.Preset.Keyframes.Count > 0)
            {
                _graphic.DOTransition(_graphic.Preset.Keyframes[0].name, 0.3f);
            }
        }
    }
}
