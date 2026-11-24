using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;

public interface IUgsLobbyGateway
{
    Task SendHeartbeatPingAsync(string lobbyId);
    Task DeleteLobbyAsync(string lobbyId);
}

/// <summary>
/// Serviço persistente desacoplado da LobbyScene para manter heartbeat e deleção segura
/// do lobby público UGS durante toda a permanência na ReadyScene até início da partida ou saída.
/// </summary>
public static class UgsLobbySessionService
{
    private sealed class UnityUgsLobbyGateway : IUgsLobbyGateway
    {
        public Task SendHeartbeatPingAsync(string lobbyId)
            => LobbyService.Instance.SendHeartbeatPingAsync(lobbyId);

        public Task DeleteLobbyAsync(string lobbyId)
            => LobbyService.Instance.DeleteLobbyAsync(lobbyId);
    }

    private static readonly object _sync = new object();
    private static IUgsLobbyGateway _gateway = new UnityUgsLobbyGateway();
    private static CancellationTokenSource _heartbeatCts;
    private static bool _quittingHooked;

    public static string ActiveLobbyId { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        StopHeartbeatOnly();
        ActiveLobbyId = null;
        _gateway = new UnityUgsLobbyGateway();
        _quittingHooked = false;
    }

    public static void SetGatewayForTests(IUgsLobbyGateway gateway)
    {
        StopHeartbeatOnly();
        ActiveLobbyId = null;
        _gateway = gateway ?? new UnityUgsLobbyGateway();
    }

    public static void AdoptHostedLobby(string lobbyId, int heartbeatIntervalMs = 15000)
    {
        if (string.IsNullOrWhiteSpace(lobbyId)) return;

        string previousId = null;
        CancellationToken token;
        lock (_sync)
        {
            if (!string.IsNullOrEmpty(ActiveLobbyId) && !string.Equals(ActiveLobbyId, lobbyId, System.StringComparison.Ordinal))
            {
                previousId = ActiveLobbyId;
            }

            StopHeartbeatOnly();
            ActiveLobbyId = lobbyId;
            _heartbeatCts = new CancellationTokenSource();
            token = _heartbeatCts.Token;

            if (!_quittingHooked)
            {
                Application.quitting += HandleApplicationQuitting;
                _quittingHooked = true;
            }
        }

        if (!string.IsNullOrEmpty(previousId))
        {
            _ = DeleteSafeAsync(previousId, _gateway);
        }

        _ = RunHeartbeatLoopAsync(lobbyId, Mathf.Max(500, heartbeatIntervalMs), _gateway, token);
    }

    public static void StopHostingLobby()
    {
        string idToDelete;
        IUgsLobbyGateway gw;
        lock (_sync)
        {
            StopHeartbeatOnly();
            idToDelete = ActiveLobbyId;
            ActiveLobbyId = null;
            gw = _gateway;
        }

        if (!string.IsNullOrEmpty(idToDelete))
        {
            _ = DeleteSafeAsync(idToDelete, gw);
        }
    }

    public static Task DeleteOrphanLobbyAsync(string lobbyId)
    {
        return string.IsNullOrWhiteSpace(lobbyId)
            ? Task.CompletedTask
            : DeleteSafeAsync(lobbyId, _gateway);
    }

    private static void HandleApplicationQuitting()
    {
        StopHostingLobby();
    }

    private static void StopHeartbeatOnly()
    {
        if (_heartbeatCts != null)
        {
            try { _heartbeatCts.Cancel(); } catch { }
            try { _heartbeatCts.Dispose(); } catch { }
            _heartbeatCts = null;
        }
    }

    private static async Task RunHeartbeatLoopAsync(
        string lobbyId,
        int intervalMs,
        IUgsLobbyGateway gateway,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(intervalMs, token);
                if (token.IsCancellationRequested) break;
                await gateway.SendHeartbeatPingAsync(lobbyId);
            }
            catch (TaskCanceledException)
            {
                break;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[UgsLobbySessionService] Falha no heartbeat do lobby '{lobbyId}': {ex.Message}");
            }
        }
    }

    private static async Task DeleteSafeAsync(string lobbyId, IUgsLobbyGateway gateway)
    {
        if (string.IsNullOrWhiteSpace(lobbyId) || gateway == null) return;
        try
        {
            await gateway.DeleteLobbyAsync(lobbyId);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[UgsLobbySessionService] Falha ao remover lobby '{lobbyId}': {ex.Message}");
        }
    }
}

public class LobbyManager : MonoBehaviour
{
    public PlayerData PlayerData;

    // Keep the Relay code available after the scene-local manager is destroyed.
    public static string CurrentJoinCode { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetJoinCode()
    {
        CurrentJoinCode = null;
    }

    private void OnEnable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
        }
    }

    private void HandleClientConnected(ulong id)
    {
        Debug.Log($"[LobbyManager] 🟢 Client Connected! Client ID: {id}");
    }

    private void HandleClientDisconnected(ulong id)
    {
        Debug.Log($"[LobbyManager] 🔴 Client Disconnected or failed to connect! Client ID: {id}");
        if (NetworkManager.Singleton != null && id == NetworkManager.Singleton.LocalClientId)
        {
            string reason = NetworkManager.Singleton.DisconnectReason;
            string displayMsg = string.IsNullOrEmpty(reason)
                ? "Conexão com o Host perdida ou recusada."
                : reason;
            if (!string.IsNullOrEmpty(reason))
            {
                Debug.LogWarning($"[LobbyManager] Disconnect Reason: {reason}");
            }
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show(displayMsg, ScaryParty.Pizzeria.Presentation.NotificationType.Error);
            ScaryParty.UI.Piaria.Presenters.ModalPresenter.Instance?.ShowAlert("Desconectado da Sala", displayMsg);
        }
    }

    private async void Start()
    {
        Debug.Log("[LobbyManager] Start called. Loading PlayerData...");
        if (PlayerData == null)
        {
            PlayerData = Resources.Load<PlayerData>("PlayerData");
        }
        Debug.Log($"[LobbyManager] PlayerData loaded: {PlayerData?.PlayerName}");

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
        UgsLobbySessionService.StopHostingLobby();
        var coord = ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance();
        int opToken = coord.BeginConnectionOperation("Criando sala Relay...", 18f, () => CreateRelayRoom(roomName));

        if (NetworkManager.Singleton == null)
        {
            coord.FailConnectionOperation(opToken, "NetworkManager não encontrado para criar a sala.");
            return;
        }

        Debug.Log("[LobbyManager] Tentando criar sala Relay (WSS)...");

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            if (this == null || !coord.IsOperationActive(opToken)) return;

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (this == null || !coord.IsOperationActive(opToken)) return;
            CurrentJoinCode = joinCode;
            Debug.Log($"[LobbyManager] ✅ Sala Relay Criada! JOIN CODE: {joinCode}");

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            string host = allocation.RelayServer.IpV4;
            ushort port = (ushort)allocation.RelayServer.Port;
            bool isSecure = false;

            Debug.Log("[LobbyManager] --- AVAILABLE HOST ENDPOINTS ---");
            foreach (var ep in allocation.ServerEndpoints)
            {
                Debug.Log($"[LobbyManager] Endpoint: {ep.ConnectionType} -> {ep.Host}:{ep.Port} | Secure: {ep.Secure}");
            }
            Debug.Log("[LobbyManager] ----------------------------------");

            foreach (var endpoint in allocation.ServerEndpoints)
            {
                if (endpoint.ConnectionType == "wss")
                {
                    host = endpoint.Host;
                    port = (ushort)endpoint.Port;
                    isSecure = endpoint.Secure;
                    if (port == 443) 
                    {
                        Debug.Log($"[LobbyManager] Preferring WSS endpoint on port 443: {host}:{port}");
                        break;
                    }
                }
            }

            // Constrói RelayServerData manualmente passando isWebSocket = true (último argumento)
            var relayServerData = new RelayServerData(
                host,
                port,
                allocation.AllocationIdBytes,
                allocation.ConnectionData,
                allocation.ConnectionData,
                allocation.Key,
                isSecure,
                true // isWebSocket
            );

            // Garante que o NetworkManager não esteja em estado inválido de tentativa anterior
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager ainda estava ativo. Fazendo Shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
                if (this == null || !coord.IsOperationActive(opToken)) return;
            }

            ConfigureTransportCapacity(transport);
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
                    var createdLobby = await LobbyService.Instance.CreateLobbyAsync(roomName, 4, lobbyOptions);
                    if (this == null || !coord.IsOperationActive(opToken))
                    {
                        if (createdLobby != null)
                        {
                            _ = UgsLobbySessionService.DeleteOrphanLobbyAsync(createdLobby.Id);
                        }
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                        {
                            NetworkManager.Singleton.Shutdown();
                        }
                        CurrentJoinCode = null;
                        return;
                    }

                    if (createdLobby != null)
                    {
                        Debug.Log($"[LobbyManager] ✅ Lobby criado: {createdLobby.Id}");
                        UgsLobbySessionService.AdoptHostedLobby(createdLobby.Id);
                    }
                }
                catch (LobbyServiceException e)
                {
                    if (this == null || !coord.IsOperationActive(opToken))
                    {
                        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                        {
                            NetworkManager.Singleton.Shutdown();
                        }
                        CurrentJoinCode = null;
                        return;
                    }
                    Debug.LogError($"[LobbyManager] Falha ao criar Lobby: {e.Message}");
                }

                if (this == null || !coord.IsOperationActive(opToken)) return;

                var status = ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance()
                    .BeginNetworkedTransition("ReadyScene");
                if (status != SceneEventProgressStatus.Started)
                {
                    Debug.LogWarning($"[LobbyManager] Falha ao iniciar transição de rede para ReadyScene ({status}).");
                    coord.FailConnectionOperation(opToken, $"Falha ao iniciar transição de rede para ReadyScene ({status}).");
                }
            }
            else
            {
                if (this == null || !coord.IsOperationActive(opToken)) return;
                Debug.LogWarning("[LobbyManager] StartHost Relay falhou. Tentando iniciar Host LAN como fallback...");
                StartLANHost(roomName);
            }
        }
        catch (System.Exception e)
        {
            if (this == null || !coord.IsOperationActive(opToken)) return;
            Debug.LogWarning($"[LobbyManager] Falha no Relay: {e.Message}. Iniciando Host LAN como fallback...");
            StartLANHost(roomName);
        }
    }

    public async void JoinRelayRoom(string joinCode)
    {
        UgsLobbySessionService.StopHostingLobby();
        var coord = ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance();
        int opToken = coord.BeginConnectionOperation("Entrando na sala Relay...", 15f, () => JoinRelayRoom(joinCode));

        if (NetworkManager.Singleton == null || string.IsNullOrWhiteSpace(joinCode))
        {
            coord.FailConnectionOperation(opToken, "Código de sala inválido ou rede indisponível.");
            return;
        }

        Debug.Log($"[LobbyManager] Tentando entrar na sala com o código: {joinCode} (WSS)...");
        CurrentJoinCode = joinCode;

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            if (this == null || !coord.IsOperationActive(opToken)) return;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            string host = joinAllocation.RelayServer.IpV4;
            ushort port = (ushort)joinAllocation.RelayServer.Port;
            bool isSecure = false;

            Debug.Log("[LobbyManager] --- AVAILABLE JOIN ENDPOINTS ---");
            foreach (var ep in joinAllocation.ServerEndpoints)
            {
                Debug.Log($"[LobbyManager] Endpoint: {ep.ConnectionType} -> {ep.Host}:{ep.Port} | Secure: {ep.Secure}");
            }
            Debug.Log("[LobbyManager] ----------------------------------");

            foreach (var endpoint in joinAllocation.ServerEndpoints)
            {
                if (endpoint.ConnectionType == "wss")
                {
                    host = endpoint.Host;
                    port = (ushort)endpoint.Port;
                    isSecure = endpoint.Secure;
                    if (port == 443) 
                    {
                        Debug.Log($"[LobbyManager] Preferring WSS endpoint on port 443: {host}:{port}");
                        break;
                    }
                }
            }

            // Constrói RelayServerData manualmente passando isWebSocket = true (último argumento)
            var relayServerData = new RelayServerData(
                host,
                port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData, // HostConnectionData é usado no Join!
                joinAllocation.Key,
                isSecure,
                true // isWebSocket
            );

            // Garante que o NetworkManager não esteja em estado inválido de tentativa anterior
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager ainda estava ativo. Fazendo Shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
                if (this == null || !coord.IsOperationActive(opToken)) return;
            }

            ConfigureTransportCapacity(transport);
            transport.UseWebSockets = true;
            transport.SetRelayServerData(relayServerData);

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[LobbyManager] ✅ StartClient executado! Aguardando o Host...");
            }
            else
            {
                Debug.LogError("[LobbyManager] ❌ Falha ao tentar iniciar o Cliente.");
                coord.FailConnectionOperation(opToken, "Falha ao iniciar conexão com a sala Relay.");
            }
        }
        catch (RelayServiceException e)
        {
            if (this == null || !coord.IsOperationActive(opToken)) return;
            Debug.LogError($"[LobbyManager] Código inválido ou falha ao conectar: {e.Message}");
            coord.FailConnectionOperation(opToken, $"Código inválido ou sala indisponível: {e.Message}");
        }
        catch (System.Exception e)
        {
            if (this == null || !coord.IsOperationActive(opToken)) return;
            Debug.LogError($"[LobbyManager] Erro inesperado ao entrar na sala Relay: {e.Message}");
            coord.FailConnectionOperation(opToken, $"Erro ao conectar na sala: {e.Message}");
        }
    }

    public async void StartLANHost(string roomName)
    {
        UgsLobbySessionService.StopHostingLobby();
        var coord = ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance();
        int opToken = coord.BeginConnectionOperation("Iniciando sala LAN...", 15f, () => StartLANHost(roomName));

        if (NetworkManager.Singleton == null)
        {
            coord.FailConnectionOperation(opToken, "NetworkManager não encontrado para iniciar servidor LAN.");
            return;
        }
        CurrentJoinCode = null;
        Debug.Log("[LobbyManager] Tentando iniciar Host (LAN)...");

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[LobbyManager] NetworkManager ativo. Fazendo Shutdown...");
            NetworkManager.Singleton.Shutdown();
            await Task.Delay(500);
            if (this == null || !coord.IsOperationActive(opToken)) return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        ConfigureTransportCapacity(transport);
        // Remove relay data
        transport.SetRelayServerData(default);
        // Configure connection for LAN (listen on all interfaces)
        transport.UseWebSockets = false;
        transport.SetConnectionData("0.0.0.0", 7777, "0.0.0.0");

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log("[LobbyManager] ✅ Host LAN iniciado com sucesso. Carregando ReadyScene...");
            
            // Inicia Room Discovery broadcast no singleton persistente
            if (RoomDiscoveryService.Instance != null)
            {
                RoomDiscoveryService.Instance.StartHosting(roomName, 7777, 4);
            }

            var status = coord.BeginNetworkedTransition("ReadyScene");
            if (status != SceneEventProgressStatus.Started)
            {
                Debug.LogWarning($"[LobbyManager] Falha ao iniciar transição LAN para ReadyScene ({status}).");
                coord.FailConnectionOperation(opToken, $"Falha ao iniciar transição LAN para ReadyScene ({status}).");
            }
        }
        else
        {
            Debug.LogWarning("[LobbyManager] Falha ao iniciar Host LAN via Netcode.");
            coord.FailConnectionOperation(opToken, "Não foi possível iniciar o servidor LAN.");
        }
    }

    public async void JoinLANClient(string ipAddress)
    {
        UgsLobbySessionService.StopHostingLobby();
        var coord = ScaryParty.Pizzeria.Presentation.PizzeriaSceneLoadingCoordinator.EnsureInstance();
        int opToken = coord.BeginConnectionOperation("Conectando ao host LAN...", 12f, () => JoinLANClient(ipAddress));

        if (NetworkManager.Singleton == null || string.IsNullOrWhiteSpace(ipAddress))
        {
            coord.FailConnectionOperation(opToken, "Endereço IP inválido ou NetworkManager indisponível.");
            return;
        }
        CurrentJoinCode = null;
        Debug.Log($"[LobbyManager] Tentando entrar em Host LAN no IP: {ipAddress}...");

        try
        {
            if (NetworkManager.Singleton.IsListening)
            {
                Debug.LogWarning("[LobbyManager] NetworkManager ativo. Fazendo Shutdown...");
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(500);
                if (this == null || !coord.IsOperationActive(opToken)) return;
            }

            if (RoomDiscoveryService.Instance != null)
            {
                RoomDiscoveryService.Instance.StopHosting();
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            ConfigureTransportCapacity(transport);
            // Remove relay data
            transport.SetRelayServerData(default);
            // Configure connection for LAN (connect to target IP)
            transport.UseWebSockets = false;
            transport.SetConnectionData(ipAddress, 7777);

            if (NetworkManager.Singleton.StartClient())
            {
                Debug.Log("[LobbyManager] ✅ StartClient LAN executado! Aguardando o Host...");
            }
            else
            {
                Debug.LogError("[LobbyManager] ❌ Falha ao tentar iniciar o Cliente LAN.");
                coord.FailConnectionOperation(opToken, $"Não foi possível conectar ao host LAN ({ipAddress}).");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[LobbyManager] Erro ao conectar em LAN ({ipAddress}): {e.Message}");
            coord.FailConnectionOperation(opToken, $"Erro ao conectar ao host LAN: {e.Message}");
        }
    }

    private static void ConfigureTransportCapacity(UnityTransport transport)
    {
        if (transport == null) return;
        if (transport.MaxPacketQueueSize < 2048) transport.MaxPacketQueueSize = 2048;
        if (transport.MaxPayloadSize < 32768) transport.MaxPayloadSize = 32768;
        if (transport.MaxSendQueueSize > 0 && transport.MaxSendQueueSize < 262144) transport.MaxSendQueueSize = 262144;
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
}
