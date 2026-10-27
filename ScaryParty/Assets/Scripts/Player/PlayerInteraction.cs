using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Interaction;
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

    [Tooltip("Layers que contêm obstáculos sólidos (paredes, balcões) que ocluem interação")]
    public LayerMask obstacleLayer;

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

        if (GetComponent<ScaryParty.Pizzeria.Presentation.BackpackVisualizer>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Presentation.BackpackVisualizer>();

        if (GetComponent<ScaryParty.Pizzeria.Integration.CargoMapAdapter>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Integration.CargoMapAdapter>();

        // Sincroniza com a geração procedural da cidade
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null)
            cityGen.OnCityGenerated += OnCityGeneratedHandler;

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

    public override void OnNetworkDespawn()
    {
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null)
            cityGen.OnCityGenerated -= OnCityGeneratedHandler;

        base.OnNetworkDespawn();
    }

    private void OnCityGeneratedHandler()
    {
        if (!IsOwner) return;
        if (IsServer) TeleportToSpawn();
        else RequestSpawnPositionServerRpc();
    }

    private void TeleportToSpawn()
    {
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null && cityGen.CityData == null)
        {
            StartCoroutine(WaitForPizzeriaThenTeleport(false));
            return;
        }

        if (ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
        {
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition((int)OwnerClientId);
            transform.rotation = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation((int)OwnerClientId);
            if (cc != null) cc.enabled = true;
            return;
        }

        // A pizzaria (prefab de rede) pode ainda não ter spawnado: espera ela existir
        // para usar os anchors PlayerSpawn_X em vez de uma posição aproximada.
        StartCoroutine(WaitForPizzeriaThenTeleport(false));
    }

    private System.Collections.IEnumerator WaitForPizzeriaThenTeleport(bool viaClientRpc)
    {
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        float timeout = 15f;
        while (((cityGen != null && cityGen.CityData == null) || ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance == null) && timeout > 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        var root = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance;
        if (root != null)
        {
            Vector3 pos = root.GetPlayerSpawnPosition((int)OwnerClientId);
            Quaternion rot = root.GetPlayerSpawnRotation((int)OwnerClientId);
            if (viaClientRpc) TeleportClientRpc(pos, rot);
            else
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = pos;
                transform.rotation = rot;
                if (cc != null) cc.enabled = true;
            }
            yield break;
        }

        // Fallback legado (sem pizzaria de prefab)
        if (cityGen != null && cityGen.CityData != null)
        {
            Vector3 pos = cityGen.CityData.pizzariaPosition + new Vector3(0, 0.1f, -12f);
            Quaternion rot = Quaternion.Euler(0, 180, 0);
            if (viaClientRpc) TeleportClientRpc(pos, rot);
            else
            {
                var cc = GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                transform.position = pos;
                transform.rotation = rot;
                if (cc != null) cc.enabled = true;
            }
        }
    }

    [ServerRpc]
    private void RequestSpawnPositionServerRpc()
    {
        CityGenerator cityGen = FindObjectOfType<CityGenerator>();
        if (cityGen != null && cityGen.CityData != null && ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance != null)
        {
            Vector3 spawnPos = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnPosition((int)OwnerClientId);
            Quaternion spawnRot = ScaryParty.Pizzeria.Composition.PizzeriaRoot.Instance.GetPlayerSpawnRotation((int)OwnerClientId);
            TeleportClientRpc(spawnPos, spawnRot);
            return;
        }

        StartCoroutine(WaitForPizzeriaThenTeleport(true));
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
        if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
        {
            if (OutlineService.Instance != null)
            {
                OutlineService.Instance.SetTarget(null);
            }
            return;
        }
        
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

        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        
        // 1. Raycast de obstáculos sólidos (paredes, portas, móveis) para limitar alcance
        LayerMask obsMask = obstacleLayer.value != 0 ? obstacleLayer : (1 << 0);
        float maxReach = interactionDistance;

        if (Physics.Raycast(ray, out RaycastHit obsHit, interactionDistance, obsMask, QueryTriggerInteraction.Ignore))
        {
            if (obsHit.collider != null && !obsHit.collider.transform.IsChildOf(this.transform))
            {
                maxReach = obsHit.distance;
            }
        }

        // 2. Raycast direto para interagíveis até o alcance limitado pelo obstáculo
        LayerMask mask = interactableLayer.value == 0 ? Physics.DefaultRaycastLayers : interactableLayer;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxReach, mask, QueryTriggerInteraction.Collide);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        IInteractable interactable = null;

        foreach (var hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(this.transform))
                continue;

            var candidate = hit.collider.GetComponent<IInteractable>() ?? hit.collider.GetComponentInParent<IInteractable>();
            if (candidate != null && !string.IsNullOrEmpty(candidate.InteractPrompt))
            {
                interactable = candidate;
                break;
            }
        }

        // Fallback SphereCast com raio estreito (0.08m) limitado pelo mesmo alcance do obstáculo
        if (interactable == null)
        {
            RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.08f, maxReach, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in sphereHits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(this.transform))
                    continue;

                var candidate = hit.collider.GetComponent<IInteractable>() ?? hit.collider.GetComponentInParent<IInteractable>();
                if (candidate != null && !string.IsNullOrEmpty(candidate.InteractPrompt))
                {
                    interactable = candidate;
                    break;
                }
            }
        }

        if (interactable != null)
        {
            if (_currentInteractable != interactable)
            {
                if (_currentInteractable != null)
                {
                    _currentInteractable.OnLoseFocus();
                    ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
                }

                _currentInteractable = interactable;
                _currentInteractable.OnFocus();

                if (interactable is Component comp)
                {
                    var target = comp.GetComponent<ScaryParty.Interaction.OutlineTarget>() 
                                 ?? comp.GetComponentInParent<ScaryParty.Interaction.OutlineTarget>();
                    if (target == null)
                    {
                        target = comp.gameObject.AddComponent<ScaryParty.Interaction.OutlineTarget>();
                    }
                    ScaryParty.Interaction.OutlineService.Instance?.SetTarget(target);
                }
            }

            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteractionPrompt(_currentInteractable.InteractPrompt);

            return;
        }

        // Sem alvo em foco
        if (_currentInteractable != null)
        {
            _currentInteractable.OnLoseFocus();
            ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
            _currentInteractable = null;

            if (UIManager.Instance != null)
                UIManager.Instance.HideInteractionPrompt();
        }
    }

    private void OnDisable()
    {
        if (_currentInteractable != null)
        {
            _currentInteractable.OnLoseFocus();
            _currentInteractable = null;
        }
        ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
    }

    public override void OnDestroy()
    {
        ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
        base.OnDestroy();
    }

        private float _lastHeartbeatTime = 0f;

#if ENABLE_INPUT_SYSTEM
        private void HandleInput()
        {
            bool interactPressed = false;
            
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                interactPressed = true;
            }

            if (interactPressed && _currentInteractable != null)
            {
                _currentInteractable.OnInteract(this.gameObject);
            }

            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
                var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;

                if (_currentInteractable is ScaryParty.Pizzeria.Stations.PrepStation prep)
                {
                    if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
                    {
                        if (cmd != null && netState != null && NetworkManager.Singleton != null)
                        {
                            var adapter = GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
                            byte hand = adapter != null ? (byte)adapter.ActiveHand : (byte)0;
                            cmd.StartWorkAtomicServerRpc(prep.stationId, 0, 0, hand); 
                        }
                    }
                    else if (UnityEngine.InputSystem.Keyboard.current.fKey.isPressed)
                    {
                        if (cmd != null && Time.time - _lastHeartbeatTime > 0.2f)
                        {
                            cmd.HeartbeatWorkServerRpc(prep.stationId, 0);
                            _lastHeartbeatTime = Time.time;
                        }
                    }
                    else if (UnityEngine.InputSystem.Keyboard.current.fKey.wasReleasedThisFrame)
                    {
                        if (cmd != null) cmd.CancelWorkServerRpc(prep.stationId, 0);
                    }
                }
                else if (_currentInteractable is ScaryParty.Pizzeria.Stations.PackagingStation pkg)
                {
                    if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
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
                else if (_currentInteractable is ScaryParty.Pizzeria.Stations.StagingStation stg)
                {
                    if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame)
                    {
                        if (netState != null && ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance != null)
                        {
                            for (int i = 0; i < netState.Items.Count; i++)
                            {
                                var item = netState.Items[i];
                                if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && item.HolderId == (ulong)stg.stationId && item.PackagingState == (byte)ScaryParty.Pizzeria.Domain.Types.PackagingState.Boxed)
                                {
                                    ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder.Instance.OpenStagingAddressPicker(item.ItemId, item.SlotId);
                                    break;
                                }
                            }
                        }
                    }
                }

                // If F was released but we lost focus of the interactable
                if (UnityEngine.InputSystem.Keyboard.current.fKey.wasReleasedThisFrame && _currentInteractable == null)
                {
                    // Tricky: we don't know which station we were interacting with. 
                    // Better to just let the server timeout the lease (LeaseExpiration).
                }
            }
        }
#else
        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.E) && _currentInteractable != null)
            {
                _currentInteractable.OnInteract(this.gameObject);
            }
        }
#endif

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
