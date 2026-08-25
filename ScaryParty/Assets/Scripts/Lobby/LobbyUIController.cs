using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LobbyUIController : MonoBehaviour
{
    public LobbyManager LobbyManager;
    private UIDocument _document;

    // Elementos da UI
    private TextField _playerNameInput;
    private VisualElement _colorPreview;
    private Button _btnColor1, _btnColor2, _btnColor3, _btnColor4;
    private Button _btnSaveProfile;
    
    private ScrollView _roomList;
    private Button _btnRefreshRooms;

    private TextField _roomNameInput;
    private TextField _roomPortInput;
    private Button _btnCreateRoom;

    private TextField _manualIpInput;
    private TextField _manualPortInput;
    private Button _btnJoinManual;

    private void OnEnable()
    {
        _document = GetComponent<UIDocument>();
        if (_document == null)
        {
            // Busca na cena caso o UIDocument esteja em outro GameObject (ex: --- UI ---)
            _document = FindObjectOfType<UIDocument>();
        }

        if (_document == null || LobbyManager == null)
        {
            Debug.LogError("[LobbyUI] UIDocument or LobbyManager missing!");
            return;
        }

        var root = _document.rootVisualElement;

        // Perfil
        _playerNameInput = root.Q<TextField>("PlayerNameInput");
        _colorPreview = root.Q<VisualElement>("ColorPreview");
        _btnColor1 = root.Q<Button>("BtnColor1");
        _btnColor2 = root.Q<Button>("BtnColor2");
        _btnColor3 = root.Q<Button>("BtnColor3");
        _btnColor4 = root.Q<Button>("BtnColor4");
        _btnSaveProfile = root.Q<Button>("BtnSaveProfile");

        // Lista de Salas
        _roomList = root.Q<ScrollView>("RoomList");
        _btnRefreshRooms = root.Q<Button>("BtnRefreshRooms");

        // Criar Sala
        _roomNameInput = root.Q<TextField>("RoomNameInput");
        _roomPortInput = root.Q<TextField>("RoomPortInput");
        _btnCreateRoom = root.Q<Button>("BtnCreateRoom");

        // Entrada Manual
        _manualIpInput = root.Q<TextField>("ManualIpInput");
        _manualPortInput = root.Q<TextField>("ManualPortInput");
        _btnJoinManual = root.Q<Button>("BtnJoinManual");

        Debug.Log("[LobbyUI] Elements queried, registering callbacks...");
        RegisterCallbacks();
        LoadProfileData();
        
        if (LobbyManager.DiscoveryService != null)
        {
            LobbyManager.DiscoveryService.OnRoomsUpdated += UpdateRoomList;
            Debug.Log("[LobbyUI] Bound to RoomDiscoveryService events.");
        }
    }

    private void OnDisable()
    {
        if (LobbyManager != null && LobbyManager.DiscoveryService != null)
        {
            LobbyManager.DiscoveryService.OnRoomsUpdated -= UpdateRoomList;
        }
    }

    private void RegisterCallbacks()
    {
        _btnColor1.clicked += () => SetPlayerColor(new Color(1f, 0.4f, 0f)); // Laranja
        _btnColor2.clicked += () => SetPlayerColor(new Color(0.2f, 0.8f, 0.2f)); // Verde
        _btnColor3.clicked += () => SetPlayerColor(new Color(0.2f, 0.4f, 1f)); // Azul
        _btnColor4.clicked += () => SetPlayerColor(new Color(0.8f, 0.2f, 0.8f)); // Roxo

        _btnSaveProfile.clicked += SaveProfileData;

        _btnRefreshRooms.clicked += () => 
        { 
            Debug.Log("[LobbyUI] Refresh clicked.");
            _roomList.Clear(); 
        };

        _btnCreateRoom.clicked += OnCreateRoomClicked;
        _btnJoinManual.clicked += OnJoinManualClicked;
    }

    private void LoadProfileData()
    {
        if (LobbyManager.PlayerData != null)
        {
            _playerNameInput.value = LobbyManager.PlayerData.PlayerName;
            SetPlayerColor(LobbyManager.PlayerData.PlayerColor);
        }
    }

    private void SaveProfileData()
    {
        if (LobbyManager.PlayerData != null)
        {
            LobbyManager.PlayerData.PlayerName = _playerNameInput.value;
            LobbyManager.PlayerData.Save();
            Debug.Log($"[LobbyUI] Profile Saved: {_playerNameInput.value}");
        }
    }

    private void SetPlayerColor(Color color)
    {
        _colorPreview.style.backgroundColor = color;
        if (LobbyManager.PlayerData != null)
        {
            LobbyManager.PlayerData.PlayerColor = color;
        }
    }

    private void UpdateRoomList(List<RoomInfo> rooms)
    {
        _roomList.Clear();

        if (rooms.Count == 0)
        {
            var noRoomsLabel = new Label("Nenhuma sala encontrada...");
            noRoomsLabel.AddToClassList("no-rooms-label");
            _roomList.Add(noRoomsLabel);
            return;
        }

        foreach (var room in rooms)
        {
            var roomElement = new VisualElement();
            roomElement.AddToClassList("room-entry");

            var roomNameLabel = new Label($"🎃 {room.RoomName}");
            roomNameLabel.AddToClassList("room-name");

            var roomDetailsLabel = new Label($"{room.HostIP}:{room.Port} | Players: {room.PlayerCount}/{room.MaxPlayers}");
            roomDetailsLabel.AddToClassList("room-details");

            var joinBtn = new Button(() => {
                Debug.Log($"[LobbyUI] Clicked Join on {room.RoomName} - UDP local discovery disabled for Relay!");
                // LobbyManager.JoinRoom(room); // Obsoleto, agora usamos JoinRelayRoom(joinCode)
            });
            joinBtn.text = "ENTRAR";
            joinBtn.AddToClassList("join-btn");

            var infoContainer = new VisualElement();
            infoContainer.Add(roomNameLabel);
            infoContainer.Add(roomDetailsLabel);

            roomElement.Add(infoContainer);
            roomElement.Add(joinBtn);

            _roomList.Add(roomElement);
        }
    }

    private void OnCreateRoomClicked()
    {
        Debug.Log("[LobbyUI] Create Room clicked.");
        string roomName = _roomNameInput.value;
        if (string.IsNullOrEmpty(roomName)) roomName = $"{_playerNameInput.value}'s Party";
        
        SaveProfileData();
        LobbyManager.CreateRelayRoom(roomName);
    }

    private void OnJoinManualClicked()
    {
        Debug.Log("[LobbyUI] Join Manual clicked.");
        
        // Vamos usar o campo que antes era o IP Manual para receber o Join Code do Relay!
        string joinCode = _manualIpInput.value;
        
        if (string.IsNullOrEmpty(joinCode))
        {
            Debug.LogError("[LobbyUI] Join Code está vazio!");
            return;
        }

        SaveProfileData();
        LobbyManager.JoinRelayRoom(joinCode);
    }
}

