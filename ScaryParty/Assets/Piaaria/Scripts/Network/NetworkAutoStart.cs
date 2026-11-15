using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// Automatically starts the NetworkManager as Host when the scene loads (for solo testing).
/// Attach this to the same GameObject as NetworkManager (--- NETWORK ---).
/// If a NetworkManager already exists from a previous scene (e.g. Lobby),
/// this duplicate instance is safely destroyed immediately.
/// </summary>
public class NetworkAutoStart : MonoBehaviour
{
    [Tooltip("If true, automatically calls StartHost() when the scene starts.")]
    public bool autoStartHost = false;

    private void Awake()
    {
        var nm = GetComponent<NetworkManager>();
        if (nm != null && NetworkManager.Singleton != null && NetworkManager.Singleton != nm)
        {
            Debug.Log("[NetworkAutoStart] Duplicate NetworkManager GameObject found in scene. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }

        EnsureTransportCapacity(nm);
    }

    private void Start()
    {
        if (NetworkManager.Singleton != null)
            EnsureTransportCapacity(NetworkManager.Singleton);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetworkAutoStart] NetworkManager is already running/connected. Skipping auto-start.");
            return;
        }

        if (!autoStartHost) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkAutoStart] NetworkManager.Singleton is null! Make sure NetworkManager is in the scene.");
            return;
        }

        NetworkManager.Singleton.StartHost();
        Debug.Log("[NetworkAutoStart] ✅ Started as Host automatically.");
    }

    private static void EnsureTransportCapacity(NetworkManager nm)
    {
        if (nm == null) return;
        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null) return;
        if (transport.MaxPacketQueueSize < 2048) transport.MaxPacketQueueSize = 2048;
        if (transport.MaxPayloadSize < 32768) transport.MaxPayloadSize = 32768;
        if (transport.MaxSendQueueSize > 0 && transport.MaxSendQueueSize < 262144) transport.MaxSendQueueSize = 262144;
    }
}
