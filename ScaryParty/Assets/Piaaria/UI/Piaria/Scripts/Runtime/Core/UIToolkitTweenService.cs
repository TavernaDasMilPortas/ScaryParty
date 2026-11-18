using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DG.Tweening;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Serviço de animação baseado em DOTween Pro específico para VisualElements do UI Toolkit.
    /// Respeita a preferência de redução de movimento e suporta Time.timeScale zero.
    /// Mantém registro de tweens ativos por VisualElement para cancelamento idempotente e sem vazamentos.
    /// </summary>
    public static class UIToolkitTweenService
    {
        private static readonly Dictionary<VisualElement, List<Tween>> _activeTweens = new Dictionary<VisualElement, List<Tween>>();

        public static event Action<bool> OnReduceMotionChanged;

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt("ReduceMotion", 0) == 1;
            set
            {
                bool current = ReduceMotion;
                PlayerPrefs.SetInt("ReduceMotion", value ? 1 : 0);
                if (current != value)
                {
                    OnReduceMotionChanged?.Invoke(value);
                }
            }
        }

        public static int ActiveTweenElementCount => _activeTweens.Count;

        public static int GetActiveTweenCount(VisualElement target = null)
        {
            if (target != null)
            {
                return _activeTweens.TryGetValue(target, out var list) ? list.Count : 0;
            }

            int total = 0;
            foreach (var kv in _activeTweens)
            {
                if (kv.Value != null) total += kv.Value.Count;
            }
            return total;
        }

        private static void RegisterTween(VisualElement target, Tween tween)
        {
            if (target == null || tween == null) return;
            if (!_activeTweens.TryGetValue(target, out var list))
            {
                list = new List<Tween>();
                _activeTweens[target] = list;
            }
            list.Add(tween);
            tween.OnKill(() => {
                if (_activeTweens.TryGetValue(target, out var activeList))
                {
                    activeList.Remove(tween);
                    if (activeList.Count == 0) _activeTweens.Remove(target);
                }
            });
        }

        public static void KillTweens(VisualElement target)
        {
            if (target == null) return;
            if (_activeTweens.TryGetValue(target, out var list))
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var tw = list[i];
                    if (tw != null && tw.IsActive())
                    {
                        tw.Kill();
                    }
                }
                _activeTweens.Remove(target);
            }
        }

        private static void EnsureDOTweenInitialized()
        {
            try
            {
                DOTween.Init(true, true, LogBehaviour.ErrorsOnly);
            }
            catch
            {
                // Ignora se já estiver inicializado
            }
        }

        public static void FadeIn(VisualElement target, float duration = 0.2f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            target.style.display = DisplayStyle.Flex;
            target.style.visibility = Visibility.Visible;

            if (ReduceMotion || duration <= 0.001f)
            {
                target.style.opacity = 1f;
                onComplete?.Invoke();
                return;
            }

            EnsureDOTweenInitialized();
            target.style.opacity = 0.01f;
            var tw = DOTween.To(() => 0.01f, alpha => {
                if (target != null) target.style.opacity = alpha;
            }, 1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => {
                    if (target != null) target.style.opacity = 1f;
                    onComplete?.Invoke();
                });

            RegisterTween(target, tw);
        }

        public static void FadeOut(VisualElement target, float duration = 0.15f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            if (ReduceMotion)
            {
                target.style.opacity = 0f;
                target.style.display = DisplayStyle.None;
                onComplete?.Invoke();
                return;
            }

            float currentAlpha = float.IsNaN(target.resolvedStyle.opacity) || target.resolvedStyle.opacity <= 0.001f ? 1f : target.resolvedStyle.opacity;
            var tw = DOTween.To(() => currentAlpha, alpha => target.style.opacity = alpha, 0f, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    target.style.display = DisplayStyle.None;
                    onComplete?.Invoke();
                });

            RegisterTween(target, tw);
        }

        public static void PopIn(VisualElement target, float duration = 0.22f, float fromScale = 0.94f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            target.style.display = DisplayStyle.Flex;
            target.style.visibility = Visibility.Visible;

            if (ReduceMotion || duration <= 0.001f)
            {
                target.style.opacity = 1f;
                target.style.scale = new StyleScale(new Scale(Vector2.one));
                onComplete?.Invoke();
                return;
            }

            EnsureDOTweenInitialized();
            target.style.opacity = 0.01f;
            target.style.scale = new StyleScale(new Scale(new Vector2(fromScale, fromScale)));

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(DOTween.To(() => 0.01f, a => {
                if (target != null) target.style.opacity = a;
            }, 1f, duration * 0.7f));
            seq.Join(DOTween.To(() => fromScale, s => {
                if (target != null) target.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
            }, 1f, duration).SetEase(Ease.OutBack));
            seq.OnComplete(() => {
                if (target != null)
                {
                    target.style.opacity = 1f;
                    target.style.scale = new StyleScale(new Scale(Vector2.one));
                }
                onComplete?.Invoke();
            });

            RegisterTween(target, seq);
        }

        public static void PopOut(VisualElement target, float duration = 0.16f, float toScale = 0.94f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            if (ReduceMotion)
            {
                target.style.opacity = 0f;
                target.style.display = DisplayStyle.None;
                onComplete?.Invoke();
                return;
            }

            float currentAlpha = float.IsNaN(target.resolvedStyle.opacity) || target.resolvedStyle.opacity <= 0.001f ? 1f : target.resolvedStyle.opacity;
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(DOTween.To(() => currentAlpha, a => target.style.opacity = a, 0f, duration));
            seq.Join(DOTween.To(() => 1f, s => target.style.scale = new StyleScale(new Scale(new Vector2(s, s))), toScale, duration).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                target.style.display = DisplayStyle.None;
                target.style.scale = new StyleScale(new Scale(Vector2.one));
                onComplete?.Invoke();
            });

            RegisterTween(target, seq);
        }

        public static void SlideIn(VisualElement target, Vector2 fromOffset, float duration = 0.22f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            target.style.display = DisplayStyle.Flex;
            target.style.visibility = Visibility.Visible;

            if (ReduceMotion)
            {
                target.style.opacity = 1f;
                target.style.translate = new StyleTranslate(new Translate(0, 0, 0));
                onComplete?.Invoke();
                return;
            }

            target.style.opacity = 0f;
            target.style.translate = new StyleTranslate(new Translate(new Length(fromOffset.x, LengthUnit.Pixel), new Length(fromOffset.y, LengthUnit.Pixel), 0));

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(DOTween.To(() => 0f, a => target.style.opacity = a, 1f, duration * 0.6f));
            seq.Join(DOTween.To(() => fromOffset, o => target.style.translate = new StyleTranslate(new Translate(new Length(o.x, LengthUnit.Pixel), new Length(o.y, LengthUnit.Pixel), 0)), Vector2.zero, duration).SetEase(Ease.OutCubic));
            seq.OnComplete(() => onComplete?.Invoke());

            RegisterTween(target, seq);
        }

        public static void SlideOut(VisualElement target, Vector2 toOffset, float duration = 0.18f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            if (ReduceMotion)
            {
                target.style.opacity = 0f;
                target.style.display = DisplayStyle.None;
                onComplete?.Invoke();
                return;
            }

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(DOTween.To(() => 1f, a => target.style.opacity = a, 0f, duration));
            seq.Join(DOTween.To(() => Vector2.zero, o => target.style.translate = new StyleTranslate(new Translate(new Length(o.x, LengthUnit.Pixel), new Length(o.y, LengthUnit.Pixel), 0)), toOffset, duration).SetEase(Ease.InCubic));
            seq.OnComplete(() =>
            {
                target.style.display = DisplayStyle.None;
                target.style.translate = new StyleTranslate(new Translate(0, 0, 0));
                onComplete?.Invoke();
            });

            RegisterTween(target, seq);
        }

        public static void Shake(VisualElement target, float amount = 6f, float duration = 0.25f, Action onComplete = null)
        {
            if (target == null) return;
            if (ReduceMotion)
            {
                onComplete?.Invoke();
                return;
            }

            KillTweens(target);

            var seq = DOTween.Sequence().SetUpdate(true);
            float step = duration / 6f;
            seq.Append(DOTween.To(() => 0f, x => target.style.translate = new StyleTranslate(new Translate(new Length(x, LengthUnit.Pixel), 0, 0)), -amount, step));
            seq.Append(DOTween.To(() => -amount, x => target.style.translate = new StyleTranslate(new Translate(new Length(x, LengthUnit.Pixel), 0, 0)), amount * 0.8f, step));
            seq.Append(DOTween.To(() => amount * 0.8f, x => target.style.translate = new StyleTranslate(new Translate(new Length(x, LengthUnit.Pixel), 0, 0)), -amount * 0.5f, step));
            seq.Append(DOTween.To(() => -amount * 0.5f, x => target.style.translate = new StyleTranslate(new Translate(new Length(x, LengthUnit.Pixel), 0, 0)), amount * 0.3f, step));
            seq.Append(DOTween.To(() => amount * 0.3f, x => target.style.translate = new StyleTranslate(new Translate(new Length(x, LengthUnit.Pixel), 0, 0)), 0f, step));
            seq.OnComplete(() =>
            {
                target.style.translate = new StyleTranslate(new Translate(0, 0, 0));
                onComplete?.Invoke();
            });

            RegisterTween(target, seq);
        }

        public static void TweenCounter(Label label, int fromVal, int toVal, float duration = 0.35f, string prefix = "R$", string suffix = "")
        {
            if (label == null) return;
            KillTweens(label);

            if (ReduceMotion || duration <= 0f)
            {
                label.text = $"{prefix}{toVal}{suffix}";
                return;
            }

            var tw = DOTween.To(() => fromVal, v => label.text = $"{prefix}{v}{suffix}", toVal, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            RegisterTween(label, tw);
        }

        private static float GetSafeScale(VisualElement el)
        {
            if (el == null) return 1f;
            try
            {
                float s = el.resolvedStyle.scale.value.x;
                if (!float.IsNaN(s) && s > 0.01f && s < 10f)
                    return s;
            }
            catch
            {
                // resolvedStyle pode falhar em elementos ainda não anexados a um painel
            }

            try
            {
                float inline = el.style.scale.value.value.x;
                if (!float.IsNaN(inline) && inline > 0.01f && inline < 10f)
                    return inline;
            }
            catch
            {
            }

            return 1f;
        }

        public static Tween ScaleTo(VisualElement target, float toScale, float duration = 0.1f, Ease ease = Ease.OutQuad, Action onComplete = null)
        {
            if (target == null) return null;
            KillTweens(target);

            if (ReduceMotion || duration <= 0.001f)
            {
                float finalScale = ReduceMotion ? 1f : toScale;
                target.style.scale = new StyleScale(new Scale(new Vector2(finalScale, finalScale)));
                onComplete?.Invoke();
                return null;
            }

            EnsureDOTweenInitialized();
            float startScale = GetSafeScale(target);
            var tw = DOTween.To(() => startScale, s =>
            {
                if (target != null)
                {
                    target.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
                }
            }, toScale, duration)
                .SetEase(ease)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    if (target != null)
                    {
                        target.style.scale = new StyleScale(new Scale(new Vector2(toScale, toScale)));
                    }
                    onComplete?.Invoke();
                });

            RegisterTween(target, tw);
            return tw;
        }

        public static Tween PulseScale(VisualElement target, float peakScale = 1.05f, float upDuration = 0.1f, float downDuration = 0.12f, Action onComplete = null)
        {
            if (target == null) return null;
            KillTweens(target);

            if (ReduceMotion || (upDuration <= 0.001f && downDuration <= 0.001f))
            {
                target.style.scale = new StyleScale(new Scale(Vector2.one));
                onComplete?.Invoke();
                return null;
            }

            EnsureDOTweenInitialized();
            float startScale = GetSafeScale(target);
            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Append(DOTween.To(() => startScale, s =>
            {
                if (target != null)
                {
                    target.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
                }
            }, peakScale, Mathf.Max(0.01f, upDuration)).SetEase(Ease.OutQuad));
            seq.Append(DOTween.To(() => peakScale, s =>
            {
                if (target != null)
                {
                    target.style.scale = new StyleScale(new Scale(new Vector2(s, s)));
                }
            }, 1f, Mathf.Max(0.01f, downDuration)).SetEase(Ease.OutBack));
            seq.OnComplete(() =>
            {
                if (target != null)
                {
                    target.style.scale = new StyleScale(new Scale(Vector2.one));
                }
                onComplete?.Invoke();
            });

            RegisterTween(target, seq);
            return seq;
        }

        public static void DropInCard(VisualElement card, float distance = 90f, float duration = 0.45f, float delay = 0f)
        {
            if (card == null) return;
            if (ReduceMotion)
            {
                card.style.opacity = 1f;
                card.style.translate = new StyleTranslate(new Translate(0f, 0f, 0f));
                return;
            }

            KillTweens(card);
            card.style.opacity = 0f;
            card.style.translate = new StyleTranslate(new Translate(0f, -Mathf.Abs(distance), 0f));

            float y = -Mathf.Abs(distance);
            var seq = DOTween.Sequence().SetUpdate(true);
            if (delay > 0f) seq.AppendInterval(delay);
            seq.Append(DOTween.To(() => 0f, a =>
            {
                if (card != null) card.style.opacity = a;
            }, 1f, Mathf.Max(0.05f, duration * 0.6f)).SetEase(Ease.OutQuad));
            seq.Join(DOTween.To(() => y, v =>
            {
                if (card != null) card.style.translate = new StyleTranslate(new Translate(0f, v, 0f));
            }, 0f, Mathf.Max(0.05f, duration)).SetEase(Ease.OutBack, 1.35f));
            seq.OnComplete(() =>
            {
                if (card != null)
                {
                    card.style.opacity = 1f;
                    card.style.translate = new StyleTranslate(new Translate(0f, 0f, 0f));
                }
            });
            RegisterTween(card, seq);
        }

        public static void StaggerDropCards(IReadOnlyList<VisualElement> cards, float stagger = 0.20f, float distance = 90f, float duration = 0.45f)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Count; i++)
            {
                DropInCard(cards[i], distance, duration, i * stagger);
            }
        }

        public static void HoverCardLift(VisualElement card, float scale = 1.015f)
        {
            if (card == null || ReduceMotion) return;
            ScaleTo(card, scale, 0.12f, Ease.OutQuad);
        }

        public static void HoverCardReset(VisualElement card)
        {
            if (card == null) return;
            ScaleTo(card, 1f, 0.12f, Ease.OutQuad);
        }

        public static void PressCard(VisualElement card, float scale = 0.97f)
        {
            if (card == null || ReduceMotion) return;
            ScaleTo(card, scale, 0.08f, Ease.OutQuad);
        }

        public static void SelectCardPulse(VisualElement card, float impactScale = 1.04f)
        {
            if (card == null || ReduceMotion) return;
            PulseScale(card, impactScale, 0.10f, 0.16f);
        }

        public static void PulseBadge(VisualElement badge)
        {
            if (badge == null || ReduceMotion) return;
            PulseScale(badge, 1.12f, 0.10f, 0.14f);
        }

        public static void ShakeReject(VisualElement target, float pixels = 6f)
        {
            if (target == null) return;
            Shake(target, pixels, 0.26f);
        }

        public static void HighlightLotterySweep(VisualElement card, float duration = 0.14f)
        {
            if (card == null || ReduceMotion) return;
            PulseScale(card, 1.03f, duration * 0.45f, duration * 0.55f);
        }

        public static void RevealWinnerStamp(VisualElement card, float impactScale = 1.06f)
        {
            if (card == null) return;
            if (ReduceMotion)
            {
                card.style.opacity = 1f;
                return;
            }
            PulseScale(card, impactScale, 0.12f, 0.22f);
        }

        public static void DimNonWinner(VisualElement card, float targetOpacity = 0.45f)
        {
            if (card == null) return;
            if (ReduceMotion)
            {
                card.style.opacity = targetOpacity;
                return;
            }
            var tween = DOTween.To(() => card.resolvedStyle.opacity, a =>
            {
                if (card != null) card.style.opacity = a;
            }, targetOpacity, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
            RegisterTween(card, tween);
        }

        public static void PunchCountdownNumber(Label label)
        {
            if (label == null || ReduceMotion) return;
            PulseScale(label, 1.24f, 0.10f, 0.22f);
        }

        public static void CrossFadePage(VisualElement page)
        {
            if (page == null) return;
            if (ReduceMotion)
            {
                page.style.opacity = 1f;
                return;
            }
            FadeIn(page, 0.22f);
        }
    }
}
