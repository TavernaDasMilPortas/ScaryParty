using System;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;
#if MOREMOUNTAINS_FEEL
using MoreMountains.Feedbacks;
#endif

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Serviço de feedbacks visuais e sonoros para componentes de UI Toolkit.
    /// Aciona efeitos de hover, clique, confirmação e erro via Feel ou DOTween.
    /// Seguro contra concorrência e perfeitamente compatível com elementos estáveis e dinâmicos.
    /// </summary>
    public class UIFeedbackService : MonoBehaviour
    {
        public static UIFeedbackService Instance { get; private set; }

        [Header("Áudio UI")]
        public AudioSource audioSource;
        public AudioClip hoverClip;
        public AudioClip clickClip;
        public AudioClip successClip;
        public AudioClip errorClip;
        public AudioClip readyClip;

#if MOREMOUNTAINS_FEEL
        [Header("Feel Players (Opcional)")]
        public MMF_Player hoverPlayer;
        public MMF_Player clickPlayer;
        public MMF_Player successPlayer;
        public MMF_Player errorPlayer;
#endif

        private readonly System.Collections.Generic.HashSet<VisualElement> _boundButtons = new System.Collections.Generic.HashSet<VisualElement>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

                if (clickClip == null)
                {
#if UNITY_EDITOR
                    clickClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/EasyGridBuilder Pro/Misc Assets/Sound/UIClick.wav");
#endif
                }
            }
            else if (Instance != this)
            {
                Destroy(this);
            }
        }

        public void BindButton(Button btn)
        {
            if (btn == null) return;
            if (!_boundButtons.Add(btn)) return; // Garante idempotência

            btn.RegisterCallback<MouseEnterEvent>(evt =>
            {
                if (!btn.enabledSelf) return;
                OnButtonHover(btn);
            });

            btn.RegisterCallback<PointerEnterEvent>(evt =>
            {
                if (!btn.enabledSelf) return;
                OnButtonHover(btn);
            });

            btn.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!btn.enabledSelf) return;
                OnButtonDown(btn);
            });

            btn.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!btn.enabledSelf) return;
                OnButtonUp(btn);
            });

            btn.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                OnButtonReset(btn);
            });

            btn.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                OnButtonReset(btn);
            });
        }

        public void BindHierarchy(VisualElement root)
        {
            if (root == null) return;
            var buttons = root.Query<Button>().ToList();
            foreach (var b in buttons)
            {
                BindButton(b);
            }
        }

        private float GetSafeScale(VisualElement el)
        {
            if (el == null) return 1f;
            try
            {
                if (el.resolvedStyle.scale.value.x > 0.01f)
                    return el.resolvedStyle.scale.value.x;
            }
            catch { }
            return 1f;
        }

        private void OnButtonHover(VisualElement el)
        {
            PlaySound(hoverClip);
#if MOREMOUNTAINS_FEEL
            if (hoverPlayer != null) hoverPlayer.PlayFeedbacks();
#endif

            if (UIToolkitTweenService.ReduceMotion) return;

            UIToolkitTweenService.KillTweens(el);
            float currentScale = GetSafeScale(el);
            DOTween.To(() => currentScale, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1.05f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnButtonDown(VisualElement el)
        {
            PlaySound(clickClip);
#if MOREMOUNTAINS_FEEL
            if (clickPlayer != null) clickPlayer.PlayFeedbacks();
#endif

            if (UIToolkitTweenService.ReduceMotion) return;

            UIToolkitTweenService.KillTweens(el);
            float currentScale = GetSafeScale(el);
            DOTween.To(() => currentScale, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 0.95f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnButtonUp(VisualElement el)
        {
            if (UIToolkitTweenService.ReduceMotion) return;

            UIToolkitTweenService.KillTweens(el);
            float currentScale = GetSafeScale(el);
            DOTween.To(() => currentScale, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1.05f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnButtonReset(VisualElement el)
        {
            UIToolkitTweenService.KillTweens(el);
            if (UIToolkitTweenService.ReduceMotion)
            {
                el.style.scale = new StyleScale(new Scale(Vector2.one));
                return;
            }

            float currentScale = GetSafeScale(el);
            DOTween.To(() => currentScale, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1f, 0.1f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        public void PlaySuccess(VisualElement target = null)
        {
            PlaySound(successClip);
#if MOREMOUNTAINS_FEEL
            if (successPlayer != null) successPlayer.PlayFeedbacks();
#endif
            if (target != null && !UIToolkitTweenService.ReduceMotion)
            {
                DOTween.Sequence().SetUpdate(true)
                    .Append(DOTween.To(() => 1f, s => target.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1.05f, 0.1f))
                    .Append(DOTween.To(() => 1.05f, s => target.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1f, 0.12f));
            }
        }

        public void PlayError(VisualElement target = null)
        {
            PlaySound(errorClip);
#if MOREMOUNTAINS_FEEL
            if (errorPlayer != null) errorPlayer.PlayFeedbacks();
#endif
            if (target != null)
            {
                UIToolkitTweenService.Shake(target, 6f, 0.22f);
            }
        }

        public void PlayReady()
        {
            PlaySound(readyClip != null ? readyClip : successClip);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, 0.8f);
            }
        }
    }
}
