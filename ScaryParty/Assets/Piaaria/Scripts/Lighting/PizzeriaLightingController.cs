using System;
using System.Collections.Generic;
using ScaryParty.VisualStyles;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Controla a iluminacao interna da pizzaria por zonas (Cozinha, Balcao, Salao),
    /// elimina Point Lights com sombra para evitar downgrade do atlas 2048 e fornece preenchimento
    /// estilizado via MPB sem sobrescrever cores de ingredientes, pizzas ou NPCs.
    /// Tambem consome dados pre-calculados B2 se validos, mantendo fallback seguro para A6.
    /// </summary>
    [DefaultExecutionOrder(270)]
    public sealed class PizzeriaLightingController : MonoBehaviour
    {
        public static PizzeriaLightingController Active { get; private set; }

        [Header("Dados B2 Opcionais (Futuro)")]
        public FixedInteriorLightingData fixedDataB2;
        public string runtimeLayoutFingerprint = "L_shape_kitchen_counter_dining_v1";

        [Header("Zonas Internas (A6)")]
        public List<InteriorZonePreset> zonePresets = new List<InteriorZonePreset>();
        [Range(0.2f, 2f)] public float maxShadowedInteriorSpotLights = 1;

        private readonly Dictionary<Light, LightShadows> _originalLightShadows = new Dictionary<Light, LightShadows>();
        private readonly Dictionary<Light, float> _originalLightIntensities = new Dictionary<Light, float>();
        private MaterialPropertyBlock _sharedMpb;
        private TimeOfDayLightingDirector _subscribedTod;
        private VisualStyleManager _subscribedMgr;
        private bool _appliedRealtimeOverrides;

        public string ActiveFallbackReason { get; private set; }
        public bool IsUsingPrecomputedB2 { get; private set; }

        private void Awake()
        {
            Active = this;
            _sharedMpb = new MaterialPropertyBlock();
            EnsureDefaultZonePresets();
        }

        private void OnEnable()
        {
            Active = this;
            if (_sharedMpb == null) _sharedMpb = new MaterialPropertyBlock();
            EnsureDefaultZonePresets();
            SubscribeEvents();
            RefreshInteriorLighting();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            RestoreInteriorLights();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            RestoreInteriorLights();
            if (Active == this) Active = null;
        }

        public void EnsureDefaultZonePresets()
        {
            if (zonePresets != null && zonePresets.Count > 0) return;

            zonePresets = new List<InteriorZonePreset>
            {
                new InteriorZonePreset
                {
                    zoneId = PizzeriaZoneId.Kitchen,
                    localCenter = new Vector3(-7.5f, 2.0f, 3.5f),
                    localSize = new Vector3(13f, 5.5f, 13f),
                    fillColor = new Color(1.00f, 0.96f, 0.90f, 1f),
                    fillWeight = 0.28f,
                    localPointLightTint = new Color(1f, 0.97f, 0.92f, 1f),
                    intensityMultiplier = 1.08f
                },
                new InteriorZonePreset
                {
                    zoneId = PizzeriaZoneId.Counter,
                    localCenter = new Vector3(0.0f, 2.0f, 0.0f),
                    localSize = new Vector3(10f, 5.5f, 10f),
                    fillColor = new Color(1.00f, 0.93f, 0.80f, 1f),
                    fillWeight = 0.26f,
                    localPointLightTint = new Color(1f, 0.94f, 0.82f, 1f),
                    intensityMultiplier = 1.05f
                },
                new InteriorZonePreset
                {
                    zoneId = PizzeriaZoneId.Dining,
                    localCenter = new Vector3(7.5f, 2.0f, -3.5f),
                    localSize = new Vector3(14f, 5.5f, 14f),
                    fillColor = new Color(1.00f, 0.89f, 0.74f, 1f),
                    fillWeight = 0.24f,
                    localPointLightTint = new Color(1f, 0.90f, 0.76f, 1f),
                    intensityMultiplier = 1.00f
                }
            };
        }

        public void RefreshInteriorLighting()
        {
            EnsureDefaultZonePresets();

            bool styled = VisualStyleRuntimeConfig.IsStyledGameplayActive() ||
                          (VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null);

            if (!styled)
            {
                if (_appliedRealtimeOverrides)
                {
                    RestoreInteriorLights();
                    ClearInteriorFillOnHierarchy(gameObject);
                    _appliedRealtimeOverrides = false;
                }
                return;
            }

            EvaluateB2OrFallback();
            SanitizeInteriorLights();
            ApplyZoneFillToHierarchy(gameObject);
            _appliedRealtimeOverrides = true;
        }

        public void SanitizeInteriorLights()
        {
            Light[] lights = GetComponentsInChildren<Light>(true);
            int allowedSpotShadows = Mathf.Max(0, Mathf.RoundToInt(maxShadowedInteriorSpotLights));
            int spotShadowCount = 0;

            float todMultiplier = TimeOfDayLightingDirector.Active != null
                ? TimeOfDayLightingDirector.Active.GetInteriorFillMultiplier()
                : 1.0f;

            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null || l.type == LightType.Directional) continue;

                if (!_originalLightShadows.ContainsKey(l))
                {
                    _originalLightShadows[l] = l.shadows;
                    _originalLightIntensities[l] = l.intensity;
                }

                // Point Lights nunca usam sombra cubemap na Solucao A (evita estourar 6 slices x N luzes no atlas 2048).
                if (l.type == LightType.Point)
                {
                    l.shadows = LightShadows.None;
                }
                else if (l.type == LightType.Spot)
                {
                    if (_originalLightShadows[l] != LightShadows.None && spotShadowCount < allowedSpotShadows)
                    {
                        l.shadows = _originalLightShadows[l];
                        spotShadowCount++;
                    }
                    else
                    {
                        l.shadows = LightShadows.None;
                    }
                }

                float baseIntensity = _originalLightIntensities[l];
                InteriorZonePreset preset = ResolvePresetForWorldPosition(l.transform.position);
                float zoneMult = preset != null ? preset.intensityMultiplier : 1.0f;
                l.intensity = baseIntensity * todMultiplier * zoneMult;
            }
        }

        public void ApplyZoneFillToHierarchy(GameObject root)
        {
            if (root == null) return;
            if (_sharedMpb == null) _sharedMpb = new MaterialPropertyBlock();

            bool styled = VisualStyleRuntimeConfig.IsStyledGameplayActive() ||
                          (VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null);
            if (!styled) return;

            float styleBoost = VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null
                ? VisualStyleManager.Active.activeProfile.sceneLighting.interiorAmbientBoost
                : 0.22f;
            float todMult = TimeOfDayLightingDirector.Active != null
                ? TimeOfDayLightingDirector.Active.GetInteriorFillMultiplier()
                : 1.0f;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                ResolveFillForPosition(r.transform.position, styleBoost * todMult, out Color fillCol, out float fillWeight);
                WriteInteriorMpbPreservingColors(r, fillCol, fillWeight);
            }
        }

        public bool ResolveFillForPosition(Vector3 worldPos, float globalScale, out Color fillColor, out float fillWeight)
        {
            // Prioriza componentes explicitos InteriorLightingZone colocados no prefab/cena
            InteriorLightingZone[] explicitZones = GetComponentsInChildren<InteriorLightingZone>(true);
            for (int i = 0; i < explicitZones.Length; i++)
            {
                InteriorLightingZone z = explicitZones[i];
                if (z != null && z.WorldBounds.Contains(worldPos))
                {
                    fillColor = z.fillColor;
                    fillWeight = Mathf.Clamp01(z.fillWeight * (0.65f + globalScale));
                    return true;
                }
            }

            InteriorZonePreset preset = ResolvePresetForWorldPosition(worldPos);
            if (preset != null)
            {
                fillColor = preset.fillColor;
                fillWeight = Mathf.Clamp01(preset.fillWeight * (0.65f + globalScale));

                if (IsUsingPrecomputedB2 && fixedDataB2 != null && fixedDataB2.bakedZones != null)
                {
                    for (int i = 0; i < fixedDataB2.bakedZones.Count; i++)
                    {
                        if (fixedDataB2.bakedZones[i].zoneId == preset.zoneId)
                        {
                            fillColor = Color.Lerp(fillColor, fixedDataB2.bakedZones[i].averageBounceColor, 0.6f);
                            fillWeight = Mathf.Max(fillWeight, fixedDataB2.bakedZones[i].bounceStrength);
                            break;
                        }
                    }
                }
                return true;
            }

            fillColor = new Color(1f, 0.94f, 0.82f, 1f);
            fillWeight = Mathf.Clamp01(0.20f * (0.65f + globalScale));
            return false;
        }

        private InteriorZonePreset ResolvePresetForWorldPosition(Vector3 worldPos)
        {
            if (zonePresets == null || zonePresets.Count == 0) return null;
            for (int i = 0; i < zonePresets.Count; i++)
            {
                if (zonePresets[i].ComputeWorldBounds(transform).Contains(worldPos))
                {
                    return zonePresets[i];
                }
            }

            // Mais proximo por centro local caso esteja nas bordas/paredes externas
            InteriorZonePreset closest = zonePresets[0];
            float bestDistSq = float.MaxValue;
            for (int i = 0; i < zonePresets.Count; i++)
            {
                float dSq = (transform.TransformPoint(zonePresets[i].localCenter) - worldPos).sqrMagnitude;
                if (dSq < bestDistSq)
                {
                    bestDistSq = dSq;
                    closest = zonePresets[i];
                }
            }
            return closest;
        }

        private void WriteInteriorMpbPreservingColors(Renderer renderer, Color fillColor, float fillWeight)
        {
            Material[] mats = renderer.sharedMaterials;
            int slotCount = mats != null && mats.Length > 0 ? mats.Length : 1;
            for (int s = 0; s < slotCount; s++)
            {
                _sharedMpb.Clear();
                renderer.GetPropertyBlock(_sharedMpb, s);
                _sharedMpb.SetColor(LightingShaderGlobals.InteriorZoneFillColorId, fillColor);
                _sharedMpb.SetFloat(LightingShaderGlobals.InteriorZoneWeightId, fillWeight);
                renderer.SetPropertyBlock(_sharedMpb, s);
            }
        }

        private void ClearInteriorFillOnHierarchy(GameObject root)
        {
            if (root == null) return;
            if (_sharedMpb == null) _sharedMpb = new MaterialPropertyBlock();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                int slotCount = mats != null && mats.Length > 0 ? mats.Length : 1;
                for (int s = 0; s < slotCount; s++)
                {
                    _sharedMpb.Clear();
                    r.GetPropertyBlock(_sharedMpb, s);
                    _sharedMpb.SetFloat(LightingShaderGlobals.InteriorZoneWeightId, 0f);
                    r.SetPropertyBlock(_sharedMpb, s);
                }
            }
        }

        private void RestoreInteriorLights()
        {
            foreach (var kvp in _originalLightShadows)
            {
                if (kvp.Key != null) kvp.Key.shadows = kvp.Value;
            }
            foreach (var kvp in _originalLightIntensities)
            {
                if (kvp.Key != null) kvp.Key.intensity = kvp.Value;
            }
        }

        private void EvaluateB2OrFallback()
        {
            IsUsingPrecomputedB2 = false;
            ActiveFallbackReason = string.Empty;

            LightingBackendMode backend = VisualStyleRuntimeConfig.LoadCached()?.lightingBackendMode ?? LightingBackendMode.RealtimeStylized;
            if (backend == LightingBackendMode.RealtimeStylized)
            {
                ActiveFallbackReason = "Modo RealtimeStylized (A6) ativo.";
                return;
            }

            if (fixedDataB2 == null)
            {
                ActiveFallbackReason = "FixedInteriorLightingData nao atribuido; mantendo A6 em tempo real.";
                return;
            }

            if (fixedDataB2.ValidateAgainstLayout(runtimeLayoutFingerprint, transform, out string invalidReason))
            {
                IsUsingPrecomputedB2 = true;
            }
            else
            {
                ActiveFallbackReason = invalidReason;
            }
        }

        private void SubscribeEvents()
        {
            if (TimeOfDayLightingDirector.Active != null && _subscribedTod != TimeOfDayLightingDirector.Active)
            {
                if (_subscribedTod != null) _subscribedTod.OnTimeOfDayEvaluated -= HandleTimeOfDayEvaluated;
                _subscribedTod = TimeOfDayLightingDirector.Active;
                _subscribedTod.OnTimeOfDayEvaluated += HandleTimeOfDayEvaluated;
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
            VisualStyleManager.OnActiveManagerChanged -= HandleActiveManagerChanged;
            if (_subscribedTod != null)
            {
                _subscribedTod.OnTimeOfDayEvaluated -= HandleTimeOfDayEvaluated;
                _subscribedTod = null;
            }
            if (_subscribedMgr != null)
            {
                _subscribedMgr.OnStyleChanged -= HandleStyleChanged;
                _subscribedMgr = null;
            }
        }

        private void HandleActiveManagerChanged(VisualStyleManager mgr)
        {
            SubscribeEvents();
            RefreshInteriorLighting();
        }

        private void HandleStyleChanged(VisualStyleManager mgr)
        {
            RefreshInteriorLighting();
        }

        private void HandleTimeOfDayEvaluated(LightingEnvironmentState state)
        {
            RefreshInteriorLighting();
        }
    }
}
