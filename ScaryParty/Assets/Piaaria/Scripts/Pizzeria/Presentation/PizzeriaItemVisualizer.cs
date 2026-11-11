using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    [DefaultExecutionOrder(150)]
    [RequireComponent(typeof(KitchenProcessEffects))]
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
        private readonly HashSet<ulong> _processHiddenItems = new HashSet<ulong>();
        private MaterialPropertyBlock _sharedItemColorBlock;
        private MaterialPropertyBlock SharedItemColorBlock => _sharedItemColorBlock ??= new MaterialPropertyBlock();

        public ItemVisualWrapper GetItemVisual(ulong id)
            => _visualWrappers.TryGetValue(id, out var wrapper) ? wrapper : null;

        public ItemVisualWrapper GetToolVisual(ulong id)
            => _toolVisualWrappers.TryGetValue(id, out var wrapper) ? wrapper : null;

        public void SetProcessHiddenItems(HashSet<ulong> hidden)
        {
            _processHiddenItems.Clear();
            _processHiddenItems.UnionWith(hidden);
            foreach (var pair in _visualWrappers)
            {
                if (pair.Value != null && pair.Value.VisualInstance != null)
                    pair.Value.VisualInstance.SetActive(!_processHiddenItems.Contains(pair.Key));
            }
        }

        private readonly HashSet<ulong> _inTransitItems = new HashSet<ulong>();

        public void SetItemInTransit(ulong itemId, bool inTransit)
        {
            if (inTransit) _inTransitItems.Add(itemId);
            else _inTransitItems.Remove(itemId);
        }

        public bool IsItemInTransit(ulong itemId) => _inTransitItems.Contains(itemId);

        public Transform GetPlayerHandAnchor(ulong clientId, int handIndex)
        {
            if (!_playerHandCache.TryGetValue(clientId, out var hands) || hands.left == null || hands.right == null)
            {
                UpdateCaches();
                _playerHandCache.TryGetValue(clientId, out hands);
            }
            return handIndex == 0 ? hands.left : hands.right;
        }
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
            if (GetComponent<KitchenProcessEffects>() == null)
                gameObject.AddComponent<KitchenProcessEffects>();
        }

        public void EnsureCatalog()
        {
            var styledCatalog = ScaryParty.VisualStyles.VisualStyleManager.Active?.activeLibrary?.styledItemCatalogAsset as ItemPresentationCatalog;
            if (styledCatalog != null)
            {
                catalog = styledCatalog;
                return;
            }

            if (catalog == null)
            {
                catalog = Resources.Load<ItemPresentationCatalog>("ItemPresentationCatalog");
#if UNITY_EDITOR
                if (catalog == null)
                {
                    catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/Piaaria/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
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
                    Transform leftHand = null;
                    Transform rightHand = null;
                    var visualCtrl = players[i].GetComponentInChildren<CharacterVisualController>(true);
                    if (visualCtrl != null)
                    {
                        leftHand = visualCtrl.LeftHandBone;
                        rightHand = visualCtrl.RightHandBone;
                    }
                    if (leftHand == null || rightHand == null)
                    {
                        var anim = players[i].GetComponentInChildren<Animator>();
                        if (anim != null && anim.isHuman)
                        {
                            if (leftHand == null) leftHand = anim.GetBoneTransform(HumanBodyBones.LeftHand);
                            if (rightHand == null) rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                        }
                        else
                        {
                            var allT = players[i].GetComponentsInChildren<Transform>(true);
                            for (int t = 0; t < allT.Length; t++)
                            {
                                string n = allT[t].name.ToLowerInvariant();
                                if (leftHand == null && (n == "arm2.l" || n.Contains("lefthand"))) leftHand = allT[t];
                                if (rightHand == null && (n == "arm2.r" || n.Contains("righthand"))) rightHand = allT[t];
                            }
                        }
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
                    ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                        model,
                        ScaryParty.VisualStyles.VisualSemanticRole.KitchenTool,
                        ScaryParty.VisualStyles.VisualCategoryIds.Props,
                        profile.presentationKey);
                }
                else
                {
                    model = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    model.transform.localScale = new Vector3(0.08f, 0.28f, 0.08f);
                    ApplyFallbackPrimitiveStyle(model, ScaryParty.VisualStyles.VisualSemanticRole.KitchenTool, new Color(0.7f, 0.75f, 0.8f));
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
                wrapper.DefinitionId = item.DefinitionId;
                wrapper.Category = item.Category;
                wrapper.PrepStage = item.PrepStage;
                wrapper.IsTool = false;

                GameObject model = CreateItemModel(item, profile);
                wrapper.BindVisual(profile, model);
                if (_processHiddenItems.Contains(item.ItemId) && model != null) model.SetActive(false);
                _visualWrappers[item.ItemId] = wrapper;
            }
            else if (wrapper.CurrentProfile != profile ||
                     wrapper.DefinitionId != item.DefinitionId ||
                     wrapper.Category != item.Category ||
                     wrapper.PrepStage != item.PrepStage)
            {
                // Re-bind quando perfil, definição (ex: ingrediente cru -> preparado) ou categoria muda
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

                wrapper.DefinitionId = item.DefinitionId;
                wrapper.Category = item.Category;
                wrapper.PrepStage = item.PrepStage;
                GameObject model = CreateItemModel(item, profile);
                wrapper.BindVisual(profile, model);
                if (_processHiddenItems.Contains(item.ItemId) && model != null) model.SetActive(false);
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

            if (!_inTransitItems.Contains(item.ItemId))
            {
                AttachItemToLocation(wrapper, item, profile);
            }
        }

        private GameObject CreateItemModel(NetItemDto item, ItemPresentationProfile profile)
        {
            if (profile != null && profile.visualPrefab != null)
            {
                var prefabInst = Instantiate(profile.visualPrefab);
                ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                    prefabInst,
                    profile.isTool ? ScaryParty.VisualStyles.VisualSemanticRole.KitchenTool : ScaryParty.VisualStyles.VisualSemanticRole.FoodGeneric,
                    profile.isTool ? ScaryParty.VisualStyles.VisualCategoryIds.Props : ScaryParty.VisualStyles.VisualCategoryIds.Food,
                    profile.presentationKey);
                return prefabInst;
            }

            bool isBoxed = item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box;
            bool isIngredient = item.Category == (byte)ItemCategory.Dough ||
                                item.Category == (byte)ItemCategory.Sauce ||
                                item.Category == (byte)ItemCategory.Cheese ||
                                item.Category == (byte)ItemCategory.Topping;

            GameObject go;
            if (isBoxed)
            {
                if (pizzaBoxPrefab != null) go = Instantiate(pizzaBoxPrefab);
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
                    ApplyFallbackPrimitiveStyle(go, ScaryParty.VisualStyles.VisualSemanticRole.PizzaBox, new Color(0.6f, 0.4f, 0.2f));
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
                    Color ingColor = Color.white;
                    ScaryParty.VisualStyles.VisualSemanticRole ingRole = ScaryParty.VisualStyles.VisualSemanticRole.FoodGeneric;
                    if (item.Category == (byte)ItemCategory.Dough)
                    {
                        ingColor = new Color(0.92f, 0.85f, 0.70f);
                        ingRole = ScaryParty.VisualStyles.VisualSemanticRole.PizzaDough;
                    }
                    else if (item.Category == (byte)ItemCategory.Sauce)
                    {
                        ingColor = new Color(0.80f, 0.15f, 0.10f);
                        ingRole = ScaryParty.VisualStyles.VisualSemanticRole.PizzaSauce;
                    }
                    else if (item.Category == (byte)ItemCategory.Cheese)
                    {
                        ingColor = new Color(0.98f, 0.88f, 0.35f);
                        ingRole = ScaryParty.VisualStyles.VisualSemanticRole.PizzaCheese;
                    }
                    else if (item.Category == (byte)ItemCategory.Topping)
                    {
                        bool isMushroom = item.DefinitionId == 8 || item.DefinitionId == 9;
                        ingColor = isMushroom ? new Color(0.75f, 0.65f, 0.52f) : new Color(0.65f, 0.15f, 0.15f);
                        ingRole = isMushroom ? ScaryParty.VisualStyles.VisualSemanticRole.PizzaMushroom : ScaryParty.VisualStyles.VisualSemanticRole.PizzaPepperoni;
                    }

                    ApplyFallbackPrimitiveStyle(go, ingRole, ingColor);
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
                    ApplyFallbackPrimitiveStyle(go, ScaryParty.VisualStyles.VisualSemanticRole.PizzaDough, new Color(0.92f, 0.85f, 0.70f));
                }
            }

            ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                go,
                ScaryParty.VisualStyles.VisualSemanticRole.FoodGeneric,
                ScaryParty.VisualStyles.VisualCategoryIds.Food,
                profile != null ? profile.presentationKey : null);

            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private void ApplyFallbackPrimitiveStyle(GameObject go, ScaryParty.VisualStyles.VisualSemanticRole role, Color tint)
        {
            var rend = go != null ? go.GetComponent<Renderer>() : null;
            if (rend == null) return;

            Material mat = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                rend.sharedMaterial,
                role,
                ScaryParty.VisualStyles.VisualCategoryIds.Food,
                () =>
                {
                    Shader urpShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    return new Material(urpShader);
                });

            if (mat != null) rend.sharedMaterial = mat;
            ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, tint, SharedItemColorBlock);
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
                        wrapper.AttachTo(st.transform, new ItemPoseOffset
                        {
                            position = st.transform.InverseTransformPoint(slotPos),
                            rotationEuler = (Quaternion.Inverse(st.transform.rotation) * slotRot).eulerAngles,
                            scale = pose.scale
                        }, 0, useSupportPoint: true);
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
                    Transform socket = toolOrItemHandSocket(hands, item.SlotId);
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
            else if (item.LocationType == (byte)LocationType.TablePlate)
            {
                int tableId = (int)item.HolderId;
                var tables = FindObjectsByType<DiningTableStation>(FindObjectsSortMode.None);
                for (int t = 0; t < tables.Length; t++)
                {
                    if (tables[t] != null && tables[t].tableId == tableId)
                    {
                        tables[t].EnsureAnchors();
                        Transform plateT = tables[t].plateAnchor != null ? tables[t].plateAnchor : tables[t].transform;
                        var pose = profile != null ? profile.stationSlotPose : ItemPoseOffset.Identity;
                        wrapper.AttachTo(plateT, pose, 0, useSupportPoint: true);
                        break;
                    }
                }
            }
        }

        private static Transform toolOrItemHandSocket((Transform left, Transform right) hands, int slotId)
            => slotId == 0 ? hands.left : hands.right;

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
                else if (item.Category == (byte)ItemCategory.Topping)
                {
                    Color topColor = (item.DefinitionId == 8 || item.DefinitionId == 9)
                        ? new Color(0.75f, 0.65f, 0.52f)
                        : new Color(0.65f, 0.15f, 0.15f);
                    ApplyColorToRenderers(renderers, topColor);
                }
            }
        }

        private void ApplyColorToRenderers(Renderer[] renderers, Color color)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                int slotCount = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
                for (int s = 0; s < slotCount; s++)
                {
                    ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(r, s, color, SharedItemColorBlock);
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
                string compSummary = PizzaOrderFormatter.FormatItemCompositionSummary(item.IngredientMask, item.IngredientCount);
                if (item.LabelDestinationId >= 0)
                {
                    tmp.text = $"<b>{AddressFormat.Display(item.LabelDestinationId)}</b>\n<size=65%>{compSummary}</size>";
                }
                else
                {
                    tmp.text = $"<size=70%>(Sem Etiqueta)</size>\n<size=60%>{compSummary}</size>";
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
            if (pizzaGo == null) return;

            var composition = ScaryParty.Pizzeria.Visual.PizzaVisualComposition.FromNetItemDto(item);
            var layout = ScaryParty.Pizzeria.Visual.PizzaLayoutGenerator.Generate(composition, ScaryParty.Pizzeria.Visual.PizzaVisualCatalog.Instance);
            ScaryParty.Pizzeria.Visual.PizzaVisualComposer.ApplyLayout(pizzaGo, layout, item);
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
