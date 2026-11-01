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

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                if (audioSource == null) audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void BindButton(Button btn)
        {
            if (btn == null) return;

            btn.RegisterCallback<MouseEnterEvent>(evt =>
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

        private void OnButtonHover(VisualElement el)
        {
            if (UIToolkitTweenService.ReduceMotion) return;

            DOTween.To(() => 1f, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1.025f, 0.12f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            PlaySound(hoverClip);
#if MOREMOUNTAINS_FEEL
            if (hoverPlayer != null) hoverPlayer.PlayFeedbacks();
#endif
        }

        private void OnButtonDown(VisualElement el)
        {
            if (UIToolkitTweenService.ReduceMotion) return;

            DOTween.To(() => 1.025f, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 0.97f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            PlaySound(clickClip);
#if MOREMOUNTAINS_FEEL
            if (clickPlayer != null) clickPlayer.PlayFeedbacks();
#endif
        }

        private void OnButtonUp(VisualElement el)
        {
            if (UIToolkitTweenService.ReduceMotion) return;

            DOTween.To(() => 0.97f, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1.025f, 0.08f)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnButtonReset(VisualElement el)
        {
            if (UIToolkitTweenService.ReduceMotion)
            {
                el.style.scale = new StyleScale(new Scale(Vector2.one));
                return;
            }

            DOTween.To(() => 1.025f, s => el.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1f, 0.1f)
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
