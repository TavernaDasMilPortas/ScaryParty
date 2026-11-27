using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Netcode;
using ScaryParty.Player;
using ScaryParty.Cutscenes;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.UI.Piaria.Presenters
{
    /// <summary>
    /// Menu de Pausa em jogo acionado por [ESC] sem conflitar com modais abertos (O2 / O20 / N3).
    /// Inclui recuperação de jogador e recuperação de moto com cooldown autoritativo de 60s.
    /// </summary>
    public class PauseEscapeMenuPresenter : MonoBehaviour, IModalView
    {
        public static PauseEscapeMenuPresenter Instance { get; private set; }

        public bool CanCloseWithEscape =>
            !_isOpen ||
            (_lastToggleFrame != Time.frameCount && _mainPanel != null && _mainPanel.style.display == DisplayStyle.Flex);
        public VisualElement RootElement { get; private set; }
        public bool IsOpen => _isOpen;

        public static PauseEscapeMenuPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<PauseEscapeMenuPresenter>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("PauseEscapeMenuPresenter");
            return go.AddComponent<PauseEscapeMenuPresenter>();
        }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _mainPanel;
        private VisualElement _controlsSubPanel;
        private VisualElement _settingsSubPanel;
        private VisualElement _confirmLeaveSubPanel;
        private Button _recoverPlayerBtn;
        private Button _recoverMotoBtn;

        private bool _isOpen = false;
        private bool _pushedToStack = false;
        private bool _pushedGateFallback = false;
        private bool _otherModalWasOpenLastFrame = false;
        private int _lastToggleFrame = -1;
        private float _openedUnscaledTime = -10f;

        private float _playerRecoverCooldownUntil = 0f;
        private float _motoRecoverCooldownUntil = 0f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (_isOpen)
            {
                ReleaseLock();
            }
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            var sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 ||
                sceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                enabled = false;
            }
        }

        private void EnsureUIInitialized()
        {
            if (RootElement != null) return;

            var hud = FindFirstObjectByType<HudPresenter>();
            _doc = hud != null ? hud.uiDocument : GetComponent<UIDocument>();
            if (_doc == null)
                _doc = gameObject.AddComponent<UIDocument>();

            if (_doc.panelSettings == null)
            {
                var panelSettingsList = Resources.FindObjectsOfTypeAll<PanelSettings>();
                if (panelSettingsList != null && panelSettingsList.Length > 0)
                    _doc.panelSettings = panelSettingsList[0];
            }

            _root = _doc.rootVisualElement;
            if (_root == null) return;

            BuildUIHierarchy();
        }

        private void BuildUIHierarchy()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            RootElement = new VisualElement();
            RootElement.name = "PauseEscapeOverlay";
            RootElement.AddToClassList("pause-overlay");
            RootElement.style.display = DisplayStyle.None;

#if UNITY_EDITOR
            var settingsSheet = UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Piaaria/UI/Piaria/USS/Settings.uss");
            if (settingsSheet != null && !RootElement.styleSheets.Contains(settingsSheet))
                RootElement.styleSheets.Add(settingsSheet);
#endif

            _card = new VisualElement();
            _card.AddToClassList("pause-card");
            if (catalog != null && catalog.paperPanel1 != null)
                _card.style.backgroundImage = new StyleBackground(catalog.paperPanel1);

            var title = new Label("PAUSA — PIZZARIA");
            title.AddToClassList("pause-title");
            _card.Add(title);

            _mainPanel = new VisualElement();
            AddMenuButton(_mainPanel, "Continuar [ESC]", Close, "pause-btn-primary");
            AddMenuButton(_mainPanel, "Controles", () => ShowSubPanel(_controlsSubPanel), "pause-btn-neutral");
            AddMenuButton(_mainPanel, "Configurações", () => ShowSubPanel(_settingsSubPanel), "pause-btn-neutral");
            AddMenuButton(_mainPanel, "Ajuda / Tutorial", () =>
            {
                Close();
                ContextualTutorialPresenter.EnsureInstance().ShowTutorial();
            }, "pause-btn-secondary");

            _recoverPlayerBtn = AddMenuButton(_mainPanel, "Recuperar Jogador (60s)", RequestRecoverPlayer, "pause-btn-warning");
            _recoverMotoBtn = AddMenuButton(_mainPanel, "Recuperar Moto (60s)", RequestRecoverMoto, "pause-btn-warning");

            AddMenuButton(_mainPanel, "Sair da Partida", () =>
            {
                if (Time.unscaledTime - _openedUnscaledTime < 0.15f) return;
                ShowSubPanel(_confirmLeaveSubPanel);
            }, "pause-btn-danger");
            _card.Add(_mainPanel);

            // Subpainel Confirmar Saída
            _confirmLeaveSubPanel = new VisualElement { style = { display = DisplayStyle.None } };
            var confirmText = new Label("Deseja realmente sair da partida e voltar ao Lobby?");
            confirmText.AddToClassList("pause-confirm-text");
            _confirmLeaveSubPanel.Add(confirmText);
            AddMenuButton(_confirmLeaveSubPanel, "Sim, Sair da Partida", LeaveMatch, "pause-btn-danger");
            AddMenuButton(_confirmLeaveSubPanel, "Cancelar [ESC]", () => ShowSubPanel(_mainPanel), "pause-btn-neutral");
            _card.Add(_confirmLeaveSubPanel);

            // Subpainel Controles organizado por contexto (Ação / Tecla)
            _controlsSubPanel = new VisualElement { style = { display = DisplayStyle.None } };
            var controlsScroll = new ScrollView { name = "PauseControlsScroll" };
            controlsScroll.AddToClassList("controls-scroll");

            AddControlsGroup(controlsScroll, "Cozinha & Preparo", new (string action, string key)[]
            {
                ("Interagir / Pegar / Soltar", "E"),
                ("Preparar / Cortar / Assar / Montar", "F (segurar)"),
                ("Descartar no lixo / Limpar comanda", "F (segurar)"),
                ("Trocar mão ativa", "1 / 2"),
                ("Guia de receitas", "H")
            });

            AddControlsGroup(controlsScroll, "Entregas & Cidade", new (string action, string key)[]
            {
                ("Mover / Correr", "WASD / Shift"),
                ("Abrir mochila de entregas", "B"),
                ("Montar / Desmontar moto", "E"),
                ("Buzinar na moto", "H"),
                ("Mapa ampliado da cidade", "M")
            });

            AddControlsGroup(controlsScroll, "Combate & Resgate", new (string action, string key)[]
            {
                ("Mirar / Atirar", "Botão Dir. / Esq."),
                ("Recarregar arma", "R"),
                ("Guardar / Sacar espingarda (Bandoleira)", "G"),
                ("Reparar mochila / Resgatar aliado", "F (segurar)")
            });

            AddControlsGroup(controlsScroll, "Menus & Equipe", new (string action, string key)[]
            {
                ("Central de operações (Pedidos/Loja/Equipe)", "J"),
                ("Placar da sessão", "TAB"),
                ("Tutorial operacional", "F1"),
                ("Pausa / Controles", "ESC")
            });

            _controlsSubPanel.Add(controlsScroll);
            AddMenuButton(_controlsSubPanel, "Voltar", () => ShowSubPanel(_mainPanel), "pause-btn-neutral");
            _card.Add(_controlsSubPanel);

            // Subpainel Configurações Rápidas
            _settingsSubPanel = new VisualElement { style = { display = DisplayStyle.None } };

            float savedMaster = PlayerPrefs.GetFloat("MasterVolume", 1.0f);
            float savedSens = PlayerPrefs.GetFloat("LookSensitivity", 1.0f);

            var volSlider = new Slider("Volume Geral", 0f, 1f) { value = savedMaster };
            volSlider.AddToClassList("pause-setting-slider");
            volSlider.RegisterValueChangedCallback(evt =>
            {
                AudioListener.volume = evt.newValue;
                PlayerPrefs.SetFloat("MasterVolume", evt.newValue);
            });
            _settingsSubPanel.Add(volSlider);

            var sensSlider = new Slider("Sensibilidade Câmera", 0.2f, 3.0f) { value = savedSens };
            sensSlider.AddToClassList("pause-setting-slider");
            sensSlider.RegisterValueChangedCallback(evt =>
            {
                PlayerPrefs.SetFloat("LookSensitivity", evt.newValue);
                var cams = FindObjectsByType<FirstPersonCameraController>(FindObjectsSortMode.None);
                for (int i = 0; i < cams.Length; i++)
                {
                    if (cams[i] != null) cams[i].mouseSensitivity = evt.newValue;
                }
            });
            _settingsSubPanel.Add(sensSlider);

            AddMenuButton(_settingsSubPanel, "Salvar e Voltar", () =>
            {
                PlayerPrefs.Save();
                ShowSubPanel(_mainPanel);
            }, "pause-btn-primary");
            _card.Add(_settingsSubPanel);

            RootElement.Add(_card);
            _root.Add(RootElement);
        }

        private VisualElement _card;

        private static void AddControlsGroup(VisualElement parent, string groupTitle, (string action, string key)[] items)
        {
            var groupBox = new VisualElement();
            groupBox.AddToClassList("controls-group-box");

            var header = new Label(groupTitle);
            header.AddToClassList("controls-group-title");
            groupBox.Add(header);

            for (int i = 0; i < items.Length; i++)
            {
                var row = new VisualElement();
                row.AddToClassList("controls-row");

                var actionLbl = new Label(items[i].action);
                actionLbl.AddToClassList("controls-action");

                var keyLbl = new Label(items[i].key);
                keyLbl.AddToClassList("controls-key");

                row.Add(actionLbl);
                row.Add(keyLbl);
                groupBox.Add(row);
            }

            parent.Add(groupBox);
        }

        private Button AddMenuButton(VisualElement parent, string text, Action onClick, string variantClass)
        {
            var btn = new Button(onClick) { text = text };
            btn.AddToClassList("pause-btn");
            if (!string.IsNullOrEmpty(variantClass))
                btn.AddToClassList(variantClass);
            UIFeedbackService.Instance?.BindButton(btn);
            UIToolkitTweenService.BindInteractiveFeedback(btn, 1.03f, 0.97f);
            parent.Add(btn);
            return btn;
        }

        private void ShowSubPanel(VisualElement target)
        {
            if (_mainPanel != null) _mainPanel.style.display = target == _mainPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_controlsSubPanel != null) _controlsSubPanel.style.display = target == _controlsSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_settingsSubPanel != null) _settingsSubPanel.style.display = target == _settingsSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (_confirmLeaveSubPanel != null) _confirmLeaveSubPanel.style.display = target == _confirmLeaveSubPanel ? DisplayStyle.Flex : DisplayStyle.None;
            if (target != null) UIToolkitTweenService.FadeIn(target, 0.14f);
        }

        private bool IsSubPanelVisible =>
            (_controlsSubPanel != null && _controlsSubPanel.style.display == DisplayStyle.Flex) ||
            (_settingsSubPanel != null && _settingsSubPanel.style.display == DisplayStyle.Flex) ||
            (_confirmLeaveSubPanel != null && _confirmLeaveSubPanel.style.display == DisplayStyle.Flex);

        private static bool WasEscapePressedThisFrame()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        public static bool ShouldBlockPauseOnEscape(
            bool hasStackModal,
            bool ticketOpen,
            bool backpackOpen,
            bool recipeBookOpen,
            bool deliverySelectOpen,
            bool deliveryResultOpen,
            bool nightResultsOpen,
            bool opsHubOpen,
            bool intermissionOpen,
            bool runPrepOpen,
            CutscenePlaybackState cutsceneState,
            bool blockingIntro,
            bool loadingOrErrorVisible)
        {
            return hasStackModal ||
                   ticketOpen ||
                   backpackOpen ||
                   recipeBookOpen ||
                   deliverySelectOpen ||
                   deliveryResultOpen ||
                   nightResultsOpen ||
                   opsHubOpen ||
                   intermissionOpen ||
                   runPrepOpen ||
                   cutsceneState != CutscenePlaybackState.Idle ||
                   blockingIntro ||
                   loadingOrErrorVisible;
        }

        private bool IsAnyOtherModalOpen()
        {
            var cutsceneState = CutscenePlaybackService.Instance != null
                ? CutscenePlaybackService.Instance.State
                : CutscenePlaybackState.Idle;
            var loadingCoord = PizzeriaSceneLoadingCoordinator.Instance;
            bool loadingOrError = loadingCoord != null &&
                                  (loadingCoord.IsLoading || loadingCoord.OperationState == LoadingOperationState.ErrorVisible);

            return ShouldBlockPauseOnEscape(
                UIModalStack.Instance != null && UIModalStack.Instance.HasActiveModal,
                TicketModalPresenter.Instance != null && TicketModalPresenter.Instance.IsOpen,
                BackpackPanelPresenter.Instance != null && BackpackPanelPresenter.Instance.IsOpen,
                RecipeBookPresenter.Instance != null && RecipeBookPresenter.Instance.IsOpen,
                DeliverySelectionPresenter.Instance != null && DeliverySelectionPresenter.Instance.IsOpen,
                DeliveryResultPresenter.Instance != null && DeliveryResultPresenter.Instance.IsOpen,
                NightResultsPresenter.Instance != null && NightResultsPresenter.Instance.IsOpen,
                OperationsHubPresenter.Instance != null && OperationsHubPresenter.Instance.IsOpen,
                NightIntermissionPresenter.Instance != null && NightIntermissionPresenter.Instance.IsOpen,
                RunPreparationPresenter.Instance != null && RunPreparationPresenter.Instance.IsOpen,
                cutsceneState,
                GameplayIntroCutsceneFlow.IsBlockingIntermission,
                loadingOrError);
        }

        private void Update()
        {
            bool otherModalOpen = IsAnyOtherModalOpen();
            bool escPressed = WasEscapePressedThisFrame();

            if (_isOpen)
            {
                UpdateRecoverButtonLabels();
                if (escPressed && Time.frameCount != _lastToggleFrame)
                {
                    if (IsSubPanelVisible)
                    {
                        _lastToggleFrame = Time.frameCount;
                        ShowSubPanel(_mainPanel);
                    }
                    else if (!_pushedToStack)
                    {
                        Close();
                    }
                }
                _otherModalWasOpenLastFrame = otherModalOpen;
                return;
            }

            if (escPressed && Time.frameCount != _lastToggleFrame && !otherModalOpen && !_otherModalWasOpenLastFrame)
            {
                Open();
            }

            _otherModalWasOpenLastFrame = otherModalOpen;
        }

        public void Open()
        {
            EnsureUIInitialized();
            if (RootElement == null || _isOpen) return;

            _isOpen = true;
            _lastToggleFrame = Time.frameCount;
            _openedUnscaledTime = Time.unscaledTime;
            ShowSubPanel(_mainPanel);
            UpdateRecoverButtonLabels();
            UIToolkitTweenService.PopIn(_card ?? RootElement, 0.18f, 0.95f);
            RootElement.style.display = DisplayStyle.Flex;

            if (UIModalStack.Instance != null)
            {
                UIModalStack.Instance.PushModal(this);
                _pushedToStack = true;
            }
            else
            {
                GameplayInputGate.PushLock();
                _pushedGateFallback = true;
            }
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _lastToggleFrame = Time.frameCount;

            if (RootElement != null)
                RootElement.style.display = DisplayStyle.None;

            ReleaseLock();
        }

        private void ReleaseLock()
        {
            if (_pushedToStack)
            {
                _pushedToStack = false;
                UIModalStack.Instance?.PopModal(this);
            }
            if (_pushedGateFallback)
            {
                _pushedGateFallback = false;
                GameplayInputGate.PopLock();
            }
        }

        private void UpdateRecoverButtonLabels()
        {
            float now = Time.unscaledTime;
            if (_recoverPlayerBtn != null)
            {
                float rem = _playerRecoverCooldownUntil - now;
                _recoverPlayerBtn.text = rem > 0.5f
                    ? $"Recuperar Jogador ({Mathf.CeilToInt(rem)}s)"
                    : "Recuperar Jogador (Pronto)";
                _recoverPlayerBtn.SetEnabled(rem <= 0f);
            }

            if (_recoverMotoBtn != null)
            {
                float rem = _motoRecoverCooldownUntil - now;
                _recoverMotoBtn.text = rem > 0.5f
                    ? $"Recuperar Moto ({Mathf.CeilToInt(rem)}s)"
                    : "Recuperar Moto (Pronto)";
                _recoverMotoBtn.SetEnabled(rem <= 0f);
            }
        }

        private void RequestRecoverPlayer()
        {
            if (Time.unscaledTime < _playerRecoverCooldownUntil) return;
            _playerRecoverCooldownUntil = Time.unscaledTime + 60f;
            PizzeriaCommandHandler.Instance?.RequestRecoverPlayerServerRpc();
            Close();
        }

        private void RequestRecoverMoto()
        {
            if (Time.unscaledTime < _motoRecoverCooldownUntil) return;
            _motoRecoverCooldownUntil = Time.unscaledTime + 60f;
            PizzeriaCommandHandler.Instance?.RequestRecoverMotorcycleServerRpc();
            Close();
        }

        private void LeaveMatch()
        {
            Close();
            GameplayInputGate.Reset();

            if (RoomDiscoveryService.Instance != null)
                RoomDiscoveryService.Instance.StopHosting();
            UgsLobbySessionService.StopHostingLobby();

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();

            ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance().TransitionToScene("LobbyScene");
        }
    }
}
