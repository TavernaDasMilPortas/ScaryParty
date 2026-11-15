using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Lighting
{
    /// <summary>
    /// Pool reutilizavel de luzes locais SEM sombras com culling por distancia, frustum e histerese
    /// (~0.25s + banda de distancia) para evitar flickering e manter orcamento fixo na URP Forward+.
    /// </summary>
    public sealed class StylizedLightPool : MonoBehaviour
    {
        [Header("Orcamento do Pool")]
        [Range(0, 32)] public int maxPoolSize = 12;
        public float activateDistance = 38f;
        public float deactivateDistance = 44f; // histerese espacial de 6m
        public float toggleHysteresisSeconds = 0.25f;

        private readonly List<Light> _pooledLights = new List<Light>();
        private readonly Dictionary<LocalLightingAnchor, Light> _activeBindings = new Dictionary<LocalLightingAnchor, Light>();
        private readonly Dictionary<LocalLightingAnchor, float> _lastToggleTimes = new Dictionary<LocalLightingAnchor, float>();
        private readonly List<(LocalLightingAnchor anchor, float score)> _candidates = new List<(LocalLightingAnchor, float)>();
        private readonly Plane[] _frustumPlanes = new Plane[6];

        public int ActivePooledLightsCount => _activeBindings.Count;
        public int PoolCapacity => _pooledLights.Count;

        private void Awake()
        {
            EnsurePoolSize(maxPoolSize);
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        public void EnsurePoolSize(int size)
        {
            int target = Mathf.Clamp(size, 0, 32);
            while (_pooledLights.Count < target)
            {
                var go = new GameObject($"PooledStylizedLight_{_pooledLights.Count}");
                go.transform.SetParent(transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.shadows = LightShadows.None; // Estritamente proibido shadow casting em luzes de rua do pool
                l.renderMode = LightRenderMode.Auto;
                go.SetActive(false);
                _pooledLights.Add(l);
            }
        }

        public void ReleaseAll()
        {
            _activeBindings.Clear();
            _lastToggleTimes.Clear();
            for (int i = 0; i < _pooledLights.Count; i++)
            {
                if (_pooledLights[i] != null)
                {
                    _pooledLights[i].gameObject.SetActive(false);
                }
            }
        }

        public void UpdatePool(
            IReadOnlyList<LocalLightingAnchor> anchors,
            Camera cam,
            float emissionWeight,
            int budgetLimit,
            bool enableLights)
        {
            int limit = Mathf.Clamp(budgetLimit, 0, maxPoolSize);
            EnsurePoolSize(limit);

            if (!enableLights || limit == 0 || anchors == null || anchors.Count == 0 || emissionWeight <= 0.05f)
            {
                ReleaseAll();
                return;
            }

            Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
            bool useFrustum = cam != null;
            if (useFrustum)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
            }

            float now = Time.unscaledTime;
            _candidates.Clear();

            for (int i = 0; i < anchors.Count; i++)
            {
                LocalLightingAnchor a = anchors[i];
                if (a == null || !a.allowRealtimePooledLight || !a.gameObject.activeInHierarchy) continue;

                bool currentlyActive = _activeBindings.ContainsKey(a);
                float cutoffDist = currentlyActive ? deactivateDistance : activateDistance;
                Vector3 pos = a.transform.position;
                float distSq = (pos - camPos).sqrMagnitude;

                bool inRange = distSq <= cutoffDist * cutoffDist;
                bool inFrustum = !useFrustum || GeometryUtility.TestPlanesAABB(_frustumPlanes, new Bounds(pos, Vector3.one * (a.range * 1.15f)));

                // Histerese temporal: evita piscar nas bordas
                if (!inRange || !inFrustum)
                {
                    if (currentlyActive && CanToggle(a, now))
                    {
                        UnbindAnchor(a, now);
                    }
                    continue;
                }

                float score = distSq - a.priority * 120f;
                _candidates.Add((a, score));
            }

            _candidates.Sort((x, y) => x.score.CompareTo(y.score));

            // Remove extras fora dos top `limit`
            var desiredSet = new HashSet<LocalLightingAnchor>();
            int desiredCount = Mathf.Min(limit, _candidates.Count);
            for (int i = 0; i < desiredCount; i++)
            {
                desiredSet.Add(_candidates[i].anchor);
            }

            var toRemove = new List<LocalLightingAnchor>();
            foreach (var kvp in _activeBindings)
            {
                if (!desiredSet.Contains(kvp.Key) && CanToggle(kvp.Key, now))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                UnbindAnchor(toRemove[i], now);
            }

            // Aloca ancoras desejadas dentro do limite do pool
            for (int i = 0; i < desiredCount; i++)
            {
                if (_activeBindings.Count >= limit) break;
                LocalLightingAnchor a = _candidates[i].anchor;
                if (_activeBindings.TryGetValue(a, out Light activeLight))
                {
                    ApplyAnchorToLight(a, activeLight, emissionWeight);
                    continue;
                }

                if (!CanToggle(a, now)) continue;
                Light freeLight = FindFreePooledLight(limit);
                if (freeLight == null) break;

                ApplyAnchorToLight(a, freeLight, emissionWeight);
                freeLight.gameObject.SetActive(true);
                _activeBindings[a] = freeLight;
                _lastToggleTimes[a] = now;
            }
        }

        private bool CanToggle(LocalLightingAnchor anchor, float now)
        {
            return !_lastToggleTimes.TryGetValue(anchor, out float last) || (now - last) >= toggleHysteresisSeconds;
        }

        private void UnbindAnchor(LocalLightingAnchor anchor, float now)
        {
            if (_activeBindings.TryGetValue(anchor, out Light l) && l != null)
            {
                l.gameObject.SetActive(false);
            }
            _activeBindings.Remove(anchor);
            _lastToggleTimes[anchor] = now;
        }

        private Light FindFreePooledLight(int limit)
        {
            for (int i = 0; i < limit && i < _pooledLights.Count; i++)
            {
                if (_pooledLights[i] != null && !_pooledLights[i].gameObject.activeSelf)
                {
                    return _pooledLights[i];
                }
            }
            return null;
        }

        private static void ApplyAnchorToLight(LocalLightingAnchor anchor, Light l, float emissionWeight)
        {
            l.transform.position = anchor.transform.position;
            l.color = anchor.lightColor;
            l.intensity = anchor.baseIntensity * (anchor.nightOnly ? emissionWeight : 1f);
            l.range = anchor.range;
            l.shadows = LightShadows.None;
        }
    }
}
