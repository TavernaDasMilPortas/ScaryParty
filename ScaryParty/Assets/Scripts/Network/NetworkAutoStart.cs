using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Automatically starts the NetworkManager as Host when the scene loads.
/// Attach this to the same GameObject as NetworkManager (--- NETWORK ---).
/// Disable this component if you want to start the network session manually.
/// </summary>
public class NetworkAutoStart : MonoBehaviour
{
    [Tooltip("If true, automatically calls StartHost() when the scene starts.")]
    public bool autoStartHost = false;

    private void Start()
    {
        if (!autoStartHost) return;

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[NetworkAutoStart] NetworkManager.Singleton is null! Make sure NetworkManager is in the scene.");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.Log("[NetworkAutoStart] NetworkManager is already running, skipping auto-start.");
            return;
        }

        NetworkManager.Singleton.StartHost();
        Debug.Log("[NetworkAutoStart] ✅ Started as Host automatically.");
    }
}
