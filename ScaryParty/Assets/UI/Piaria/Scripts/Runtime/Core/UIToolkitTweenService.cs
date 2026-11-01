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

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt("ReduceMotion", 0) == 1;
            set => PlayerPrefs.SetInt("ReduceMotion", value ? 1 : 0);
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

        public static void FadeIn(VisualElement target, float duration = 0.2f, Action onComplete = null)
        {
            if (target == null) return;
            KillTweens(target);

            target.style.display = DisplayStyle.Flex;
            target.style.visibility = Visibility.Visible;

            if (ReduceMotion)
            {
                target.style.opacity = 1f;
                onComplete?.Invoke();
                return;
            }

            target.style.opacity = 0f;
            var tw = DOTween.To(() => 0f, alpha => target.style.opacity = alpha, 1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());

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

            if (ReduceMotion)
            {
                target.style.opacity = 1f;
                target.style.scale = new StyleScale(new Scale(Vector2.one));
                onComplete?.Invoke();
                return;
            }

            target.style.opacity = 0f;
            target.style.scale = new StyleScale(new Scale(new Vector2(fromScale, fromScale)));

            var seq = DOTween.Sequence().SetUpdate(true);
            seq.Join(DOTween.To(() => 0f, a => target.style.opacity = a, 1f, duration * 0.7f));
            seq.Join(DOTween.To(() => fromScale, s => target.style.scale = new StyleScale(new Scale(new Vector2(s, s))), 1f, duration).SetEase(Ease.OutBack));
            seq.OnComplete(() => onComplete?.Invoke());

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
    }
}
