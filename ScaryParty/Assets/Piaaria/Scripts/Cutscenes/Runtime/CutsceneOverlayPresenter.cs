using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using UIImage = UnityEngine.UIElements.Image;

namespace ScaryParty.Cutscenes
{
    public sealed class CutsceneOverlayPresenter : MonoBehaviour
    {
        private GameObject _uiDocGo;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _root;
        private UIImage _frameImage;
        private Button _skipButton;
        private int _sortingOrder = 4000;
        private bool _allowSkip;
        private bool _isVisible;

        public event Action SkipRequested;

        public void Initialize(int sortingOrder = 4000)
        {
            _sortingOrder = Mathf.Max(_sortingOrder, sortingOrder);

            if (_uiDocGo == null)
            {
                _uiDocGo = new GameObject("CutsceneOverlayUIDocument");
                _uiDocGo.transform.SetParent(transform, false);
            }

            if (_uiDocument == null)
            {
                _uiDocument = _uiDocGo.GetComponent<UIDocument>() ?? _uiDocGo.AddComponent<UIDocument>();
            }

            if (_panelSettings == null)
            {
                PanelSettings templateSettings = FindTemplatePanelSettings();
                if (templateSettings != null)
                {
                    _panelSettings = Instantiate(templateSettings);
                }
                else
                {
                    _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                    var themes = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>();
                    if (themes != null && themes.Length > 0)
                        _panelSettings.themeStyleSheet = themes[0];
                }
                _panelSettings.name = "CutsceneOverlayPanelSettings_Runtime";
                _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                _panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                _panelSettings.match = 0.5f;
                _panelSettings.targetDisplay = 0;
            }

            _panelSettings.sortingOrder = _sortingOrder;
            _uiDocument.panelSettings = _panelSettings;
            _uiDocument.sortingOrder = _sortingOrder;

            BuildVisualTreeIfNeeded();
        }

        private static PanelSettings FindTemplatePanelSettings()
        {
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc != null && doc.panelSettings != null && doc.panelSettings.themeStyleSheet != null)
                    return doc.panelSettings;
            }
            foreach (var ps in Resources.FindObjectsOfTypeAll<PanelSettings>())
            {
                if (ps != null && ps.themeStyleSheet != null)
                    return ps;
            }
            return null;
        }

        private void BuildVisualTreeIfNeeded()
        {
            if (_root != null || _uiDocument == null || _uiDocument.rootVisualElement == null)
                return;

            var docRoot = _uiDocument.rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;

            _root = new VisualElement
            {
                name = "CutsceneOverlayRoot",
                pickingMode = PickingMode.Ignore
            };
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.right = 0;
            _root.style.top = 0;
            _root.style.bottom = 0;
            _root.style.backgroundColor = Color.black;
            _root.style.justifyContent = Justify.Center;
            _root.style.alignItems = Align.Center;
            _root.style.display = DisplayStyle.None;
            _root.style.opacity = 0f;

            _frameImage = new UIImage
            {
                name = "CutsceneFrameImage",
                scaleMode = ScaleMode.ScaleToFit,
                pickingMode = PickingMode.Ignore
            };
            _frameImage.style.width = Length.Percent(100);
            _frameImage.style.height = Length.Percent(100);
            _frameImage.style.flexGrow = 1;
            _root.Add(_frameImage);

            _skipButton = new Button(HandleSkipClicked)
            {
                name = "CutsceneSkipButton",
                text = "Pular [ESC / Espaço]"
            };
            _skipButton.style.position = Position.Absolute;
            _skipButton.style.right = 28;
            _skipButton.style.bottom = 24;
            _skipButton.style.paddingLeft = 18;
            _skipButton.style.paddingRight = 18;
            _skipButton.style.paddingTop = 10;
            _skipButton.style.paddingBottom = 10;
            _skipButton.style.fontSize = 18;
            _skipButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _skipButton.style.color = new Color(1f, 0.96f, 0.88f, 0.95f);
            _skipButton.style.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 0.78f);
            var borderColor = new Color(0.98f, 0.78f, 0.28f, 0.75f);
            _skipButton.style.borderTopWidth = 2;
            _skipButton.style.borderBottomWidth = 2;
            _skipButton.style.borderLeftWidth = 2;
            _skipButton.style.borderRightWidth = 2;
            _skipButton.style.borderTopColor = borderColor;
            _skipButton.style.borderBottomColor = borderColor;
            _skipButton.style.borderLeftColor = borderColor;
            _skipButton.style.borderRightColor = borderColor;
            _skipButton.style.borderTopLeftRadius = 8;
            _skipButton.style.borderTopRightRadius = 8;
            _skipButton.style.borderBottomLeftRadius = 8;
            _skipButton.style.borderBottomRightRadius = 8;
            _root.Add(_skipButton);

            docRoot.Add(_root);
        }

        private void HandleSkipClicked()
        {
            if (_isVisible && _allowSkip)
            {
                SkipRequested?.Invoke();
            }
        }

        public void Show(RenderTexture rt, float aspectRatio, bool allowSkip)
        {
            Initialize(_sortingOrder);
            BuildVisualTreeIfNeeded();

            _allowSkip = allowSkip;
            _isVisible = true;

            if (_frameImage != null)
            {
                _frameImage.scaleMode = ScaleMode.ScaleToFit;
                _frameImage.image = rt;
            }

            if (_skipButton != null)
            {
                _skipButton.style.display = allowSkip ? DisplayStyle.Flex : DisplayStyle.None;
                _skipButton.pickingMode = allowSkip ? PickingMode.Position : PickingMode.Ignore;
            }

            if (_root != null)
            {
                _root.pickingMode = PickingMode.Position;
                _root.style.display = DisplayStyle.Flex;
            }
        }

        public void SetAlpha(float alpha)
        {
            if (_root != null)
                _root.style.opacity = Mathf.Clamp01(alpha);
        }

        public void Hide()
        {
            _isVisible = false;
            _allowSkip = false;

            if (_frameImage != null)
                _frameImage.image = null;

            if (_root != null)
            {
                _root.style.opacity = 0f;
                _root.style.display = DisplayStyle.None;
                _root.pickingMode = PickingMode.Ignore;
            }
        }

        private void Update()
        {
            if (!_isVisible || !_allowSkip)
                return;

            var kb = Keyboard.current;
            bool skipPressed = kb != null && (kb.escapeKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame);
#if ENABLE_LEGACY_INPUT_MANAGER
            if (!skipPressed)
                skipPressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space);
#endif
            if (skipPressed)
            {
                SkipRequested?.Invoke();
            }
        }

        private void OnDestroy()
        {
            if (_skipButton != null)
                _skipButton.clicked -= HandleSkipClicked;

            if (_panelSettings != null)
            {
                if (Application.isPlaying)
                    Destroy(_panelSettings);
                else
                    DestroyImmediate(_panelSettings);
                _panelSettings = null;
            }
        }
    }
}
