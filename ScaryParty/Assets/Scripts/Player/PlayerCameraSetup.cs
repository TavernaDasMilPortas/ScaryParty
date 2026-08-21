using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Attached to the Player prefab to handle camera logic in multiplayer.
/// Ensures that only the local player's camera is active.
/// </summary>
public class PlayerCameraSetup : NetworkBehaviour
{
    [Tooltip("The Main Camera inside the Player prefab")]
    public GameObject mainCamera;

    [Tooltip("The Cinemachine Follow Camera inside the Player prefab")]
    public GameObject followCamera;

    public override void OnNetworkSpawn()
    {
        // If we are not the local player, disable this player's cameras!
        if (!IsOwner)
        {
            if (mainCamera != null) mainCamera.SetActive(false);
            if (followCamera != null) followCamera.SetActive(false);
        }
        else
        {
            // We are the local player, make sure our cameras are active
            if (mainCamera != null) 
            {
                mainCamera.SetActive(true);
                // Hide MinimapOnly layer from the main camera!
                Camera cam = mainCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
                    if (minimapLayer >= 0)
                    {
                        cam.cullingMask &= ~(1 << minimapLayer);
                    }
                }
            }
            if (followCamera != null) followCamera.SetActive(true);
            
            // Hook up minimap tracking
            if (MinimapRouteManager.Instance != null)
            {
                MinimapRouteManager.Instance.TrackPlayer(transform);
            }
        }
    }
}
