using UnityEngine;
using Unity.Netcode;

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
    }

    private void Start()
    {
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
}
