using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.UI.Piaria.Core;

namespace ScaryParty.UI.Piaria.Presenters
{
    public class LobbyPresenter : MonoBehaviour, IScreenView
    {
        public static LobbyPresenter Instance { get; private set; }

        public string ScreenId => "LobbyScreen";
        public VisualElement RootElement { get; private set; }
        public ScreenState State { get; private set; } = ScreenState.Hidden;

        [Header("Configuração de UI Document")]
        public UIDocument uiDocument;
        public LobbyManager lobbyManager;

        private Button _btnBack;
        private VisualElement _titleLobby;
        private VisualElement _colProfile;
        private VisualElement _colRoomList;
        private VisualElement _colConnect;

        // Perfil
        private TextField _playerNameInput;
        private Button _btnColorOrange, _btnColorGreen, _btnColorBlue, _btnColorPink, _btnColorRed;
        private VisualElement _previewContainer;
        private VisualElement _previewRenderTexture;
        private VisualElement _previewPlaceholder;
        private VisualElement _previewFrame;
        private Button _btnSaveProfile;
        private Color _selectedColor = new Color(1f, 0.45f, 0.1f);

        // Salas
        private ScrollView _roomList;
        private VisualElement _emptyRoomsNotice;
        private Button _btnRefreshRooms;

        // Conexão
        private TextField _roomNameInput;
        private Button _btnCreateRoom;
        private TextField _manualCodeInput;
        private Button _btnJoinManual;
        private Button _btnCreateLAN;
        private TextField _lanIpInput;
        private Button _btnJoinLAN;

        private List<RoomInfo> _latestLANRooms = new List<RoomInfo>();

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

            ResolveReferences();
        }

        public void ResolveReferences()
        {
            PiariaPresenterUtils.ResolveUIDocument(this, ref uiDocument, "Assets/UI/Piaria/UXML/LobbySceneContainer.uxml");
            PiariaPresenterUtils.ResolveLobbyManager(this, ref lobbyManager);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInParent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            }
            if (lobbyManager == null)
            {
                lobbyManager = GetComponent<LobbyManager>() ?? GetComponentInParent<LobbyManager>() ?? FindFirstObjectByType<LobbyManager>();
                if (lobbyManager == null)
                {
                    var lobbyGo = GameObject.Find("--- LOBBY ---") ?? GameObject.Find("LobbyManager");
                    if (lobbyGo != null) lobbyManager = lobbyGo.GetComponent<LobbyManager>();
                }
            }
            ResolveReferences();
        }

        private void Reset()
        {
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInParent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            }
            if (lobbyManager == null)
            {
                lobbyManager = GetComponent<LobbyManager>() ?? GetComponentInParent<LobbyManager>() ?? FindFirstObjectByType<LobbyManager>();
                if (lobbyManager == null)
                {
                    var lobbyGo = GameObject.Find("--- LOBBY ---") ?? GameObject.Find("LobbyManager");
                    if (lobbyGo != null) lobbyManager = lobbyGo.GetComponent<LobbyManager>();
                }
            }
            ResolveReferences();
        }
#endif

        private void Start()
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            EnsureInitialized();
            UIScreenRouter.Instance?.RegisterScreen(this);

            if (RoomDiscoveryService.Instance != null)
            {
                RoomDiscoveryService.Instance.OnRoomsUpdated -= OnLANRoomsDiscovered;
                RoomDiscoveryService.Instance.OnRoomsUpdated += OnLANRoomsDiscovered;
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
        }

        private void OnDestroy()
        {
            if (RoomDiscoveryService.Instance != null)
            {
                RoomDiscoveryService.Instance.OnRoomsUpdated -= OnLANRoomsDiscovered;
            }
            UIScreenRouter.Instance?.UnregisterScreen(this);
            if (Instance == this) Instance = null;
        }

        public void EnsureInitialized()
        {
            if (RootElement != null) return;
            ResolveReferences();
            if (uiDocument == null)
            {
                uiDocument = GetComponent<UIDocument>() ?? GetComponentInParent<UIDocument>() ?? FindFirstObjectByType<UIDocument>();
            }
            if (lobbyManager == null)
            {
                lobbyManager = GetComponent<LobbyManager>() ?? GetComponentInParent<LobbyManager>() ?? FindFirstObjectByType<LobbyManager>();
                if (lobbyManager == null)
                {
                    var lobbyGo = GameObject.Find("--- LOBBY ---") ?? GameObject.Find("LobbyManager");
                    if (lobbyGo != null) lobbyManager = lobbyGo.GetComponent<LobbyManager>();
                }
            }
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;

            RootElement = uiDocument.rootVisualElement.Q<VisualElement>("LobbyScreenRoot");
            if (RootElement == null)
            {
                var template = Resources.Load<VisualTreeAsset>("LobbyScreen")
#if UNITY_EDITOR
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI/Piaria/UXML/LobbyScreen.uxml")
#endif
                    ;
                if (template != null)
                {
                    template.CloneTree(uiDocument.rootVisualElement);
                    RootElement = uiDocument.rootVisualElement.Q<VisualElement>("LobbyScreenRoot");
                }
            }
            if (RootElement == null) return;

            _btnBack = RootElement.Q<Button>("BtnBack");
            _titleLobby = RootElement.Q<VisualElement>("TitleLobby");
            _colProfile = RootElement.Q<VisualElement>("ColProfile");
            _colRoomList = RootElement.Q<VisualElement>("ColRoomList");
            _colConnect = RootElement.Q<VisualElement>("ColConnect");

            // Perfil
            _playerNameInput = RootElement.Q<TextField>("PlayerNameInput");
            _btnColorOrange = RootElement.Q<Button>("BtnColorOrange");
            _btnColorGreen = RootElement.Q<Button>("BtnColorGreen");
            _btnColorBlue = RootElement.Q<Button>("BtnColorBlue");
            _btnColorPink = RootElement.Q<Button>("BtnColorPink");
            _btnColorRed = RootElement.Q<Button>("BtnColorRed");
            _previewContainer = RootElement.Q<VisualElement>("PreviewContainer");
            _previewRenderTexture = RootElement.Q<VisualElement>("PreviewRenderTexture");
            _previewPlaceholder = RootElement.Q<VisualElement>("PreviewPlaceholder");
            _previewFrame = RootElement.Q<VisualElement>("PreviewFrame");
            _btnSaveProfile = RootElement.Q<Button>("BtnSaveProfile");

            // Salas
            _roomList = RootElement.Q<ScrollView>("RoomList");
            _emptyRoomsNotice = RootElement.Q<VisualElement>("EmptyRoomsNotice");
            _btnRefreshRooms = RootElement.Q<Button>("BtnRefreshRooms");

            // Conexão
            _roomNameInput = RootElement.Q<TextField>("RoomNameInput");
            _btnCreateRoom = RootElement.Q<Button>("BtnCreateRoom");
            _manualCodeInput = RootElement.Q<TextField>("ManualCodeInput");
            _btnJoinManual = RootElement.Q<Button>("BtnJoinManual");
            _btnCreateLAN = RootElement.Q<Button>("BtnCreateLAN");
            _lanIpInput = RootElement.Q<TextField>("LanIpInput");
            _btnJoinLAN = RootElement.Q<Button>("BtnJoinLAN");

            ApplyCatalogAssets();
            LoadProfileData();
            BindEvents();
            SetupPreview();

            if (_lanIpInput != null && string.IsNullOrEmpty(_lanIpInput.value))
            {
                _lanIpInput.value = RoomDiscoveryService.GetLocalIPAddress();
            }
        }

        private void ApplyCatalogAssets()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog == null) return;

            // Fundo ilustrado do Lobby
            if (catalog.backgroundLobby != null)
                RootElement.style.backgroundImage = new StyleBackground(catalog.backgroundLobby);

            // Botão Voltar Ilustrado
            if (_btnBack != null && catalog.btnBack != null)
            {
                _btnBack.style.backgroundImage = new StyleBackground(catalog.btnBack);
                var backLbl = _btnBack.Q<Label>();
                if (backLbl != null) backLbl.style.display = DisplayStyle.None;
            }

            if (_titleLobby != null && catalog.titleLobby != null)
                _titleLobby.style.backgroundImage = new StyleBackground(catalog.titleLobby);

            if (_colProfile != null && catalog.paperPanel1 != null)
                _colProfile.style.backgroundImage = new StyleBackground(catalog.paperPanel1);

            if (_colRoomList != null && catalog.paperPanel2 != null)
                _colRoomList.style.backgroundImage = new StyleBackground(catalog.paperPanel2);

            if (_colConnect != null && catalog.paperPanel3 != null)
                _colConnect.style.backgroundImage = new StyleBackground(catalog.paperPanel3);

            // Botões de Amostra de Cores Ilustrados
            if (_btnColorOrange != null && catalog.colorOrange != null)
                _btnColorOrange.style.backgroundImage = new StyleBackground(catalog.colorOrange);
            if (_btnColorGreen != null && catalog.colorGreen != null)
                _btnColorGreen.style.backgroundImage = new StyleBackground(catalog.colorGreen);
            if (_btnColorBlue != null && catalog.colorBlue != null)
                _btnColorBlue.style.backgroundImage = new StyleBackground(catalog.colorBlue);
            if (_btnColorPink != null && catalog.colorPink != null)
                _btnColorPink.style.backgroundImage = new StyleBackground(catalog.colorPink);
            if (_btnColorRed != null && catalog.colorRed != null)
                _btnColorRed.style.backgroundImage = new StyleBackground(catalog.colorRed);

            // Cabeçalhos
            var headProf = RootElement.Q<VisualElement>("HeaderMyProfile");
            if (headProf != null && catalog.headerMyProfile != null) headProf.style.backgroundImage = new StyleBackground(catalog.headerMyProfile);

            var headRooms = RootElement.Q<VisualElement>("HeaderRooms");
            if (headRooms != null && catalog.headerAvailableRooms != null) headRooms.style.backgroundImage = new StyleBackground(catalog.headerAvailableRooms);

            var headCreate = RootElement.Q<VisualElement>("HeaderCreateRoom");
            if (headCreate != null && catalog.headerCreateRoom != null) headCreate.style.backgroundImage = new StyleBackground(catalog.headerCreateRoom);

            var headCode = RootElement.Q<VisualElement>("HeaderJoinCode");
            if (headCode != null && catalog.headerJoinCode != null) headCode.style.backgroundImage = new StyleBackground(catalog.headerJoinCode);

            var headLan = RootElement.Q<VisualElement>("HeaderLan");
            if (headLan != null && catalog.headerLan != null) headLan.style.backgroundImage = new StyleBackground(catalog.headerLan);

            // Moldura 3D Viewer
            if (_previewFrame != null && catalog.frame3DPreview != null)
                _previewFrame.style.backgroundImage = new StyleBackground(catalog.frame3DPreview);

            // Arte sem salas
            var noRoomsArt = RootElement.Q<VisualElement>("NoRoomsArt");
            if (noRoomsArt != null && catalog.noRooms != null)
                noRoomsArt.style.backgroundImage = new StyleBackground(catalog.noRooms);

            // NOTA: Os botões de ação (_btnCreateRoom, _btnJoinManual, _btnCreateLAN, _btnJoinLAN, _btnRefreshRooms)
            // utilizam estilo visual via USS com cores vibrantes e tipografia nítida (--color-orange, --color-blue),
            // evitando sobreposição de texto duplo com sprites legados.
        }

        private void SetupPreview()
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
                if (_previewRenderTexture != null)
                {
                    var rt = CharacterPreviewRig.Instance.RenderTexture;
                    if (rt != null)
                    {
                        _previewRenderTexture.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
                        if (_previewPlaceholder != null)
                        {
                            _previewPlaceholder.style.display = DisplayStyle.None;
                        }
                    }
                }
                CharacterPreviewRig.Instance.BindDragEvents(_previewContainer);
                CharacterPreviewRig.Instance.SetPlayerColor(_selectedColor);
            }
        }

        private void LoadProfileData()
        {
            if (lobbyManager != null && lobbyManager.PlayerData != null)
            {
                if (_playerNameInput != null)
                    _playerNameInput.value = lobbyManager.PlayerData.PlayerName;
                _selectedColor = lobbyManager.PlayerData.PlayerColor;
                SetColor(_selectedColor);
            }
            else
            {
                var savedName = PlayerPrefs.GetString("PlayerName", "rubens");
                if (_playerNameInput != null)
                    _playerNameInput.value = savedName;
            }
        }

        private void SaveProfileData()
        {
            if (_playerNameInput != null)
            {
                PlayerPrefs.GetString("PlayerName", _playerNameInput.value);
                PlayerPrefs.Save();
            }

            if (_playerNameInput != null && lobbyManager != null && lobbyManager.PlayerData != null)
            {
                lobbyManager.PlayerData.PlayerName = _playerNameInput.value;
                lobbyManager.PlayerData.PlayerColor = _selectedColor;
                lobbyManager.PlayerData.Save();
                UIFeedbackService.Instance?.PlaySuccess();
            }
        }

        private void SetColor(Color c)
        {
            _selectedColor = c;
            if (CharacterPreviewRig.Instance != null)
                CharacterPreviewRig.Instance.SetPlayerColor(c);

            if (lobbyManager != null && lobbyManager.PlayerData != null)
                lobbyManager.PlayerData.PlayerColor = c;
        }

        private void BindEvents()
        {
            if (_btnBack != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnBack);
                _btnBack.clicked += () =>
                {
                    if (UIScreenRouter.Instance != null)
                    {
                        UIScreenRouter.Instance.NavigateTo("MainMenuScreen");
                    }
                    else
                    {
                        Close(() => MainMenuPresenter.Instance?.Open());
                    }
                };
            }

            // Cores
            _btnColorOrange?.RegisterCallback<ClickEvent>(evt => SetColor(new Color(1f, 0.45f, 0.1f)));
            _btnColorGreen?.RegisterCallback<ClickEvent>(evt => SetColor(new Color(0.2f, 0.8f, 0.2f)));
            _btnColorBlue?.RegisterCallback<ClickEvent>(evt => SetColor(new Color(0.2f, 0.5f, 1f)));
            _btnColorPink?.RegisterCallback<ClickEvent>(evt => SetColor(new Color(0.9f, 0.3f, 0.6f)));
            _btnColorRed?.RegisterCallback<ClickEvent>(evt => SetColor(new Color(0.85f, 0.15f, 0.15f)));

            if (_btnSaveProfile != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnSaveProfile);
                _btnSaveProfile.clicked += SaveProfileData;
            }

            if (_btnRefreshRooms != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnRefreshRooms);
                _btnRefreshRooms.clicked += RefreshRooms;
            }

            if (_btnCreateRoom != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCreateRoom);
                _btnCreateRoom.clicked += OnCreateRoomClicked;
            }

            if (_btnJoinManual != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnJoinManual);
                _btnJoinManual.clicked += OnJoinManualClicked;
            }

            if (_btnCreateLAN != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnCreateLAN);
                _btnCreateLAN.clicked += OnCreateLANClicked;
            }

            if (_btnJoinLAN != null)
            {
                UIFeedbackService.Instance?.BindButton(_btnJoinLAN);
                _btnJoinLAN.clicked += OnJoinLANClicked;
            }
        }

        private void OnLANRoomsDiscovered(List<RoomInfo> rooms)
        {
            _latestLANRooms = rooms ?? new List<RoomInfo>();
            if (_latestLANRooms.Count > 0 && _lanIpInput != null)
            {
                if (string.IsNullOrEmpty(_lanIpInput.value) || _lanIpInput.value == "127.0.0.1")
                {
                    _lanIpInput.value = _latestLANRooms[0].HostIP;
                }
            }
        }

        private async void RefreshRooms()
        {
            if (_roomList == null) return;
            _roomList.Clear();

            var loadingLabel = new Label("Buscando salas disponíveis...");
            loadingLabel.AddToClassList("empty-main-text");
            _roomList.Add(loadingLabel);

            int count = 0;

            // Busca salas Relay públicas
            try
            {
                var queryLobbies = await Unity.Services.Lobbies.LobbyService.Instance.QueryLobbiesAsync();
                _roomList.Clear();
                foreach (var lobby in queryLobbies.Results)
                {
                    count++;
                    string roomCode = lobby.Data != null && lobby.Data.ContainsKey("JoinCode") ? lobby.Data["JoinCode"].Value : "";
                    AddRoomEntry(lobby.Name, $"{lobby.Players.Count}/{lobby.MaxPlayers} jogadores (Relay)", () =>
                    {
                        if (lobbyManager != null)
                        {
                            lobbyManager.JoinRelayRoom(roomCode);
                        }
                        else
                        {
                            UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
                        }
                    });
                }
            }
            catch (Exception)
            {
                // Relay offline ou não autenticado
                _roomList.Clear();
            }

            // Salas LAN descobertas via UDP Broadcast
            if (_latestLANRooms != null)
            {
                foreach (var lanRoom in _latestLANRooms)
                {
                    count++;
                    AddRoomEntry(lanRoom.RoomName, $"{lanRoom.PlayerCount}/{lanRoom.MaxPlayers} (LAN @ {lanRoom.HostIP})", () =>
                    {
                        if (lobbyManager != null)
                        {
                            lobbyManager.JoinLANClient(lanRoom.HostIP);
                        }
                        else
                        {
                            UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
                        }
                    });
                }
            }

            if (count == 0)
            {
                if (_emptyRoomsNotice != null)
                {
                    _roomList.Add(_emptyRoomsNotice);
                    _emptyRoomsNotice.style.display = DisplayStyle.Flex;
                }
            }
        }

        private void AddRoomEntry(string name, string details, Action onJoin)
        {
            var row = new VisualElement();
            row.AddToClassList("room-entry");

            var info = new VisualElement();
            var nameLbl = new Label(name);
            nameLbl.AddToClassList("room-name-lbl");
            var detailsLbl = new Label(details);
            detailsLbl.AddToClassList("room-details-lbl");
            info.Add(nameLbl);
            info.Add(detailsLbl);

            var joinBtn = new Button(onJoin);
            joinBtn.text = "ENTRAR";
            joinBtn.AddToClassList("btn-room-enter");
            UIFeedbackService.Instance?.BindButton(joinBtn);

            row.Add(info);
            row.Add(joinBtn);
            _roomList.Add(row);
        }

        private void OnCreateRoomClicked()
        {
            string roomName = _roomNameInput != null && !string.IsNullOrEmpty(_roomNameInput.value)
                ? _roomNameInput.value
                : $"{_playerNameInput?.value}'s Pizza Party";

            SaveProfileData();
            if (lobbyManager != null)
            {
                lobbyManager.CreateRelayRoom(roomName);
            }
            else
            {
                Debug.LogWarning("[LobbyPresenter] LobbyManager não encontrado. Carregando ReadyScene...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
            }
        }

        private void OnJoinManualClicked()
        {
            string code = _manualCodeInput != null ? _manualCodeInput.value.Trim() : "";
            if (string.IsNullOrEmpty(code))
            {
                UIFeedbackService.Instance?.PlayError(_manualCodeInput);
                ModalPresenter.Instance?.ShowAlert("Código Inválido", "Por favor, digite um código de sala válido.");
                return;
            }

            SaveProfileData();
            if (lobbyManager != null)
            {
                lobbyManager.JoinRelayRoom(code);
            }
            else
            {
                Debug.LogWarning("[LobbyPresenter] LobbyManager não encontrado. Carregando ReadyScene...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
            }
        }

        private void OnCreateLANClicked()
        {
            string roomName = _roomNameInput != null && !string.IsNullOrEmpty(_roomNameInput.value)
                ? _roomNameInput.value
                : $"{_playerNameInput?.value}'s LAN Party";

            SaveProfileData();
            if (lobbyManager != null)
            {
                lobbyManager.StartLANHost(roomName);
            }
            else
            {
                Debug.LogWarning("[LobbyPresenter] LobbyManager não encontrado. Carregando ReadyScene...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
            }
        }

        private void OnJoinLANClicked()
        {
            string ip = _lanIpInput != null ? _lanIpInput.value.Trim() : "";
            if (string.IsNullOrEmpty(ip))
            {
                UIFeedbackService.Instance?.PlayError(_lanIpInput);
                ModalPresenter.Instance?.ShowAlert("IP Inválido", "Por favor, insira o endereço de IP do host LAN.");
                return;
            }

            SaveProfileData();
            if (lobbyManager != null)
            {
                lobbyManager.JoinLANClient(ip);
            }
            else
            {
                Debug.LogWarning("[LobbyPresenter] LobbyManager não encontrado. Carregando ReadyScene...");
                UnityEngine.SceneManagement.SceneManager.LoadScene("ReadyScene");
            }
        }

        public void Open(Action onComplete = null)
        {
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
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

            State = ScreenState.Opening;
            CharacterPreviewRig.Instance?.SetActive(true);
            SetupPreview();
            RefreshRooms();

            RootElement.style.opacity = 1f;
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
                CharacterPreviewRig.Instance?.SetActive(false);
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
                CharacterPreviewRig.Instance?.SetActive(false);
                onComplete?.Invoke();
            });
        }
    }
}
