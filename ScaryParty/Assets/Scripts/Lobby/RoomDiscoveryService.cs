using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class RoomDiscoveryService : MonoBehaviour
{
    private const int BROADCAST_PORT = 7778;
    private const float BROADCAST_INTERVAL = 2f;
    private const float ROOM_TIMEOUT = 5f;

    public Action<List<RoomInfo>> OnRoomsUpdated;

    private UdpClient _udpClient;
    private float _nextBroadcastTime;
    
    private Dictionary<string, RoomEntry> _discoveredRooms = new Dictionary<string, RoomEntry>();
    private bool _isHosting;
    private RoomInfo _myRoomInfo;

    private class RoomEntry
    {
        public RoomInfo Info;
        public float LastSeenTime;
    }

    private void Start()
    {
        StartListening();
    }

    private void Update()
    {
        if (_isHosting && Time.time >= _nextBroadcastTime)
        {
            BroadcastRoom();
            _nextBroadcastTime = Time.time + BROADCAST_INTERVAL;
        }

        CleanupStaleRooms();
    }

    private void OnDestroy()
    {
        StopListening();
    }

    public void StartHosting(string roomName, ushort port, int maxPlayers)
    {
        _myRoomInfo = new RoomInfo
        {
            RoomName = roomName,
            HostIP = GetLocalIPAddress(),
            Port = port,
            PlayerCount = 1,
            MaxPlayers = maxPlayers
        };
        _isHosting = true;
        _nextBroadcastTime = Time.time;
        Debug.Log($"[RoomDiscovery] Started hosting room '{roomName}' on {_myRoomInfo.HostIP}:{port}");
    }

    public void StopHosting()
    {
        _isHosting = false;
        Debug.Log("[RoomDiscovery] Stopped hosting.");
    }

    private void StartListening()
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));
            _udpClient.EnableBroadcast = true; // ESSENCIAL PARA FUNCIONAR LAN
            
            _udpClient.BeginReceive(OnReceive, null);
            Debug.Log($"[RoomDiscovery] Started UDP listener on port {BROADCAST_PORT}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomDiscovery] Failed to start listening on port {BROADCAST_PORT}: {e.Message}");
        }
    }

    private void StopListening()
    {
        if (_udpClient != null)
        {
            _udpClient.Close();
            _udpClient = null;
            Debug.Log("[RoomDiscovery] UDP listener stopped.");
        }
    }

    private void OnReceive(IAsyncResult ar)
    {
        if (_udpClient == null) return;

        try
        {
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, BROADCAST_PORT);
            byte[] bytes = _udpClient.EndReceive(ar, ref endPoint);
            string json = Encoding.UTF8.GetString(bytes);

            RoomInfo info = JsonUtility.FromJson<RoomInfo>(json);
            
            // Ignorar mensagens de nós mesmos se estamos hospedando
            if (_isHosting && info.HostIP == _myRoomInfo.HostIP && info.Port == _myRoomInfo.Port)
            {
                // Ignora silenciosamente
            }
            else
            {
                Debug.Log($"[RoomDiscovery] Received broadcast from {info.HostIP}:{info.Port} (Room: {info.RoomName})");
                MainThreadDispatcher.Enqueue(() => UpdateRoomInfo(info));
            }

            // Continuar ouvindo
            _udpClient.BeginReceive(OnReceive, null);
        }
        catch (ObjectDisposedException) { /* Ignorar fechamento normal */ }
        catch (Exception e)
        {
            Debug.LogError($"[RoomDiscovery] Receive error: {e.Message}");
        }
    }

    private void BroadcastRoom()
    {
        if (!_isHosting) return;

        try
        {
            string json = JsonUtility.ToJson(_myRoomInfo);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
            
            using (UdpClient broadcastClient = new UdpClient())
            {
                broadcastClient.EnableBroadcast = true;
                broadcastClient.Send(bytes, bytes.Length, endPoint);
            }
            
            Debug.Log($"[RoomDiscovery] Broadcasted room '{_myRoomInfo.RoomName}' at {_myRoomInfo.HostIP}:{_myRoomInfo.Port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[RoomDiscovery] Broadcast error: {e.Message}");
        }
    }

    private void UpdateRoomInfo(RoomInfo info)
    {
        string key = $"{info.HostIP}:{info.Port}";
        if (_discoveredRooms.ContainsKey(key))
        {
            _discoveredRooms[key].Info = info;
            _discoveredRooms[key].LastSeenTime = Time.time;
        }
        else
        {
            Debug.Log($"[RoomDiscovery] 🆕 New room discovered: {info.RoomName} ({key})");
            _discoveredRooms[key] = new RoomEntry { Info = info, LastSeenTime = Time.time };
            NotifyRoomsUpdated();
        }
    }

    private void CleanupStaleRooms()
    {
        bool changed = false;
        List<string> keysToRemove = new List<string>();

        foreach (var kvp in _discoveredRooms)
        {
            if (Time.time - kvp.Value.LastSeenTime > ROOM_TIMEOUT)
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            Debug.Log($"[RoomDiscovery] ⏳ Room {key} timed out and removed.");
            _discoveredRooms.Remove(key);
            changed = true;
        }

        if (changed)
        {
            NotifyRoomsUpdated();
        }
    }

    private void NotifyRoomsUpdated()
    {
        List<RoomInfo> rooms = new List<RoomInfo>();
        foreach (var entry in _discoveredRooms.Values)
        {
            rooms.Add(entry.Info);
        }
        OnRoomsUpdated?.Invoke(rooms);
    }

    public static string GetLocalIPAddress()
    {
        try 
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                // Conectar a um IP externo forca o SO a usar a placa de rede com internet (ignora adaptadores virtuais)
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        } 
        catch(Exception e) 
        {
            Debug.LogError($"[RoomDiscovery] Failed to get Local IP: {e.Message}");
        }
        return "127.0.0.1";
    }
}

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_instance == null)
        {
            GameObject go = new GameObject("MainThreadDispatcher");
            _instance = go.AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(go);
        }
    }

    private void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}
