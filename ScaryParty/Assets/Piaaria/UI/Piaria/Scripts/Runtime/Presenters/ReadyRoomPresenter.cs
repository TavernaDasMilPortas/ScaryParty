using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Unity.Netcode;
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

        private float _lastUpdate = -999f;
        private string _currentRoomCode = "";
        private bool _offlineReady = false;
        private bool? _optimisticReadyState = null;
        private readonly HashSet<PlayerState> _observedPlayers = new HashSet<PlayerState>();

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

            EnsureInitialized();
            UIScreenRouter.Instance?.RegisterScreen(this);
            Open();
        }

        private void OnDestroy()
        {
            foreach (var ps in _observedPlayers)
            {
                if (ps != null)
                {
                    ps.IsReady.OnValueChanged -= OnPlayerReadyChanged;
                }
            }
            _observedPlayers.Clear();

            UIScreenRouter.Instance?.UnregisterScreen(this);
            if (Instance == this) Instance = null;
        }

        private void OnPlayerReadyChanged(bool previous, bool current)
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
                var template = Resources.Load<VisualTreeAsset>("ReadyRoomScreen")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Piaaria/UI/Piaria/UXML/ReadyRoomScreen.uxml")
#endif
                    ;
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
            BindEvents();
            UpdateRoomCodeDisplay();
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            if (_titleWaitingRoom != null && catalog.titleWaitingRoom != null)
                _titleWaitingRoom.style.backgroundImage = new StyleBackground(catalog.titleWaitingRoom);

            if (_readyCard != null && catalog.waitingPaperPanel != null)
                _readyCard.style.backgroundImage = new StyleBackground(catalog.waitingPaperPanel);

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
                _btnReady.clicked += OnReadyClicked;
            }

            if (_btnStartGame != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnStartGame);
                _btnStartGame.clicked += OnStartGameClicked;
            }

            if (_btnLeaveRoom != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnLeaveRoom);
                _btnLeaveRoom.clicked += OnLeaveRoomClicked;
            }

            if (_btnEditProfile != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnEditProfile);
                _btnEditProfile.clicked += () =>
                {
                    SettingsPresenter.Instance?.OpenModal();
                };
            }
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
                }
            }

            var localPs = GetLocalPlayerState();

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
                        SetupPreviewRig(ps.PlayerColor.Value);
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

                SetupPreviewRig(localColor);
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

            if (_slots[0].Initialized && _slots[0].ReadyBadge != null)
            {
                _slots[0].ReadyBadge.text = ready ? "PRONTO!" : "PREPARANDO...";
                if (ready)
                {
                    _slots[0].ReadyBadge.RemoveFromClassList("status-waiting");
                    _slots[0].ReadyBadge.AddToClassList("status-ready");
                }
                else
                {
                    _slots[0].ReadyBadge.RemoveFromClassList("status-ready");
                    _slots[0].ReadyBadge.AddToClassList("status-waiting");
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
            if (CharacterPreviewRig.Instance != null)
            {
                Destroy(CharacterPreviewRig.Instance.gameObject);
            }

            if (_btnStartGame != null) _btnStartGame.SetEnabled(false);
            if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();

            UIFeedbackService.Instance?.PlaySuccess();

            var myState = GetLocalPlayerState();
            if (myState != null && !myState.IsReady.Value)
            {
                myState.SetReady(true);
            }

            string targetScene = ScaryParty.VisualStyles.VisualStyleRuntimeConfig.GetGameplaySceneName();
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                targetScene = ScaryParty.VisualStyles.VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
            }

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && NetworkManager.Singleton.SceneManager != null)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(targetScene, LoadSceneMode.Single);
            }
            else
            {
                SceneManager.LoadScene(targetScene);
            }
        }

        private void OnLeaveRoomClicked()
        {
            Action doLeave = () =>
            {
                CharacterPreviewRig.Instance?.SetActive(false);
                if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.Shutdown();
                }
                SceneManager.LoadScene("LobbyScene");
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

        private void SetupPreviewRig(Color playerColor)
        {
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
                var rt = CharacterPreviewRig.Instance.RenderTexture;
                if (rt != null && _slots[0].PreviewRT != null)
                {
                    _slots[0].PreviewRT.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                    _slots[0].PreviewRT.style.display = DisplayStyle.Flex;
                    if (_slots[0].AvatarIcon != null)
                    {
                        _slots[0].AvatarIcon.style.display = DisplayStyle.None;
                    }
                    CharacterPreviewRig.Instance.BindDragEvents(_slots[0].PreviewRT);
                }
                CharacterPreviewRig.Instance.SetPlayerColor(playerColor);
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

            State = ScreenState.Opening;
            UpdateRoomCodeDisplay();
            RefreshPlayers();

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
