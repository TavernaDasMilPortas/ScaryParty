using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Interaction;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
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
    private ScaryParty.Interaction.OutlineTarget _currentOutline;
    private float _lastSyncTime = 0f;

    /// <summary>
    /// Resolve o interagível a partir dos hits (ordenados por distância).
    /// Obedece às duas regras de slots:
    /// 1) Se o slot estiver atribuído a um objeto 3D (StationWorkpostTarget.IsBoundTo3DObject), apenas esse objeto
    ///    recebe outline e só é possível interagir com o raycast diretamente nele (não na malha da mesa).
    /// 2) Se o slot não tiver objeto 3D atribuído, a mesa recebe outline, é possível interagir com a malha da mesa
    ///    e o item usa apenas o slot mais próximo em distância ao ponto mirado.
    /// </summary>
    private void ResolveInteractableFromHits(RaycastHit[] hits, ref IInteractable interactable, ref Vector3 hitPoint,
        ref ScaryParty.Pizzeria.Authoring.StationWorkpostTarget workpostOut, out bool blockedByBoundStationBody)
    {
        blockedByBoundStationBody = false;

        for (int h = 0; h < hits.Length; h++)
        {
            var hit = hits[h];
            if (hit.collider == null || hit.collider.transform.IsChildOf(this.transform))
                continue;

            var workpost = hit.collider.GetComponentInParent<ScaryParty.Pizzeria.Authoring.StationWorkpostTarget>();
            if (workpost != null && workpost.targetStation != null && workpost.IsBoundTo3DObject)
            {
                workpost.targetStation.SetAimContext(hit.point, workpost.slotIndex);
                if (!string.IsNullOrEmpty(workpost.targetStation.InteractPrompt))
                {
                    interactable = workpost.targetStation;
                    hitPoint = hit.point;
                    workpostOut = workpost;
                    return;
                }
            }

            var candidate = hit.collider.GetComponentInParent<IInteractable>();
            if (candidate != null)
            {
                if (candidate is ScaryParty.Pizzeria.Stations.StationView station)
                {
                    // Verificar se o mesmo raio atravessa o BoxCollider externo da estação e atinge um objeto 3D de slot nela
                    for (int k = h + 1; k < hits.Length; k++)
                    {
                        if (hits[k].collider == null) continue;
                        var wp = hits[k].collider.GetComponentInParent<ScaryParty.Pizzeria.Authoring.StationWorkpostTarget>();
                        if (wp != null && wp.targetStation == station && wp.IsBoundTo3DObject)
                        {
                            station.SetAimContext(hits[k].point, wp.slotIndex);
                            if (!string.IsNullOrEmpty(station.InteractPrompt))
                            {
                                interactable = station;
                                hitPoint = hits[k].point;
                                workpostOut = wp;
                                return;
                            }
                        }
                    }

                    // Regra 1: se todos os slots desta estação estão vinculados a objetos 3D e o raycast acertou apenas
                    // o corpo da mesa (ou adereços não-interagíveis), bloquear interação com a mesa!
                    if (!station.HasUnboundSlots)
                    {
                        blockedByBoundStationBody = true;
                        return;
                    }

                    // Regra 2: estação possui slots sem objeto 3D -> interage com a malha da mesa no slot mais próximo
                    station.SetAimContext(hit.point, -1);
                }

                if (!string.IsNullOrEmpty(candidate.InteractPrompt))
                {
                    interactable = candidate;
                    hitPoint = hit.point;
                    workpostOut = null;
                    return;
                }
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        // Replicated bag views are needed on remote player avatars too.
        if (GetComponent<ScaryParty.Pizzeria.Presentation.BackpackVisualizer>() == null)
            gameObject.AddComponent<ScaryParty.Pizzeria.Presentation.BackpackVisualizer>();

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
        StopTrackedWork();
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

    private int _workStation = -1;
    private int _workSlot;
    private float _nextWorkHeartbeat;
    private ScaryParty.Player.CameraLookLockToken _workCameraLock;
    private int _workAttemptGeneration = 0;
    private bool _workCompletedAwaitingFRelease = false;
    private ScaryParty.Pizzeria.Domain.Types.HandSlotIndex _lastObservedHand = ScaryParty.Pizzeria.Domain.Types.HandSlotIndex.Left;

    private bool IsWorkFKeyHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.isPressed)
            return true;
#endif
        return Input.GetKey(KeyCode.F);
    }

    private void ReleaseWorkLockOnly()
    {
        if (_workCameraLock.IsValid)
        {
            _workCameraLock.Dispose();
            _workCameraLock = default;
        }
    }

    private void StopTrackedWork()
    {
        ReleaseWorkLockOnly();
        if (_workStation < 0) return;
        int station = _workStation;
        int slot = _workSlot;
        _workStation = -1;
        if (IsSpawned && IsOwner)
            ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.CancelWorkServerRpc(station, slot);
    }

    private void UpdateTrackedWork()
    {
        if (_workStation < 0 && !_workCameraLock.IsValid) return;

        // 1. Soltar F libera imediatamente o lock e cancela o trabalho
        if (!IsWorkFKeyHeld())
        {
            _workCompletedAwaitingFRelease = false;
            StopTrackedWork();
            return;
        }

        // 2. Verificar se o alvo ainda está em alcance
        if (mainCamera == null || !(_currentInteractable is ScaryParty.Pizzeria.Stations.PrepStation prep) ||
            prep.stationId != _workStation || prep.ResolveTargetSlotIndex() != _workSlot)
        {
            StopTrackedWork();
            return;
        }

        // 3. Verificar estado replicado do slot no servidor
        var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
        if (netState != null && NetworkManager.Singleton != null)
        {
            ulong myId = NetworkManager.Singleton.LocalClientId;
            for (int i = 0; i < netState.StationSlots.Count; i++)
            {
                var slot = netState.StationSlots[i];
                if (slot.StationId == _workStation && slot.SlotIndex == _workSlot)
                {
                    if (slot.Phase == (byte)ScaryParty.Pizzeria.Domain.Types.OperationPhase.Completed)
                    {
                        // Conclusão: liberar câmera mesmo que F continue pressionado; exigir nova pressão
                        _workCompletedAwaitingFRelease = true;
                        ReleaseWorkLockOnly();
                        _workStation = -1;
                        return;
                    }
                    if (slot.Phase == (byte)ScaryParty.Pizzeria.Domain.Types.OperationPhase.Cancelled ||
                        (slot.Phase == (byte)ScaryParty.Pizzeria.Domain.Types.OperationPhase.Running && slot.WorkerId != myId))
                    {
                        // Cancelamento ou tomado por outro jogador
                        ReleaseWorkLockOnly();
                        _workStation = -1;
                        return;
                    }
                    break;
                }
            }
        }

        if (Time.unscaledTime < _nextWorkHeartbeat) return;
        _nextWorkHeartbeat = Time.unscaledTime + 0.2f;
        ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance?.HeartbeatWorkServerRpc(_workStation, _workSlot);
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Se F não está pressionado, resetar flag de conclusão aguardando soltura
        bool isFHeld = IsWorkFKeyHeld();
        if (!isFHeld)
        {
            _workCompletedAwaitingFRelease = false;
            if (_workStation >= 0 || _workCameraLock.IsValid)
            {
                StopTrackedWork();
            }
        }

        // Verificar se a mão ativa mudou; se mudou durante trabalho, cancelar
        var invAdapter = GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
        if (invAdapter != null)
        {
            if (invAdapter.ActiveHand != _lastObservedHand)
            {
                _lastObservedHand = invAdapter.ActiveHand;
                if (_workStation >= 0 || _workCameraLock.IsValid)
                {
                    StopTrackedWork();
                }
            }
        }

#if ENABLE_INPUT_SYSTEM
        bool rawFCheck = (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame) || Input.GetKeyDown(KeyCode.F);
#else
        bool rawFCheck = Input.GetKeyDown(KeyCode.F);
#endif
        if (rawFCheck)
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked)
            {
                Debug.LogWarning("[PlayerInteraction] Tecla F pressionada mas GameplayInputGate estÃ¡ BLOQUEADO!");
            }
            else if (Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
            {
                Debug.LogWarning($"[PlayerInteraction] Tecla F pressionada mas cursor nÃ£o estÃ¡ bloqueado na janela (lockState={Cursor.lockState}, visible={Cursor.visible})! Clique na tela do jogo.");
            }
        }

        if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || Cursor.lockState != CursorLockMode.Locked || Cursor.visible)
        {
            StopTrackedWork();
            if (OutlineService.Instance != null)
            {
                OutlineService.Instance.SetTarget(null);
            }
            _currentOutline = null;
            return;
        }
        
        HandleRaycast();
        HandleInput();
        UpdateTrackedWork();

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
        Vector3 hitPoint = Vector3.zero;
        int explicitWorkpostSlot = -1;
        ScaryParty.Pizzeria.Authoring.StationWorkpostTarget aimedWorkpost = null;

        ResolveInteractableFromHits(hits, ref interactable, ref hitPoint, ref aimedWorkpost, out bool blockedByBoundStationBody);

        // Fallback SphereCast com raio estreito (0.08m) apenas se o raio direto não bateu no corpo de uma bancada bloqueada pela Regra 1
        if (interactable == null && !blockedByBoundStationBody)
        {
            RaycastHit[] sphereHits = Physics.SphereCastAll(ray, 0.08f, maxReach, mask, QueryTriggerInteraction.Collide);
            System.Array.Sort(sphereHits, (a, b) => a.distance.CompareTo(b.distance));
            ResolveInteractableFromHits(sphereHits, ref interactable, ref hitPoint, ref aimedWorkpost, out _);
        }

        if (aimedWorkpost != null) explicitWorkpostSlot = aimedWorkpost.slotIndex;

        if (interactable != null)
        {
            if (interactable is ScaryParty.Pizzeria.Stations.StationView stView)
            {
                stView.SetAimContext(hitPoint, explicitWorkpostSlot);
            }

            if (_currentInteractable != interactable)
            {
                if (_currentInteractable != null)
                {
                    _currentInteractable.OnLoseFocus();
                }

                _currentInteractable = interactable;
                _currentInteractable.OnFocus();
            }

            // Outline:
            // - Regra 1 (aimedWorkpost != null): APENAS o objeto 3D do slot recebe outline (nunca a bancada toda).
            // - Regra 2 (aimedWorkpost == null): a mesa/estação recebe outline.
            ScaryParty.Interaction.OutlineTarget outline = null;
            if (aimedWorkpost != null)
            {
                outline = aimedWorkpost.outlineTarget != null
                    ? aimedWorkpost.outlineTarget
                    : aimedWorkpost.GetComponent<ScaryParty.Interaction.OutlineTarget>();
                if (outline == null)
                {
                    outline = aimedWorkpost.gameObject.AddComponent<ScaryParty.Interaction.OutlineTarget>();
                    aimedWorkpost.outlineTarget = outline;
                }
            }
            else if (interactable is Component comp)
            {
                outline = comp.GetComponent<ScaryParty.Interaction.OutlineTarget>()
                          ?? comp.GetComponentInParent<ScaryParty.Interaction.OutlineTarget>();
                if (outline == null)
                {
                    outline = comp.gameObject.AddComponent<ScaryParty.Interaction.OutlineTarget>();
                }
            }
            if (outline != _currentOutline)
            {
                _currentOutline = outline;
                if (outline != null) outline.InvalidateCache(); // itens podem ter entrado/saído do prato
                ScaryParty.Interaction.OutlineService.Instance?.SetTarget(outline);
            }

            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteractionPrompt(_currentInteractable.InteractPrompt);

            return;
        }

        if (_currentOutline != null)
        {
            _currentOutline = null;
            ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
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

    private void OnEnable()
    {
        ScaryParty.Pizzeria.Presentation.NotificationManager.OnNotificationShown += HandleNotificationShown;
    }

    private void OnDisable()
    {
        ScaryParty.Pizzeria.Presentation.NotificationManager.OnNotificationShown -= HandleNotificationShown;
        StopTrackedWork();
        if (_currentInteractable != null)
        {
            _currentInteractable.OnLoseFocus();
            _currentInteractable = null;
        }
        _currentOutline = null;
        ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
    }

    public override void OnDestroy()
    {
        ScaryParty.Pizzeria.Presentation.NotificationManager.OnNotificationShown -= HandleNotificationShown;
        StopTrackedWork();
        ScaryParty.Interaction.OutlineService.Instance?.ClearTarget();
        base.OnDestroy();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            StopTrackedWork();
        }
    }

    private void HandleNotificationShown(string message, ScaryParty.Pizzeria.Presentation.NotificationType type)
    {
        if (!IsOwner) return;
        if (type == ScaryParty.Pizzeria.Presentation.NotificationType.Error || type == ScaryParty.Pizzeria.Presentation.NotificationType.Warning)
        {
            if (_workCameraLock.IsValid)
            {
                ReleaseWorkLockOnly();
                _workStation = -1;
            }
        }
    }


    private void ExecuteWorkInteraction()
    {
        var netState = ScaryParty.Pizzeria.Network.PizzeriaNetworkState.Instance;
        var cmd = ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.Instance;
        var adapter = GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
        byte hand = adapter != null ? (byte)adapter.ActiveHand : (byte)0;
        int otherHand = hand == 0 ? 1 : 0;

        Debug.Log($"[PlayerInteraction] [F] Pressionado! InteragÃ­vel atual: {(_currentInteractable != null ? _currentInteractable.GetType().Name : "NENHUM (null)")}, MÃ£o Ativa: {hand}");

        if (_currentInteractable == null)
        {
            Debug.LogWarning("[PlayerInteraction] [F] Tecla pressionada, mas nenhum objeto/bancada estÃ¡ em foco na mira!");
            ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Nenhum objeto na mira!", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
            return;
        }

        if (_currentInteractable is ScaryParty.Pizzeria.Authoring.DeliveryBagAuthoring bag)
        {
            bag.StoreFromHand(gameObject);
            return;
        }

        // Subtipos especÃ­ficos primeiro (evita que a heranÃ§a de StationView engula a chamada)
        if (_currentInteractable is ScaryParty.Pizzeria.Stations.PackagingStation pkg)
        {
            if (netState != null && cmd != null)
            {
                int targetSlot = pkg.ResolveTargetSlotIndex();
                Debug.Log($"[PlayerInteraction] [F] Acionando bancada de embalagem (Slot={targetSlot})");
                if (targetSlot >= 0)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && item.HolderId == (ulong)pkg.stationId && item.SlotId == targetSlot)
                        {
                            bool canPackage = item.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.PizzaBase || 
                                              item.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Pizza;
                            if (canPackage && item.PackagingState != (byte)ScaryParty.Pizzeria.Domain.Types.PackagingState.Boxed)
                            {
                                Debug.Log($"[PlayerInteraction] [F] Enviando StartPackagingServerRpc para item {item.ItemId}");
                                cmd.StartPackagingServerRpc(item.ItemId);
                            }
                            else
                            {
                                Debug.LogWarning($"[PlayerInteraction] [F] Item {item.ItemId} na bancada de embalagem nÃ£o pode ser embalado (CanPackage={canPackage}, Boxed={item.PackagingState})");
                            }
                            return;
                        }
                    }
                    Debug.LogWarning($"[PlayerInteraction] [F] Nenhum item encontrado no slot {targetSlot} da bancada de embalagem.");
                }
            }
            return;
        }
        else if (_currentInteractable is ScaryParty.Pizzeria.Stations.StagingStation stg)
        {
            if (netState != null && ScaryParty.UI.Piaria.Presenters.StagingPresenter.Instance != null)
            {
                int targetSlot = stg.ResolveTargetSlotIndex();
                Debug.Log($"[PlayerInteraction] [F] Acionando bancada de expediÃ§Ã£o (Slot={targetSlot})");
                if (targetSlot >= 0)
                {
                    for (int i = 0; i < netState.Items.Count; i++)
                    {
                        var item = netState.Items[i];
                        if (item.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && item.HolderId == (ulong)stg.stationId && item.SlotId == targetSlot && item.PackagingState == (byte)ScaryParty.Pizzeria.Domain.Types.PackagingState.Boxed)
                        {
                            ScaryParty.UI.Piaria.Presenters.StagingPresenter.Instance.Open(item.ItemId, item.SlotId);
                            return;
                        }
                    }
                }
            }
            return;
        }
        else if (_currentInteractable is ScaryParty.Pizzeria.Stations.StationView station)
        {
            int slotIndex = station.ResolveTargetSlotIndex();
            Debug.Log($"[PlayerInteraction] [F] EstaÃ§Ã£o detectada: '{station.stationName}' (ID: {station.stationId}, Classe: {station.GetType().Name}), SlotIndex resolvido: {slotIndex}");

            if (slotIndex < 0)
            {
                Debug.LogWarning($"[PlayerInteraction] [F] NÃ£o foi possÃ­vel resolver slot para a estaÃ§Ã£o '{station.stationName}'. (HasUnboundSlots={station.HasUnboundSlots}, LastAimExplicitSlot={station.LastAimExplicitSlot}). Aponte diretamente para o posto/objeto 3D!");
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Aponte diretamente para o posto na bancada!", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
                return;
            }

            if (netState == null || cmd == null || NetworkManager.Singleton == null)
            {
                Debug.LogWarning("[PlayerInteraction] [F] netState, cmd ou NetworkManager estÃ¡ nulo!");
                return;
            }

            ulong myId = NetworkManager.Singleton.LocalClientId;
            NetItemDto itemOnStation = default;
            bool hasItemOnStation = false;
            NetItemDto itemInHand = default;
            bool hasItemInHand = false;
            NetItemDto itemInOtherHand = default;
            bool hasItemInOtherHand = false;

            for (int i = 0; i < netState.Items.Count; i++)
            {
                var it = netState.Items[i];
                if (it.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.StationSlot && it.HolderId == (ulong)station.stationId && it.SlotId == slotIndex)
                {
                    itemOnStation = it;
                    hasItemOnStation = true;
                }
                if (it.LocationType == (byte)ScaryParty.Pizzeria.Domain.Types.LocationType.Hand && it.HolderId == myId)
                {
                    if (it.SlotId == hand)
                    {
                        itemInHand = it;
                        hasItemInHand = true;
                    }
                    else if (it.SlotId == otherHand)
                    {
                        itemInOtherHand = it;
                        hasItemInOtherHand = true;
                    }
                }
            }

            Debug.Log($"[PlayerInteraction] [F] Itens detectados -> Bancada: {(hasItemOnStation ? $"ItemId={itemOnStation.ItemId}, Def={itemOnStation.DefinitionId}, Cat={(ScaryParty.Pizzeria.Domain.Types.ItemCategory)itemOnStation.Category}, Stage={(ScaryParty.Pizzeria.Domain.Types.ProcessingStage)itemOnStation.PrepStage}" : "Vazia")} | MÃ£o Ativa ({hand}): {(hasItemInHand ? $"ItemId={itemInHand.ItemId}, Def={itemInHand.DefinitionId}, Cat={(ScaryParty.Pizzeria.Domain.Types.ItemCategory)itemInHand.Category}, Stage={(ScaryParty.Pizzeria.Domain.Types.ProcessingStage)itemInHand.PrepStage}" : "Vazia")} | Outra MÃ£o ({otherHand}): {(hasItemInOtherHand ? $"ItemId={itemInOtherHand.ItemId}, Def={itemInOtherHand.DefinitionId}" : "Vazia")}");

            bool isPreparedIngredient = hasItemInHand && (itemInHand.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Sauce ||
                                        ((itemInHand.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Cheese || itemInHand.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Topping) &&
                                         itemInHand.PrepStage == (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared));

            bool isPreparedPizzaOrBase = hasItemOnStation && (itemOnStation.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Pizza ||
                                         itemOnStation.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.PizzaBase ||
                                         (itemOnStation.Category == (byte)ScaryParty.Pizzeria.Domain.Types.ItemCategory.Dough && itemOnStation.PrepStage == (byte)ScaryParty.Pizzeria.Domain.Types.ProcessingStage.Prepared));

            Debug.Log($"[PlayerInteraction] [F] CondiÃ§Ãµes de montagem -> isPreparedPizzaOrBase={isPreparedPizzaOrBase}, isPreparedIngredient={isPreparedIngredient}");

            if (isPreparedPizzaOrBase && isPreparedIngredient)
            {
                Debug.Log($"[PlayerInteraction] [F] CondiÃ§Ãµes satisfeitas! Disparando AddIngredientToPizzaServerRpc (Pizza={itemOnStation.ItemId}, Ingrediente={itemInHand.ItemId})");
                cmd.AddIngredientToPizzaServerRpc(itemOnStation.ItemId, itemInHand.ItemId);
                return;
            }

            if (station is ScaryParty.Pizzeria.Stations.PrepStation prep)
            {
                bool hasAnyWorkItem = hasItemOnStation || hasItemInHand || hasItemInOtherHand;
                if (!hasAnyWorkItem)
                {
                    Debug.LogWarning($"[PlayerInteraction] [F] Nenhum item disponível para preparo na bancada {prep.stationName}.");
                    ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Coloque um ingrediente ou segure-o para preparar!", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
                    return;
                }

                _workAttemptGeneration++;
                ReleaseWorkLockOnly();
                _workCameraLock = ScaryParty.Player.FirstPersonCameraController.LocalInstance != null
                    ? ScaryParty.Player.FirstPersonCameraController.LocalInstance.AcquireLookLock("PrepStationWorkF")
                    : default;

                Debug.Log($"[PlayerInteraction] [F] Estação é PrepStation! Disparando StartWorkAtomicServerRpc (Estação={prep.stationId}, Slot={slotIndex}, Hand={hand}, Gen={_workAttemptGeneration})");
                cmd.StartWorkAtomicServerRpc(prep.stationId, slotIndex, 0, hand);
                _workStation = prep.stationId;
                _workSlot = slotIndex;
                _nextWorkHeartbeat = Time.unscaledTime + 0.2f;
                return;
            }

            // DiagnÃ³sticos especÃ­ficos para feedback claro ao usuÃ¡rio quando nada acontece:
            if (hasItemInHand && !isPreparedIngredient)
            {
                Debug.LogWarning($"[PlayerInteraction] [F] O ingrediente segurado (Def={itemInHand.DefinitionId}, Cat={(ScaryParty.Pizzeria.Domain.Types.ItemCategory)itemInHand.Category}, Stage={(ScaryParty.Pizzeria.Domain.Types.ProcessingStage)itemInHand.PrepStage}) precisa ser preparado antes na Bancada de Preparo!");
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Prepare o ingrediente na bancada de preparo primeiro!", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
            }
            else if (hasItemOnStation && !isPreparedPizzaOrBase)
            {
                Debug.LogWarning($"[PlayerInteraction] [F] A massa na bancada (Def={itemOnStation.DefinitionId}, Stage={(ScaryParty.Pizzeria.Domain.Types.ProcessingStage)itemOnStation.PrepStage}) ainda nÃ£o foi aberta! Abra a massa na Bancada de Preparo primeiro.");
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Abra a massa na bancada de preparo antes de montar!", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
            }
            else if (!hasItemInHand && hasItemInOtherHand)
            {
                Debug.LogWarning($"[PlayerInteraction] [F] O ingrediente estÃ¡ na OUTRA mÃ£o (mÃ£o {otherHand}). Use [1], [2] ou [Q] para alternar para a mÃ£o com o item!");
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Alterne para a mÃ£o com o ingrediente (teclas 1, 2 ou Q)!", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
            else
            {
                Debug.LogWarning($"[PlayerInteraction] [F] Nenhuma aÃ§Ã£o compatÃ­vel para [F] na estaÃ§Ã£o '{station.stationName}' ({station.GetType().Name}). (hasItemOnStation={hasItemOnStation}, hasItemInHand={hasItemInHand})");
                ScaryParty.Pizzeria.Presentation.NotificationManager.Show($"Esta bancada ({station.stationName}) nÃ£o aceita a tecla [F] no estado atual.", ScaryParty.Pizzeria.Presentation.NotificationType.Info);
            }
        }
    }

    private void InspectBag()
    {
        if (_currentInteractable is ScaryParty.Pizzeria.Authoring.DeliveryBagAuthoring bag)
            ScaryParty.Pizzeria.Presentation.BackpackPanelPresenter.Instance?.Inspect(bag.BackpackId);
    }

#if ENABLE_INPUT_SYSTEM
    private void HandleInput()
    {
        bool interactPressed = false;
        bool workPressed = false;

        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame) interactPressed = true;
            if (UnityEngine.InputSystem.Keyboard.current.fKey.wasPressedThisFrame) workPressed = true;
            if (UnityEngine.InputSystem.Keyboard.current.iKey.wasPressedThisFrame) InspectBag();
        }
        if (Input.GetKeyDown(KeyCode.I)) InspectBag();
        if (Input.GetKeyDown(KeyCode.E)) interactPressed = true;
        if (Input.GetKeyDown(KeyCode.F)) workPressed = true;

        if (interactPressed && _currentInteractable != null)
        {
            _currentInteractable.OnInteract(this.gameObject);
        }

        if (workPressed)
        {
            if (_workCompletedAwaitingFRelease)
            {
                return;
            }
            ExecuteWorkInteraction();
        }
    }
#else
    private void HandleInput()
    {
        bool interactPressed = Input.GetKeyDown(KeyCode.E);
        bool workPressed = Input.GetKeyDown(KeyCode.F);
        if (Input.GetKeyDown(KeyCode.I)) InspectBag();

        if (interactPressed && _currentInteractable != null)
        {
            _currentInteractable.OnInteract(this.gameObject);
        }

        if (workPressed)
        {
            if (_workCompletedAwaitingFRelease)
            {
                return;
            }
            ExecuteWorkInteraction();
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
