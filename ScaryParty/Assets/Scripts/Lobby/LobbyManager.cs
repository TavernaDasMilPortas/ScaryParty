using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public class LobbyManager : MonoBehaviour
{
    public PlayerData PlayerData;

    private Lobby _currentLobby;
    private Coroutine _heartbeatCoroutine;

    private async void Start()
    {
        Debug.Log("[LobbyManager] Start called. Loading PlayerData...");
        if (PlayerData == null)
        {
            PlayerData = Resources.Load<PlayerData>("PlayerData");
        }
        Debug.Log($"[LobbyManager] PlayerData loaded: {PlayerData?.PlayerName}");

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += (id) =>
            {
                Debug.Log($"[LobbyManager] 🟢 Client Connected! Client ID: {id}");
            };

            NetworkManager.Singleton.OnClientDisconnectCallback += (id) =>
            {
                Debug.Log($"[LobbyManager] 🔴 Client Disconnected or failed to connect! Client ID: {id}");
            };
        }

        await InitializeUnityServices();
    }

    private async Task InitializeUnityServices()
    {
        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[LobbyManager] Autenticado na Unity Services com ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Falha ao inicializar Unity Services: {e.Message}");
        }
    }

    public async void CreateRelayRoom(string roomName)
    {
        if (NetworkManager.Singleton == null) return;

        Debug.Log("[LobbyManager] Tentando criar sala Relay (WSS)...");

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[LobbyManager] ✅ Sala Relay Criada! JOIN CODE: {joinCode}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // CORREÇÃO: RelayServerData(allocation, "wss") seleciona internamente o endpoint WSS
            // da lista retornada pelo servidor, garantindo sincronia entre alocação e transport.
            // Isso resolve o erro "Mismatched Relay configuration and network interface".
            var relayServerData = new RelayServerData(allocation, "wss");

            // Garante que o NetworkManager não esteja em estado inválido de tentativa anterior
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager ainda estava ativo. Fazendo Shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
            }

            transport.UseWebSockets = true;
            transport.SetRelayServerData(relayServerData);

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[LobbyManager] ✅ Host iniciado com sucesso. Carregando ReadyScene...");

                try
                {
                    var lobbyOptions = new CreateLobbyOptions
                    {
                        IsPrivate = false,
                        Data = new Dictionary<string, DataObject>
                        {
                            { "JoinCode", new DataObject(DataObject.VisibilityOptions.Public, joinCode) }
                        }
                    };
                    _currentLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, 4, lobbyOptions);
                    Debug.Log($"[LobbyManager] ✅ Lobby criado: {_currentLobby.Id}");

                    _heartbeatCoroutine = StartCoroutine(HeartbeatLobbyCoroutine(_currentLobby.Id, 15f));
                }
                catch (LobbyServiceException e)
                {
                    Debug.LogError($"[LobbyManager] Falha ao criar Lobby: {e.Message}");
                }

                NetworkManager.Singleton.SceneManager.LoadScene("ReadyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[LobbyManager] ❌ Falha ao iniciar Host.");
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"[LobbyManager] Falha no Relay: {e.Message}");
        }
    }

    public async void JoinRelayRoom(string joinCode)
    {
        if (NetworkManager.Singleton == null) return;
        if (string.IsNullOrEmpty(joinCode)) return;

        Debug.Log($"[LobbyManager] Tentando entrar na sala com o código: {joinCode} (WSS)...");

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            // CORREÇÃO: mesmo padrão do host — RelayServerData com "wss" seleciona o endpoint correto
            var relayServerData = new RelayServerData(joinAllocation, "wss");

            // Garante que o NetworkManager não esteja em estado inválido de tentativa anterior
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager ainda estava ativo. Fazendo Shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
            }

            transport.UseWebSockets = true;
            transport.SetRelayServerData(relayServerData);

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[LobbyManager] ✅ StartClient executado! Aguardando o Host...");
            }
            else
            {
                Debug.LogError("[LobbyManager] ❌ Falha ao tentar iniciar o Cliente.");
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError($"[LobbyManager] Código inválido ou falha ao conectar: {e.Message}");
        }
    }

    private IEnumerator HeartbeatLobbyCoroutine(string lobbyId, float intervalSeconds)
    {
        while (true)
        {
            LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);
            yield return new WaitForSeconds(intervalSeconds);
        }
    }

    public async Task<List<Lobby>> QueryLobbiesAsync()
    {
        try
        {
            var options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };
            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            return response.Results;
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[LobbyManager] Falha ao buscar lobbies: {e.Message}");
            return new List<Lobby>();
        }
    }

    private void OnDestroy()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
            _heartbeatCoroutine = null;
        }

        if (_currentLobby != null)
        {
            LobbyService.Instance.DeleteLobbyAsync(_currentLobby.Id);
            _currentLobby = null;
        }
    }
}
