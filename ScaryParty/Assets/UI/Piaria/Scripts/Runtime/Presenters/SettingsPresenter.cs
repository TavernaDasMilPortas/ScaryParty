using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.Player;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class SettingsPresenter : MonoBehaviour, IModalView
    {
        public static SettingsPresenter Instance { get; private set; }

        public bool CanCloseWithEscape => true;
        public VisualElement RootElement { get; private set; }

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _settingsCard;
        private Slider _sliderMaster;
        private Label _labelMaster;
        private Slider _sliderMusic;
        private Label _labelMusic;
        private Slider _sliderSfx;
        private Label _labelSfx;
        private Slider _sliderSens;
        private Label _labelSens;
        private Slider _sliderScale;
        private Label _labelScale;
        private Toggle _toggleReduceMotion;
        private DropdownField _dropdownQuality;

        private Button _btnCloseTop;
        private Button _btnReset;
        private Button _btnSave;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(this);
                return;
            }

            ResolveUIDocument();
        }

        public void ResolveUIDocument()
        {
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/UI/Piaria/UXML/LobbySceneContainer.uxml");
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            ResolveUIDocument();
        }

        private void Reset()
        {
            ResolveUIDocument();
        }
#endif

        private void Start()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("SettingsModalRoot");
            if (RootElement == null)
            {
                var template = Resources.Load<VisualTreeAsset>("SettingsModal")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/SettingsModal.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("SettingsModalRoot");
                }
            }
            if (RootElement == null) return;

            _settingsCard = RootElement.Q<VisualElement>("SettingsCard");
            _sliderMaster = RootElement.Q<Slider>("SliderMasterVolume");
            _labelMaster = RootElement.Q<Label>("LabelMasterVal");
            _sliderMusic = RootElement.Q<Slider>("SliderMusicVolume");
            _labelMusic = RootElement.Q<Label>("LabelMusicVal");
            _sliderSfx = RootElement.Q<Slider>("SliderSfxVolume");
            _labelSfx = RootElement.Q<Label>("LabelSfxVal");
            _sliderSens = RootElement.Q<Slider>("SliderSensitivity");
            _labelSens = RootElement.Q<Label>("LabelSensVal");
            _sliderScale = RootElement.Q<Slider>("SliderUiScale");
            _labelScale = RootElement.Q<Label>("LabelScaleVal");
            _toggleReduceMotion = RootElement.Q<Toggle>("ToggleReduceMotion");
            _dropdownQuality = RootElement.Q<DropdownField>("DropdownQuality");

            _btnCloseTop = RootElement.Q<Button>("BtnCloseTop");
            _btnReset = RootElement.Q<Button>("BtnResetSettings");
            _btnSave = RootElement.Q<Button>("BtnSaveSettings");

            ApplyCatalogAssets();
            SetupQualityOptions();
            LoadSettings();
            BindEvents();

            RootElement.style.display = DisplayStyle.None;
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null || _settingsCard == null) return;

            if (catalog.paperPanel2 != null)
            {
                _settingsCard.style.backgroundImage = new StyleBackground(catalog.paperPanel2);
            }
        }

        private void SetupQualityOptions()
        {
            if (_dropdownQuality == null) return;
            var names = new List<string>(QualitySettings.names);
            _dropdownQuality.choices = names;
            int currentQuality = QualitySettings.GetQualityLevel();
            if (currentQuality >= 0 && currentQuality < names.Count)
                _dropdownQuality.value = names[currentQuality];
        }

        private void LoadSettings()
        {
            float master = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            float music = PlayerPrefs.GetFloat("MusicVolume", 0.8f);
            float sfx = PlayerPrefs.GetFloat("SfxVolume", 1.0f);
            float sens = PlayerPrefs.GetFloat("LookSensitivity", 1.0f);
            float scale = PlayerPrefs.GetFloat("UIScale", 1.0f);
            bool reduceMotion = PlayerPrefs.GetInt("ReduceMotion", 0) == 1;

            if (_sliderMaster != null) { _sliderMaster.value = master; UpdateLabel(_labelMaster, $"{Mathf.RoundToInt(master * 100)}%"); }
            if (_sliderMusic != null) { _sliderMusic.value = music; UpdateLabel(_labelMusic, $"{Mathf.RoundToInt(music * 100)}%"); }
            if (_sliderSfx != null) { _sliderSfx.value = sfx; UpdateLabel(_labelSfx, $"{Mathf.RoundToInt(sfx * 100)}%"); }
            if (_sliderSens != null) { _sliderSens.value = sens; UpdateLabel(_labelSens, $"{sens:0.1}"); }
            if (_sliderScale != null) { _sliderScale.value = scale; UpdateLabel(_labelScale, $"{Mathf.RoundToInt(scale * 100)}%"); }
            if (_toggleReduceMotion != null) { _toggleReduceMotion.value = reduceMotion; }

            ApplyLiveSettings();
        }

        private void BindEvents()
        {
            _sliderMaster?.RegisterValueChangedCallback(evt =>
            {
                UpdateLabel(_labelMaster, $"{Mathf.RoundToInt(evt.newValue * 100)}%");
                AudioListener.volume = evt.newValue;
            });

            _sliderMusic?.RegisterValueChangedCallback(evt =>
            {
                UpdateLabel(_labelMusic, $"{Mathf.RoundToInt(evt.newValue * 100)}%");
            });

            _sliderSfx?.RegisterValueChangedCallback(evt =>
            {
                UpdateLabel(_labelSfx, $"{Mathf.RoundToInt(evt.newValue * 100)}%");
            });

            _sliderSens?.RegisterValueChangedCallback(evt =>
            {
                UpdateLabel(_labelSens, $"{evt.newValue:0.1}");
                ApplySensitivity(evt.newValue);
            });

            _sliderScale?.RegisterValueChangedCallback(evt =>
            {
                UpdateLabel(_labelScale, $"{Mathf.RoundToInt(evt.newValue * 100)}%");
                ApplyScale(evt.newValue);
            });

            _toggleReduceMotion?.RegisterValueChangedCallback(evt =>
            {
                UIToolkitTweenService.ReduceMotion = evt.newValue;
            });

            _dropdownQuality?.RegisterValueChangedCallback(evt =>
            {
                int idx = _dropdownQuality.choices.IndexOf(evt.newValue);
                if (idx >= 0) QualitySettings.SetQualityLevel(idx, true);
            });

            if (_btnCloseTop != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCloseTop);
                _btnCloseTop.clicked += Close;
            }

            if (_btnSave != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnSave);
                _btnSave.clicked += () =>
                {
                    SaveSettings();
                    Close();
                };
            }

            if (_btnReset != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnReset);
                _btnReset.clicked += ResetToDefaults;
            }
        }

        private void UpdateLabel(Label lbl, string text)
        {
            if (lbl != null) lbl.text = text;
        }

        private void ApplySensitivity(float val)
        {
            var cameras = FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam != null) cam.mouseSensitivity = val;
            }
        }

        private void ApplyScale(float val)
        {
            if (uiDocument != null && uiDocument.panelSettings != null)
            {
                uiDocument.panelSettings.scale = val;
            }
        }

        private void ApplyLiveSettings()
        {
            if (_sliderMaster != null) AudioListener.volume = _sliderMaster.value;
            if (_sliderSens != null) ApplySensitivity(_sliderSens.value);
            if (_sliderScale != null) ApplyScale(_sliderScale.value);
            if (_toggleReduceMotion != null) UIToolkitTweenService.ReduceMotion = _toggleReduceMotion.value;
        }

        private void SaveSettings()
        {
            if (_sliderMaster != null) PlayerPrefs.SetFloat("MasterVolume", _sliderMaster.value);
            if (_sliderMusic != null) PlayerPrefs.SetFloat("MusicVolume", _sliderMusic.value);
            if (_sliderSfx != null) PlayerPrefs.SetFloat("SfxVolume", _sliderSfx.value);
            if (_sliderSens != null) PlayerPrefs.SetFloat("LookSensitivity", _sliderSens.value);
            if (_sliderScale != null) PlayerPrefs.SetFloat("UIScale", _sliderScale.value);
            if (_toggleReduceMotion != null) PlayerPrefs.SetInt("ReduceMotion", _toggleReduceMotion.value ? 1 : 0);
            if (_dropdownQuality != null) PlayerPrefs.SetInt("GraphicsQuality", QualitySettings.GetQualityLevel());
            PlayerPrefs.Save();
        }

        private void ResetToDefaults()
        {
            if (_sliderMaster != null) _sliderMaster.value = 1.0f;
            if (_sliderMusic != null) _sliderMusic.value = 0.8f;
            if (_sliderSfx != null) _sliderSfx.value = 1.0f;
            if (_sliderSens != null) _sliderSens.value = 1.0f;
            if (_sliderScale != null) _sliderScale.value = 1.0f;
            if (_toggleReduceMotion != null) _toggleReduceMotion.value = false;
            ApplyLiveSettings();
        }

        public void OpenModal()
        {
            EnsureInitialized();
            if (RootElement == null) return;

            UIModalStack.Instance?.PushModal(this);
            var curr = RootElement;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }
            UIToolkitTweenService.PopIn(_settingsCard != null ? _settingsCard : RootElement, 0.22f);
        }

        public void Close()
        {
            if (RootElement == null) return;

            UIToolkitTweenService.PopOut(_settingsCard != null ? _settingsCard : RootElement, 0.16f, 0.95f, () =>
            {
                RootElement.style.display = DisplayStyle.None;
                if (RootElement.parent != null && RootElement.parent != uiDocument?.rootVisualElement)
                {
                    RootElement.parent.style.display = DisplayStyle.None;
                }
                UIModalStack.Instance?.PopModal(this);
            });
        }
    }
}
