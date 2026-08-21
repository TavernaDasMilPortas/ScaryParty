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

    // Hand Slots (Inventory)
    [System.Serializable]
    public class HandSlot
    {
        public bool isFull;
        public string itemName;
        public int networkObjectId; // Reference to the object if needed
    }

    public HandSlot rightHand = new HandSlot();
    public HandSlot leftHand = new HandSlot();

    private IInteractable _currentInteractable;

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
    }

    private void HandleRaycast()
    {
        if (mainCamera == null) return;

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
    }

    /// <summary>
    /// Attempts to place an item in an empty hand.
    /// Returns true if successful.
    /// </summary>
    public bool TryPickUpItem(string itemName, int objectId)
    {
        if (!rightHand.isFull)
        {
            rightHand.isFull = true;
            rightHand.itemName = itemName;
            rightHand.networkObjectId = objectId;
            
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHand(true, itemName);
                
            return true;
        }
        else if (!leftHand.isFull)
        {
            leftHand.isFull = true;
            leftHand.itemName = itemName;
            leftHand.networkObjectId = objectId;
            
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHand(false, itemName);
                
            return true;
        }

        // Both hands full
        return false;
    }

    /// <summary>
    /// Checks if the player is holding a specific item.
    /// </summary>
    public bool IsHoldingItem(string itemName)
    {
        return (rightHand.isFull && rightHand.itemName == itemName) || 
               (leftHand.isFull && leftHand.itemName == itemName);
    }
    
    /// <summary>
    /// Removes a specific item from the hands (e.g. after delivery).
    /// </summary>
    public void RemoveItem(string itemName)
    {
        if (rightHand.isFull && rightHand.itemName == itemName)
        {
            rightHand.isFull = false;
            rightHand.itemName = "";
            rightHand.networkObjectId = -1;
            
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHand(true, "Empty");
        }
        else if (leftHand.isFull && leftHand.itemName == itemName)
        {
            leftHand.isFull = false;
            leftHand.itemName = "";
            leftHand.networkObjectId = -1;
            
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateHand(false, "Empty");
        }
    }
}
