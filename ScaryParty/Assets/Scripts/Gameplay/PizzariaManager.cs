using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Manages generating pizza orders, spawning pizzas at the base, and paying the players for deliveries.
/// Waits for the city to be generated before opening orders.
/// Race-condition safe: if the city is already ready when this spawns, orders start immediately.
/// </summary>
public class PizzariaManager : NetworkBehaviour
{
    public static PizzariaManager Instance { get; private set; }

    [Header("Settings")]
    public float orderGenerationInterval = 20f;
    public int maxActiveOrders = 2;
    
    [Header("Prefabs")]
    public GameObject pizzaBoxPrefab;

    // Whether the city is ready and orders can be generated
    private bool _cityReady = false;

    private float _orderTimer;
    private List<string> _activeOrders = new List<string>();

    // Cached reference to avoid FindObjectOfType every order
    private CityGenerator _cityGen;
    
    private string[] _pizzaTypes = new string[] 
    { 
        "Pepperoni Pizza", 
        "Cheese Pizza", 
        "Slop Pizza", 
        "Cosmic Pizza", 
        "Meatball Pizza" 
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _cityGen = FindObjectOfType<CityGenerator>();

        if (_cityGen == null)
        {
            Debug.LogWarning("[PizzariaManager] CityGenerator not found! Orders will never start.");
            return;
        }

        // Subscribe to future city generation events
        _cityGen.OnCityGenerated += OnCityReady;

        // Race-condition guard: if CityGenerator.OnNetworkSpawn() already fired before us,
        // the city is already generated — check for it and start immediately.
        if (IsServer && _cityGen.CityData != null && _cityGen.CityData.DeliveryPointCount > 0)
        {
            Debug.Log("[PizzariaManager] City was already generated before PizzariaManager spawned — starting orders now.");
            OnCityReady();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (_cityGen != null)
            _cityGen.OnCityGenerated -= OnCityReady;
    }

    private void OnCityReady()
    {
        if (!IsServer) return;
        if (_cityReady) return; // Guard against double-call

        _cityReady = true;
        _orderTimer = 2f; // Small delay after city is ready before first pizza
        Debug.Log("[PizzariaManager] City is ready — pizza orders will start in 2 seconds. Starting game for all players.");

        var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        foreach (var pState in allPlayers)
        {
            if (pState != null) pState.IsGameStarted.Value = true;
        }
    }

    private void Update()
    {
        if (!IsServer) return;
        if (!_cityReady) return;

        // Não gera pedidos até que o jogo tenha começado
        if (!IsGameActuallyStarted()) return;

        if (_activeOrders.Count < maxActiveOrders)
        {
            _orderTimer -= Time.deltaTime;
            if (_orderTimer <= 0)
            {
                GenerateOrder();
                _orderTimer = orderGenerationInterval;
            }
        }
    }

    private void GenerateOrder()
    {
        Debug.Log("[PIZZA SPAWN] Tentando gerar um novo pedido...");

        // Use cached reference, refresh only if null
        if (_cityGen == null)
            _cityGen = FindObjectOfType<CityGenerator>();

        if (_cityGen == null) 
        { 
            Debug.LogWarning("[PIZZA SPAWN] Cancelado: CityGenerator is missing in the scene!"); 
            return; 
        }
        if (_cityGen.CityData == null) 
        { 
            Debug.LogWarning("[PIZZA SPAWN] Cancelado: CityData is null — city may not have generated yet."); 
            return; 
        }
        if (_cityGen.CityData.DeliveryPointCount == 0) 
        { 
            Debug.LogWarning("[PIZZA SPAWN] Cancelado: DeliveryPointCount is 0! The CityData has no valid delivery points."); 
            return; 
        }

        Debug.Log($"[PIZZA SPAWN] Sucesso! Total Delivery Points available: {_cityGen.CityData.DeliveryPointCount}");

        // Select a random pizza type
        string pizza = _pizzaTypes[Random.Range(0, _pizzaTypes.Length)];
        
        // Select a random delivery point index
        int deliveryPointId = Random.Range(0, _cityGen.CityData.DeliveryPointCount);

        string orderString = $"{pizza} -> Delivery Point #{deliveryPointId}";
        Debug.Log($"[PIZZA SPAWN] Pedido Gerado: {orderString}");
        
        _activeOrders.Add(orderString);

        // Spawn the pizza box on the bancada
        if (pizzaBoxPrefab != null)
        {
            Vector3 spawnPos = _cityGen.CityData.bancadaPosition;
            // Slight random offset along the bancada so boxes don't perfectly overlap
            spawnPos += new Vector3(Random.Range(-1.2f, 1.2f), 0.1f, Random.Range(-0.4f, 0.4f));

            GameObject box = Instantiate(pizzaBoxPrefab, spawnPos, Quaternion.identity);
            PizzaItem item = box.GetComponent<PizzaItem>();
            if (item != null)
            {
                item.pizzaType = pizza;
                item.deliveryPointId = deliveryPointId;
            }

            NetworkObject netObj = box.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log($"[PIZZA SPAWN] Pizza física instanciada na bancada em {spawnPos}!");
            }
            else
            {
                Debug.LogError("[PIZZA SPAWN] pizzaBoxPrefab não tem NetworkObject! Adicione o componente NetworkObject ao prefab.");
                Destroy(box);
            }
        }
        else
        {
            Debug.LogWarning("[PIZZA SPAWN] pizzaBoxPrefab não está assignado no PizzariaManager!");
        }

        Debug.Log("[PIZZA SPAWN] Sincronizando o pedido com os clientes...");
        UpdateClientsOrdersClientRpc(orderString, true, deliveryPointId);
    }

    public bool HasOrder(string pizzaType, int deliveryPointId)
    {
        string targetOrder = $"{pizzaType} -> Delivery Point #{deliveryPointId}";
        return _activeOrders.Contains(targetOrder);
    }

    /// <summary>
    /// Called when a player successfully delivers a pizza.
    /// O servidor é a autoridade: incrementa Money diretamente via NetworkVariable.
    /// </summary>
    public void CompleteOrder(string pizzaType, int deliveryPointId, ulong clientId)
    {
        if (!IsServer) return;

        string targetOrder = $"{pizzaType} -> Delivery Point #{deliveryPointId}";
        
        if (_activeOrders.Contains(targetOrder))
        {
            _activeOrders.Remove(targetOrder);
            
            // Paga o jogador via NetworkVariable (autoridade do servidor)
            int reward = Random.Range(10, 30);
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                var playerState = client.PlayerObject?.GetComponent<PlayerState>();
                if (playerState != null)
                {
                    playerState.Money.Value += reward;
                }
            }

            // Notifica o jogador que fez a entrega para efeitos visuais locais
            NotifyDeliveryClientRpc(reward, new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } } });

            UpdateClientsOrdersClientRpc(targetOrder, false, deliveryPointId);
        }
    }

    [ClientRpc]
    private void UpdateClientsOrdersClientRpc(string orderString, bool isAdded, int deliveryPointId)
    {
        if (!IsServer) // Server already modified its local list
        {
            if (isAdded)
                _activeOrders.Add(orderString);
            else
                _activeOrders.Remove(orderString);
        }

        // 1. Toggle Delivery Point Visuals & Minimap Routes FIRST so colors are assigned
        Vector3 targetPos = Vector3.zero;

        DeliveryPoint[] allPoints = FindObjectsOfType<DeliveryPoint>();
        foreach (var point in allPoints)
        {
            if (point.pointIndex == deliveryPointId)
            {
                point.isActive = isAdded;
                targetPos = point.transform.position;
                break;
            }
        }

        if (MinimapRouteManager.Instance != null)
        {
            if (_cityGen == null)
                _cityGen = FindObjectOfType<CityGenerator>();

            if (isAdded && _cityGen != null && _cityGen.CityData != null)
            {
                MinimapRouteManager.Instance.CreateRoute(orderString, _cityGen.CityData.pizzariaPosition, targetPos);
            }
            else
            {
                MinimapRouteManager.Instance.RemoveRoute(orderString);
            }
        }

        // 2. Update UI SECOND so it can fetch the correctly assigned color
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateOrdersList(_activeOrders);
        }
    }

    /// <summary>
    /// Notifica o cliente que fez a entrega para feedback visual (ex: "+R$20").
    /// O dinheiro real já foi adicionado via NetworkVariable no servidor.
    /// </summary>
    [ClientRpc]
    private void NotifyDeliveryClientRpc(int amount, ClientRpcParams rpcParams = default)
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.AddCompletedDelivery();
        }
    }

    private bool IsGameActuallyStarted()
    {
        var allPlayers = FindObjectsByType<PlayerState>(FindObjectsSortMode.None);
        foreach (var pState in allPlayers)
        {
            if (pState != null && pState.IsGameStarted.Value)
                return true;
        }
        return false;
    }
}
