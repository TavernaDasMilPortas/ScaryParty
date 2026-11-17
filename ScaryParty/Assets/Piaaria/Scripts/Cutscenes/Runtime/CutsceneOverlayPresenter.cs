using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace ScaryParty.Cutscenes
{
    public sealed class CutsceneOverlayPresenter : MonoBehaviour
    {
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RawImage _rawImage;
        private AspectRatioFitter _aspectFitter;
        private bool _allowSkip;

        public event Action SkipRequested;

        public void Initialize(int sortingOrder = 500)
        {
            if (_canvas != null) return;

            var root = new GameObject("CutsceneOverlayCanvas");
            root.transform.SetParent(transform, false);

            _canvas = root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            root.AddComponent<GraphicRaycaster>();

            _canvasGroup = root.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            // Black letterbox/pillarbox background
            var bgGo = new GameObject("LetterboxBackground");
            bgGo.transform.SetParent(root.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = Color.black;

            // RawImage presenting RenderTexture
            var rawGo = new GameObject("CutsceneRawImage");
            rawGo.transform.SetParent(root.transform, false);
            var rawRect = rawGo.AddComponent<RectTransform>();
            rawRect.anchorMin = Vector2.zero;
            rawRect.anchorMax = Vector2.one;
            rawRect.offsetMin = Vector2.zero;
            rawRect.offsetMax = Vector2.zero;

            _rawImage = rawGo.AddComponent<RawImage>();
            _rawImage.color = Color.white;

            _aspectFitter = rawGo.AddComponent<AspectRatioFitter>();
            _aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            _aspectFitter.aspectRatio = 16f / 9f;

            root.SetActive(false);
        }

        public void Show(RenderTexture rt, float aspectRatio, bool allowSkip)
        {
            Initialize();
            _allowSkip = allowSkip;
            _rawImage.texture = rt;
            _aspectFitter.aspectRatio = Mathf.Max(0.1f, aspectRatio);
            _canvas.gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
        }

        public void SetAlpha(float alpha)
        {
            if (_canvasGroup != null)
                _canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        public void Hide()
        {
            if (_canvas == null) return;
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _rawImage.texture = null;
            _canvas.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeSelf || !_allowSkip)
                return;

            var kb = Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
            {
                SkipRequested?.Invoke();
            }
        }
    }
}
