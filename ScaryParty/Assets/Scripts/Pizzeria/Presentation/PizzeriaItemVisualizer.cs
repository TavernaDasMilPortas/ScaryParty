using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    [DefaultExecutionOrder(150)]
    public class PizzeriaItemVisualizer : NetworkBehaviour
    {
        public static PizzeriaItemVisualizer Instance { get; private set; }

        [Header("Catálogo de Apresentação")]
        public ItemPresentationCatalog catalog;

        [Header("Prefabs de Fallback")]
        public GameObject pizzaBoxPrefab;
        public GameObject pizzaBasePrefab;
        public GameObject doughPrefab;

        private readonly Dictionary<ulong, ItemVisualWrapper> _visualWrappers = new Dictionary<ulong, ItemVisualWrapper>();
        private readonly Dictionary<ulong, ItemVisualWrapper> _toolVisualWrappers = new Dictionary<ulong, ItemVisualWrapper>();
        private readonly Dictionary<ulong, GameObject[]> _pizzaIngredientLayers = new Dictionary<ulong, GameObject[]>();

        private readonly Dictionary<int, StationView> _stationCache = new Dictionary<int, StationView>();
        private readonly Dictionary<ulong, (Transform left, Transform right)> _playerHandCache = new Dictionary<ulong, (Transform left, Transform right)>();

        private float _cacheUpdateTimer = 0f;
        private const float CacheUpdateInterval = 2f;

        private void Awake()
        {
            Instance = this;
            EnsureCatalog();
        }

        public void EnsureCatalog()
        {
            if (catalog == null)
            {
                catalog = Resources.Load<ItemPresentationCatalog>("ItemPresentationCatalog");
#if UNITY_EDITOR
                if (catalog == null)
                {
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
                }
#endif
            }
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

            ClearAllVisuals();
        }

        private void ClearAllVisuals()
        {
            foreach (var kvp in _visualWrappers)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _visualWrappers.Clear();

            foreach (var kvp in _toolVisualWrappers)
            {
                if (kvp.Value != null) Destroy(kvp.Value.gameObject);
            }
            _toolVisualWrappers.Clear();

            foreach (var kvp in _pizzaIngredientLayers)
            {
                if (kvp.Value != null)
                {
                    foreach (var l in kvp.Value) if (l != null) Destroy(l);
                }
            }
            _pizzaIngredientLayers.Clear();
        }

        private void UpdateCaches()
        {
            _stationCache.Clear();
            var stations = FindObjectsByType<StationView>(FindObjectsSortMode.None);
            for (int i = 0; i < stations.Length; i++)
            {
                _stationCache[stations[i].stationId] = stations[i];
            }

            _playerHandCache.Clear();
            var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
            for (int i = 0; i < players.Length; i++)
            {
                var netObj = players[i].GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    var anim = players[i].GetComponentInChildren<Animator>();
                    Transform leftHand = null;
                    Transform rightHand = null;
                    if (anim != null)
                    {
                        leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                        rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                    }
                    if (leftHand == null) leftHand = players[i].transform;
                    if (rightHand == null) rightHand = players[i].transform;

                    _playerHandCache[netObj.OwnerClientId] = (leftHand, rightHand);
                }
            }
        }

        private void OnItemsChanged(NetworkListEvent<NetItemDto> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Remove ||
                changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.RemoveAt)
            {
                if (_visualWrappers.TryGetValue(changeEvent.Value.ItemId, out var wrapper))
                {
                    if (wrapper != null) Destroy(wrapper.gameObject);
                    _visualWrappers.Remove(changeEvent.Value.ItemId);
                }
                if (_pizzaIngredientLayers.TryGetValue(changeEvent.Value.ItemId, out var layers))
                {
                    if (layers != null)
                    {
                        for (int i = 0; i < layers.Length; i++)
                            if (layers[i] != null) Destroy(layers[i]);
                    }
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
                if (_toolVisualWrappers.TryGetValue(changeEvent.Value.ToolId, out var wrapper))
                {
                    if (wrapper != null) Destroy(wrapper.gameObject);
                    _toolVisualWrappers.Remove(changeEvent.Value.ToolId);
                }
            }
            else
            {
                UpdateOrSpawnToolVisual(changeEvent.Value);
            }
        }

        private void UpdateOrSpawnToolVisual(NetToolDto tool)
        {
            ItemPresentationProfile profile = catalog != null
                ? catalog.FindProfile(ItemCategory.Other, tool.DefinitionId, true, false)
                : null;

            if (!_toolVisualWrappers.TryGetValue(tool.ToolId, out var wrapper) || wrapper == null)
            {
                var wrapperGo = new GameObject($"ToolVisual_{tool.ToolId}");
                wrapper = wrapperGo.AddComponent<ItemVisualWrapper>();
                wrapper.ItemId = tool.ToolId;
                wrapper.IsTool = true;

                GameObject model = null;
                if (profile != null && profile.visualPrefab != null)
                {
                    model = Instantiate(profile.visualPrefab);
                }
                else
                {
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
                    var rend = model.GetComponent<Renderer>();
                    if (rend != null) rend.material.color = new Color(0.7f, 0.75f, 0.8f);
                    var col = model.GetComponent<Collider>();
                    if (col != null) Destroy(col);
                }

                wrapper.BindVisual(profile, model);
                _toolVisualWrappers[tool.ToolId] = wrapper;
            }

            if (tool.LocationType == (byte)LocationType.Destroyed ||
                tool.LocationType == (byte)LocationType.Backpack ||
                tool.LocationType == (byte)LocationType.StorageSlot)
            {
                if (wrapper.gameObject.activeSelf) wrapper.gameObject.SetActive(false);
                return;
            }

            if (!wrapper.gameObject.activeSelf) wrapper.gameObject.SetActive(true);

            AttachToolToLocation(wrapper, tool, profile);
        }

        private void AttachToolToLocation(ItemVisualWrapper wrapper, NetToolDto tool, ItemPresentationProfile profile)
        {
            int fpLayer = LayerMask.NameToLayer("FirstPersonOnly");
            if (fpLayer < 0) fpLayer = 0;

            if (tool.LocationType == (byte)LocationType.StationSlot)
            {
                if (_stationCache.TryGetValue((int)tool.HolderId, out var st))
                {
                    Transform slotT = st.slotAnchors != null && tool.SlotId < st.slotAnchors.Length
                        ? st.slotAnchors[tool.SlotId]
                        : st.transform;

                    var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;
                    wrapper.AttachTo(slotT, pose, 0);
                }
            }
            else if (tool.LocationType == (byte)LocationType.Hand)
            {
                ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                bool isLocalPlayer = tool.HolderId == localClientId;

                if (isLocalPlayer && FirstPersonCameraController.LocalInstance != null)
                {
                    var fpCam = FirstPersonCameraController.LocalInstance;
                    Transform socket = tool.SlotId == 0 ? fpCam.LeftHandAnchor : fpCam.RightHandAnchor;
                    if (socket != null)
                    {
                        var pose = tool.SlotId == 0
                            ? (profile != null ? profile.fpLeftHandPose : ItemPoseOffset.Identity)
                            : (profile != null ? profile.fpRightHandPose : ItemPoseOffset.Identity);

                        wrapper.AttachTo(socket, pose, fpLayer);
                    }
                }
                else if (_playerHandCache.TryGetValue(tool.HolderId, out var hands))
                {
                    Transform socket = tool.SlotId == 0 ? hands.left : hands.right;
                    if (socket != null)
                    {
                        var pose = tool.SlotId == 0
                            ? (profile != null ? profile.remoteLeftHandPose : ItemPoseOffset.Identity)
                            : (profile != null ? profile.remoteRightHandPose : ItemPoseOffset.Identity);

                        wrapper.AttachTo(socket, pose, 0);
                    }
                }
            }
            else if (tool.LocationType == (byte)LocationType.StagingSlot)
            {
                int stagingId = ResolveStagingStationId();
                if (_stationCache.TryGetValue(stagingId, out var stg))
                {
                    Transform slotT = stg.slotAnchors != null && tool.SlotId >= 0 && tool.SlotId < stg.slotAnchors.Length
                        ? stg.slotAnchors[tool.SlotId]
                        : stg.transform;

                    var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;
                    wrapper.AttachTo(slotT, pose, 0);
                }
            }
        }

        private int ResolveStagingStationId()
        {
            var bindings = GetComponent<ScaryParty.Pizzeria.Authoring.PizzeriaAuthoringBindings>();
            if (bindings != null)
            {
                int id = bindings.ResolvePrimaryStagingId(10);
                if (_stationCache.ContainsKey(id)) return id;
            }
            if (_stationCache.ContainsKey(10)) return 10;
            foreach (var kvp in _stationCache)
            {
                if (kvp.Value is StagingStation) return kvp.Key;
            }
            return 10;
        }

        private int ResolveDefaultStackStationId()
        {
            var bindings = GetComponent<ScaryParty.Pizzeria.Authoring.PizzeriaAuthoringBindings>();
            if (bindings != null)
            {
                int id = bindings.ResolvePrimaryBoxStackStationId(10);
                if (_stationCache.ContainsKey(id)) return id;
            }
            foreach (var kvp in _stationCache)
            {
                if (kvp.Value is StackStation) return kvp.Key;
            }
            return ResolveStagingStationId();
        }

        private void UpdateOrSpawnVisual(NetItemDto item)
        {
            if (catalog == null) EnsureCatalog();

            bool isBoxed = item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box;
            ItemPresentationProfile profile = catalog != null
                ? catalog.FindProfile((ItemCategory)item.Category, item.DefinitionId, false, isBoxed)
                : null;

            if (!_visualWrappers.TryGetValue(item.ItemId, out var wrapper) || wrapper == null)
            {
                var wrapperGo = new GameObject($"ItemVisual_{item.ItemId}");
                wrapper = wrapperGo.AddComponent<ItemVisualWrapper>();
                wrapper.ItemId = item.ItemId;
                wrapper.IsTool = false;

                GameObject model = CreateItemModel(item, profile);
                wrapper.BindVisual(profile, model);
                _visualWrappers[item.ItemId] = wrapper;
            }
            else if (wrapper.CurrentProfile != profile)
            {
                // Re-bind quando perfil muda (ex: pizza empacotada em caixa)
                if (wrapper.VisualInstance != null) Destroy(wrapper.VisualInstance);
                if (_pizzaIngredientLayers.TryGetValue(item.ItemId, out var layers))
                {
                    if (layers != null)
                    {
                        for (int i = 0; i < layers.Length; i++)
                            if (layers[i] != null) Destroy(layers[i]);
                    }
                    _pizzaIngredientLayers.Remove(item.ItemId);
                }

                GameObject model = CreateItemModel(item, profile);
                wrapper.BindVisual(profile, model);
            }

            // Atualiza aparência conforme o estado de cozimento / ingredientes
            UpdateItemAppearance(wrapper, item);

            if (item.LocationType == (byte)LocationType.Destroyed ||
                item.LocationType == (byte)LocationType.Backpack ||
                item.LocationType == (byte)LocationType.StorageSlot)
            {
                if (wrapper.gameObject.activeSelf) wrapper.gameObject.SetActive(false);
                return;
            }

            if (!wrapper.gameObject.activeSelf) wrapper.gameObject.SetActive(true);

            AttachItemToLocation(wrapper, item, profile);
        }

        private GameObject CreateItemModel(NetItemDto item, ItemPresentationProfile profile)
        {
            if (profile != null && profile.visualPrefab != null)
            {
                return Instantiate(profile.visualPrefab);
            }

            bool isBoxed = item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box;
            bool isIngredient = item.Category == (byte)ItemCategory.Dough ||
                                item.Category == (byte)ItemCategory.Sauce ||
                                item.Category == (byte)ItemCategory.Cheese ||
                                item.Category == (byte)ItemCategory.Topping;

            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            GameObject go;
            if (isBoxed)
            {
                if (pizzaBoxPrefab != null) go = Instantiate(pizzaBoxPrefab);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
                    var rend = go.GetComponent<Renderer>();
                    var mat = new Material(urpShader) { color = new Color(0.6f, 0.4f, 0.2f) };
                    rend.sharedMaterial = mat;
                }
            }
            else if (isIngredient)
            {
                if (item.Category == (byte)ItemCategory.Dough && doughPrefab != null)
                    go = Instantiate(doughPrefab);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    var rend = go.GetComponent<Renderer>();
                    Color ingColor = Color.white;
                    if (item.Category == (byte)ItemCategory.Dough) ingColor = new Color(0.92f, 0.85f, 0.70f);
                    else if (item.Category == (byte)ItemCategory.Sauce) ingColor = new Color(0.80f, 0.15f, 0.10f);
                    else if (item.Category == (byte)ItemCategory.Cheese) ingColor = new Color(0.98f, 0.88f, 0.35f);
                    else if (item.Category == (byte)ItemCategory.Topping) ingColor = new Color(0.65f, 0.15f, 0.15f);

                    var mat = new Material(urpShader) { color = ingColor };
                    rend.sharedMaterial = mat;
                }
            }
            else
            {
                // Pizza Base
                if (pizzaBasePrefab != null) go = Instantiate(pizzaBasePrefab);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
                    var rend = go.GetComponent<Renderer>();
                    var mat = new Material(urpShader) { color = new Color(0.92f, 0.85f, 0.70f) };
                    rend.sharedMaterial = mat;
                }
            }

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private void AttachItemToLocation(ItemVisualWrapper wrapper, NetItemDto item, ItemPresentationProfile profile)
        {
            int fpLayer = LayerMask.NameToLayer("FirstPersonOnly");
            if (fpLayer < 0) fpLayer = 0;

            if (item.LocationType == (byte)LocationType.StationSlot)
            {
                if (!_stationCache.TryGetValue((int)item.HolderId, out var st) || st == null)
                {
                    UpdateCaches();
                    _stationCache.TryGetValue((int)item.HolderId, out st);
                }

                if (st != null)
                {
                    Transform slotT = st.slotAnchors != null && item.SlotId >= 0 && item.SlotId < st.slotAnchors.Length && st.slotAnchors[item.SlotId] != null
                        ? st.slotAnchors[item.SlotId]
                        : null;

                    var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;

                    if (slotT != null)
                    {
                        wrapper.AttachTo(slotT, pose, 0, useSupportPoint: true);
                    }
                    else
                    {
                        // Fallback quando não há âncora explícita: usa a posição exata da bancada calculada pela estação
                        Vector3 slotPos = st.GetSlotPosition(item.SlotId);
                        Quaternion slotRot = st.GetSlotRotation(item.SlotId);
                        wrapper.transform.SetParent(st.transform, true);
                        wrapper.transform.position = slotPos;
                        wrapper.transform.rotation = slotRot;
                        ItemVisualWrapper.SetLayerRecursively(wrapper.gameObject, 0);
                        if (wrapper.VisualInstance != null)
                        {
                            wrapper.VisualInstance.transform.localPosition = Vector3.zero;
                            wrapper.VisualInstance.transform.localRotation = Quaternion.identity;
                        }
                    }
                }
            }
            else if (item.LocationType == (byte)LocationType.Hand)
            {
                ulong localClientId = NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : 0;
                bool isLocalPlayer = item.HolderId == localClientId;

                if (isLocalPlayer && FirstPersonCameraController.LocalInstance != null)
                {
                    var fpCam = FirstPersonCameraController.LocalInstance;
                    Transform socket = item.SlotId == 0 ? fpCam.LeftHandAnchor : fpCam.RightHandAnchor;
                    if (socket != null)
                    {
                        var pose = item.SlotId == 0
                            ? (profile != null ? profile.fpLeftHandPose : ItemPoseOffset.Identity)
                            : (profile != null ? profile.fpRightHandPose : ItemPoseOffset.Identity);

                        wrapper.AttachTo(socket, pose, fpLayer, useSupportPoint: false);
                    }
                }
                else if (_playerHandCache.TryGetValue(item.HolderId, out var hands))
                {
                    Transform socket = item.SlotId == 0 ? hands.left : hands.right;
                    if (socket != null)
                    {
                        var pose = item.SlotId == 0
                            ? (profile != null ? profile.remoteLeftHandPose : ItemPoseOffset.Identity)
                            : (profile != null ? profile.remoteRightHandPose : ItemPoseOffset.Identity);

                        wrapper.AttachTo(socket, pose, 0, useSupportPoint: false);
                    }
                }
            }
            else if (item.LocationType == (byte)LocationType.StagingSlot)
            {
                int stagingId = ResolveStagingStationId();
                if (_stationCache.TryGetValue(stagingId, out var stg))
                {
                    Transform slotT = stg.slotAnchors != null && item.SlotId < stg.slotAnchors.Length
                        ? stg.slotAnchors[item.SlotId]
                        : stg.transform;

                    var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;
                    wrapper.AttachTo(slotT, pose, 0, useSupportPoint: true);
                }
            }
            else if (item.LocationType == (byte)LocationType.Stack)
            {
                int stackStationId = ResolveDefaultStackStationId();
                var netState = PizzeriaNetworkState.Instance;
                if (netState != null)
                {
                    for (int s = 0; s < netState.Stacks.Count; s++)
                    {
                        if (netState.Stacks[s].StackId == item.HolderId)
                        {
                            stackStationId = netState.Stacks[s].StationId;
                            break;
                        }
                    }
                }

                if (_stationCache.TryGetValue(stackStationId, out var stackStation))
                {
                    Transform slotT = stackStation.slotAnchors != null && stackStation.slotAnchors.Length > 0
                        ? stackStation.slotAnchors[0]
                        : stackStation.transform;

                    var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;
                    pose.position += Vector3.up * (item.SlotId * 0.12f);
                    wrapper.AttachTo(slotT, pose, 0, useSupportPoint: true);
                }
            }
        }

        private void UpdateItemAppearance(ItemVisualWrapper wrapper, NetItemDto item)
        {
            if (wrapper.VisualInstance == null) return;
            var renderers = wrapper.VisualInstance.GetComponentsInChildren<Renderer>(true);

            if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
            {
                ApplyColorToRenderers(renderers, new Color(0.6f, 0.4f, 0.2f));
                UpdateBoxLabel(wrapper.VisualInstance, item);
            }
            else if (item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza)
            {
                Color doughColor = new Color(0.95f, 0.88f, 0.72f);
                Color bakedColor = new Color(0.80f, 0.48f, 0.15f);
                Color burnedColor = new Color(0.12f, 0.10f, 0.10f);

                Color targetColor;
                if (item.CookingStage == (byte)CookingStage.Burned)
                {
                    targetColor = burnedColor;
                }
                else if (item.BurnProgress > 0f)
                {
                    targetColor = Color.Lerp(bakedColor, burnedColor, item.BurnProgress);
                }
                else if (item.CookingStage == (byte)CookingStage.Baked)
                {
                    targetColor = bakedColor;
                }
                else
                {
                    targetColor = Color.Lerp(doughColor, bakedColor, Mathf.Clamp01(item.CookProgress));
                }

                ApplyColorToRenderers(renderers, targetColor);
                UpdatePizzaIngredientLayers(wrapper.VisualInstance, item);
                UpdatePizzaSmoke(wrapper.VisualInstance, item);
            }
            else
            {
                // Ingrediente
                if (item.Category == (byte)ItemCategory.Dough) ApplyColorToRenderers(renderers, new Color(0.92f, 0.85f, 0.70f));
                else if (item.Category == (byte)ItemCategory.Sauce) ApplyColorToRenderers(renderers, new Color(0.80f, 0.15f, 0.10f));
                else if (item.Category == (byte)ItemCategory.Cheese) ApplyColorToRenderers(renderers, new Color(0.98f, 0.88f, 0.35f));
                else if (item.Category == (byte)ItemCategory.Topping) ApplyColorToRenderers(renderers, new Color(0.65f, 0.15f, 0.15f));
            }
        }

        private static void ApplyColorToRenderers(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null || renderers[i] is ParticleSystemRenderer) continue;
                if (renderers[i].material != null)
                {
                    renderers[i].material.color = color;
                }
            }
        }

        private void UpdateBoxLabel(GameObject boxGo, NetItemDto item)
        {
            Transform anchor = ItemVisualWrapper.FindDeepChild(boxGo.transform, "BoxLabelAnchor") ?? boxGo.transform;
            var labelTrans = anchor.Find("BoxLabel") ?? boxGo.transform.Find("BoxLabel");
            TMPro.TextMeshPro tmp;
            if (labelTrans == null)
            {
                var labelGo = new GameObject("BoxLabel");
                labelGo.transform.SetParent(anchor, false);
                if (anchor == boxGo.transform)
                {
                    labelGo.transform.localPosition = new Vector3(0f, 0.52f, 0f);
                    labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
                else
                {
                    labelGo.transform.localPosition = Vector3.zero;
                    labelGo.transform.localRotation = Quaternion.identity;
                }
                labelGo.transform.localScale = Vector3.one * 0.1f;
                tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 2.5f;
                tmp.color = Color.black;
                var rect = labelGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(3.5f, 3.5f);
            }
            else
            {
                tmp = labelTrans.GetComponent<TMPro.TextMeshPro>();
            }

            if (tmp != null)
            {
                if (item.LabelDestinationId >= 0)
                {
                    tmp.text = $"<b>{AddressFormat.Display(item.LabelDestinationId)}</b>\n<size=70%>PEDIDO PIZZA</size>";
                }
                else
                {
                    tmp.text = "<size=70%>(Sem Etiqueta)</size>";
                }
            }
        }

        private void UpdatePizzaSmoke(GameObject pizzaGo, NetItemDto item)
        {
            Transform anchor = ItemVisualWrapper.FindDeepChild(pizzaGo.transform, "SmokeAnchor") ?? pizzaGo.transform;
            var smokeTrans = anchor.Find("PizzaSmoke") ?? pizzaGo.transform.Find("PizzaSmoke");
            ParticleSystem ps;
            if (smokeTrans == null)
            {
                var smokeGo = new GameObject("PizzaSmoke");
                smokeGo.transform.SetParent(anchor, false);
                if (anchor == pizzaGo.transform)
                {
                    smokeGo.transform.localPosition = new Vector3(0f, 0.3f, 0f);
                }
                else
                {
                    smokeGo.transform.localPosition = Vector3.zero;
                }
                ps = smokeGo.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.startLifetime = 0.8f;
                main.startSpeed = 0.4f;
                main.startSize = 0.15f;
                main.startColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
                main.playOnAwake = false;
                var em = ps.emission;
                em.rateOverTime = 6f;
                var rend = smokeGo.GetComponent<ParticleSystemRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                if (shader != null) rend.material = new Material(shader);
                ps.Stop();
            }
            else
            {
                ps = smokeTrans.GetComponent<ParticleSystem>();
            }

            if (ps != null)
            {
                bool isBurning = item.BurnProgress > 0f || item.CookingStage == (byte)CookingStage.Burned;
                if (isBurning && !ps.isPlaying) ps.Play();
                else if (!isBurning && ps.isPlaying) ps.Stop();
            }
        }

        private void UpdatePizzaIngredientLayers(GameObject pizzaGo, NetItemDto item)
        {
            if (!_pizzaIngredientLayers.TryGetValue(item.ItemId, out var layers))
            {
                layers = new GameObject[3];
                _pizzaIngredientLayers[item.ItemId] = layers;
            }

            Transform layerParent = ItemVisualWrapper.FindDeepChild(pizzaGo.transform, "IngredientLayerAnchor") ?? pizzaGo.transform;
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Sauce: Bit 0
            bool hasSauce = (item.IngredientMask & 1) != 0;
            if (hasSauce && layers[0] == null)
            {
                var sauce = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                sauce.transform.SetParent(layerParent, false);
                sauce.transform.localPosition = new Vector3(0, 0.02f, 0);
                sauce.transform.localScale = new Vector3(0.38f, 0.005f, 0.38f);
                sauce.transform.localRotation = Quaternion.identity;
                var mat = new Material(urpShader) { color = new Color(0.80f, 0.15f, 0.10f) };
                sauce.GetComponent<Renderer>().sharedMaterial = mat;
                Destroy(sauce.GetComponent<Collider>());
                layers[0] = sauce;
            }
            if (layers[0] != null) layers[0].SetActive(hasSauce);

            // Cheese: Bit 1
            bool hasCheese = (item.IngredientMask & 2) != 0;
            if (hasCheese && layers[1] == null)
            {
                var cheese = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cheese.transform.SetParent(layerParent, false);
                cheese.transform.localPosition = new Vector3(0, 0.03f, 0);
                cheese.transform.localScale = new Vector3(0.36f, 0.005f, 0.36f);
                cheese.transform.localRotation = Quaternion.identity;
                var mat = new Material(urpShader) { color = new Color(0.98f, 0.88f, 0.35f) };
                cheese.GetComponent<Renderer>().sharedMaterial = mat;
                Destroy(cheese.GetComponent<Collider>());
                layers[1] = cheese;
            }
            if (layers[1] != null) layers[1].SetActive(hasCheese);

            // Topping: Bit 2
            bool hasTopping = (item.IngredientMask & 4) != 0;
            if (hasTopping && layers[2] == null)
            {
                var toppingRoot = new GameObject("Toppings");
                toppingRoot.transform.SetParent(layerParent, false);
                toppingRoot.transform.localPosition = new Vector3(0, 0.04f, 0);
                toppingRoot.transform.localRotation = Quaternion.identity;
                toppingRoot.transform.localScale = Vector3.one;

                var topMat = new Material(urpShader) { color = new Color(0.65f, 0.15f, 0.15f) };

                for (int i = 0; i < 4; i++)
                {
                    var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    sphere.transform.SetParent(toppingRoot.transform, false);

                    float angle = i * Mathf.PI / 2f + UnityEngine.Random.Range(-0.2f, 0.2f);
                    float radius = UnityEngine.Random.Range(0.04f, 0.12f);
                    sphere.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    sphere.transform.localScale = new Vector3(0.05f, 0.02f, 0.05f);
                    sphere.GetComponent<Renderer>().sharedMaterial = topMat;
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
        }
    }
}
