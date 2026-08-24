using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;

public class LobbyManager : MonoBehaviour
{
    public RoomDiscoveryService DiscoveryService;
    public PlayerData PlayerData;

    private void Start()
    {
        Debug.Log("[LobbyManager] Start called. Loading PlayerData...");
        if (PlayerData == null)
        {
            PlayerData = Resources.Load<PlayerData>("PlayerData");
        }
        Debug.Log($"[LobbyManager] PlayerData loaded: {PlayerData?.PlayerName}");
    }

    public static ushort GetAvailablePort()
    {
        // Pega uma porta UDP aleatória que esteja livre no sistema
        using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
        {
            socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            ushort port = (ushort)((IPEndPoint)socket.LocalEndPoint).Port;
            Debug.Log($"[LobbyManager] Found available random port: {port}");
            return port;
        }
    }

    public void CreateRoom(string roomName, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[LobbyManager] NetworkManager.Singleton is null! Cannot create room.");
            return;
        }

        Debug.Log($"[LobbyManager] Attempting to create room '{roomName}' on port {port}...");
        
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        string localIp = RoomDiscoveryService.GetLocalIPAddress();
        
        // Host escuta em todas as interfaces (0.0.0.0)
        // E conecta-se a si mesmo localmente via 127.0.0.1 ou o IP LAN
        Debug.Log($"[LobbyManager] Setting UnityTransport: Connection IP={localIp}, Listen IP=0.0.0.0, Port={port}");
        transport.SetConnectionData(localIp, port, "0.0.0.0"); 

        if (NetworkManager.Singleton.StartHost())
        {
            Debug.Log($"[LobbyManager] ✅ StartHost successful! Starting broadcast as '{roomName}'.");
            DiscoveryService.StartHosting(roomName, port, 4); // Max 4 players
            
            Debug.Log("[LobbyManager] Loading ReadyScene...");
            NetworkManager.Singleton.SceneManager.LoadScene("ReadyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
        else
        {
            Debug.LogError("[LobbyManager] ❌ Failed to start host. Unity Netcode blocked it (port in use or misconfigured).");
        }
    }

    public void JoinRoom(RoomInfo info)
    {
        Debug.Log($"[LobbyManager] Joining selected room: {info.RoomName} at {info.HostIP}:{info.Port}");
        JoinByAddress(info.HostIP, info.Port);
    }

    public void JoinByAddress(string ip, ushort port)
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[LobbyManager] NetworkManager.Singleton is null! Cannot join.");
            return;
        }

        Debug.Log($"[LobbyManager] Attempting to join {ip}:{port} as Client...");

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetConnectionData(ip, port);

        if (NetworkManager.Singleton.StartClient())
        {
            Debug.Log($"[LobbyManager] ✅ StartClient successful! Waiting for host to transition scene...");
        }
        else
        {
            Debug.LogError("[LobbyManager] ❌ Failed to start client.");
        }
    }
}

