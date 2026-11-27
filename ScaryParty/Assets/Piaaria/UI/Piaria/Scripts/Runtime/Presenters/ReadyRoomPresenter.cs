using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class ReadyRoomPresenter : MonoBehaviour, IScreenView
    {
        public static ReadyRoomPresenter Instance { get; private set; }

        public string ScreenId => "ReadyRoomScreen";
        public VisualElement RootElement { get; private set; }
        public ScreenState State { get; private set; } = ScreenState.Hidden;

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;

        private struct SlotCardElements
        {
            public VisualElement SlotRoot;
            public VisualElement Occupied;
            public VisualElement Empty;
            public VisualElement NameBar;
            public Label NameLabel;
            public VisualElement ColorBox;
            public VisualElement PreviewRT;
            public Label AvatarIcon;
            public Label RoleLabel;
            public Label ReadyBadge;
            public bool LastKnownReady;
            public bool Initialized;
        }

        private readonly SlotCardElements[] _slots = new SlotCardElements[4];

        private VisualElement _readyCard;
        private VisualElement _titleWaitingRoom;
        private Label _roomCodeLabel;
        private Button _btnCopyCode;
        private ScrollView _playersContainer;

        private Button _btnReady;
        private Button _btnStartGame;
        private Button _btnLeaveRoom;
        private Button _btnEditProfile;
        private ProfileEditorController _profileEditor;
        private bool _profileEditorSyncedOnce;

        private float _lastUpdate = -999f;
        private string _currentRoomCode = "";
        private bool _offlineReady = false;
        private bool? _optimisticReadyState = null;
        private readonly HashSet<PlayerState> _observedPlayers = new HashSet<PlayerState>();
        private NetworkManager _hookedNm;
        private bool _isLeavingVoluntarily;
        private bool _handlingDisconnect;
        private int _boundPreviewSlotIndex = -1;
        private int _localPlayerSlotIndex = 0;

        // P4: Configuração da sessão pelo Host
        private VisualElement _hostConfigPanel;
        private Button _btnPreset;
        private Button _btnDifficulty;
        private Button _btnNights;
        private Button _btnTutorial;
        private TextField _seedField;
        private Label _hostConfigStatusLabel;
        private int _presetIndex = 0;
        private static readonly string[] BuiltInPresets = { "Entrega_3Noites", "Playtest_Integracao", "Tutorial_Noite0", "NoiteDeTeste" };

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
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/Piaaria/UI/Piaria/UXML/ReadyRoomScreen.uxml");
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
            var activeSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeSceneName.IndexOf("Lobby", StringComparison.OrdinalIgnoreCase) >= 0 &&
                activeSceneName.IndexOf("Ready", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Este presenter é exclusivo da sala de espera (ReadyScene). Na LobbyScene, desativa-se.
                enabled = false;
                return;
            }

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            EnsureInitialized();
            UIScreenRouter.Instance?.RegisterScreen(this);
            SessionLaunchConfig.OnConfigChanged += OnSessionLaunchConfigChanged;
            SessionLaunchConfig.EnsureNetworkHooksRegistered();
            EnsureDisconnectHooks();
            Open();
        }

        private void OnDestroy()
        {
            UnhookDisconnectCallbacks();
            SessionLaunchConfig.OnConfigChanged -= OnSessionLaunchConfigChanged;
            foreach (var ps in _observedPlayers)
            {
                if (ps != null)
                {
                    ps.IsReady.OnValueChanged -= OnPlayerReadyChanged;
                    ps.PlayerName.OnValueChanged -= OnPlayerNameChanged;
                    ps.PlayerColor.OnValueChanged -= OnPlayerColorChanged;
                }
            }
            _observedPlayers.Clear();

            UIScreenRouter.Instance?.UnregisterScreen(this);
            if (Instance == this) Instance = null;
        }

        private void EnsureDisconnectHooks()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (ReferenceEquals(_hookedNm, nm)) return;

            UnhookDisconnectCallbacks();
            _hookedNm = nm;
            _hookedNm.OnClientDisconnectCallback += HandleNetworkClientDisconnected;
            _hookedNm.OnClientStopped += HandleNetworkClientStopped;
        }

        private void UnhookDisconnectCallbacks()
        {
            if (_hookedNm == null) return;
            try { _hookedNm.OnClientDisconnectCallback -= HandleNetworkClientDisconnected; } catch { }
            try { _hookedNm.OnClientStopped -= HandleNetworkClientStopped; } catch { }
            _hookedNm = null;
        }

        private void HandleNetworkClientDisconnected(ulong clientId)
        {
            if (_isLeavingVoluntarily || _handlingDisconnect) return;
            var nm = NetworkManager.Singleton ?? _hookedNm;
            if (nm == null || nm.IsServer) return;
            if (clientId != nm.LocalClientId && clientId != NetworkManager.ServerClientId) return;

            string reason = !string.IsNullOrWhiteSpace(nm.DisconnectReason)
                ? nm.DisconnectReason
                : "O líder encerrou a sala ou a conexão foi perdida.";
            TriggerUnexpectedDisconnectReturn(reason);
        }

        private void HandleNetworkClientStopped(bool isHost)
        {
            if (_isLeavingVoluntarily || _handlingDisconnect || isHost) return;
            var nm = NetworkManager.Singleton ?? _hookedNm;
            string reason = nm != null && !string.IsNullOrWhiteSpace(nm.DisconnectReason)
                ? nm.DisconnectReason
                : "Conexão com a sala encerrada.";
            TriggerUnexpectedDisconnectReturn(reason);
        }

        private void TriggerUnexpectedDisconnectReturn(string reason)
        {
            _handlingDisconnect = true;
            CharacterPreviewRig.Instance?.SetActive(false);
            NotificationManager.Show(reason, NotificationType.Warning);
            PizzeriaSceneLoadingCoordinator.EnsureInstance().TransitionToScene("LobbyScene");
        }

        private void OnPlayerReadyChanged(bool previous, bool current)
        {
            RefreshPlayers();
        }

        private void OnPlayerNameChanged(Unity.Collections.FixedString32Bytes previous, Unity.Collections.FixedString32Bytes current)
        {
            RefreshPlayers();
        }

        private void OnPlayerColorChanged(Color previous, Color current)
        {
            RefreshPlayers();
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("ReadyRoomRoot");
            if (RootElement == null)
            {
                var template = PiariaPresenterUtils.ResolveTemplate(
                    c => c.templateReadyRoom,
                    "Assets/Piaaria/UI/Piaria/UXML/ReadyRoomScreen.uxml");
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("ReadyRoomRoot");
                }
            }
            if (RootElement == null) return;

            _readyCard = RootElement.Q<VisualElement>("ReadyCard");
            _titleWaitingRoom = RootElement.Q<VisualElement>("TitleWaitingRoom");
            _roomCodeLabel = RootElement.Q<Label>("RoomCodeLabel");
            _btnCopyCode = RootElement.Q<Button>("BtnCopyCode");
            _playersContainer = RootElement.Q<ScrollView>("PlayersContainer");

            _btnReady = RootElement.Q<Button>("BtnReady");
            _btnStartGame = RootElement.Q<Button>("BtnStartGame");
            _btnLeaveRoom = RootElement.Q<Button>("BtnLeaveRoom");
            _btnEditProfile = RootElement.Q<Button>("BtnEditProfile");

            PiariaPresenterUtils.RegisterResponsiveLayout(RootElement);

            _profileEditor = new ProfileEditorController(RootElement);
            _profileEditor.LoadFromPrefsOrData(Resources.Load<PlayerData>("PlayerData"));
            _profileEditor.OnProfileSaved += HandleLocalProfileSaved;

            // 4 Cartões persistentes
            for (int i = 0; i < 4; i++)
            {
                var slotRoot = RootElement.Q<VisualElement>($"SlotCard{i}");
                if (slotRoot != null)
                {
                    _slots[i] = new SlotCardElements
                    {
                        SlotRoot = slotRoot,
                        Occupied = slotRoot.Q<VisualElement>($"CardOccupied{i}"),
                        Empty = slotRoot.Q<VisualElement>($"CardEmpty{i}"),
                        NameBar = slotRoot.Q<VisualElement>($"PlayerNameBar{i}"),
                        NameLabel = slotRoot.Q<Label>($"PlayerNameLabel{i}"),
                        ColorBox = slotRoot.Q<VisualElement>($"PlayerColor{i}"),
                        PreviewRT = slotRoot.Q<VisualElement>($"PlayerPreviewRT{i}"),
                        AvatarIcon = slotRoot.Q<Label>($"PlayerAvatarIcon{i}"),
                        RoleLabel = slotRoot.Q<Label>($"PlayerRoleLabel{i}"),
                        ReadyBadge = slotRoot.Q<Label>($"PlayerReadyBadge{i}"),
                        LastKnownReady = false,
                        Initialized = true
                    };
                }
            }

            ApplyCatalogAssets();
            EnsureHostLaunchConfigPanel();
            BindEvents();
            UpdateRoomCodeDisplay();
            RefreshHostLaunchConfigUi();
        }

        private void OnSessionLaunchConfigChanged(SessionLaunchConfigData _)
        {
            RefreshHostLaunchConfigUi();
        }

        private void EnsureHostLaunchConfigPanel()
        {
            if (RootElement == null || _hostConfigPanel != null) return;

            _hostConfigPanel = new VisualElement { name = "HostLaunchConfigPanel" };
            _hostConfigPanel.style.flexDirection = FlexDirection.Row;
            _hostConfigPanel.style.flexWrap = Wrap.Wrap;
            _hostConfigPanel.style.alignItems = Align.Center;
            _hostConfigPanel.style.justifyContent = Justify.Center;
            _hostConfigPanel.style.backgroundColor = new Color(0.09f, 0.10f, 0.14f, 0.90f);
            _hostConfigPanel.style.borderTopWidth = 1;
            _hostConfigPanel.style.borderBottomWidth = 1;
            _hostConfigPanel.style.borderLeftWidth = 1;
            _hostConfigPanel.style.borderRightWidth = 1;
            var border = new Color(0.90f, 0.68f, 0.28f, 0.85f);
            _hostConfigPanel.style.borderTopColor = border;
            _hostConfigPanel.style.borderBottomColor = border;
            _hostConfigPanel.style.borderLeftColor = border;
            _hostConfigPanel.style.borderRightColor = border;
            _hostConfigPanel.style.borderTopLeftRadius = 8;
            _hostConfigPanel.style.borderTopRightRadius = 8;
            _hostConfigPanel.style.borderBottomLeftRadius = 8;
            _hostConfigPanel.style.borderBottomRightRadius = 8;
            _hostConfigPanel.style.paddingLeft = 12;
            _hostConfigPanel.style.paddingRight = 12;
            _hostConfigPanel.style.paddingTop = 6;
            _hostConfigPanel.style.paddingBottom = 6;
            _hostConfigPanel.style.marginTop = 6;
            _hostConfigPanel.style.marginBottom = 6;
            _hostConfigPanel.style.alignSelf = Align.Center;

            _hostConfigStatusLabel = new Label("Configuração da RUN:");
            _hostConfigStatusLabel.style.fontSize = 13;
            _hostConfigStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _hostConfigStatusLabel.style.color = new Color(0.98f, 0.84f, 0.42f);
            _hostConfigStatusLabel.style.marginRight = 8;
            _hostConfigPanel.Add(_hostConfigStatusLabel);

            _btnPreset = CreateConfigButton(CyclePreset);
            _btnDifficulty = CreateConfigButton(CycleDifficulty);
            _btnNights = CreateConfigButton(CycleMaxNights);
            _btnTutorial = CreateConfigButton(ToggleTutorialNight0);

            _seedField = new TextField("Seed:") { value = SessionLaunchConfig.Current.SeedOverride.ToString() };
            _seedField.style.width = 130;
            _seedField.style.marginLeft = 6;
            _seedField.style.color = Color.white;
            _seedField.RegisterValueChangedCallback(evt =>
            {
                if (!IsLocalHostAuthority()) return;
                if (int.TryParse(evt.newValue, out int parsed))
                {
                    var cur = SessionLaunchConfig.Current;
                    SessionLaunchConfig.ApplyHostSelection(cur.PresetId, cur.Difficulty, cur.MaxNights, parsed, cur.IncludeNight0Tutorial);
                }
            });
            _hostConfigPanel.Add(_seedField);

            var actionsBar = RootElement.Q<VisualElement>(className: "ready-actions-bar");
            if (actionsBar != null && actionsBar.parent != null)
            {
                int idx = actionsBar.parent.IndexOf(actionsBar);
                actionsBar.parent.Insert(idx, _hostConfigPanel);
            }
            else
            {
                RootElement.Add(_hostConfigPanel);
            }
        }

        private Button CreateConfigButton(Action onClick)
        {
            var btn = new Button(onClick);
            btn.style.height = 28;
            btn.style.marginLeft = 4;
            btn.style.marginRight = 4;
            btn.style.paddingLeft = 10;
            btn.style.paddingRight = 10;
            btn.style.fontSize = 12;
            btn.style.backgroundColor = new Color(0.22f, 0.28f, 0.38f);
            btn.style.color = Color.white;
            btn.style.borderTopLeftRadius = 5;
            btn.style.borderTopRightRadius = 5;
            btn.style.borderBottomLeftRadius = 5;
            btn.style.borderBottomRightRadius = 5;
            _hostConfigPanel.Add(btn);
            return btn;
        }

        private static bool IsLocalHostAuthority()
            => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        private void CyclePreset()
        {
            if (!IsLocalHostAuthority()) return;
            _presetIndex = (_presetIndex + 1) % BuiltInPresets.Length;
            string pid = BuiltInPresets[_presetIndex];
            bool tut = pid == "Tutorial_Noite0";
            var cur = SessionLaunchConfig.Current;
            SessionLaunchConfig.ApplyHostSelection(pid, cur.Difficulty, cur.MaxNights, cur.SeedOverride, tut);
        }

        private void CycleDifficulty()
        {
            if (!IsLocalHostAuthority()) return;
            var cur = SessionLaunchConfig.Current;
            var next = cur.Difficulty == RunDifficulty.Hard
                ? RunDifficulty.Normal
                : RunDifficulty.Hard;
            SessionLaunchConfig.ApplyHostSelection(cur.PresetId, next, cur.MaxNights, cur.SeedOverride, cur.IncludeNight0Tutorial);
        }

        private void CycleMaxNights()
        {
            if (!IsLocalHostAuthority()) return;
            var cur = SessionLaunchConfig.Current;
            int nextNights = cur.MaxNights switch
            {
                1 => 2,
                2 => 3,
                3 => 5,
                _ => 1
            };
            SessionLaunchConfig.ApplyHostSelection(cur.PresetId, cur.Difficulty, nextNights, cur.SeedOverride, cur.IncludeNight0Tutorial);
        }

        private void ToggleTutorialNight0()
        {
            if (!IsLocalHostAuthority()) return;
            var cur = SessionLaunchConfig.Current;
            SessionLaunchConfig.ApplyHostSelection(cur.PresetId, cur.Difficulty, cur.MaxNights, cur.SeedOverride, !cur.IncludeNight0Tutorial);
        }

        private void RefreshHostLaunchConfigUi()
        {
            if (_hostConfigPanel == null) return;
            bool isHost = IsLocalHostAuthority();
            var cfg = SessionLaunchConfig.Current;

            if (_hostConfigStatusLabel != null)
                _hostConfigStatusLabel.text = isHost ? "Configuração (Host):" : "Configuração do Host (somente leitura):";
            if (_btnPreset != null)
            {
                string rawPreset = string.IsNullOrEmpty(cfg.PresetId) ? "Entrega_3Noites" : cfg.PresetId;
                string label = rawPreset == "NoiteDeTeste" ? "Noite de Teste — sem limite de tempo" : rawPreset;
                _btnPreset.text = $"Preset: {label}";
                _btnPreset.SetEnabled(isHost);
            }
            if (_btnDifficulty != null)
            {
                _btnDifficulty.text = $"Dificuldade: {cfg.Difficulty}";
                _btnDifficulty.SetEnabled(isHost);
            }
            if (_btnNights != null)
            {
                _btnNights.text = $"Noites: {Mathf.Clamp(cfg.MaxNights, 1, 7)}";
                _btnNights.SetEnabled(isHost);
            }
            if (_btnTutorial != null)
            {
                _btnTutorial.text = $"Tutorial Noite 0: {(cfg.IncludeNight0Tutorial ? "SIM" : "NÃO")}";
                _btnTutorial.SetEnabled(isHost);
            }
            if (_seedField != null)
            {
                _seedField.SetEnabled(isHost);
                string seedTxt = cfg.SeedOverride.ToString();
                if (_seedField.value != seedTxt) _seedField.SetValueWithoutNotify(seedTxt);
            }
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            if (_titleWaitingRoom != null && catalog.titleWaitingRoom != null)
                _titleWaitingRoom.style.backgroundImage = new StyleBackground(catalog.titleWaitingRoom);

            if (_readyCard != null && catalog.waitingPaperPanel != null)
                _readyCard.style.backgroundImage = new StyleBackground(catalog.waitingPaperPanel);

            var colProfile = RootElement.Q<VisualElement>("ColProfile");
            if (colProfile != null && catalog.paperPanel1 != null)
                colProfile.style.backgroundImage = new StyleBackground(catalog.paperPanel1);

            _profileEditor?.ApplyCatalogAssets();

            for (int i = 0; i < 4; i++)
            {
                if (!_slots[i].Initialized) continue;
                var barSprite = catalog.GetPlayerNameBar(i);
                if (_slots[i].NameBar != null && barSprite != null)
                {
                    _slots[i].NameBar.style.backgroundImage = new StyleBackground(barSprite);
                }
            }
        }

        private void BindEvents()
        {
            if (_btnCopyCode != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCopyCode);
                UIToolkitTweenService.BindInteractiveFeedback(_btnCopyCode, 1.04f, 0.96f);
                _btnCopyCode.clicked += () =>
                {
                    if (!string.IsNullOrEmpty(_currentRoomCode))
                    {
                        GUIUtility.systemCopyBuffer = _currentRoomCode;
                        _btnCopyCode.text = "COPIADO!";
                        UIFeedbackService.Instance?.PlaySuccess();
                    }
                };
            }

            if (_btnReady != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnReady);
                UIToolkitTweenService.BindInteractiveFeedback(_btnReady, 1.03f, 0.97f);
                _btnReady.clicked += OnReadyClicked;
            }

            if (_btnStartGame != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnStartGame);
                UIToolkitTweenService.BindInteractiveFeedback(_btnStartGame, 1.03f, 0.97f);
                _btnStartGame.clicked += OnStartGameClicked;
            }

            if (_btnLeaveRoom != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnLeaveRoom);
                UIToolkitTweenService.BindInteractiveFeedback(_btnLeaveRoom, 1.03f, 0.97f);
                _btnLeaveRoom.clicked += OnLeaveRoomClicked;
            }

            if (_btnEditProfile != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnEditProfile);
                UIToolkitTweenService.BindInteractiveFeedback(_btnEditProfile, 1.03f, 0.97f);
                _btnEditProfile.clicked += () =>
                {
                    RootElement?.Q<TextField>("PlayerNameInput")?.Focus();
                };
            }
        }

        private void HandleLocalProfileSaved(string savedName, Color savedColor)
        {
            var localPs = GetLocalPlayerState();
            bool currentlyReady = _optimisticReadyState ?? (localPs != null ? localPs.IsReady.Value : _offlineReady);

            if (currentlyReady)
            {
                _optimisticReadyState = false;
                _offlineReady = false;
                if (localPs != null)
                {
                    localPs.SetReady(false);
                }
                ApplyLocalReadyUI(false);
                NotificationManager.Show("Perfil alterado: 'Pronto' desmarcado para confirmação.", NotificationType.Info);
            }

            _profileEditor?.SaveProfileToStorage(Resources.Load<PlayerData>("PlayerData"));

            if (localPs != null)
            {
                localPs.UpdateProfile(savedName, savedColor);
            }

            NotificationManager.Show($"Perfil atualizado: {savedName}", NotificationType.Success);
            RefreshPlayers();
        }

        private void UpdateRoomCodeDisplay()
        {
            if (!string.IsNullOrEmpty(LobbyManager.CurrentJoinCode))
            {
                _currentRoomCode = LobbyManager.CurrentJoinCode;
            }
            else
            {
                _currentRoomCode = RoomDiscoveryService.GetLocalIPAddress();
            }

            if (_roomCodeLabel != null)
            {
                _roomCodeLabel.text = $"CÓDIGO DA SALA: {_currentRoomCode}";
            }
        }

        private void Update()
        {
            if (State == ScreenState.Active || State == ScreenState.Opening)
            {
                if (UnityEngine.Cursor.lockState != CursorLockMode.None)
                {
                    UnityEngine.Cursor.lockState = CursorLockMode.None;
                }
                if (!UnityEngine.Cursor.visible)
                {
                    UnityEngine.Cursor.visible = true;
                }
            }

            if (RootElement == null || RootElement.style.display == DisplayStyle.None) return;
            if (Time.time - _lastUpdate < 0.25f) return;
            _lastUpdate = Time.time;

            RefreshPlayers();
        }

        private void RefreshPlayers()
        {
            var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            Array.Sort(allPlayers, (a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                if (a.OwnerClientId == 0) return -1;
                if (b.OwnerClientId == 0) return 1;
                return a.OwnerClientId.CompareTo(b.OwnerClientId);
            });

            // Inscreve ouvintes de atualização imediata para cada jogador encontrado
            foreach (var ps in allPlayers)
            {
                if (ps != null && _observedPlayers.Add(ps))
                {
                    ps.IsReady.OnValueChanged += OnPlayerReadyChanged;
                    ps.PlayerName.OnValueChanged += OnPlayerNameChanged;
                    ps.PlayerColor.OnValueChanged += OnPlayerColorChanged;
                }
            }

            var localPs = GetLocalPlayerState();
            if (!_profileEditorSyncedOnce && localPs != null && _profileEditor != null)
            {
                _profileEditorSyncedOnce = true;
                _profileEditor.SetProfileValuesWithoutNotify(localPs.PlayerName.Value.ToString(), localPs.PlayerColor.Value);
            }

            // Sincroniza estado otimista se a rede já confirmou
            if (_optimisticReadyState.HasValue && localPs != null)
            {
                if (localPs.IsReady.Value == _optimisticReadyState.Value)
                {
                    _optimisticReadyState = null;
                }
            }

            bool allReady = true;
            int playerCount = allPlayers.Length;

            for (int i = 0; i < 4; i++)
            {
                if (!_slots[i].Initialized) continue;

                if (i < playerCount && allPlayers[i] != null)
                {
                    var ps = allPlayers[i];
                    bool isHost = ps.OwnerClientId == 0;
                    bool isMe = (localPs != null && ps == localPs) || ps.IsOwner;

                    bool isReady = (isMe && _optimisticReadyState.HasValue)
                        ? _optimisticReadyState.Value
                        : ps.IsReady.Value;

                    if (_slots[i].Occupied != null) _slots[i].Occupied.style.display = DisplayStyle.Flex;
                    if (_slots[i].Empty != null) _slots[i].Empty.style.display = DisplayStyle.None;

                    string pName = ps.PlayerName.Value.ToString();
                    if (string.IsNullOrWhiteSpace(pName)) pName = $"Jogador #{ps.OwnerClientId}";
                    if (_slots[i].NameLabel != null)
                    {
                        _slots[i].NameLabel.text = isHost ? $"👑 {pName}" : pName;
                    }

                    if (_slots[i].ColorBox != null)
                    {
                        _slots[i].ColorBox.style.backgroundColor = ps.PlayerColor.Value;
                    }

                    if (isMe)
                    {
                        _localPlayerSlotIndex = i;
                        SetupPreviewRig(i, ps.PlayerColor.Value);
                    }
                    else
                    {
                        ClearPreviewFromSlot(i);
                    }

                    if (_slots[i].RoleLabel != null)
                    {
                        _slots[i].RoleLabel.text = isHost ? "👑 Líder da Sala" : (i == 1 ? "🍕 Pizzaiolo" : (i == 2 ? "🛵 Entregador" : "🧑‍🍳 Ajudante"));
                    }

                    if (_slots[i].ReadyBadge != null)
                    {
                        if (isReady)
                        {
                            _slots[i].ReadyBadge.text = "PRONTO!";
                            _slots[i].ReadyBadge.RemoveFromClassList("status-waiting");
                            _slots[i].ReadyBadge.AddToClassList("status-ready");
                            if (!_slots[i].LastKnownReady)
                            {
                                UIFeedbackService.Instance?.PlayReady();
                            }
                        }
                        else
                        {
                            _slots[i].ReadyBadge.text = "PREPARANDO...";
                            _slots[i].ReadyBadge.RemoveFromClassList("status-ready");
                            _slots[i].ReadyBadge.AddToClassList("status-waiting");
                            allReady = false;
                        }
                    }

                    _slots[i].LastKnownReady = isReady;
                }
                else
                {
                    ClearPreviewFromSlot(i);
                    if (_slots[i].Occupied != null) _slots[i].Occupied.style.display = DisplayStyle.None;
                    if (_slots[i].Empty != null) _slots[i].Empty.style.display = DisplayStyle.Flex;
                    _slots[i].LastKnownReady = false;
                }
            }

            // Fallback legado se a tela usar PlayersContainer
            if (_playersContainer != null && !_slots[0].Initialized)
            {
                _playersContainer.Clear();
                var catalog = PiariaUIAssetCatalog.Instance;
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    var ps = allPlayers[i];
                    if (ps == null) continue;
                    var row = new VisualElement();
                    row.AddToClassList("player-slot-row");
                    var bgSprite = catalog != null ? catalog.GetPlayerNameBar(i) : null;
                    if (bgSprite != null) row.style.backgroundImage = new StyleBackground(bgSprite);

                    var colorDot = new VisualElement();
                    colorDot.AddToClassList("player-color-indicator");
                    colorDot.style.backgroundColor = ps.PlayerColor.Value;

                    string pName = ps.PlayerName.Value.ToString();
                    if (string.IsNullOrWhiteSpace(pName)) pName = $"Jogador #{ps.OwnerClientId}";
                    var nameLbl = new Label(ps.OwnerClientId == 0 ? $"👑 {pName} (Host)" : pName);
                    nameLbl.AddToClassList("player-name-text");

                    bool isMe = (localPs != null && ps == localPs) || ps.IsOwner;
                    bool isReady = (isMe && _optimisticReadyState.HasValue) ? _optimisticReadyState.Value : ps.IsReady.Value;
                    var statusLbl = new Label(isReady ? "✅ PRONTO" : "⏳ AGUARDANDO...");
                    statusLbl.AddToClassList("player-status-tag");
                    statusLbl.AddToClassList(isReady ? "status-ready" : "status-waiting");

                    row.Add(colorDot);
                    row.Add(nameLbl);
                    row.Add(statusLbl);
                    _playersContainer.Add(row);
                    if (!isReady) allReady = false;
                }
            }

            // Se não houver jogadores instanciados em rede ainda, exibe o host local no Slot 0
            if (playerCount == 0 && _slots[0].Initialized)
            {
                if (_slots[0].Occupied != null) _slots[0].Occupied.style.display = DisplayStyle.Flex;
                if (_slots[0].Empty != null) _slots[0].Empty.style.display = DisplayStyle.None;

                string localName = PlayerPrefs.GetString("PlayerName", "rubens");
                var pd = Resources.Load<PlayerData>("PlayerData");
                Color localColor = pd != null ? pd.PlayerColor : new Color(1f, 0.45f, 0.1f);
                if (PlayerPrefs.HasKey("PlayerColorR"))
                {
                    localColor = new Color(
                        PlayerPrefs.GetFloat("PlayerColorR"),
                        PlayerPrefs.GetFloat("PlayerColorG"),
                        PlayerPrefs.GetFloat("PlayerColorB")
                    );
                }

                if (_slots[0].NameLabel != null) _slots[0].NameLabel.text = $"👑 {localName}";
                if (_slots[0].ColorBox != null) _slots[0].ColorBox.style.backgroundColor = localColor;
                if (_slots[0].RoleLabel != null) _slots[0].RoleLabel.text = "👑 Líder da Sala";

                _localPlayerSlotIndex = 0;
                SetupPreviewRig(0, localColor);
                bool isReady = _optimisticReadyState ?? _offlineReady;
                if (_slots[0].ReadyBadge != null)
                {
                    if (isReady)
                    {
                        _slots[0].ReadyBadge.text = "PRONTO!";
                        _slots[0].ReadyBadge.RemoveFromClassList("status-waiting");
                        _slots[0].ReadyBadge.AddToClassList("status-ready");
                    }
                    else
                    {
                        _slots[0].ReadyBadge.text = "PREPARANDO...";
                        _slots[0].ReadyBadge.RemoveFromClassList("status-ready");
                        _slots[0].ReadyBadge.AddToClassList("status-waiting");
                    }
                }
            }

            // Atualiza texto e estilo do botão Pronto para o jogador local
            bool effectiveMyReady = _optimisticReadyState ?? (localPs != null ? localPs.IsReady.Value : _offlineReady);
            if (_btnReady != null)
            {
                _btnReady.text = effectiveMyReady ? "CANCELAR PRONTO" : "ESTOU PRONTO";
                if (effectiveMyReady)
                {
                    _btnReady.RemoveFromClassList("piaria-button-success");
                    _btnReady.AddToClassList("piaria-button-secondary");
                }
                else
                {
                    _btnReady.RemoveFromClassList("piaria-button-secondary");
                    _btnReady.AddToClassList("piaria-button-success");
                }
            }

            // Botão Iniciar Jogo (somente Host)
            if (_btnStartGame != null)
            {
                bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
                _btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
                if (isHost)
                {
                    bool canStart = (playerCount > 0)
                        ? (allReady && effectiveMyReady)
                        : effectiveMyReady;
                    _btnStartGame.SetEnabled(canStart);
                }
            }
        }

        private void ApplyLocalReadyUI(bool ready)
        {
            _offlineReady = ready;

            if (_btnReady != null)
            {
                _btnReady.text = ready ? "CANCELAR PRONTO" : "ESTOU PRONTO";
                if (ready)
                {
                    _btnReady.RemoveFromClassList("piaria-button-success");
                    _btnReady.AddToClassList("piaria-button-secondary");
                }
                else
                {
                    _btnReady.RemoveFromClassList("piaria-button-secondary");
                    _btnReady.AddToClassList("piaria-button-success");
                }
            }

            int targetSlot = (_localPlayerSlotIndex >= 0 && _localPlayerSlotIndex < _slots.Length) ? _localPlayerSlotIndex : 0;
            if (_slots[targetSlot].Initialized && _slots[targetSlot].ReadyBadge != null)
            {
                _slots[targetSlot].ReadyBadge.text = ready ? "PRONTO!" : "PREPARANDO...";
                if (ready)
                {
                    _slots[targetSlot].ReadyBadge.RemoveFromClassList("status-waiting");
                    _slots[targetSlot].ReadyBadge.AddToClassList("status-ready");
                }
                else
                {
                    _slots[targetSlot].ReadyBadge.RemoveFromClassList("status-ready");
                    _slots[targetSlot].ReadyBadge.AddToClassList("status-waiting");
                }
            }

            bool isHost = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (_btnStartGame != null && isHost)
            {
                _btnStartGame.SetEnabled(ready);
            }
        }

        private void OnReadyClicked()
        {
            var myState = GetLocalPlayerState();
            bool targetReady;

            if (_optimisticReadyState.HasValue)
            {
                targetReady = !_optimisticReadyState.Value;
            }
            else if (myState != null)
            {
                targetReady = !myState.IsReady.Value;
            }
            else
            {
                targetReady = !_offlineReady;
            }

            _optimisticReadyState = targetReady;
            _offlineReady = targetReady;

            Debug.Log($"[ReadyRoomPresenter] ✅ Ready alternado: novo={targetReady}, jogador={(myState != null ? myState.OwnerClientId.ToString() : "offline")}");

            if (myState != null)
            {
                myState.SetReady(targetReady);
            }

            ApplyLocalReadyUI(targetReady);
            UIFeedbackService.Instance?.PlayReady();
            RefreshPlayers();
        }

        private void OnStartGameClicked()
        {
            var nm = NetworkManager.Singleton;
            bool isNetworkingActive = nm != null && (nm.IsListening || nm.IsClient || nm.IsServer);

            // A01: Cliente conectado NUNCA inicia carga local nem fallback de SceneManager.
            if (isNetworkingActive && !nm.IsServer)
            {
                Debug.LogWarning("[ReadyRoomPresenter] Cliente conectado não pode iniciar a partida manualmente.");
                if (_btnStartGame != null) _btnStartGame.SetEnabled(false);
                ModalPresenter.Instance?.ShowAlert("Aguarde o Líder", "Apenas o líder da sala (Host) pode iniciar o turno.");
                return;
            }

            var myState = GetLocalPlayerState();
            if (isNetworkingActive)
            {
                // Valida prontidão real no servidor antes de iniciar transição em rede
                var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
                bool anyNotReady = false;
                for (int i = 0; i < allPlayers.Length; i++)
                {
                    var ps = allPlayers[i];
                    if (ps == null) continue;
                    bool isMe = (myState != null && ps == myState) || ps.IsOwner;
                    bool ready = isMe ? (_optimisticReadyState ?? ps.IsReady.Value) : ps.IsReady.Value;
                    if (!ready)
                    {
                        anyNotReady = true;
                        break;
                    }
                }

                if (anyNotReady || allPlayers.Length == 0)
                {
                    ModalPresenter.Instance?.ShowAlert("Equipe Não Pronta", "Todos os jogadores conectados precisam confirmar que estão prontos.");
                    if (_btnStartGame != null) _btnStartGame.SetEnabled(true);
                    return;
                }
            }
            else if (!_offlineReady && (myState == null || !myState.IsReady.Value))
            {
                ModalPresenter.Instance?.ShowAlert("Não Pronto", "Marque 'ESTOU PRONTO' antes de iniciar o turno offline.");
                if (_btnStartGame != null) _btnStartGame.SetEnabled(true);
                return;
            }

            if (_btnStartGame != null) _btnStartGame.SetEnabled(false);

            if (CharacterPreviewRig.Instance != null)
            {
                Destroy(CharacterPreviewRig.Instance.gameObject);
            }

            if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();
            UgsLobbySessionService.StopHostingLobby();

            UIFeedbackService.Instance?.PlaySuccess();

            if (myState != null && !myState.IsReady.Value)
            {
                myState.SetReady(true);
            }

            string targetScene = ScaryParty.VisualStyles.VisualStyleRuntimeConfig.GetGameplaySceneName();
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                targetScene = ScaryParty.VisualStyles.VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
            }

            var cfg = SessionLaunchConfig.Current;
            int initialNight = cfg.HasHostSelection && cfg.IncludeNight0Tutorial ? 0 : 1;
            var nightPreview = ScaryParty.Pizzeria.Domain.Services.NightCycleService.GetDefaultParameters(Math.Max(1, initialNight));
            var coordinator = PizzeriaSceneLoadingCoordinator.EnsureInstance();

            if (isNetworkingActive)
            {
                if (nm.SceneManager == null)
                {
                    Debug.LogError("[ReadyRoomPresenter] NetworkSceneManager indisponível no servidor.");
                    if (_btnStartGame != null) _btnStartGame.SetEnabled(true);
                    ModalPresenter.Instance?.ShowAlert("Erro de Rede", "Gerenciador de cena de rede indisponível.");
                    return;
                }

                SessionLaunchConfig.TryBroadcastToAllClients();
                var status = coordinator.BeginNetworkedTransition(targetScene, initialNight, nightPreview.RevenueGoal);
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogError($"[ReadyRoomPresenter] LoadScene em rede falhou com status: {status}");
                    if (_btnStartGame != null) _btnStartGame.SetEnabled(true);
                    ModalPresenter.Instance?.ShowAlert("Erro ao Iniciar", $"Não foi possível carregar a cena em rede ({status}).");
                }
            }
            else
            {
                coordinator.SetNightContext(initialNight, nightPreview.RevenueGoal);
                coordinator.TransitionToScene(targetScene);
            }
        }

        private void OnLeaveRoomClicked()
        {
            Action doLeave = () =>
            {
                _isLeavingVoluntarily = true;
                CharacterPreviewRig.Instance?.SetActive(false);
                if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();
                UgsLobbySessionService.StopHostingLobby();
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance().TransitionToScene("LobbyScene");
            };

            if (ModalPresenter.Instance != null)
            {
                ModalPresenter.Instance.ShowConfirmation("Deseja sair da sala de espera?", doLeave);
            }
            else
            {
                doLeave();
            }
        }

        private PlayerState GetLocalPlayerState()
        {
            if (NetworkManager.Singleton?.SpawnManager != null)
            {
                var po = NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject();
                if (po != null)
                {
                    var ps = po.GetComponent<PlayerState>();
                    if (ps != null) return ps;
                }
            }

            var all = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            foreach (var ps in all)
            {
                if (ps != null && ps.IsOwner) return ps;
            }

            // Fallback: se houver exatamente 1 jogador e formos o Host/Server, é o jogador local
            if (all.Length == 1 && (NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer))
            {
                return all[0];
            }

            return null;
        }

        private void ClearPreviewFromSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) return;
            if (_slots[slotIndex].PreviewRT != null)
            {
                _slots[slotIndex].PreviewRT.style.backgroundImage = StyleKeyword.None;
                _slots[slotIndex].PreviewRT.style.display = DisplayStyle.None;
            }
            if (_slots[slotIndex].AvatarIcon != null)
            {
                _slots[slotIndex].AvatarIcon.style.display = DisplayStyle.Flex;
            }
            if (_boundPreviewSlotIndex == slotIndex)
            {
                _boundPreviewSlotIndex = -1;
            }
        }

        private void SetupPreviewRig(int slotIndex, Color playerColor)
        {
            if (slotIndex < 0 || slotIndex >= _slots.Length) slotIndex = 0;

            if (_boundPreviewSlotIndex >= 0 && _boundPreviewSlotIndex != slotIndex)
            {
                ClearPreviewFromSlot(_boundPreviewSlotIndex);
            }

            if (CharacterPreviewRig.Instance == null)
            {
                var existingRig = FindFirstObjectByType<CharacterPreviewRig>();
                if (existingRig == null)
                {
                    var rigGo = new GameObject("CharacterPreviewRig");
                    rigGo.AddComponent<CharacterPreviewRig>();
                }
            }

            if (CharacterPreviewRig.Instance != null)
            {
                CharacterPreviewRig.Instance.SetActive(true);
                _profileEditor?.ActivatePreview();
                var rt = CharacterPreviewRig.Instance.RenderTexture;
                var targetSlot = _slots[slotIndex];
                if (rt != null && targetSlot.PreviewRT != null)
                {
                    targetSlot.PreviewRT.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                    targetSlot.PreviewRT.style.display = DisplayStyle.Flex;
                    if (targetSlot.AvatarIcon != null)
                    {
                        targetSlot.AvatarIcon.style.display = DisplayStyle.None;
                    }
                    _boundPreviewSlotIndex = slotIndex;
                }
                CharacterPreviewRig.Instance.SetPlayerColor(_profileEditor != null ? _profileEditor.SelectedColor : playerColor);
            }
        }

        public void Open(Action onComplete = null)
        {
            PizzeriaSceneLoadingCoordinator.EnsureInstance();
            EnsureInitialized();
            EnsureDisconnectHooks();
            if (RootElement == null)
            {
                onComplete?.Invoke();
                return;
            }

            State = ScreenState.Opening;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            SessionLaunchConfig.EnsureNetworkHooksRegistered();
            UpdateRoomCodeDisplay();
            RefreshPlayers();
            RefreshHostLaunchConfigUi();

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
                onComplete?.Invoke();
            });
        }
    }
}
