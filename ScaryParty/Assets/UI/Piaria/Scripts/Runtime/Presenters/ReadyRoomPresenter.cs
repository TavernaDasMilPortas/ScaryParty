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

        private void ResolveUIDocument()
        {
            if (uiDocument == null)
                uiDocument = GetComponent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
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
            UIScreenRouter.Instance?.UnregisterScreen(this);
            if (Instance == this) Instance = null;
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
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/ReadyRoomScreen.uxml")
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
            var lobbyMgr = FindFirstObjectByType<LobbyManager>();
            if (lobbyMgr != null && !string.IsNullOrEmpty(lobbyMgr.CurrentJoinCode))
            {
                _currentRoomCode = lobbyMgr.CurrentJoinCode;
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
            if (RootElement == null || State != ScreenState.Active) return;
            if (Time.time - _lastUpdate < 0.25f) return;
            _lastUpdate = Time.time;

            RefreshPlayers();
        }

        private void RefreshPlayers()
        {
            if (_playersContainer == null) return;

            var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
            bool allReady = true;
            int playerCount = 0;

            _playersContainer.Clear();
            var catalog = PiariaUIAssetCatalog.Instance;

            for (int i = 0; i < allPlayers.Length; i++)
            {
                var ps = allPlayers[i];
                if (ps == null) continue;
                playerCount++;

                var row = new VisualElement();
                row.AddToClassList("player-slot-row");

                var bgSprite = catalog != null ? catalog.GetPlayerNameBar(i) : null;
                if (bgSprite != null)
                {
                    row.style.backgroundImage = new StyleBackground(bgSprite);
                }

                // Lado Esquerdo: Cor e Nome
                var leftGroup = new VisualElement();
                leftGroup.style.flexDirection = FlexDirection.Row;
                leftGroup.style.alignItems = Align.Center;

                var colorDot = new VisualElement();
                colorDot.AddToClassList("player-color-indicator");
                colorDot.style.backgroundColor = ps.PlayerColor.Value;

                string pName = ps.PlayerName.Value.ToString();
                if (string.IsNullOrWhiteSpace(pName)) pName = $"Jogador #{ps.OwnerClientId}";
                if (ps.OwnerClientId == 0) pName = $"👑 {pName} (Host)";

                var nameLbl = new Label(pName);
                nameLbl.AddToClassList("player-name-text");

                leftGroup.Add(colorDot);
                leftGroup.Add(nameLbl);

                // Lado Direito: Status Pronto
                bool isReady = ps.IsReady.Value;
                var statusLbl = new Label(isReady ? "✅ PRONTO" : "⏳ AGUARDANDO...");
                statusLbl.AddToClassList("player-status-tag");
                statusLbl.AddToClassList(isReady ? "status-ready" : "status-waiting");

                row.Add(leftGroup);
                row.Add(statusLbl);
                _playersContainer.Add(row);

                if (!isReady) allReady = false;
            }

            // Exibir vagas livres se houver menos de 4
            int maxSlots = 4;
            for (int j = playerCount; j < maxSlots; j++)
            {
                var emptyRow = new VisualElement();
                emptyRow.AddToClassList("player-slot-row");
                emptyRow.AddToClassList("slot-empty");

                var emptyLbl = new Label($"— Vaga Disponível ({j + 1}/{maxSlots}) —");
                emptyLbl.AddToClassList("player-name-text");
                emptyRow.Add(emptyLbl);

                _playersContainer.Add(emptyRow);
            }

            // Botão Iniciar Jogo (somente Host)
            if (_btnStartGame != null)
            {
                bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
                _btnStartGame.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
                if (isHost)
                {
                    _btnStartGame.SetEnabled(allReady && playerCount > 0);
                }
            }

            // Atualiza texto do botão Pronto para o jogador local
            var localPs = GetLocalPlayerState();
            if (localPs != null && _btnReady != null)
            {
                _btnReady.text = localPs.IsReady.Value ? "❌ NÃO ESTOU PRONTO" : "✅ ESTOU PRONTO";
            }
        }

        private void OnReadyClicked()
        {
            var myState = GetLocalPlayerState();
            if (myState != null)
            {
                bool newState = !myState.IsReady.Value;
                myState.SetReadyServerRpc(newState);
                UIFeedbackService.Instance?.PlayReady();
            }
        }

        private void OnStartGameClicked()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;

            if (_btnStartGame != null) _btnStartGame.SetEnabled(false);
            if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();

            UIFeedbackService.Instance?.PlaySuccess();
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", LoadSceneMode.Single);
        }

        private void OnLeaveRoomClicked()
        {
            ModalPresenter.Instance?.ShowConfirmation("Deseja sair da sala de espera?", () =>
            {
                if (RoomDiscoveryService.Instance != null) RoomDiscoveryService.Instance.StopHosting();
                if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
                SceneManager.LoadScene("LobbyScene");
            });
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
            return null;
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
