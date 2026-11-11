#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    public enum VisualPreviewSampleType
    {
        Pizzeria = 0,
        Pizza = 1,
        Character = 2,
        ModularBuilding = 3,
        CustomAsset = 4
    }

    public sealed class PreviewRendererSlotTarget
    {
        public Renderer renderer;
        public string rendererPath;
        public int slotIndex;
        public Material originalMaterial;
        public VisualAssetSlotDescriptor descriptor;
        public VisualClassificationDecision decision;
        public Material transientToonMaterial;
        public bool supportsDynamicTint;
        public Color dynamicTintColor;
        public bool hasOriginalColorBlock;
        public Color originalColorBlockColor;
    }

    public sealed class VisualPreviewSlotState : IDisposable
    {
        public string slotId;
        public string title;
        public VisualPreviewSampleType sampleType;
        public bool isDefaultSlot;
        public bool isExpanded;
        public UnityEngine.Object customAsset;
        public string defaultAssetPath;

        // Camera state
        public float yaw = -35f;
        public float pitch = 22f;
        public float distance = 5f;
        public Vector3 panOffset = Vector3.zero;
        public float defaultYaw = -35f;
        public float defaultPitch = 22f;
        public float defaultDistance = 5f;
        public VisualPreviewCameraMode cameraMode = VisualPreviewCameraMode.Perspective;

        // Sample-specific controls
        public int selectedPoseIndex = 0; // 0:Idle, 1:Walk, 2:Run, 3:Prepare, 4:Sit
        public ModularBuildingRecipe buildingRecipe = ModularBuildingRecipe.TwoStoryHouse;
        public string selectedModuleId = ""; // empty = full recipe layout
        public int swatchIndex = 0;
        public bool dynamicTintEnabled = true;
        public float splitRatio = 0.5f;

        // Runtime preview scene resources (isolated + HideAndDontSave)
        internal Scene previewScene;
        internal bool hasPreviewScene;
        internal GameObject rootGo;
        internal Camera camera;
        internal Light keyLight;
        internal Light fillLight;
        internal Light rimFillLight;
        internal readonly List<Light> interiorLights = new List<Light>();
        internal GameObject gridGo;
        internal Mesh gridMesh;
        internal Material gridMaterial;
        internal GameObject lightGizmoGo;
        internal Mesh lightGizmoMesh;
        internal Material lightGizmoMaterial;
        internal GameObject subjectHostGo;
        internal GameObject subjectGo;
        internal Mesh transientPrimitiveMesh;
        internal Material transientBaseMaterial;
        internal Bounds subjectBounds = new Bounds(Vector3.zero, Vector3.one);

        public RenderTexture toonTexture;
        public RenderTexture originalTexture;

        public readonly List<PreviewRendererSlotTarget> targets = new List<PreviewRendererSlotTarget>();
        internal readonly List<Material> transientToonMaterials = new List<Material>();
        internal readonly List<Material> transientFallbackMaterials = new List<Material>();
        internal readonly MaterialPropertyBlock mpbScratch = new MaterialPropertyBlock();

        public void ResetCamera()
        {
            yaw = defaultYaw;
            pitch = defaultPitch;
            distance = defaultDistance;
            panOffset = Vector3.zero;
        }

        public void DisposeTransientToonMaterials()
        {
            for (int i = 0; i < transientToonMaterials.Count; i++)
            {
                if (transientToonMaterials[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientToonMaterials[i]);
                }
            }
            transientToonMaterials.Clear();

            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] != null)
                {
                    targets[i].transientToonMaterial = null;
                }
            }
        }

        public void DisposeSubject()
        {
            DisposeTransientToonMaterials();
            targets.Clear();

            if (subjectGo != null)
            {
                UnityEngine.Object.DestroyImmediate(subjectGo);
                subjectGo = null;
            }
            if (transientPrimitiveMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(transientPrimitiveMesh);
                transientPrimitiveMesh = null;
            }
            if (transientBaseMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(transientBaseMaterial);
                transientBaseMaterial = null;
            }
            for (int i = 0; i < transientFallbackMaterials.Count; i++)
            {
                if (transientFallbackMaterials[i] != null)
                {
                    UnityEngine.Object.DestroyImmediate(transientFallbackMaterials[i]);
                }
            }
            transientFallbackMaterials.Clear();
        }

        public void DisposeTextures()
        {
            if (toonTexture != null)
            {
                toonTexture.Release();
                UnityEngine.Object.DestroyImmediate(toonTexture);
                toonTexture = null;
            }
            if (originalTexture != null)
            {
                originalTexture.Release();
                UnityEngine.Object.DestroyImmediate(originalTexture);
                originalTexture = null;
            }
        }

        public void Dispose()
        {
            DisposeSubject();
            DisposeTextures();

            if (camera != null)
            {
                camera.targetTexture = null;
            }

            if (gridMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(gridMaterial);
                gridMaterial = null;
            }
            if (gridMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(gridMesh);
                gridMesh = null;
            }
            if (lightGizmoMaterial != null)
            {
                UnityEngine.Object.DestroyImmediate(lightGizmoMaterial);
                lightGizmoMaterial = null;
            }
            if (lightGizmoMesh != null)
            {
                UnityEngine.Object.DestroyImmediate(lightGizmoMesh);
                lightGizmoMesh = null;
            }
            if (rootGo != null)
            {
                UnityEngine.Object.DestroyImmediate(rootGo);
                rootGo = null;
            }

            camera = null;
            keyLight = null;
            fillLight = null;
            rimFillLight = null;
            interiorLights.Clear();
            gridGo = null;
            lightGizmoGo = null;
            subjectHostGo = null;

            if (hasPreviewScene && previewScene.IsValid())
            {
                EditorSceneManager.ClosePreviewScene(previewScene);
                hasPreviewScene = false;
            }
        }
    }

    /// <summary>
    /// Isolated real-time URP 3D preview renderer supporting simultaneous viewport cards,
    /// arbitrary dragged assets (Prefabs, FBX, Mesh, Material), A/B modes, lighting presets,
    /// camera orbit/zoom/pan/projections, dynamic MPB tint testing, and strict zero-leak disposal.
    /// Guarantees zero AssetDatabase or disk writes during preview tuning.
    /// </summary>
    public sealed class VisualStylePreviewRenderer : IDisposable
    {
        public static readonly Color[] CharacterSwatches =
        {
            new Color(0.86f, 0.20f, 0.18f, 1f), // Pizza Red
            new Color(0.18f, 0.56f, 0.90f, 1f), // Cobalt Blue
            new Color(0.20f, 0.74f, 0.42f, 1f), // Emerald
            new Color(0.95f, 0.74f, 0.18f, 1f), // Golden
            new Color(0.16f, 0.75f, 0.72f, 1f)  // Turquoise
        };

        public static readonly Color[] BuildingSwatches =
        {
            new Color(0.89f, 0.76f, 0.58f, 1f), // Warm Stucco
            new Color(0.76f, 0.42f, 0.34f, 1f), // Terracotta
            new Color(0.62f, 0.76f, 0.82f, 1f), // Pastel Coastal
            new Color(0.88f, 0.84f, 0.74f, 1f), // Cream Brick
            new Color(0.68f, 0.78f, 0.60f, 1f)  // Sage Plaster
        };

        public static readonly string[] CharacterPoseNames = { "Idle", "Walk", "Run", "Prepare", "Sit" };

        private readonly List<VisualPreviewSlotState> _slots = new List<VisualPreviewSlotState>();
        private int _resolution = 256;
        private VisualPreviewComparisonMode _comparisonMode = VisualPreviewComparisonMode.Toon;
        private VisualLightingPreviewMode _lightingMode = VisualLightingPreviewMode.Interior;
        private VisualPreviewCameraMode _globalCameraMode = VisualPreviewCameraMode.Perspective;
        private bool _showGrid = true;
        private bool _showLightHandles = true;
        private bool _disposed;

        public IReadOnlyList<VisualPreviewSlotState> Slots => _slots;

        public int Resolution
        {
            get => _resolution;
            set => _resolution = Mathf.Clamp(value, 128, 1024);
        }

        public VisualPreviewComparisonMode ComparisonMode
        {
            get => _comparisonMode;
            set => _comparisonMode = value;
        }

        public VisualLightingPreviewMode LightingMode
        {
            get => _lightingMode;
            set => _lightingMode = value;
        }

        public VisualPreviewCameraMode GlobalCameraMode
        {
            get => _globalCameraMode;
            set
            {
                _globalCameraMode = value;
                for (int i = 0; i < _slots.Count; i++)
                {
                    _slots[i].cameraMode = value;
                }
            }
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set => _showGrid = value;
        }

        public bool ShowLightHandles
        {
            get => _showLightHandles;
            set => _showLightHandles = value;
        }

        public bool IsDisposed => _disposed;

        public int ActivePreviewSceneCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].hasPreviewScene && _slots[i].previewScene.IsValid()) count++;
                }
                return count;
            }
        }

        public int ActiveTransientMaterialCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    for (int m = 0; m < _slots[i].transientToonMaterials.Count; m++)
                    {
                        if (_slots[i].transientToonMaterials[m] != null) count++;
                    }
                    for (int m = 0; m < _slots[i].transientFallbackMaterials.Count; m++)
                    {
                        if (_slots[i].transientFallbackMaterials[m] != null) count++;
                    }
                    if (_slots[i].transientBaseMaterial != null) count++;
                }
                return count;
            }
        }

        public int ActiveRenderTextureCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].toonTexture != null) count++;
                    if (_slots[i].originalTexture != null) count++;
                }
                return count;
            }
        }

        public void InitializeDefaultSlots(VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            ClearAllSlots();

            CreateSlot(
                "slot_pizzaria",
                "Pizzaria",
                VisualPreviewSampleType.Pizzeria,
                VisualStyleAuditService.PizzeriaManualPrefabPath,
                isDefault: true,
                yaw: -36f,
                pitch: 24f,
                distanceScale: 1.25f);

            CreateSlot(
                "slot_pizza",
                "Pizza & Ingredientes",
                VisualPreviewSampleType.Pizza,
                null,
                isDefault: true,
                yaw: 24f,
                pitch: 44f,
                distanceScale: 1.45f);

            CreateSlot(
                "slot_character",
                "Personagem",
                VisualPreviewSampleType.Character,
                VisualStyleAuditService.CharacterVisualPrefabPath,
                isDefault: true,
                yaw: 158f,
                pitch: 14f,
                distanceScale: 1.55f);

            CreateSlot(
                "slot_modular_building",
                "Prédio Modular",
                VisualPreviewSampleType.ModularBuilding,
                VisualStyleAuditService.CityModuleCatalogPath,
                isDefault: true,
                yaw: 145f,
                pitch: 22f,
                distanceScale: 1.4f);

            for (int i = 0; i < _slots.Count; i++)
            {
                RebuildSlotSubject(_slots[i], ruleSet);
                RebuildSlotTransientMaterials(_slots[i], profile, ruleSet);
            }
        }

        public VisualPreviewSlotState AddCustomSampleSlot(
            string title,
            UnityEngine.Object asset,
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet)
        {
            var slot = CreateSlot(
                "slot_custom_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                string.IsNullOrWhiteSpace(title) ? "Amostra Custom" : title,
                VisualPreviewSampleType.CustomAsset,
                null,
                isDefault: false,
                yaw: -35f,
                pitch: 22f,
                distanceScale: 1.45f);

            slot.customAsset = asset;
            RebuildSlotSubject(slot, ruleSet);
            RebuildSlotTransientMaterials(slot, profile, ruleSet);
            return slot;
        }

        public bool RemoveSlot(string slotId)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].slotId == slotId && !_slots[i].isDefaultSlot)
                {
                    _slots[i].Dispose();
                    _slots.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public void ResetAllCameras()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i].ResetCamera();
            }
        }

        public void SetSlotCustomAsset(VisualPreviewSlotState slot, UnityEngine.Object asset, VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            if (slot == null) return;
            slot.customAsset = asset;
            if (asset != null)
            {
                slot.title = asset.name;
            }
            RebuildSlotSubject(slot, ruleSet);
            RebuildSlotTransientMaterials(slot, profile, ruleSet);
        }

        private VisualPreviewSlotState CreateSlot(
            string id,
            string title,
            VisualPreviewSampleType sampleType,
            string defaultPath,
            bool isDefault,
            float yaw,
            float pitch,
            float distanceScale)
        {
            var slot = new VisualPreviewSlotState
            {
                slotId = id,
                title = title,
                sampleType = sampleType,
                isDefaultSlot = isDefault,
                defaultAssetPath = defaultPath,
                defaultYaw = yaw,
                defaultPitch = pitch,
                yaw = yaw,
                pitch = pitch,
                cameraMode = _globalCameraMode,
                distance = Mathf.Max(2f, distanceScale * 2f)
            };

            EnsurePreviewSceneCreated(slot);
            _slots.Add(slot);
            return slot;
        }

        private void EnsurePreviewSceneCreated(VisualPreviewSlotState slot)
        {
            if (slot.hasPreviewScene && slot.previewScene.IsValid()) return;

            slot.previewScene = EditorSceneManager.NewPreviewScene();
            slot.hasPreviewScene = true;

            slot.rootGo = new GameObject($"__VisualPreviewRoot_{slot.slotId}")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            SceneManager.MoveGameObjectToScene(slot.rootGo, slot.previewScene);

            // Subject host kept cleanly under preview root
            slot.subjectHostGo = new GameObject("SubjectHost") { hideFlags = HideFlags.HideAndDontSave };
            slot.subjectHostGo.transform.SetParent(slot.rootGo.transform, false);

            // Preview camera with URP AdditionalCameraData
            var camGo = new GameObject("PreviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            camGo.transform.SetParent(slot.rootGo.transform, false);
            slot.camera = camGo.AddComponent<Camera>();
            slot.camera.scene = slot.previewScene;
            slot.camera.cameraType = CameraType.Preview;
            slot.camera.clearFlags = CameraClearFlags.SolidColor;
            slot.camera.backgroundColor = new Color(0.14f, 0.16f, 0.20f, 1f);
            slot.camera.nearClipPlane = 0.05f;
            slot.camera.farClipPlane = 250f;
            slot.camera.allowMSAA = false;
            slot.camera.enabled = false;

            var urpCam = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpCam.renderShadows = true;
            urpCam.renderPostProcessing = false;
            urpCam.requiresColorOption = CameraOverrideOption.Off;
            urpCam.requiresDepthOption = CameraOverrideOption.Off;

            // Lighting rig inside preview scene (never touches RenderSettings of active scene)
            slot.keyLight = CreatePreviewLight(slot.rootGo.transform, "KeyLight", LightType.Directional, 1.35f, LightShadows.Soft);
            slot.fillLight = CreatePreviewLight(slot.rootGo.transform, "FillLight", LightType.Directional, 0.50f, LightShadows.None);
            slot.rimFillLight = CreatePreviewLight(slot.rootGo.transform, "RimFillLight", LightType.Directional, 0.35f, LightShadows.None);

            for (int i = 0; i < 2; i++)
            {
                var pt = CreatePreviewLight(slot.rootGo.transform, $"InteriorPointLight_{i}", LightType.Point, 1.4f, LightShadows.None);
                pt.range = 10f;
                pt.color = new Color(1f, 0.91f, 0.76f, 1f);
                pt.enabled = false;
                slot.interiorLights.Add(pt);
            }

            CreateGridFloor(slot);
            CreateLightDirectionGizmo(slot);
        }

        private static Light CreatePreviewLight(Transform parent, string name, LightType type, float intensity, LightShadows shadows)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent, false);
            var light = go.AddComponent<Light>();
            light.type = type;
            light.intensity = intensity;
            light.shadows = shadows;
            light.color = Color.white;
            return light;
        }

        private static void CreateGridFloor(VisualPreviewSlotState slot)
        {
            slot.gridGo = new GameObject("PreviewGrid") { hideFlags = HideFlags.HideAndDontSave };
            slot.gridGo.transform.SetParent(slot.rootGo.transform, false);

            var mf = slot.gridGo.AddComponent<MeshFilter>();
            var mr = slot.gridGo.AddComponent<MeshRenderer>();

            slot.gridMesh = new Mesh { name = "PreviewFloorGridMesh", hideFlags = HideFlags.HideAndDontSave };
            const float half = 12f;
            slot.gridMesh.vertices = new[]
            {
                new Vector3(-half, 0f, -half),
                new Vector3(half, 0f, -half),
                new Vector3(half, 0f, half),
                new Vector3(-half, 0f, half)
            };
            slot.gridMesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(12f, 0f),
                new Vector2(12f, 12f),
                new Vector2(0f, 12f)
            };
            slot.gridMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            slot.gridMesh.RecalculateNormals();
            mf.sharedMesh = slot.gridMesh;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            slot.gridMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            VisualStyleShaderUtility.SetColorSafe(slot.gridMaterial, "_BaseColor", new Color(0.18f, 0.21f, 0.25f, 1f));
            VisualStyleShaderUtility.SetColorSafe(slot.gridMaterial, "_Color", new Color(0.18f, 0.21f, 0.25f, 1f));
            mr.sharedMaterial = slot.gridMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private static void CreateLightDirectionGizmo(VisualPreviewSlotState slot)
        {
            slot.lightGizmoGo = new GameObject("LightHandleGizmo") { hideFlags = HideFlags.HideAndDontSave };
            slot.lightGizmoGo.transform.SetParent(slot.rootGo.transform, false);

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            slot.lightGizmoMesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            slot.lightGizmoMesh.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DestroyImmediate(primitive);

            var mf = slot.lightGizmoGo.AddComponent<MeshFilter>();
            var mr = slot.lightGizmoGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = slot.lightGizmoMesh;
            slot.lightGizmoGo.transform.localScale = Vector3.one * 0.22f;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            slot.lightGizmoMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            VisualStyleShaderUtility.SetColorSafe(slot.lightGizmoMaterial, "_BaseColor", new Color(1f, 0.85f, 0.25f, 1f));
            VisualStyleShaderUtility.SetColorSafe(slot.lightGizmoMaterial, "_Color", new Color(1f, 0.85f, 0.25f, 1f));
            mr.sharedMaterial = slot.lightGizmoMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        public void RebuildSlotSubject(VisualPreviewSlotState slot, VisualConversionRuleSet ruleSet)
        {
            if (slot == null) return;
            EnsurePreviewSceneCreated(slot);
            slot.DisposeSubject();

            bool prevHostActive = slot.subjectHostGo.activeSelf;
            slot.subjectHostGo.SetActive(false);

            try
            {
                if (slot.customAsset != null)
                {
                    slot.subjectGo = InstantiateFromAnyAsset(slot, slot.customAsset, slot.subjectHostGo.transform);
                }
                else
                {
                    slot.subjectGo = BuildDefaultSubject(slot, slot.subjectHostGo.transform);
                }

                if (slot.subjectGo != null)
                {
                    SetHideFlagsRecursive(slot.subjectGo, HideFlags.HideAndDontSave);
                    StripUnsafeRuntimeComponents(slot.subjectGo, keepAnimatorDisabled: true);

                    if (slot.sampleType == VisualPreviewSampleType.Character)
                    {
                        ApplyCharacterPoseSample(slot.subjectGo, slot.selectedPoseIndex);
                    }
                }
            }
            finally
            {
                slot.subjectHostGo.SetActive(prevHostActive);
            }

            if (slot.subjectGo == null)
            {
                slot.subjectGo = CreateFallbackCubeSubject(slot, slot.subjectHostGo.transform);
            }

            ComputeBoundsAndCollectTargets(slot, ruleSet);
            slot.defaultDistance = ComputeIdealCameraDistance(slot.subjectBounds, slot.sampleType);
            slot.distance = slot.defaultDistance;
        }

        private GameObject BuildDefaultSubject(VisualPreviewSlotState slot, Transform parent)
        {
            switch (slot.sampleType)
            {
                case VisualPreviewSampleType.Pizzeria:
                    var pizzeriaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualStyleAuditService.PizzeriaManualPrefabPath);
                    if (pizzeriaPrefab != null)
                    {
                        return PrefabUtility.InstantiatePrefab(pizzeriaPrefab, parent) as GameObject
                               ?? UnityEngine.Object.Instantiate(pizzeriaPrefab, parent);
                    }
                    break;

                case VisualPreviewSampleType.Pizza:
                    return BuildRichPizzaComposite(slot, parent);

                case VisualPreviewSampleType.Character:
                    var charPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualStyleAuditService.CharacterVisualPrefabPath);
                    if (charPrefab != null)
                    {
                        return PrefabUtility.InstantiatePrefab(charPrefab, parent) as GameObject
                               ?? UnityEngine.Object.Instantiate(charPrefab, parent);
                    }
                    break;

                case VisualPreviewSampleType.ModularBuilding:
                    return BuildModularBuildingPreview(slot, parent);
            }

            return null;
        }

        private GameObject InstantiateFromAnyAsset(VisualPreviewSlotState slot, UnityEngine.Object asset, Transform parent)
        {
            if (asset is GameObject goAsset)
            {
                return PrefabUtility.InstantiatePrefab(goAsset, parent) as GameObject
                       ?? UnityEngine.Object.Instantiate(goAsset, parent);
            }

            if (asset is Mesh meshAsset)
            {
                var holder = new GameObject($"MeshPreview_{meshAsset.name}") { hideFlags = HideFlags.HideAndDontSave };
                holder.transform.SetParent(parent, false);
                var mf = holder.AddComponent<MeshFilter>();
                var mr = holder.AddComponent<MeshRenderer>();
                mf.sharedMesh = meshAsset;
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                slot.transientBaseMaterial = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                VisualStyleShaderUtility.SetColorSafe(slot.transientBaseMaterial, "_BaseColor", new Color(0.78f, 0.78f, 0.80f, 1f));
                mr.sharedMaterial = slot.transientBaseMaterial;
                return holder;
            }

            if (asset is Material matAsset)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"MatPreview_{matAsset.name}";
                sphere.hideFlags = HideFlags.HideAndDontSave;
                sphere.transform.SetParent(parent, false);
                UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());
                slot.transientPrimitiveMesh = UnityEngine.Object.Instantiate(sphere.GetComponent<MeshFilter>().sharedMesh);
                slot.transientPrimitiveMesh.hideFlags = HideFlags.HideAndDontSave;
                sphere.GetComponent<MeshFilter>().sharedMesh = slot.transientPrimitiveMesh;
                sphere.GetComponent<MeshRenderer>().sharedMaterial = matAsset;
                return sphere;
            }

            return null;
        }

        private GameObject BuildRichPizzaComposite(VisualPreviewSlotState slot, Transform parent)
        {
            var root = new GameObject("PreviewBakedPizzaComposite") { hideFlags = HideFlags.HideAndDontSave };
            root.transform.SetParent(parent, false);

            Material doughMat = ExtractFirstMaterialFromPrefab("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_MassaAberta.prefab");
            Material sauceMat = ExtractFirstMaterialFromPrefab("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_Sauce.prefab");
            Material cheeseMat = ExtractFirstMaterialFromPrefab("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_Cheese.prefab");
            Material pepMat = ExtractFirstMaterialFromPrefab("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_Pepperoni.prefab");
            Material mushMat = ExtractFirstMaterialFromPrefab("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_Mushroom.prefab");

            CreateMeshChild(slot, root.transform, "Dough_Base", PizzaModularGeometryFactory.GetDoughMesh(), doughMat, new Color(0.88f, 0.72f, 0.45f, 1f), Vector3.zero, Quaternion.identity, Vector3.one);
            CreateMeshChild(slot, root.transform, "Sauce_Layer", PizzaModularGeometryFactory.GetSauceMesh(), sauceMat, new Color(0.76f, 0.14f, 0.10f, 1f), new Vector3(0f, 0.004f, 0f), Quaternion.identity, Vector3.one);
            CreateMeshChild(slot, root.transform, "Cheese_Layer", PizzaModularGeometryFactory.GetCheeseCoatingMesh(), cheeseMat, new Color(0.96f, 0.84f, 0.46f, 1f), new Vector3(0f, 0.008f, 0f), Quaternion.identity, Vector3.one);

            Mesh pepMesh = PizzaModularGeometryFactory.GetPepperoniSliceMesh();
            Mesh mushMesh = PizzaModularGeometryFactory.GetMushroomSliceMesh();
            for (int i = 0; i < 6; i++)
            {
                float angle = i * (360f / 6f) * Mathf.Deg2Rad;
                float radius = (i % 2 == 0) ? 0.095f : 0.055f;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0.015f, Mathf.Sin(angle) * radius);
                CreateMeshChild(slot, root.transform, $"Pepperoni_{i}", pepMesh, pepMat, new Color(0.68f, 0.14f, 0.12f, 1f), pos, Quaternion.Euler(0f, i * 47f, 0f), Vector3.one);
            }

            for (int i = 0; i < 4; i++)
            {
                float angle = (i * 90f + 35f) * Mathf.Deg2Rad;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * 0.08f, 0.016f, Mathf.Sin(angle) * 0.08f);
                CreateMeshChild(slot, root.transform, $"Mushroom_{i}", mushMesh, mushMat, new Color(0.54f, 0.46f, 0.39f, 1f), pos, Quaternion.Euler(0f, i * 85f, 0f), Vector3.one);
            }

            // Wooden pizza peel/board under pizza for surface shading contrast
            var boardMesh = BuildSimpleCylinderBoardMesh(0.24f, 0.012f, out slot.transientPrimitiveMesh);
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Piaaria/Materials/City/Placeholders/Mat_Modular_Wood.mat");
            CreateMeshChild(slot, root.transform, "WoodenBoard", boardMesh, woodMat, new Color(0.55f, 0.36f, 0.22f, 1f), new Vector3(0f, -0.01f, 0f), Quaternion.identity, Vector3.one);

            root.transform.localScale = Vector3.one * 3.5f;
            return root;
        }

        private GameObject BuildModularBuildingPreview(VisualPreviewSlotState slot, Transform parent)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(VisualStyleAuditService.CityModuleCatalogPath);
            if (catalog == null) return null;

            if (!string.IsNullOrEmpty(slot.selectedModuleId))
            {
                for (int i = 0; i < catalog.definitions.Count; i++)
                {
                    var def = catalog.definitions[i];
                    if (def != null && def.prefab != null && string.Equals(def.id.ToString(), slot.selectedModuleId, StringComparison.OrdinalIgnoreCase))
                    {
                        var pieceGo = PrefabUtility.InstantiatePrefab(def.prefab, parent) as GameObject
                                      ?? UnityEngine.Object.Instantiate(def.prefab, parent);
                        pieceGo.name = $"ModularPiece_{def.id}";
                        return pieceGo;
                    }
                }
            }

            var layout = ModularBuildingAssembler.CreateRecipeLayout(
                slot.buildingRecipe,
                Vector3.zero,
                Quaternion.identity,
                42,
                $"preview_{slot.buildingRecipe}",
                0);
            return ModularBuildingAssembler.AssembleBuilding(layout, catalog, parent);
        }

        private static Material ExtractFirstMaterialFromPrefab(string prefabPath)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return null;
            var r = prefab.GetComponentInChildren<Renderer>(true);
            return r != null ? r.sharedMaterial : null;
        }

        private static void CreateMeshChild(VisualPreviewSlotState slot, Transform parent, string name, Mesh mesh, Material existingMat, Color fallbackColor, Vector3 pos, Quaternion rot, Vector3 scale)
        {
            if (mesh == null) return;
            var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = mesh;

            if (existingMat != null)
            {
                mr.sharedMaterial = existingMat;
            }
            else
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var mat = new Material(lit) { name = $"Fallback_{name}", hideFlags = HideFlags.HideAndDontSave };
                VisualStyleShaderUtility.SetColorSafe(mat, "_BaseColor", fallbackColor);
                VisualStyleShaderUtility.SetColorSafe(mat, "_Color", fallbackColor);
                mr.sharedMaterial = mat;
                slot?.transientFallbackMaterials.Add(mat);
            }
        }

        private static Mesh BuildSimpleCylinderBoardMesh(float radius, float height, out Mesh createdMesh)
        {
            var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            createdMesh = UnityEngine.Object.Instantiate(cylinder.GetComponent<MeshFilter>().sharedMesh);
            createdMesh.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DestroyImmediate(cylinder);

            Vector3[] verts = createdMesh.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vector3(verts[i].x * radius * 2f, verts[i].y * height * 0.5f, verts[i].z * radius * 2f);
            }
            createdMesh.vertices = verts;
            createdMesh.RecalculateBounds();
            createdMesh.RecalculateNormals();
            return createdMesh;
        }

        private static GameObject CreateFallbackCubeSubject(VisualPreviewSlotState slot, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "FallbackPreviewCube";
            cube.hideFlags = HideFlags.HideAndDontSave;
            cube.transform.SetParent(parent, false);
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Collider>());
            slot.transientPrimitiveMesh = UnityEngine.Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
            slot.transientPrimitiveMesh.hideFlags = HideFlags.HideAndDontSave;
            cube.GetComponent<MeshFilter>().sharedMesh = slot.transientPrimitiveMesh;
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            slot.transientBaseMaterial = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
            cube.GetComponent<MeshRenderer>().sharedMaterial = slot.transientBaseMaterial;
            return cube;
        }

        private static void SetHideFlagsRecursive(GameObject root, HideFlags flags)
        {
            if (root == null) return;
            root.hideFlags = flags;
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i].gameObject.hideFlags = flags;
            }
        }

        private static void StripUnsafeRuntimeComponents(GameObject root, bool keepAnimatorDisabled)
        {
            if (root == null) return;
            for (int pass = 0; pass < 4; pass++)
            {
                bool removedAny = false;
                var components = root.GetComponentsInChildren<Component>(true);
                for (int i = components.Length - 1; i >= 0; i--)
                {
                    var c = components[i];
                    if (c == null) continue;
                    if (c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup || c is Light)
                        continue;
                    if (keepAnimatorDisabled && c is Animator anim)
                    {
                        anim.enabled = false;
                        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        continue;
                    }
                    if (c is Behaviour beh)
                    {
                        beh.enabled = false;
                    }
                    if (IsRequiredByRemainingSibling(c))
                    {
                        continue;
                    }
                    UnityEngine.Object.DestroyImmediate(c);
                    removedAny = true;
                }
                if (!removedAny) break;
            }
        }

        private static bool IsRequiredByRemainingSibling(Component target)
        {
            if (target == null || target.gameObject == null) return false;
            Type targetType = target.GetType();
            var siblings = target.gameObject.GetComponents<Component>();
            for (int i = 0; i < siblings.Length; i++)
            {
                var sibling = siblings[i];
                if (sibling == null || ReferenceEquals(sibling, target)) continue;
                var reqs = sibling.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                for (int r = 0; r < reqs.Length; r++)
                {
                    if (reqs[r] is RequireComponent rc)
                    {
                        if ((rc.m_Type0 != null && rc.m_Type0.IsAssignableFrom(targetType)) ||
                            (rc.m_Type1 != null && rc.m_Type1.IsAssignableFrom(targetType)) ||
                            (rc.m_Type2 != null && rc.m_Type2.IsAssignableFrom(targetType)))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void ApplyCharacterPoseSample(GameObject charGo, int poseIndex)
        {
            if (charGo == null) return;
            var anim = charGo.GetComponentInChildren<Animator>(true);
            if (anim == null || anim.runtimeAnimatorController == null) return;

            var clips = anim.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0) return;

            string token = poseIndex switch
            {
                1 => "Walk",
                2 => "Run",
                3 => "Prepare",
                4 => "Sit",
                _ => "Idle"
            };

            AnimationClip matched = null;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched = clips[i];
                    break;
                }
            }
            if (matched == null) matched = clips[0];

            matched.SampleAnimation(anim.gameObject, matched.length * 0.28f);
        }

        private void ComputeBoundsAndCollectTargets(VisualPreviewSlotState slot, VisualConversionRuleSet ruleSet)
        {
            slot.targets.Clear();
            var renderers = slot.subjectGo.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);

            string defaultCat = slot.sampleType switch
            {
                VisualPreviewSampleType.Pizzeria => VisualCategoryIds.Architecture,
                VisualPreviewSampleType.Pizza => VisualCategoryIds.Food,
                VisualPreviewSampleType.Character => VisualCategoryIds.Characters,
                VisualPreviewSampleType.ModularBuilding => VisualCategoryIds.City,
                _ => VisualCategoryIds.Props
            };

            VisualSemanticRole defaultRole = slot.sampleType switch
            {
                VisualPreviewSampleType.Pizzeria => VisualSemanticRole.PizzeriaWall,
                VisualPreviewSampleType.Pizza => VisualSemanticRole.FoodGeneric,
                VisualPreviewSampleType.Character => VisualSemanticRole.PlayerBody,
                VisualPreviewSampleType.ModularBuilding => VisualSemanticRole.BuildingFacade,
                _ => VisualSemanticRole.GenericOpaque
            };

            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend == null) continue;
                if (rend.bounds.size.sqrMagnitude > 1e-6f)
                {
                    if (!hasBounds)
                    {
                        bounds = rend.bounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rend.bounds);
                    }
                }

                string relPath = GetRelativePath(slot.subjectGo.transform, rend.transform);
                var identifier = rend.GetComponent<VisualStyleIdentifier>();
                var sharedMats = rend.sharedMaterials;
                for (int s = 0; s < sharedMats.Length; s++)
                {
                    Material mat = sharedMats[s];
                    string matPath = mat != null ? AssetDatabase.GetAssetPath(mat) : "";
                    string matGuid = "";
                    long lid = 0;
                    if (mat != null)
                    {
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mat, out matGuid, out lid);
                    }

                    var slotMeta = identifier != null ? identifier.GetMetadataForSlot(s) : null;
                    bool hasDeclared = slot.sampleType != VisualPreviewSampleType.CustomAsset || identifier != null;
                    string declCat = slotMeta != null && !string.IsNullOrEmpty(slotMeta.categoryId)
                        ? slotMeta.categoryId
                        : (identifier != null && !string.IsNullOrEmpty(identifier.defaultCategoryId)
                            ? identifier.defaultCategoryId
                            : (slot.sampleType != VisualPreviewSampleType.CustomAsset ? defaultCat : null));
                    VisualSemanticRole declRole = slotMeta != null && slotMeta.role != VisualSemanticRole.Unclassified
                        ? slotMeta.role
                        : (identifier != null && identifier.defaultRole != VisualSemanticRole.Unclassified
                            ? identifier.defaultRole
                            : InferSlotRole(slot.sampleType, mat != null ? mat.name : "", relPath, s, defaultRole));

                    bool dynamicTint = IsSlotEligibleForDynamicTint(slot.sampleType, declRole, mat != null ? mat.name : "", s);

                    var desc = VisualStyleAuditService.BuildDescriptor(
                        rend, s, mat, matPath, matGuid, lid, relPath,
                        hasDeclared, declCat, declRole, dynamicTint,
                        $"Preview:{slot.title}");

                    var decision = ruleSet != null
                        ? ruleSet.Evaluate(desc)
                        : new VisualClassificationDecision
                        {
                            categoryId = declCat ?? VisualCategoryIds.Props,
                            semanticRole = declRole,
                            shaderFamily = desc.declaredShaderFamily,
                            action = VisualConversionAction.Convert,
                            confidence = 0.8f,
                            preserveTexture = true,
                            preserveColor = true,
                            allowDynamicTint = dynamicTint
                        };

                    slot.targets.Add(new PreviewRendererSlotTarget
                    {
                        renderer = rend,
                        rendererPath = relPath,
                        slotIndex = s,
                        originalMaterial = mat,
                        descriptor = desc,
                        decision = decision,
                        supportsDynamicTint = dynamicTint || decision.allowDynamicTint
                    });
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            }

            // Center subject X/Z at origin with base Y = 0
            Vector3 shift = new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
            slot.subjectGo.transform.position += shift;
            bounds.center += shift;
            slot.subjectBounds = bounds;
        }

        private static VisualSemanticRole InferSlotRole(VisualPreviewSampleType sampleType, string matName, string path, int slotIdx, VisualSemanticRole fallback)
        {
            if (sampleType == VisualPreviewSampleType.Character)
            {
                string n = matName ?? "";
                if (n.IndexOf("Pupil", StringComparison.OrdinalIgnoreCase) >= 0) return VisualSemanticRole.PlayerPupil;
                if (n.IndexOf("Eye", StringComparison.OrdinalIgnoreCase) >= 0) return VisualSemanticRole.PlayerEye;
                if (n.IndexOf("Skin", StringComparison.OrdinalIgnoreCase) >= 0) return VisualSemanticRole.PlayerSkin;
                if (n.IndexOf("Apron", StringComparison.OrdinalIgnoreCase) >= 0) return VisualSemanticRole.PlayerApron;
                if (n.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0 || slotIdx == 3) return VisualSemanticRole.PlayerBody;
            }
            else if (sampleType == VisualPreviewSampleType.ModularBuilding)
            {
                string n = matName ?? "";
                if (n.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Window", StringComparison.OrdinalIgnoreCase) >= 0)
                    return VisualSemanticRole.BuildingWindow;
                if (n.IndexOf("Trim", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
                    return VisualSemanticRole.BuildingTrim;
                if (n.IndexOf("Fabric", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Awning", StringComparison.OrdinalIgnoreCase) >= 0)
                    return VisualSemanticRole.BuildingAwning;
                if (n.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
                    return VisualSemanticRole.BuildingDoor;
                if (n.IndexOf("Wall", StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf("Facade", StringComparison.OrdinalIgnoreCase) >= 0)
                    return VisualSemanticRole.BuildingFacade;
            }
            return fallback;
        }

        private static bool IsSlotEligibleForDynamicTint(VisualPreviewSampleType sampleType, VisualSemanticRole role, string matName, int slotIndex)
        {
            if (sampleType == VisualPreviewSampleType.Character)
            {
                return role == VisualSemanticRole.PlayerBody || slotIndex == 3 ||
                       (!string.IsNullOrEmpty(matName) && matName.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0);
            }
            if (sampleType == VisualPreviewSampleType.ModularBuilding)
            {
                return role == VisualSemanticRole.BuildingFacade;
            }
            return false;
        }

        private static float ComputeIdealCameraDistance(Bounds b, VisualPreviewSampleType type)
        {
            float radius = Mathf.Max(0.35f, b.extents.magnitude);
            float mult = type == VisualPreviewSampleType.Pizzeria ? 1.55f : 1.75f;
            return Mathf.Clamp(radius * mult, 0.7f, 45f);
        }

        public void RebuildAllTransientMaterials(VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                RebuildSlotTransientMaterials(_slots[i], profile, ruleSet);
            }
        }

        public void RebuildSlotTransientMaterials(VisualPreviewSlotState slot, VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            if (slot == null) return;
            slot.DisposeTransientToonMaterials();
            if (profile == null) return;

            for (int i = 0; i < slot.targets.Count; i++)
            {
                var t = slot.targets[i];
                if (t == null || t.renderer == null) continue;

                if (ruleSet != null)
                {
                    t.decision = ruleSet.Evaluate(t.descriptor);
                }

                if (t.decision.action == VisualConversionAction.Preserve ||
                    t.decision.action == VisualConversionAction.Skip ||
                    t.decision.shaderFamily == IllustrateShaderFamily.PreserveOriginal)
                {
                    t.transientToonMaterial = null;
                    continue;
                }

                t.transientToonMaterial = CreateTransientPreviewMaterial(t.originalMaterial, t.decision, profile);
                if (t.transientToonMaterial != null)
                {
                    slot.transientToonMaterials.Add(t.transientToonMaterial);
                }
            }
        }

        private static Material CreateTransientPreviewMaterial(
            Material source,
            VisualClassificationDecision decision,
            VisualStyleProfile profile)
        {
            if (source == null || profile == null) return null;

            EffectiveCategorySettings cat = profile.GetEffectiveCategorySettings(decision.categoryId);
            IllustrateShaderFamily family = decision.shaderFamily;
            if (family == IllustrateShaderFamily.Opaque && cat.preferredShaderFamily == IllustrateShaderFamily.Simple)
            {
                family = IllustrateShaderFamily.Simple;
            }

            if (family == IllustrateShaderFamily.Opaque &&
                profile.outlines.outlineMode == VisualStyleOutlineMode.MaterialOutline &&
                cat.outlineMultiplier > 0.001f &&
                profile.outlines.outlineWidth > 0.0005f)
            {
                family = IllustrateShaderFamily.Outline;
            }

            string shaderName = VisualStyleShaderUtility.GetShaderName(family);
            Shader targetShader = Shader.Find(shaderName);
            if (targetShader == null) return null;

            var mat = new Material(targetShader)
            {
                name = $"Preview_{source.name}",
                hideFlags = HideFlags.HideAndDontSave
            };

            Color baseColor = VisualStyleShaderUtility.ExtractBaseColor(source, Color.white);
            Texture mainTex = null;
            Vector2 texScale = Vector2.one;
            Vector2 texOffset = Vector2.zero;
            if (decision.preserveTexture && cat.preserveTextures)
            {
                mainTex = VisualStyleShaderUtility.ExtractMainTexture(source, out texScale, out texOffset);
            }

            Texture normalMap = null;
            if (profile.materials.preserveNormalMaps)
            {
                if (source.HasProperty("_BumpMap")) normalMap = source.GetTexture("_BumpMap");
                else if (source.HasProperty("_NormalMap")) normalMap = source.GetTexture("_NormalMap");
            }

            float? alphaCutoff = null;
            if (source.HasProperty("_Cutoff")) alphaCutoff = source.GetFloat("_Cutoff");
            else if (source.HasProperty("_AlphaClipThreshold")) alphaCutoff = source.GetFloat("_AlphaClipThreshold");

            int? cullMode = null;
            if (source.HasProperty("_Cull")) cullMode = Mathf.RoundToInt(source.GetFloat("_Cull"));
            else if (source.HasProperty("_CullMode")) cullMode = Mathf.RoundToInt(source.GetFloat("_CullMode"));

            Texture emissionTex = null;
            Color? emissionCol = null;
            if (profile.materials.preserveEmission)
            {
                bool hadEmission = source.IsKeywordEnabled("_EMISSION") ||
                                   (source.HasProperty("_EmissionColor") && source.GetColor("_EmissionColor").maxColorComponent > 0.05f);
                if (hadEmission)
                {
                    if (source.HasProperty("_EmissionMap")) emissionTex = source.GetTexture("_EmissionMap");
                    else if (source.HasProperty("_EmissionTexture")) emissionTex = source.GetTexture("_EmissionTexture");
                    if (source.HasProperty("_EmissionColor")) emissionCol = source.GetColor("_EmissionColor");
                }
            }

            VisualStyleShaderUtility.ApplyProfileToMaterial(
                mat,
                profile,
                decision.categoryId,
                decision.semanticRole,
                baseColor,
                family,
                mainTex,
                profile.materials.preserveUVTilingOffset ? texScale : (Vector2?)null,
                profile.materials.preserveUVTilingOffset ? texOffset : (Vector2?)null,
                normalMap,
                alphaCutoff,
                cullMode,
                emissionTex,
                emissionCol);

            return mat;
        }

        public void RenderAllSlots(VisualStyleProfile profile)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                RenderSlot(_slots[i], profile);
            }
        }

        public void RenderSlot(VisualPreviewSlotState slot, VisualStyleProfile profile)
        {
            if (_disposed || slot == null) return;
            EnsurePreviewSceneCreated(slot);

            int size = slot.isExpanded ? Mathf.Min(768, Mathf.RoundToInt(_resolution * 1.5f)) : _resolution;
            EnsureSlotTextures(slot, size);
            ConfigureSlotLightingAndHelpers(slot, profile);
            ConfigureSlotCamera(slot);

            RenderTexture prevActive = RenderTexture.active;
            try
            {
                if (_comparisonMode == VisualPreviewComparisonMode.Original)
                {
                    ApplySlotRenderMode(slot, useToon: false);
                    RenderToTarget(slot.camera, slot.originalTexture);
                }
                else if (_comparisonMode == VisualPreviewComparisonMode.SideBySideAB ||
                         _comparisonMode == VisualPreviewComparisonMode.SplitAB)
                {
                    ApplySlotRenderMode(slot, useToon: false);
                    RenderToTarget(slot.camera, slot.originalTexture);

                    ApplySlotRenderMode(slot, useToon: true);
                    RenderToTarget(slot.camera, slot.toonTexture);
                }
                else
                {
                    ApplySlotRenderMode(slot, useToon: true);
                    RenderToTarget(slot.camera, slot.toonTexture);
                }
            }
            finally
            {
                slot.camera.targetTexture = null;
                RenderTexture.active = prevActive;
            }
        }

        public Texture2D CaptureSlotTextureCopy(VisualPreviewSlotState slot, bool captureToon)
        {
            if (slot == null) return null;
            RenderTexture src = captureToon ? slot.toonTexture : slot.originalTexture;
            if (src == null) return null;

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = src;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false)
            {
                name = $"Capture_{slot.slotId}_{(captureToon ? "Toon" : "Original")}",
                hideFlags = HideFlags.HideAndDontSave
            };
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static void RenderToTarget(Camera cam, RenderTexture rt)
        {
            if (cam == null || rt == null) return;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = null;
        }

        private static void EnsureSlotTextures(VisualPreviewSlotState slot, int size)
        {
            if (slot.toonTexture == null || slot.toonTexture.width != size || slot.toonTexture.height != size)
            {
                if (slot.toonTexture != null)
                {
                    slot.toonTexture.Release();
                    UnityEngine.Object.DestroyImmediate(slot.toonTexture);
                }
                slot.toonTexture = new RenderTexture(size, size, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default)
                {
                    name = $"RT_Preview_Toon_{slot.slotId}",
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1
                };
                slot.toonTexture.Create();
            }

            if (slot.originalTexture == null || slot.originalTexture.width != size || slot.originalTexture.height != size)
            {
                if (slot.originalTexture != null)
                {
                    slot.originalTexture.Release();
                    UnityEngine.Object.DestroyImmediate(slot.originalTexture);
                }
                slot.originalTexture = new RenderTexture(size, size, 24, RenderTextureFormat.Default, RenderTextureReadWrite.Default)
                {
                    name = $"RT_Preview_Original_{slot.slotId}",
                    hideFlags = HideFlags.HideAndDontSave,
                    antiAliasing = 1
                };
                slot.originalTexture.Create();
            }
        }

        private void ApplySlotRenderMode(VisualPreviewSlotState slot, bool useToon)
        {
            if (slot.subjectGo == null) return;

            var rendererMats = new Dictionary<Renderer, Material[]>();
            for (int i = 0; i < slot.targets.Count; i++)
            {
                var t = slot.targets[i];
                if (t == null || t.renderer == null) continue;
                if (!rendererMats.TryGetValue(t.renderer, out var arr))
                {
                    arr = t.renderer.sharedMaterials;
                    if (arr == null || arr.Length == 0) continue;
                    arr = (Material[])arr.Clone();
                    rendererMats[t.renderer] = arr;
                }

                if (t.slotIndex >= 0 && t.slotIndex < arr.Length)
                {
                    arr[t.slotIndex] = useToon && t.transientToonMaterial != null
                        ? t.transientToonMaterial
                        : t.originalMaterial;
                }
            }

            foreach (var kvp in rendererMats)
            {
                kvp.Key.sharedMaterials = kvp.Value;
            }

            // Re-apply per-slot dynamic MaterialPropertyBlock tint (Character Body / Modular Wall)
            for (int i = 0; i < slot.targets.Count; i++)
            {
                var t = slot.targets[i];
                if (t == null || t.renderer == null || !t.supportsDynamicTint) continue;

                if (slot.dynamicTintEnabled)
                {
                    Color[] palette = slot.sampleType == VisualPreviewSampleType.Character
                        ? CharacterSwatches
                        : BuildingSwatches;
                    Color tint = palette[Mathf.Abs(slot.swatchIndex) % palette.Length];
                    VisualStyleShaderUtility.ApplyColorBlockToSlot(t.renderer, t.slotIndex, tint, slot.mpbScratch);
                }
                else
                {
                    t.renderer.SetPropertyBlock(null, t.slotIndex);
                }
            }
        }

        private void ConfigureSlotLightingAndHelpers(VisualPreviewSlotState slot, VisualStyleProfile profile)
        {
            if (slot.gridGo != null)
            {
                slot.gridGo.SetActive(_showGrid);
            }

            if (slot.keyLight == null) return;

            Quaternion keyRot;
            float keyIntensity;
            Color keyColor;
            float fillIntensity;
            Color fillColor;
            bool enableInteriorPoints = false;

            switch (_lightingMode)
            {
                case VisualLightingPreviewMode.Exterior:
                    keyRot = Quaternion.Euler(48f, 35f, 0f);
                    keyIntensity = profile != null && profile.sceneLighting.overrideSceneLightingInVariant
                        ? profile.sceneLighting.directionalLightIntensity
                        : 1.45f;
                    keyColor = profile != null ? profile.sceneLighting.directionalLightColor : new Color(1f, 0.96f, 0.88f);
                    fillIntensity = 0.55f;
                    fillColor = profile != null ? profile.sceneLighting.ambientSkyColor : new Color(0.68f, 0.78f, 0.92f);
                    break;

                case VisualLightingPreviewMode.Neutral:
                    keyRot = Quaternion.Euler(35f, -45f, 0f);
                    keyIntensity = 1.15f;
                    keyColor = Color.white;
                    fillIntensity = 0.65f;
                    fillColor = new Color(0.84f, 0.86f, 0.90f);
                    break;

                case VisualLightingPreviewMode.Interior:
                default:
                    keyRot = Quaternion.Euler(58f, -28f, 0f);
                    keyIntensity = 1.1f;
                    keyColor = new Color(1f, 0.96f, 0.90f);
                    fillIntensity = 0.50f;
                    fillColor = new Color(0.75f, 0.78f, 0.86f);
                    enableInteriorPoints = profile == null || profile.sceneLighting.addInteriorFillLights;
                    break;
            }

            slot.keyLight.transform.rotation = keyRot;
            slot.keyLight.intensity = keyIntensity;
            slot.keyLight.color = keyColor;

            if (slot.fillLight != null)
            {
                slot.fillLight.transform.rotation = Quaternion.Euler(-25f, 145f, 0f);
                slot.fillLight.intensity = fillIntensity;
                slot.fillLight.color = fillColor;
            }

            if (slot.rimFillLight != null)
            {
                slot.rimFillLight.transform.rotation = Quaternion.Euler(15f, -155f, 0f);
                slot.rimFillLight.intensity = 0.35f;
                slot.rimFillLight.color = Color.white;
            }

            Vector3 bCenter = slot.subjectBounds.center;
            float bSpan = Mathf.Max(1.5f, slot.subjectBounds.extents.x);
            for (int i = 0; i < slot.interiorLights.Count; i++)
            {
                var pt = slot.interiorLights[i];
                if (pt == null) continue;
                pt.enabled = enableInteriorPoints;
                if (enableInteriorPoints)
                {
                    float offsetX = i == 0 ? -bSpan * 0.45f : bSpan * 0.45f;
                    pt.transform.position = bCenter + new Vector3(offsetX, Mathf.Max(1.5f, slot.subjectBounds.size.y * 0.7f), 0f);
                    pt.intensity = profile != null ? profile.sceneLighting.interiorFillIntensity : 1.4f;
                    pt.range = profile != null ? Mathf.Max(4f, profile.sceneLighting.interiorFillRange) : 10f;
                }
            }

            if (slot.lightGizmoGo != null)
            {
                slot.lightGizmoGo.SetActive(_showLightHandles);
                if (_showLightHandles)
                {
                    Vector3 lightDir = -(keyRot * Vector3.forward);
                    slot.lightGizmoGo.transform.position = bCenter + lightDir * Mathf.Max(1.8f, slot.distance * 0.65f);
                }
            }
        }

        private static void ConfigureSlotCamera(VisualPreviewSlotState slot)
        {
            if (slot.camera == null) return;

            float yaw = slot.yaw;
            float pitch = slot.pitch;
            bool ortho = false;

            if (slot.cameraMode == VisualPreviewCameraMode.Isometric)
            {
                ortho = true;
                pitch = Mathf.Clamp(slot.pitch, 25f, 45f);
            }
            else if (slot.cameraMode == VisualPreviewCameraMode.Orthographic)
            {
                ortho = true;
            }

            slot.camera.orthographic = ortho;
            slot.camera.fieldOfView = 38f;
            slot.camera.orthographicSize = Mathf.Max(0.45f, slot.distance * 0.44f);

            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 target = slot.subjectBounds.center + slot.panOffset;
            slot.camera.transform.position = target + rot * new Vector3(0f, 0f, -slot.distance);
            slot.camera.transform.LookAt(target, Vector3.up);
        }

        private static string GetRelativePath(Transform root, Transform target)
        {
            if (root == target) return target.name;
            var stack = new Stack<string>();
            Transform cur = target;
            while (cur != null && cur != root)
            {
                stack.Push(cur.name);
                cur = cur.parent;
            }
            return string.Join("/", stack);
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                _slots[i]?.Dispose();
            }
            _slots.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ClearAllSlots();
        }
    }
}
#endif
