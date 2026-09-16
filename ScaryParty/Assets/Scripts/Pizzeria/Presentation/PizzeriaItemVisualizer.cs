using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Presentation
{
    public class PizzeriaItemVisualizer : NetworkBehaviour
    {
        public static PizzeriaItemVisualizer Instance { get; private set; }

        private Dictionary<ulong, GameObject> _visuals = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, GameObject> _toolVisuals = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, GameObject[]> _pizzaIngredientLayers = new Dictionary<ulong, GameObject[]>();

        private Dictionary<int, StationView> _stationCache = new Dictionary<int, StationView>();
        private Dictionary<ulong, (Transform left, Transform right)> _playerHandCache = new Dictionary<ulong, (Transform left, Transform right)>();
        
        private float _cacheUpdateTimer = 0f;
        private const float CacheUpdateInterval = 2f;

        public GameObject pizzaBoxPrefab; // Optional: If null, we use a brown cube
        public GameObject pizzaBasePrefab; // Optional: If null, we use a cylinder
        public GameObject doughPrefab; // Optional: If null, we use a sphere

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.Items.OnListChanged += OnItemsChanged;
                PizzeriaNetworkState.Instance.Tools.OnListChanged += OnToolsChanged;
                
                UpdateCaches();

                foreach (var item in PizzeriaNetworkState.Instance.Items)
                    UpdateOrSpawnVisual(item);
                
                foreach (var tool in PizzeriaNetworkState.Instance.Tools)
                    UpdateOrSpawnToolVisual(tool);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.Items.OnListChanged -= OnItemsChanged;
                PizzeriaNetworkState.Instance.Tools.OnListChanged -= OnToolsChanged;
            }
        }

        private void UpdateCaches()
        {
            _stationCache.Clear();
            var stations = FindObjectsByType<StationView>(FindObjectsSortMode.None);
            foreach (var st in stations)
            {
                _stationCache[st.stationId] = st;
            }

            _playerHandCache.Clear();
            var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
            foreach (var p in players)
            {
                var netObj = p.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    var anim = p.GetComponentInChildren<Animator>();
                    Transform leftHand = null;
                    Transform rightHand = null;
                    if (anim != null)
                    {
                        leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                        rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    }
                    if (leftHand == null) leftHand = p.transform;
                    if (rightHand == null) rightHand = p.transform;
                    
                    _playerHandCache[netObj.OwnerClientId] = (leftHand, rightHand);
                }
            }
        }

        private void OnItemsChanged(NetworkListEvent<NetItemDto> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Remove ||
                changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.RemoveAt)
            {
                if (_visuals.TryGetValue(changeEvent.Value.ItemId, out var go))
                {
                    Destroy(go);
                    _visuals.Remove(changeEvent.Value.ItemId);
                }
                if (_pizzaIngredientLayers.TryGetValue(changeEvent.Value.ItemId, out var layers))
                {
                    foreach (var l in layers) if (l != null) Destroy(l);
                    _pizzaIngredientLayers.Remove(changeEvent.Value.ItemId);
                }
            }
            else
            {
                UpdateOrSpawnVisual(changeEvent.Value);
            }
        }

        private void OnToolsChanged(NetworkListEvent<NetToolDto> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<NetToolDto>.EventType.Remove ||
                changeEvent.Type == NetworkListEvent<NetToolDto>.EventType.RemoveAt)
            {
                if (_toolVisuals.TryGetValue(changeEvent.Value.ToolId, out var go))
                {
                    Destroy(go);
                    _toolVisuals.Remove(changeEvent.Value.ToolId);
                }
            }
            else
            {
                UpdateOrSpawnToolVisual(changeEvent.Value);
            }
        }

        private void UpdateOrSpawnToolVisual(NetToolDto tool)
        {
            if (!_toolVisuals.TryGetValue(tool.ToolId, out var go))
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(0.1f, 0.3f, 0.1f);
                var rend = go.GetComponent<Renderer>();
                if (rend != null) rend.material.color = new Color(0.7f, 0.75f, 0.8f);
                var col = go.GetComponent<Collider>();
                if (col != null) Destroy(col);
                _toolVisuals[tool.ToolId] = go;
            }

            if (tool.LocationType == (byte)LocationType.Destroyed || 
                tool.LocationType == (byte)LocationType.Backpack || 
                tool.LocationType == (byte)LocationType.StorageSlot)
            {
                if (go.activeSelf) go.SetActive(false);
                return;
            }

            if (!go.activeSelf) go.SetActive(true);

            Transform parent = null;
            Vector3 targetPos = go.transform.position;
            Quaternion targetRot = go.transform.rotation;

            if (tool.LocationType == (byte)LocationType.StationSlot)
            {
                if (_stationCache.TryGetValue((int)tool.HolderId, out var st))
                {
                    parent = st.transform;
                    // Tools sit next to the item slot
                    targetPos = st.GetSlotPosition((int)tool.SlotId) + st.transform.right * 0.4f;
                    targetRot = st.GetSlotRotation((int)tool.SlotId);
                }
            }
            else if (tool.LocationType == (byte)LocationType.Hand)
            {
                if (_playerHandCache.TryGetValue(tool.HolderId, out var hands))
                {
                    var handTransform = tool.SlotId == 0 ? hands.left : hands.right;
                    parent = handTransform;
                    if (parent != null)
                    {
                        targetPos = parent.position + parent.forward * 0.1f;
                        targetRot = parent.rotation;
                    }
                }
            }
            else if (tool.LocationType == (byte)LocationType.StagingSlot)
            {
                parent = null;
                targetPos = new Vector3(tool.SlotId * 0.5f, 1f, 0f);
                targetRot = Quaternion.identity;
            }

            if (parent != null || tool.LocationType == (byte)LocationType.StagingSlot)
            {
                float lerpSpeed = 10f * Time.deltaTime;
                go.transform.position = Vector3.Lerp(go.transform.position, targetPos, lerpSpeed);
                go.transform.rotation = Quaternion.Lerp(go.transform.rotation, targetRot, lerpSpeed);
            }
        }

        private void UpdateOrSpawnVisual(NetItemDto item)
        {
            if (!_visuals.TryGetValue(item.ItemId, out var go))
            {
                go = CreateVisualPrimitive(item);
                _visuals[item.ItemId] = go;
            }

            // Update appearance based on state
            UpdateAppearance(go, item);

            if (item.LocationType == (byte)LocationType.Destroyed || 
                item.LocationType == (byte)LocationType.Backpack || 
                item.LocationType == (byte)LocationType.StorageSlot)
            {
                if (go.activeSelf) go.SetActive(false);
                return;
            }

            if (!go.activeSelf) go.SetActive(true);

            // Update Position
            Transform parent = null;
            Vector3 targetPos = go.transform.position;
            Quaternion targetRot = go.transform.rotation;

            if (item.LocationType == (byte)LocationType.StationSlot)
            {
                if (_stationCache.TryGetValue((int)item.HolderId, out var st))
                {
                    parent = st.transform;
                    targetPos = st.GetSlotPosition((int)item.SlotId);
                    targetRot = st.GetSlotRotation((int)item.SlotId);
                }
            }
            else if (item.LocationType == (byte)LocationType.Hand)
            {
                if (_playerHandCache.TryGetValue(item.HolderId, out var hands))
                {
                    var handTransform = item.SlotId == 0 ? hands.left : hands.right;
                    parent = handTransform;
                    if (parent != null)
                    {
                        targetPos = parent.position + parent.forward * 0.1f;
                        targetRot = parent.rotation;
                    }
                }
            }
            else if (item.LocationType == (byte)LocationType.StagingSlot)
            {
                parent = null;
                targetPos = new Vector3(item.SlotId * 0.5f, 1f, 0f);
                targetRot = Quaternion.identity;
            }

            if (parent != null || item.LocationType == (byte)LocationType.StagingSlot)
            {
                float lerpSpeed = 10f * Time.deltaTime;
                go.transform.position = Vector3.Lerp(go.transform.position, targetPos, lerpSpeed); // Smooth quick snap
                go.transform.rotation = Quaternion.Lerp(go.transform.rotation, targetRot, lerpSpeed);
            }
        }

        private GameObject CreateVisualPrimitive(NetItemDto item)
        {
            GameObject go;
            bool isIngredient = item.Category == (byte)ItemCategory.Dough || 
                                item.Category == (byte)ItemCategory.Sauce || 
                                item.Category == (byte)ItemCategory.Cheese || 
                                item.Category == (byte)ItemCategory.Topping;
                                
            if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
                go.GetComponent<Renderer>().material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
            }
            else if (isIngredient)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                go.GetComponent<Renderer>().material.color = Color.white;
            }
            else
            {
                // Pizza Base
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
            }
            
            // Remove colliders so it doesn't interfere with physics
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private void UpdateAppearance(GameObject go, NetItemDto item)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
            {
                rend.material.color = new Color(0.6f, 0.4f, 0.2f); // Brown cardboard
                go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            }
            else if (item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza)
            {
                go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
                if (item.CookingStage == (byte)CookingStage.Uncooked) rend.material.color = new Color(1f, 0.9f, 0.7f); // Pale dough
                else if (item.CookingStage == (byte)CookingStage.Baked) rend.material.color = new Color(0.8f, 0.5f, 0.2f); // Baked crust
                else if (item.CookingStage == (byte)CookingStage.Burned) rend.material.color = Color.black;

                UpdatePizzaIngredientLayers(go, item);
            }
            else
            {
                // Ingredient
                bool prepared = item.PrepStage == (byte)ProcessingStage.Prepared;
                go.transform.localScale = prepared ? new Vector3(0.15f, 0.15f, 0.15f) : new Vector3(0.25f, 0.25f, 0.25f);
                
                if (item.Category == (byte)ItemCategory.Dough) rend.material.color = new Color(0.92f, 0.85f, 0.70f);
                else if (item.Category == (byte)ItemCategory.Sauce) rend.material.color = new Color(0.80f, 0.15f, 0.10f);
                else if (item.Category == (byte)ItemCategory.Cheese) rend.material.color = new Color(0.98f, 0.88f, 0.35f);
                else if (item.Category == (byte)ItemCategory.Topping) rend.material.color = new Color(0.65f, 0.15f, 0.15f);
            }
        }

        private void UpdatePizzaIngredientLayers(GameObject pizzaGo, NetItemDto item)
        {
            if (!_pizzaIngredientLayers.TryGetValue(item.ItemId, out var layers))
            {
                layers = new GameObject[3];
                _pizzaIngredientLayers[item.ItemId] = layers;
            }

            // Sauce: Bit 0
            bool hasSauce = (item.IngredientMask & 1) != 0;
            if (hasSauce && layers[0] == null)
            {
                var sauce = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sauce.transform.SetParent(pizzaGo.transform);
                sauce.transform.localPosition = new Vector3(0, 1.25f, 0); 
                sauce.transform.localScale = new Vector3(0.875f, 0.25f, 0.875f); // Global roughly 0.35 x 0.005 x 0.35
                sauce.transform.localRotation = Quaternion.identity;
                sauce.GetComponent<Renderer>().material.color = new Color(0.80f, 0.15f, 0.10f); // Red
                Destroy(sauce.GetComponent<Collider>());
                layers[0] = sauce;
            }
            if (layers[0] != null) layers[0].SetActive(hasSauce);

            // Cheese: Bit 1
            bool hasCheese = (item.IngredientMask & 2) != 0;
            if (hasCheese && layers[1] == null)
            {
                var cheese = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cheese.transform.SetParent(pizzaGo.transform);
                cheese.transform.localPosition = new Vector3(0, 1.75f, 0);
                cheese.transform.localScale = new Vector3(0.825f, 0.25f, 0.825f); // Global roughly 0.33 x 0.005 x 0.33
                cheese.transform.localRotation = Quaternion.identity;
                cheese.GetComponent<Renderer>().material.color = new Color(0.98f, 0.88f, 0.35f); // Yellow
                Destroy(cheese.GetComponent<Collider>());
                layers[1] = cheese;
            }
            if (layers[1] != null) layers[1].SetActive(hasCheese);

            // Topping: Bit 2
            bool hasTopping = (item.IngredientMask & 4) != 0;
            if (hasTopping && layers[2] == null)
            {
                var toppingRoot = new GameObject("Toppings");
                toppingRoot.transform.SetParent(pizzaGo.transform);
                toppingRoot.transform.localPosition = new Vector3(0, 2.25f, 0);
                toppingRoot.transform.localRotation = Quaternion.identity;
                toppingRoot.transform.localScale = Vector3.one;

                for (int i = 0; i < 4; i++)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.SetParent(toppingRoot.transform);
                    
                    float angle = i * Mathf.PI / 2f + Random.Range(-0.2f, 0.2f);
                    float radius = Random.Range(0.1f, 0.3f) / 0.4f;
                    sphere.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    sphere.transform.localScale = new Vector3(0.25f, 5f, 0.25f); // Global roughly 0.1 x 0.1 x 0.1
                    sphere.GetComponent<Renderer>().material.color = new Color(0.4f, 0.1f, 0.1f);
                    Destroy(sphere.GetComponent<Collider>());
                }
                layers[2] = toppingRoot;
            }
            if (layers[2] != null) layers[2].SetActive(hasTopping);
        }

        private void Update()
        {
            if (PizzeriaNetworkState.Instance == null) return;

            _cacheUpdateTimer -= Time.deltaTime;
            if (_cacheUpdateTimer <= 0f)
            {
                UpdateCaches();
                _cacheUpdateTimer = CacheUpdateInterval;
            }

            // Continuously lerp items to their target anchors just to keep them locked
            foreach (var item in PizzeriaNetworkState.Instance.Items)
            {
                UpdateOrSpawnVisual(item);
            }
            foreach (var tool in PizzeriaNetworkState.Instance.Tools)
            {
                UpdateOrSpawnToolVisual(tool);
            }
        }
    }
}
