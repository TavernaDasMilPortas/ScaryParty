using System;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    public class MainMenuPresenter : MonoBehaviour, IScreenView
    {
        public static MainMenuPresenter Instance { get; private set; }

        public string ScreenId => "MainMenuScreen";
        public VisualElement RootElement { get; private set; }
        public ScreenState State { get; private set; } = ScreenState.Hidden;

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private VisualElement _logoPiaria;
        private VisualElement _logoSubtitle;
        private VisualElement _checkeredLeft;
        private VisualElement _checkeredRight;
        private Button _btnPlay;
        private Button _btnSettings;
        private Button _btnQuit;

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
            if (UIScreenRouter.Instance != null)
            {
                UIScreenRouter.Instance.RegisterScreen(this);
                if (UIScreenRouter.Instance.ActiveScreen == null)
                {
                    UIScreenRouter.Instance.NavigateTo(ScreenId);
                }
            }
            else
            {
                Open();
            }
        }

        private void OnDestroy()
        {
            UIScreenRouter.Instance?.UnregisterScreen(this);
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            ResolveUIDocument();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("MainMenuRoot");
            if (RootElement == null)
            {
                var template = Resources.Load<VisualTreeAsset>("MainMenuScreen")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/MainMenuScreen.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("MainMenuRoot");
                }
            }
            if (RootElement == null) return;

            _logoPiaria = RootElement.Q<VisualElement>("LogoPiaria");
            _logoSubtitle = RootElement.Q<VisualElement>("LogoSubtitle");
            _checkeredLeft = RootElement.Q<VisualElement>("CheckeredTopLeft");
            _checkeredRight = RootElement.Q<VisualElement>("CheckeredTopRight");

            _btnPlay = RootElement.Q<Button>("BtnPlay");
            _btnSettings = RootElement.Q<Button>("BtnSettings");
            _btnQuit = RootElement.Q<Button>("BtnQuit");

            ApplyCatalogAssets();
            BindEvents();
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            if (_logoPiaria != null && catalog.logoPiaria != null)
                _logoPiaria.style.backgroundImage = new StyleBackground(catalog.logoPiaria);

            if (_logoSubtitle != null && catalog.logoSubtitle != null)
                _logoSubtitle.style.backgroundImage = new StyleBackground(catalog.logoSubtitle);

            if (_checkeredLeft != null && catalog.checkeredDeco1 != null)
                _checkeredLeft.style.backgroundImage = new StyleBackground(catalog.checkeredDeco1);

            if (_checkeredRight != null && catalog.checkeredDeco2 != null)
                _checkeredRight.style.backgroundImage = new StyleBackground(catalog.checkeredDeco2);

            // Fundos e artes dos botões
            if (_btnPlay != null)
            {
                if (catalog.ribbonButtonBg1 != null) _btnPlay.style.backgroundImage = new StyleBackground(catalog.ribbonButtonBg1);
                var art = _btnPlay.Q<VisualElement>("ArtPlay");
                if (art != null && catalog.btnPlayText != null) art.style.backgroundImage = new StyleBackground(catalog.btnPlayText);
            }

            if (_btnSettings != null)
            {
                if (catalog.ribbonButtonBg2 != null) _btnSettings.style.backgroundImage = new StyleBackground(catalog.ribbonButtonBg2);
                var art = _btnSettings.Q<VisualElement>("ArtSettings");
                if (art != null && catalog.btnSettingsText != null) art.style.backgroundImage = new StyleBackground(catalog.btnSettingsText);
            }

            if (_btnQuit != null)
            {
                if (catalog.ribbonButtonBg3 != null) _btnQuit.style.backgroundImage = new StyleBackground(catalog.ribbonButtonBg3);
                var art = _btnQuit.Q<VisualElement>("ArtQuit");
                if (art != null && catalog.btnQuitText != null) art.style.backgroundImage = new StyleBackground(catalog.btnQuitText);
            }
        }

        private void BindEvents()
        {
            if (_btnPlay != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnPlay);
                _btnPlay.clicked += () =>
                {
                    if (UIScreenRouter.Instance != null)
                    {
                        UIScreenRouter.Instance.NavigateTo("LobbyScreen");
                    }
                    else
                    {
                        Close(() => LobbyPresenter.Instance?.Open());
                    }
                };
            }

            if (_btnSettings != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnSettings);
                _btnSettings.clicked += () =>
                {
                    SettingsPresenter.Instance?.OpenModal();
                };
            }

            if (_btnQuit != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnQuit);
                _btnQuit.clicked += () =>
                {
                    ModalPresenter.Instance?.ShowConfirmation("Deseja realmente sair do jogo?", () =>
                    {
#if UNITY_EDITOR
                        EditorApplication.isPlaying = false;
#else
                        Application.Quit();
#endif
                    });
                };
            }
        }

        public void Open(Action onComplete = null)
        {
            EnsureInitialized();
            if (RootElement == null)
            {
                onComplete?.Invoke();
                return;
            }

            var curr = RootElement;
            while (curr != null && curr != uiDocument?.rootVisualElement)
            {
                curr.style.display = DisplayStyle.Flex;
                curr = curr.parent;
            }

            RootElement.style.opacity = 1f;
            State = ScreenState.Opening;
            UIToolkitTweenService.FadeIn(RootElement, 0.22f, () =>
            {
                State = ScreenState.Active;
                onComplete?.Invoke();
            });
        }

        public void Close(Action onComplete = null)
        {
            if (RootElement == null)
            {
                State = ScreenState.Hidden;
                onComplete?.Invoke();
                return;
            }

            State = ScreenState.Closing;
            UIToolkitTweenService.FadeOut(RootElement, 0.18f, () =>
            {
                State = ScreenState.Hidden;
                if (RootElement.parent != null && RootElement.parent != uiDocument?.rootVisualElement)
                {
                    RootElement.parent.style.display = DisplayStyle.None;
                }
                onComplete?.Invoke();
            });
        }
    }
}
