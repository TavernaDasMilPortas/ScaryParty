using UnityEngine;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Handles raycasting for interactables and manages what the player is holding in their hands.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("The camera used for raycasting")]
    public Camera mainCamera;
    
    [Tooltip("How far the player can interact with objects")]
    public float interactionDistance = 3f;
    
    [Tooltip("Layers that contain interactable objects")]
    public LayerMask interactableLayer;

    private IInteractable _currentInteractable;
    private float _lastSyncTime = 0f;

    public override void OnNetworkSpawn()
    {
        // Only the owning client runs interaction logic
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        if (mainCamera == null)
            mainCamera = Camera.main;

        // Anexar adaptadores da Pizzaria se ausentes
        if (GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();

        if (GetComponent<ScaryParty.Pizzeria.Player.BackpackController>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Player.BackpackController>();

        if (GetComponent<ScaryParty.Pizzeria.Integration.CargoMapAdapter>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Integration.CargoMapAdapter>();

        // SERVER AUTHORITY: Teleport the player to the pizzaria spawn point
        // If this client is also the server (Host), do it directly.
        // If not, request it via RPC.
        if (IsServer)
        {
            TeleportToSpawn();
        }
        else
        {
            RequestSpawnPositionServerRpc();
        }
    }

    private void TeleportToSpawn()
    {
        if (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition((int)OwnerClientId);
            transform.rotation = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation((int)OwnerClientId);
            if (cc != null) cc.enabled = true;
            return;
        }

        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null && cityGen.CityData != null)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = cityGen.CityData.pizzariaPosition + new Vector3(0, 0.1f, -12f);
            transform.rotation = Quaternion.Euler(0, 180, 0);
            if (cc != null) cc.enabled = true;
        }
    }

    [ServerRpc]
    private void RequestSpawnPositionServerRpc()
    {
        if (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
        {
            Vector3 spawnPos = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition((int)OwnerClientId);
            Quaternion spawnRot = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation((int)OwnerClientId);
            TeleportClientRpc(spawnPos, spawnRot);
            return;
        }

        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null && cityGen.CityData != null)
        {
            Vector3 spawnPos = cityGen.CityData.pizzariaPosition + new Vector3(0, 0.1f, -12f);
            TeleportClientRpc(spawnPos, Quaternion.Euler(0, 180, 0));
        }
    }

    [ClientRpc]
    private void TeleportClientRpc(Vector3 position, Quaternion rotation)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        transform.position = position;
        transform.rotation = rotation;
        if (cc != null) cc.enabled = true;
    }

    private void Update()
    {
        if (!IsOwner) return;
        
        HandleRaycast();
        HandleInput();

        if (Time.time - _lastSyncTime > 0.5f)
        {
            _lastSyncTime = Time.time;
            SyncHeldPizzaCount();
        }
    }

    private void HandleRaycast()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Shoot a thick beam (SphereCast) from the player's chest, pointing where the camera looks
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f; // Approx chest height
        Vector3 rayDirection = mainCamera.transform.forward; // Aim down at the floor if camera looks down
        
        Ray ray = new Ray(rayOrigin, rayDirection);

        // If interactableLayer is 0 (not configured), fall back to Physics.DefaultRaycastLayers
        LayerMask mask = interactableLayer.value == 0 ? Physics.DefaultRaycastLayers : interactableLayer;

        // 0.5f radius makes it a 1-meter thick beam, impossible to miss the pizza box
        RaycastHit[] hits = Physics.SphereCastAll(ray, 0.5f, interactionDistance, mask);
        
        // Sort by distance
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        IInteractable interactable = null;

        foreach (var hit in hits)
        {
            // Ignore self
            if (hit.collider.transform.IsChildOf(this.transform))
                continue;

            interactable = hit.collider.GetComponentInParent<IInteractable>();
            if (interactable != null)
            {
                break; // Found the closest valid interactable
            }
        }

        if (interactable != null)
        {
            if (_currentInteractable != interactable)
            {
                if (_currentInteractable != null) _currentInteractable.OnLoseFocus();
                _currentInteractable = interactable;
                _currentInteractable.OnFocus();
            }

            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteractionPrompt(_currentInteractable.InteractPrompt);

            return;
        }

        // Hit nothing or something without an interactable
        if (_currentInteractable != null)
        {
            _currentInteractable.OnLoseFocus();
            _currentInteractable = null;

            if (UIManager.Instance != null)
                UIManager.Instance.HideInteractionPrompt();
        }
    }

    private void HandleInput()
    {
        bool interactPressed = false;
        
#if ENABLE_INPUT_SYSTEM
        // Simple fallback checking Keyboard directly if InputSystem is used but no specific action mapped
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            interactPressed = true;
        }
#else
        if (Input.GetKeyDown(KeyCode.E))
        {
            interactPressed = true;
        }
#endif

        if (interactPressed && _currentInteractable != null)
        {
            _currentInteractable.OnInteract(this.gameObject);
        }

        // Suporte a segurar [F] para trabalhar em bancadas da Pizzaria (Preparo / Embalagem)
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
            var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;

            if (Keyboard.current.fKey.wasPressedThisFrame && _currentInteractable is ScaryParty.Pizzeria.Stations.PrepStation prep)
            {
                if (netState != null && cmd != null && Unity.Netcode.NetworkManager.Singleton != null)
                {
                    bool stationEmpty = true;
                    ulong itemInHandId = 0;
                    ulong localClientId = Unity.Netcode.NetworkManager.Singleton.LocalClientId;

                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && item.HolderId == (ulong)prep.stationId)
                        {
                            stationEmpty = false;
                        }
                        if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && item.HolderId == localClientId)
                        {
                            itemInHandId = item.ItemId;
                        }
                    }

                    if (stationEmpty && itemInHandId != 0)
                    {
                        cmd.TransferItemServerRpc(itemInHandId, (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot, (ulong)prep.stationId, 0);
                    }
                    else
                    {
                        cmd.StartWorkServerRpc(prep.stationId, 0, 0); // 0 = auto-detect processId
                    }
                }
                else if (cmd != null)
                {
                    cmd.StartWorkServerRpc(prep.stationId, 0, 0); // 0 = auto-detect processId
                }
            }
            else if (Keyboard.current.fKey.isPressed && _currentInteractable is ScaryParty.Pizzeria.Stations.PrepStation prepActive)
            {
                if (netState != null && cmd != null)
                {
                    // Send heartbeat so progress continues
                    cmd.HeartbeatWorkServerRpc(prepActive.stationId, 0);

                    // Check if progress reached 1.0 to auto-complete
                    for (int i = 0; i < netState.StationSlots.Count; i++)
                    {
                        var slot = netState.StationSlots[i];
                        if (slot.StationId == prepActive.stationId && slot.Progress >= 1f)
                        {
                            cmd.CompleteWorkServerRpc(prepActive.stationId, 0);
                            break;
                        }
                    }
                }
            }
            else if (Keyboard.current.fKey.wasReleasedThisFrame && _currentInteractable is ScaryParty.Pizzeria.Stations.PrepStation prepRel)
            {
                if (cmd != null) cmd.CancelWorkServerRpc(prepRel.stationId, 0);
            }
            else if (Keyboard.current.fKey.wasPressedThisFrame && _currentInteractable is ScaryParty.Pizzeria.Stations.PackagingStation pkg)
            {
                if (netState != null && cmd != null)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && item.HolderId == (ulong)pkg.stationId)
                        {
                            bool canPackage = item.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.PizzaBase || 
                                              item.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Pizza;
                            if (canPackage && item.PackagingState != (byte)ScaryParty.Pizzeria.Domain.Types.PackagingState.Boxed)
                            {
                                cmd.StartPackagingServerRpc(item.ItemId);
                            }
                            break;
                        }
                    }
                }
            }
        }
#endif
    }

    public bool IsHandFull(ScaryParty.Pizzeria.Domain.Types.HandSlotIndex hand)
    {
        var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
        if (netState == null) return false;
        ulong myId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < netState.Items.Count; i++)
        {
            var it = netState.Items[i];
            if (it.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && it.HolderId == myId && it.SlotId == (int)hand)
                return true;
        }
        // Also check tools
        for (int i = 0; i < netState.Tools.Count; i++)
        {
            var tool = netState.Tools[i];
            if (tool.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && tool.HolderId == myId && tool.SlotId == (int)hand)
                return true;
        }
        return false;
    }

    public int CountItemsInHands()
    {
        int count = 0;
        var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
        if (netState == null) return 0;
        ulong myId = NetworkManager.Singleton.LocalClientId;
        for (int i = 0; i < netState.Items.Count; i++)
        {
            if (netState.Items[i].LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && netState.Items[i].HolderId == myId)
                count++;
        }
        for (int i = 0; i < netState.Tools.Count; i++)
        {
            if (netState.Tools[i].LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && netState.Tools[i].HolderId == myId)
                count++;
        }
        return count;
    }

    /// <summary>
    /// Conta quantas pizzas o jogador está segurando e informa ao servidor via ServerRpc.
    /// O servidor atualiza PlayerState.HeldPizzas (NetworkVariable), tornando o dado
    /// visível para todos os clientes no scoreboard.
    /// </summary>
    private void SyncHeldPizzaCount()
    {
        int count = CountItemsInHands();

        var ps = GetComponent<PlayerState>();
        if (ps != null)
        {
            ps.UpdateHeldPizzasServerRpc(count);
        }
    }
}
