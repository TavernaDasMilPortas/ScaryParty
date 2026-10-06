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
                Camera cam = mainCamera.GetComponent<Camera>();
                if (cam != null)
                {
                    int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
                    if (minimapLayer >= 0) cam.cullingMask &= ~(1 << minimapLayer);
                    
                    int localPlayerLayer = LayerMask.NameToLayer("LocalPlayerModel");
                    if (localPlayerLayer >= 0) cam.cullingMask &= ~(1 << localPlayerLayer);

                    int firstPersonLayer = LayerMask.NameToLayer("FirstPersonOnly");
                    if (firstPersonLayer >= 0) cam.cullingMask |= (1 << firstPersonLayer);
                }
            }
            if (followCamera != null) 
            {
                // Em primeira pessoa, não precisamos do Cinemachine Virtual Camera conflitando.
                // O FirstPersonCameraController assumirá a posição e rotação absolutas.
                followCamera.SetActive(false);
            }
            
            if (mainCamera != null)
            {
                var brain = mainCamera.GetComponent("CinemachineBrain") ?? mainCamera.GetComponent("Unity.Cinemachine.CinemachineBrain");
                if (brain is Behaviour b) b.enabled = false;
            }
            
            // Add First Person sync script
            if (GetComponent<ScaryParty.Player.FirstPersonCameraController>() == null)
            {
                gameObject.AddComponent<ScaryParty.Player.FirstPersonCameraController>();
            }

            // Hide the player's own body but cast shadows
            int localLayer = LayerMask.NameToLayer("LocalPlayerModel");
            if (localLayer < 0) localLayer = 0; // fallback se não rodou o builder
            
            var smrs = GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (var smr in smrs)
            {
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (localLayer > 0) smr.gameObject.layer = localLayer;
            }
            var mrs = GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in mrs)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                if (localLayer > 0) mr.gameObject.layer = localLayer;
            }
            
            // Hook up minimap tracking
            if (MinimapRouteManager.Instance != null)
            {
                MinimapRouteManager.Instance.TrackPlayer(transform);
            }
        }
    }
}
