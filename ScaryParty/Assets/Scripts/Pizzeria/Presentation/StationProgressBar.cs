using UnityEngine;
using UnityEngine.UI;

namespace ScaryParty.Pizzeria.Presentation
{
    public class StationProgressBar : MonoBehaviour
    {
        private Canvas _canvas;
        private Image _bgImage;
        private Image _fillImage;
        private Transform _mainCamera;

        public static StationProgressBar Create(Transform parent)
        {
            GameObject go = new GameObject("StationProgressBar");
            go.transform.SetParent(parent, false);
            // Default offset above the slot anchor
            go.transform.localPosition = new Vector3(0, 0.4f, 0); 
            go.transform.localScale = Vector3.one * 0.005f; // Scale down for world space

            var bar = go.AddComponent<StationProgressBar>();
            bar.Initialize();
            bar.Hide();
            return bar;
        }

        private void Initialize()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100; // Always on top

            var rt = _canvas.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 15);

            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(transform, false);
            _bgImage = bgObj.AddComponent<Image>();
            _bgImage.color = new Color(0, 0, 0, 0.7f);
            var bgRt = _bgImage.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;

            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.color = Color.green;
            var fillRt = _fillImage.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0, 0);
            fillRt.anchorMax = new Vector2(0, 1); // Start empty
            fillRt.pivot = new Vector2(0, 0.5f);
            fillRt.sizeDelta = Vector2.zero;

            if (Camera.main != null)
                _mainCamera = Camera.main.transform;
        }

        private void Update()
        {
            if (_mainCamera == null && Camera.main != null)
            {
                _mainCamera = Camera.main.transform;
            }

            if (_mainCamera != null && _canvas.enabled)
            {
                // Billboard effect: look at camera
                transform.rotation = _mainCamera.rotation;
            }
        }

        public void SetProgress(float normalizedProgress, Color color)
        {
            if (!_canvas.enabled) _canvas.enabled = true;
            
            normalizedProgress = Mathf.Clamp01(normalizedProgress);
            var fillRt = _fillImage.GetComponent<RectTransform>();
            fillRt.anchorMax = new Vector2(normalizedProgress, 1);
            _fillImage.color = color;
        }

        public void Hide()
        {
            if (_canvas != null && _canvas.enabled)
            {
                _canvas.enabled = false;
            }
        }
    }
}
