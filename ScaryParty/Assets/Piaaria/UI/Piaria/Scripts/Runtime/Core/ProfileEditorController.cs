using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Controlador compartilhado do editor de perfil (nome, swatches de cor, preview 3D e Salvar Perfil).
    /// Reutilizado no Lobby (coluna esquerda) e na Sala de Espera (bloco esquerdo).
    /// </summary>
    public sealed class ProfileEditorController
    {
        public static readonly Color OrangeColor = new Color(1f, 0.45f, 0.1f);
        public static readonly Color GreenColor = new Color(0.2f, 0.8f, 0.2f);
        public static readonly Color BlueColor = new Color(0.2f, 0.5f, 1f);
        public static readonly Color PinkColor = new Color(0.9f, 0.3f, 0.6f);
        public static readonly Color RedColor = new Color(0.85f, 0.15f, 0.15f);

        private readonly VisualElement _scopeRoot;
        private TextField _nameInput;
        private Button _btnOrange;
        private Button _btnGreen;
        private Button _btnBlue;
        private Button _btnPink;
        private Button _btnRed;
        private VisualElement _previewContainer;
        private VisualElement _previewRenderTexture;
        private Button _btnSave;

        private Color _selectedColor = OrangeColor;
        private bool _isBound;

        public event Action<string, Color> OnProfileSaved;

        public Color SelectedColor => _selectedColor;
        public string PlayerName => _nameInput != null ? _nameInput.value : PlayerPrefs.GetString("PlayerName", "Jogador");

        public ProfileEditorController(VisualElement scopeRoot)
        {
            _scopeRoot = scopeRoot;
            BindElements();
        }

        private void BindElements()
        {
            if (_scopeRoot == null) return;

            _nameInput = _scopeRoot.Q<TextField>("PlayerNameInput");
            _btnOrange = _scopeRoot.Q<Button>("BtnColorOrange");
            _btnGreen = _scopeRoot.Q<Button>("BtnColorGreen");
            _btnBlue = _scopeRoot.Q<Button>("BtnColorBlue");
            _btnPink = _scopeRoot.Q<Button>("BtnColorPink");
            _btnRed = _scopeRoot.Q<Button>("BtnColorRed");
            _previewContainer = _scopeRoot.Q<VisualElement>("PreviewContainer");
            _previewRenderTexture = _scopeRoot.Q<VisualElement>("PreviewRenderTexture");
            _btnSave = _scopeRoot.Q<Button>("BtnSaveProfile");

            ApplyCatalogAssets();

            if (!_isBound)
            {
                _isBound = true;
                BindSwatch(_btnOrange, OrangeColor);
                BindSwatch(_btnGreen, GreenColor);
                BindSwatch(_btnBlue, BlueColor);
                BindSwatch(_btnPink, PinkColor);
                BindSwatch(_btnRed, RedColor);

                if (_btnSave != null)
                {
                    _btnSave.text = string.Empty;
                    UIFeedbackService.Instance?.BindButton(_btnSave);
                    UIToolkitTweenService.BindInteractiveFeedback(_btnSave, 1.03f, 0.96f);
                    _btnSave.clicked += HandleSaveClicked;
                }
            }
        }

        private void BindSwatch(Button btn, Color color)
        {
            if (btn == null) return;
            btn.style.display = DisplayStyle.Flex;
            UIFeedbackService.Instance?.BindButton(btn);
            UIToolkitTweenService.BindInteractiveFeedback(btn, 1.07f, 0.94f);
            btn.clicked += () =>
            {
                SetColor(color);
                UIToolkitTweenService.PulseScale(btn, 1.12f, 0.08f, 0.12f);
            };
        }

        public void ApplyCatalogAssets()
        {
            if (_scopeRoot == null) return;
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            var header = _scopeRoot.Q<VisualElement>("HeaderMyProfile");
            if (header != null && catalog.headerMyProfile != null)
            {
                header.style.backgroundImage = new StyleBackground(catalog.headerMyProfile);
                var lbl = header.Q<Label>();
                if (lbl != null)
                {
                    lbl.text = string.Empty;
                    lbl.style.display = DisplayStyle.None;
                }
            }

            if (_btnOrange != null && catalog.colorOrange != null)
                _btnOrange.style.backgroundImage = new StyleBackground(catalog.colorOrange);
            if (_btnGreen != null && catalog.colorGreen != null)
                _btnGreen.style.backgroundImage = new StyleBackground(catalog.colorGreen);
            if (_btnBlue != null && catalog.colorBlue != null)
                _btnBlue.style.backgroundImage = new StyleBackground(catalog.colorBlue);
            if (_btnPink != null && catalog.colorPink != null)
                _btnPink.style.backgroundImage = new StyleBackground(catalog.colorPink);
            if (_btnRed != null && catalog.colorRed != null)
                _btnRed.style.backgroundImage = new StyleBackground(catalog.colorRed);
        }

        public void LoadFromPrefsOrData(PlayerData playerData = null)
        {
            if (playerData != null)
            {
                if (_nameInput != null)
                    _nameInput.SetValueWithoutNotify(string.IsNullOrWhiteSpace(playerData.PlayerName) ? "Jogador" : playerData.PlayerName);
                SetColor(playerData.PlayerColor);
                return;
            }

            string savedName = PlayerPrefs.GetString("PlayerName", "Jogador");
            if (_nameInput != null)
                _nameInput.SetValueWithoutNotify(savedName);

            if (PlayerPrefs.HasKey("PlayerColorR"))
            {
                var c = new Color(
                    PlayerPrefs.GetFloat("PlayerColorR", OrangeColor.r),
                    PlayerPrefs.GetFloat("PlayerColorG", OrangeColor.g),
                    PlayerPrefs.GetFloat("PlayerColorB", OrangeColor.b),
                    1f
                );
                SetColor(c);
            }
            else
            {
                SetColor(OrangeColor);
            }
        }

        public void SetProfileValuesWithoutNotify(string nameValue, Color color)
        {
            if (_nameInput != null && !string.IsNullOrWhiteSpace(nameValue) && _nameInput.focusController?.focusedElement != _nameInput)
            {
                _nameInput.SetValueWithoutNotify(nameValue);
            }
            SetColor(color);
        }

        public void SetColor(Color color)
        {
            _selectedColor = color;
            UpdateSwatchSelectionHighlight(color);

            if (CharacterPreviewRig.Instance != null)
            {
                CharacterPreviewRig.Instance.SetPlayerColor(color);
            }
        }

        private void UpdateSwatchSelectionHighlight(Color color)
        {
            void Mark(Button btn, Color swatchColor)
            {
                if (btn == null) return;
                float dist = Vector4.Distance((Vector4)color, (Vector4)swatchColor);
                btn.EnableInClassList("is-selected", dist < 0.2f);
            }

            Mark(_btnOrange, OrangeColor);
            Mark(_btnGreen, GreenColor);
            Mark(_btnBlue, BlueColor);
            Mark(_btnPink, PinkColor);
            Mark(_btnRed, RedColor);
        }

        public void ActivatePreview()
        {
            if (CharacterPreviewRig.Instance == null)
            {
                var existingRig = UnityEngine.Object.FindFirstObjectByType<CharacterPreviewRig>();
                if (existingRig == null)
                {
                    var rigGo = new GameObject("CharacterPreviewRig");
                    rigGo.AddComponent<CharacterPreviewRig>();
                }
            }

            if (CharacterPreviewRig.Instance != null)
            {
                CharacterPreviewRig.Instance.SetActive(true);
                if (_previewRenderTexture != null)
                {
                    var rt = CharacterPreviewRig.Instance.RenderTexture;
                    if (rt != null)
                    {
                        _previewRenderTexture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                    }
                }
                CharacterPreviewRig.Instance.BindDragEvents(_previewContainer);
                CharacterPreviewRig.Instance.SetPlayerColor(_selectedColor);
            }
        }

        public void SaveProfileToStorage(PlayerData playerData = null)
        {
            string finalName = _nameInput != null ? _nameInput.value?.Trim() : PlayerPrefs.GetString("PlayerName", "Jogador");
            if (string.IsNullOrWhiteSpace(finalName))
                finalName = "Jogador";

            if (_nameInput != null && _nameInput.value != finalName)
                _nameInput.SetValueWithoutNotify(finalName);

            PlayerPrefs.SetString("PlayerName", finalName);
            PlayerPrefs.SetFloat("PlayerColorR", _selectedColor.r);
            PlayerPrefs.SetFloat("PlayerColorG", _selectedColor.g);
            PlayerPrefs.SetFloat("PlayerColorB", _selectedColor.b);
            PlayerPrefs.Save();

            if (playerData != null)
            {
                playerData.PlayerName = finalName;
                playerData.PlayerColor = _selectedColor;
                playerData.Save();
            }
        }

        private void HandleSaveClicked()
        {
            string finalName = _nameInput != null ? _nameInput.value?.Trim() : string.Empty;
            if (string.IsNullOrWhiteSpace(finalName))
            {
                if (_nameInput != null)
                    UIToolkitTweenService.ShakeReject(_nameInput);
                UIFeedbackService.Instance?.PlayError(_nameInput);
                return;
            }

            SaveProfileToStorage();
            if (_btnSave != null)
                UIToolkitTweenService.PulseScale(_btnSave, 1.05f, 0.08f, 0.14f);
            UIFeedbackService.Instance?.PlaySuccess();
            OnProfileSaved?.Invoke(finalName, _selectedColor);
        }
    }
}
