using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using System.Threading.Tasks;

public class LobbyManager : MonoBehaviour
{
    public RoomDiscoveryService DiscoveryService; 
    public PlayerData PlayerData;

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

        Debug.Log($"[LobbyManager] Tentando criar sala Relay na nuvem...");

        try
        {
            // Pede para a Unity um servidor Relay para até 4 jogadores
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            
            // Pega o código curto para compartilhar com os amigos
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log($"[LobbyManager] ✅ Sala Relay Criada! JOIN CODE: {joinCode}");

            // Configura o Unity Transport usando a API bruta para evitar conflitos de struct
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            bool isSecure = true; // dtls
            transport.SetRelayServerData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                null,
                isSecure
            );

            if (NetworkManager.Singleton.StartHost())
            {
                Debug.Log("[LobbyManager] Host iniciado com sucesso. Carregando ReadyScene...");
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

        Debug.Log($"[LobbyManager] Tentando entrar na sala com o código: {joinCode}...");

        try
        {
            // Entra na alocação usando o código curto
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            // Configura o Transport usando a API bruta
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            bool isSecure = true; // dtls
            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                isSecure
            );

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log($"[LobbyManager] ✅ StartClient executado! Aguardando o Host...");
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
}


