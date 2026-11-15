using System;
using System.Collections.Generic;
using ScaryParty.City;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.VisualStyles;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Orquestra iluminacao deterministica noturna da cidade procedural (vitrines, janelas por MPB e pool de
    /// luzes sem sombra com culling) sincronizada aos eventos OnCityGenerated/OnCityCleared e TimeOfDay.
    /// </summary>
    [DefaultExecutionOrder(275)]
    public sealed class CityLightingDirector : MonoBehaviour
    {
        public static CityLightingDirector Active { get; private set; }

        private struct TrackedModuleTarget
        {
            public Renderer renderer;
            public int slotIndex;
            public CityModuleId moduleId;
            public bool isWindowGlassSlot;
            public float litFactor;
            public Color emissionColor;
        }

        [Header("Referencia e Orcamento")]
        public CityGenerator cityGenerator;
        public LightingQualityBudget qualityBudget;
        public StylizedLightPool lightPool;

        private readonly List<TrackedModuleTarget> _trackedTargets = new List<TrackedModuleTarget>();
        private readonly List<LocalLightingAnchor> _anchors = new List<LocalLightingAnchor>();
        private readonly List<GameObject> _runtimeAnchorObjects = new List<GameObject>();
        private MaterialPropertyBlock _mpb;
        private TimeOfDayLightingDirector _subscribedTod;
        private VisualStyleManager _subscribedMgr;
        private float _nextPoolUpdateTime;
        private float _lastAppliedEmissionWeight = -1f;

        public int TrackedWindowTargetsCount => _trackedTargets.Count;
        public int RegisteredAnchorsCount => _anchors.Count;
        public int ActivePooledLightsCount => lightPool != null ? lightPool.ActivePooledLightsCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterBootstrapHook()
        {
            VisualStyleGameplayBootstrap.OnSceneLightingBootstrapped -= HandleSceneLightingBootstrapped;
            VisualStyleGameplayBootstrap.OnSceneLightingBootstrapped += HandleSceneLightingBootstrapped;
        }

        private static void HandleSceneLightingBootstrapped(Scene scene, VisualStyleManager mgr, VisualStyleRuntimeConfig cfg)
        {
            if (!scene.IsValid() || !scene.isLoaded) return;

            CityGenerator gen = FindAnyObjectByType<CityGenerator>(FindObjectsInactive.Exclude);
            if (gen != null)
            {
                CityLightingDirector director = FindAnyObjectByType<CityLightingDirector>(FindObjectsInactive.Include);
                if (director == null)
                {
                    var go = new GameObject("[CityLightingDirector]");
                    go.hideFlags = HideFlags.DontSave;
                    SceneManager.MoveGameObjectToScene(go, scene);
                    director = go.AddComponent<CityLightingDirector>();
                }
                director.BindCityGenerator(gen);
            }

            PizzeriaRoot pizzeria = FindAnyObjectByType<PizzeriaRoot>(FindObjectsInactive.Include);
            if (pizzeria != null && pizzeria.GetComponent<PizzeriaLightingController>() == null)
            {
                pizzeria.gameObject.AddComponent<PizzeriaLightingController>();
            }
        }

        private void Awake()
        {
            Active = this;
            _mpb = new MaterialPropertyBlock();
            EnsureLightPool();
            if (qualityBudget == null)
            {
                qualityBudget = Resources.Load<LightingQualityBudget>("LightingQualityBudget_Default");
            }
        }

        private void OnEnable()
        {
            Active = this;
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            EnsureLightPool();
            if (cityGenerator == null)
            {
                cityGenerator = FindAnyObjectByType<CityGenerator>(FindObjectsInactive.Exclude);
            }
            SubscribeEvents();
            if (cityGenerator != null && cityGenerator.CityData != null)
            {
                RebuildCityLightingTargets();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            ClearTrackedTargets();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            ClearTrackedTargets();
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            if (Time.unscaledTime < _nextPoolUpdateTime) return;
            _nextPoolUpdateTime = Time.unscaledTime + 0.10f;

            UpdatePooledLightsAndTargets(forceMpbRefresh: false);
        }

        public void BindCityGenerator(CityGenerator gen)
        {
            if (cityGenerator == gen)
            {
                if (_trackedTargets.Count == 0 && gen != null && gen.CityData != null)
                {
                    RebuildCityLightingTargets();
                }
                return;
            }

            UnsubscribeCityGenerator();
            cityGenerator = gen;
            SubscribeCityGenerator();

            if (cityGenerator != null && cityGenerator.CityData != null)
            {
                RebuildCityLightingTargets();
            }
        }

        public static uint ComputeFnv1aHash(int citySeed, int blockId, int buildingHash, int floorIndex, int slotIndex, int moduleType)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Mix(h, (uint)citySeed);
                h = Mix(h, (uint)blockId);
                h = Mix(h, (uint)buildingHash);
                h = Mix(h, (uint)floorIndex);
                h = Mix(h, (uint)slotIndex);
                h = Mix(h, (uint)moduleType);
                return h;
            }
        }

        private static uint Mix(uint hash, uint data)
        {
            unchecked
            {
                hash ^= (data & 0xFFu); hash *= 16777619u;
                hash ^= ((data >> 8) & 0xFFu); hash *= 16777619u;
                hash ^= ((data >> 16) & 0xFFu); hash *= 16777619u;
                hash ^= ((data >> 24) & 0xFFu); hash *= 16777619u;
                return hash;
            }
        }

        public static bool ComputeWindowLitState(
            int citySeed,
            int blockId,
            int buildingHash,
            int floorIndex,
            int slotIndex,
            CityModuleId moduleId,
            out float litIntensity,
            out Color emissionColor)
        {
            litIntensity = 0f;
            emissionColor = new Color(1f, 0.85f, 0.56f, 1f);

            bool isShopfrontOrEntry = moduleId == CityModuleId.D || moduleId == CityModuleId.J || moduleId == CityModuleId.C;
            bool isWindow = moduleId == CityModuleId.B || moduleId == CityModuleId.G || moduleId == CityModuleId.H;
            if (!isShopfrontOrEntry && !isWindow) return false;

            uint h = ComputeFnv1aHash(citySeed, blockId, buildingHash, floorIndex, slotIndex, (int)moduleId);
            float roll = (h % 1000u) / 1000f;

            if (isShopfrontOrEntry)
            {
                if (floorIndex == 0 && roll < 0.86f)
                {
                    litIntensity = 1.65f;
                    emissionColor = new Color(1.0f, 0.88f, 0.62f, 1f);
                    return true;
                }
                return false;
            }

            if (roll < 0.58f)
            {
                uint variant = (h >> 12) % 4u;
                litIntensity = 1.15f + (roll * 0.45f);
                emissionColor = variant == 3u
                    ? new Color(0.68f, 0.84f, 1.0f, 1f) // Luz sutil azulada residencial/TV
                    : new Color(1.0f, 0.84f, 0.54f, 1f);
                return true;
            }
            return false;
        }

        public void RebuildCityLightingTargets()
        {
            ClearTrackedTargets();
            if (cityGenerator == null) return;

            int citySeed = cityGenerator.CurrentSeed;
            CityBuilding[] buildings = cityGenerator.GetComponentsInChildren<CityBuilding>(true);

            for (int b = 0; b < buildings.Length; b++)
            {
                CityBuilding building = buildings[b];
                if (building == null) continue;
                int bHash = !string.IsNullOrEmpty(building.buildingId) ? building.buildingId.GetHashCode() : b;

                CityModuleInstance[] modules = building.GetComponentsInChildren<CityModuleInstance>(true);
                for (int m = 0; m < modules.Length; m++)
                {
                    CityModuleInstance mod = modules[m];
                    if (mod == null) continue;

                    bool lit = ComputeWindowLitState(
                        citySeed,
                        building.blockId,
                        bHash,
                        mod.floorIndex,
                        mod.slotIndex,
                        mod.moduleId,
                        out float litFactor,
                        out Color emCol);

                    if (!lit) continue;

                    RegisterModuleRenderers(mod, litFactor, emCol);

                    // Cria ancora de rua para vitrines e entradas principais terreas sem exceder pool
                    if (mod.floorIndex == 0 && (mod.moduleId == CityModuleId.D || mod.moduleId == CityModuleId.J))
                    {
                        CreateAnchorForModule(mod, emCol, litFactor);
                    }
                }
            }

            _lastAppliedEmissionWeight = -1f;
            UpdatePooledLightsAndTargets(forceMpbRefresh: true);
        }

        private void RegisterModuleRenderers(CityModuleInstance mod, float litFactor, Color emCol)
        {
            Renderer[] renderers = mod.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer rend = renderers[r];
                if (rend == null) continue;

                // B1 Metadata Hook se presente no modulo
                VisualStyleIdentifier id = rend.GetComponent<VisualStyleIdentifier>();
                if (id != null && (id.bakedAoTexture != null || id.useVertexColorAo || id.emissionMask != null))
                {
                    ApplyB1HooksToRenderer(rend, id);
                }

                Material[] mats = rend.sharedMaterials;
                if (mats == null || mats.Length == 0) continue;

                for (int s = 0; s < mats.Length; s++)
                {
                    Material mat = mats[s];
                    string matName = mat != null ? mat.name.ToLowerInvariant() : string.Empty;
                    string objName = rend.name.ToLowerInvariant();
                    bool isWindowGlass = matName.Contains("glass") || matName.Contains("vidro") ||
                                         matName.Contains("window") || matName.Contains("janela") ||
                                         matName.Contains("vitrine") || objName.Contains("glass") ||
                                         objName.Contains("vidro") || objName.Contains("vitrine");

                    if (!isWindowGlass && (id == null || id.emissionMask == null))
                    {
                        continue;
                    }

                    _trackedTargets.Add(new TrackedModuleTarget
                    {
                        renderer = rend,
                        slotIndex = s,
                        moduleId = mod.moduleId,
                        isWindowGlassSlot = isWindowGlass,
                        litFactor = litFactor,
                        emissionColor = emCol
                    });
                }
            }
        }

        private void ApplyB1HooksToRenderer(Renderer rend, VisualStyleIdentifier id)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            _mpb.Clear();
            rend.GetPropertyBlock(_mpb, 0);
            if (id.bakedAoTexture != null) _mpb.SetTexture(LightingShaderGlobals.AoTextureId, id.bakedAoTexture);
            _mpb.SetFloat(LightingShaderGlobals.AoStrengthId, id.aoStrength);
            _mpb.SetFloat(LightingShaderGlobals.UseVertexAoId, id.useVertexColorAo ? 1f : 0f);
            if (id.emissionMask != null) _mpb.SetTexture(LightingShaderGlobals.EmissionMaskId, id.emissionMask);
            rend.SetPropertyBlock(_mpb, 0);
        }

        private void CreateAnchorForModule(CityModuleInstance mod, Color emCol, float litFactor)
        {
            var anchorGo = new GameObject($"StreetAnchor_{mod.buildingId}_{mod.slotIndex}");
            anchorGo.hideFlags = HideFlags.DontSave;
            anchorGo.transform.SetParent(mod.transform, false);
            anchorGo.transform.localPosition = new Vector3(0f, 2.2f, 1.35f);

            var anchor = anchorGo.AddComponent<LocalLightingAnchor>();
            anchor.anchorId = anchorGo.name;
            anchor.lightColor = emCol;
            anchor.baseIntensity = Mathf.Clamp(litFactor, 0.8f, 1.8f);
            anchor.range = mod.moduleId == CityModuleId.D ? 9.5f : 7.5f;
            anchor.priority = mod.moduleId == CityModuleId.D ? 2 : 1;

            _anchors.Add(anchor);
            _runtimeAnchorObjects.Add(anchorGo);
        }

        private void ClearTrackedTargets()
        {
            _trackedTargets.Clear();
            _anchors.Clear();
            for (int i = 0; i < _runtimeAnchorObjects.Count; i++)
            {
                if (_runtimeAnchorObjects[i] != null)
                {
                    Destroy(_runtimeAnchorObjects[i]);
                }
            }
            _runtimeAnchorObjects.Clear();
            lightPool?.ReleaseAll();
        }

        private void UpdatePooledLightsAndTargets(bool forceMpbRefresh)
        {
            bool styled = VisualStyleRuntimeConfig.IsStyledGameplayActive() ||
                          (VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null);

            float emissionWeight = styled && TimeOfDayLightingDirector.Active != null
                ? TimeOfDayLightingDirector.Active.GetCityNightEmissionWeight()
                : 0f;

            if (forceMpbRefresh || Mathf.Abs(emissionWeight - _lastAppliedEmissionWeight) > 0.02f)
            {
                _lastAppliedEmissionWeight = emissionWeight;
                ApplyWindowEmissionWeights(emissionWeight);
            }

            if (lightPool != null)
            {
                LightingTierSpec spec = qualityBudget != null ? qualityBudget.GetActiveSpec() : null;
                int limit = spec != null ? spec.maxPooledStreetLights : 8;
                lightPool.activateDistance = spec != null ? spec.cityLightCullDistance : 38f;
                lightPool.deactivateDistance = lightPool.activateDistance + 6f;

                lightPool.UpdatePool(_anchors, Camera.main, emissionWeight, limit, styled);
            }
        }

        private void ApplyWindowEmissionWeights(float emissionWeight)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();

            for (int i = 0; i < _trackedTargets.Count; i++)
            {
                TrackedModuleTarget target = _trackedTargets[i];
                if (target.renderer == null) continue;

                _mpb.Clear();
                target.renderer.GetPropertyBlock(_mpb, target.slotIndex);

                // Preserva integralmente _MainColor/_BaseColor existentes da parede/vidro; altera apenas emissao
                float strength = target.litFactor * emissionWeight;
                _mpb.SetColor(LightingShaderGlobals.EmissionMaskColorId, target.emissionColor);
                _mpb.SetFloat(LightingShaderGlobals.EmissionMaskStrengthId, strength);
                _mpb.SetColor(VisualStyleShaderUtility.EmissionColorId, target.emissionColor * (target.isWindowGlassSlot ? strength * 0.85f : 0f));

                target.renderer.SetPropertyBlock(_mpb, target.slotIndex);
            }
        }

        private void EnsureLightPool()
        {
            if (lightPool != null) return;
            lightPool = GetComponent<StylizedLightPool>();
            if (lightPool == null)
            {
                lightPool = gameObject.AddComponent<StylizedLightPool>();
            }
        }

        private void SubscribeEvents()
        {
            SubscribeCityGenerator();

            if (TimeOfDayLightingDirector.Active != null && _subscribedTod != TimeOfDayLightingDirector.Active)
            {
                if (_subscribedTod != null) _subscribedTod.OnTimeOfDayEvaluated -= HandleTimeOfDayChanged;
                _subscribedTod = TimeOfDayLightingDirector.Active;
                _subscribedTod.OnTimeOfDayEvaluated += HandleTimeOfDayChanged;
            }

            if (VisualStyleManager.Active != null && _subscribedMgr != VisualStyleManager.Active)
            {
                if (_subscribedMgr != null) _subscribedMgr.OnStyleChanged -= HandleStyleChanged;
                _subscribedMgr = VisualStyleManager.Active;
                _subscribedMgr.OnStyleChanged += HandleStyleChanged;
            }

            VisualStyleManager.OnActiveManagerChanged -= HandleActiveManagerChanged;
            VisualStyleManager.OnActiveManagerChanged += HandleActiveManagerChanged;
        }

        private void UnsubscribeEvents()
        {
            UnsubscribeCityGenerator();
            VisualStyleManager.OnActiveManagerChanged -= HandleActiveManagerChanged;
            if (_subscribedTod != null)
            {
                _subscribedTod.OnTimeOfDayEvaluated -= HandleTimeOfDayChanged;
                _subscribedTod = null;
            }
            if (_subscribedMgr != null)
            {
                _subscribedMgr.OnStyleChanged -= HandleStyleChanged;
                _subscribedMgr = null;
            }
        }

        private void SubscribeCityGenerator()
        {
            if (cityGenerator == null) return;
            cityGenerator.OnCityGenerated -= HandleCityGenerated;
            cityGenerator.OnCityGenerated += HandleCityGenerated;
            cityGenerator.OnCityCleared -= HandleCityCleared;
            cityGenerator.OnCityCleared += HandleCityCleared;
        }

        private void UnsubscribeCityGenerator()
        {
            if (cityGenerator == null) return;
            cityGenerator.OnCityGenerated -= HandleCityGenerated;
            cityGenerator.OnCityCleared -= HandleCityCleared;
        }

        private void HandleCityGenerated()
        {
            RebuildCityLightingTargets();
        }

        private void HandleCityCleared()
        {
            ClearTrackedTargets();
        }

        private void HandleTimeOfDayChanged(LightingEnvironmentState state)
        {
            UpdatePooledLightsAndTargets(forceMpbRefresh: false);
        }

        private void HandleStyleChanged(VisualStyleManager mgr)
        {
            SubscribeEvents();
            UpdatePooledLightsAndTargets(forceMpbRefresh: true);
        }

        private void HandleActiveManagerChanged(VisualStyleManager mgr)
        {
            SubscribeEvents();
            UpdatePooledLightsAndTargets(forceMpbRefresh: true);
        }
    }
}
