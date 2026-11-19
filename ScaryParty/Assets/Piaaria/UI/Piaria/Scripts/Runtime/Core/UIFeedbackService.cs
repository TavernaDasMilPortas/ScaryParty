using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using MoreMountains.Feedbacks;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Serviço de feedbacks visuais e sonoros para componentes de UI Toolkit.
    /// Aciona efeitos de hover, foco, clique, confirmação e erro via Feel e UIToolkitTweenService.
    /// Mantém ownership exclusivo dos tweens de escala via UIToolkitTweenService e desregistro seguro em Detach.
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

        [Header("Feel Players (Opcional)")]
        public MMF_Player hoverPlayer;
        public MMF_Player clickPlayer;
        public MMF_Player successPlayer;
        public MMF_Player errorPlayer;

        private sealed class ButtonCallbackRecord
        {
            public EventCallback<PointerEnterEvent> OnPointerEnter;
            public EventCallback<PointerLeaveEvent> OnPointerLeave;
            public EventCallback<PointerDownEvent> OnPointerDown;
            public EventCallback<PointerUpEvent> OnPointerUp;
            public EventCallback<FocusInEvent> OnFocusIn;
            public EventCallback<FocusOutEvent> OnFocusOut;
            public EventCallback<DetachFromPanelEvent> OnDetach;
            public bool IsPointerInside;
            public bool HasFocus;
        }

        private readonly Dictionary<Button, ButtonCallbackRecord> _boundButtons = new Dictionary<Button, ButtonCallbackRecord>();
        private float _lastHoverSoundTime = -10f;
        private const float HoverSoundCooldown = 0.045f;

        public int BoundButtonCount => _boundButtons.Count;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                if (audioSource == null) audioSource = GetComponent<AudioSource>();
                if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0f;

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

        private void OnDestroy()
        {
            ClearAllBindings();
            if (Instance == this) Instance = null;
        }

        public void BindButton(Button btn)
        {
            if (btn == null || _boundButtons.ContainsKey(btn)) return;

            var record = new ButtonCallbackRecord();

            record.OnPointerEnter = _ =>
            {
                if (!btn.enabledSelf) return;
                bool wasHighlighted = record.IsPointerInside || record.HasFocus;
                record.IsPointerInside = true;
                if (!wasHighlighted) OnButtonHover(btn);
            };

            record.OnPointerLeave = _ =>
            {
                record.IsPointerInside = false;
                if (!record.HasFocus) OnButtonReset(btn);
            };

            record.OnFocusIn = _ =>
            {
                if (!btn.enabledSelf) return;
                bool wasHighlighted = record.IsPointerInside || record.HasFocus;
                record.HasFocus = true;
                if (!wasHighlighted) OnButtonHover(btn);
            };

            record.OnFocusOut = _ =>
            {
                record.HasFocus = false;
                if (!record.IsPointerInside) OnButtonReset(btn);
            };

            record.OnPointerDown = _ =>
            {
                if (!btn.enabledSelf) return;
                OnButtonDown(btn);
            };

            record.OnPointerUp = _ =>
            {
                if (!btn.enabledSelf) return;
                OnButtonUp(btn, record.IsPointerInside || record.HasFocus);
            };

            record.OnDetach = _ => UnbindButton(btn);

            btn.RegisterCallback(record.OnPointerEnter);
            btn.RegisterCallback(record.OnPointerLeave);
            btn.RegisterCallback(record.OnFocusIn);
            btn.RegisterCallback(record.OnFocusOut);
            btn.RegisterCallback(record.OnPointerDown);
            btn.RegisterCallback(record.OnPointerUp);
            btn.RegisterCallback(record.OnDetach);

            _boundButtons[btn] = record;
        }

        public void UnbindButton(Button btn)
        {
            if (btn == null || !_boundButtons.TryGetValue(btn, out var record)) return;

            btn.UnregisterCallback(record.OnPointerEnter);
            btn.UnregisterCallback(record.OnPointerLeave);
            btn.UnregisterCallback(record.OnFocusIn);
            btn.UnregisterCallback(record.OnFocusOut);
            btn.UnregisterCallback(record.OnPointerDown);
            btn.UnregisterCallback(record.OnPointerUp);
            btn.UnregisterCallback(record.OnDetach);

            UIToolkitTweenService.KillTweens(btn);
            btn.style.scale = new StyleScale(new Scale(Vector2.one));
            _boundButtons.Remove(btn);
        }

        public void BindHierarchy(VisualElement root)
        {
            if (root == null) return;
            var buttons = root.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                BindButton(buttons[i]);
            }
        }

        public void UnbindHierarchy(VisualElement root)
        {
            if (root == null) return;
            var buttons = root.Query<Button>().ToList();
            for (int i = 0; i < buttons.Count; i++)
            {
                UnbindButton(buttons[i]);
            }
        }

        public void ClearAllBindings()
        {
            if (_boundButtons.Count == 0) return;
            var keys = new List<Button>(_boundButtons.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                UnbindButton(keys[i]);
            }
            _boundButtons.Clear();
        }

        private void OnButtonHover(VisualElement el)
        {
            float now = Time.unscaledTime;
            if (now - _lastHoverSoundTime >= HoverSoundCooldown)
            {
                _lastHoverSoundTime = now;
                PlaySound(hoverClip, 0.65f);
                PlayFeelPlayer(hoverPlayer);
            }

            UIToolkitTweenService.ScaleTo(el, 1.05f, 0.11f);
        }

        private void OnButtonDown(VisualElement el)
        {
            PlaySound(clickClip, 0.85f);
            PlayFeelPlayer(clickPlayer);
            UIToolkitTweenService.ScaleTo(el, 0.94f, 0.07f);
        }

        private void OnButtonUp(VisualElement el, bool keepHoverScale = true)
        {
            UIToolkitTweenService.ScaleTo(el, keepHoverScale ? 1.05f : 1f, 0.09f);
        }

        private void OnButtonReset(VisualElement el)
        {
            UIToolkitTweenService.ScaleTo(el, 1f, 0.1f);
        }

        public void PlaySuccess(VisualElement target = null)
        {
            PlaySound(successClip);
            PlayFeelPlayer(successPlayer);
            if (target != null)
            {
                UIToolkitTweenService.PulseScale(target, 1.06f, 0.09f, 0.12f);
            }
        }

        public void PlayError(VisualElement target = null)
        {
            PlaySound(errorClip);
            PlayFeelPlayer(errorPlayer);
            if (target != null)
            {
                UIToolkitTweenService.Shake(target, 6f, 0.22f);
            }
        }

        public void PlayReady()
        {
            PlaySound(readyClip != null ? readyClip : successClip);
        }

        public void PlayClick()
        {
            PlaySound(clickClip, 0.85f);
            PlayFeelPlayer(clickPlayer);
        }

        public void PlayHover()
        {
            PlaySound(hoverClip, 0.65f);
            PlayFeelPlayer(hoverPlayer);
        }

        private void PlayFeelPlayer(MMF_Player player)
        {
            if (player == null || UIToolkitTweenService.ReduceMotion) return;
            player.PlayFeedbacks();
        }

        private void PlaySound(AudioClip clip, float volume = 0.8f)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.PlayOneShot(clip, volume);
            }
        }
    }
}
