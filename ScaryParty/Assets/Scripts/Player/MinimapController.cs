using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Placed on the Player prefab. On spawn, registers this player with the MinimapRouteManager
/// so the minimap camera follows the local player.
/// Also handles toggling the minimap (M key is handled inside MinimapRouteManager.Update).
/// </summary>
public class MinimapController : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        // Only the local owner needs to register with the minimap
        if (!IsOwner) return;

        // Wait one frame so MinimapRouteManager is guaranteed to be initialized
        StartCoroutine(RegisterWithMinimap());
    }

    private System.Collections.IEnumerator RegisterWithMinimap()
    {
        yield return null; // One frame delay

        if (MinimapRouteManager.Instance != null)
        {
            MinimapRouteManager.Instance.TrackPlayer(transform);
        }
        else
        {
            Debug.LogWarning("[MinimapController] MinimapRouteManager not found in scene. Add it via Scene Builder.");
        }
    }
}
