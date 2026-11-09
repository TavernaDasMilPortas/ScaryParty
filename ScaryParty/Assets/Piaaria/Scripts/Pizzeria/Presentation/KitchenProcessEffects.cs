using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Presentation
{
    [DefaultExecutionOrder(160)]
    [RequireComponent(typeof(PizzeriaItemVisualizer))]
    public class KitchenProcessEffects : MonoBehaviour
    {
        [Header("VFX (world scale, independent of station)")]
        [Min(0.01f)] public float effectScale = 1f;
        [SerializeField] private VisualEffectAsset originalMess;
        [SerializeField] private VisualEffectAsset originalSmoke;
        [Min(0.05f)] public float transitionDuration = 0.5f;
        [Min(0.01f)] public float completionTransitionDuration = 0.1f;

    #if UNITY_EDITOR
        private void OnValidate()
        {
            if (originalMess == null)
            originalMess = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Piaaria/VFX/VFX_Mess.vfx");
            if (originalSmoke == null)
            originalSmoke = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Piaaria/VFX/VFX_Smoke.vfx");
        }
    #endif

        public GameObject CreateEffectPreview(Vector3 position)
        {
            LoadAssets();
            var root = new GameObject("Kitchen VFX Preview");
            root.transform.position = position;
            root.transform.localScale = Vector3.one * Mathf.Max(0.01f, effectScale);
            AddEffect(root, _messAsset);
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(root.transform, false);
            AddEffect(smoke, _smokeAsset);
            return root;
        }

        private static void AddEffect(GameObject target, VisualEffectAsset asset)
        {
            if (asset == null) return;
            var effect = target.AddComponent<VisualEffect>();
            effect.visualEffectAsset = asset;
            effect.Play();
        }

        private sealed class ActiveEffect
        {
            public ulong Primary;
            public ulong Secondary;
            public double Start;
            public GameObject Root;
            public VisualEffect Mess;
            public Transform[] Flying;
            public Vector3[] Origins;
            public Vector3 Center;
            public float Duration;
            public float Accumulated;
            public float EndingAt = -1f;
            public bool Interrupted;
            public ulong ToolId;
            public Transform ToolClone;
            public Vector3 ToolOrigin;
            public Vector3 ToolReturnOrigin;
        }

        private readonly Dictionary<(int, int), ActiveEffect> _active = new Dictionary<(int, int), ActiveEffect>();
        private readonly HashSet<ulong> _hidden = new HashSet<ulong>();
        private PizzeriaItemVisualizer _visualizer;
        private readonly List<(int, int)> _expired = new List<(int, int)>();
        private readonly HashSet<(int, int)> _seen = new HashSet<(int, int)>();
        private VisualEffectAsset _messAsset;
        private VisualEffectAsset _smokeAsset;
        private PizzeriaNetworkState _state;
        private const float FlightDuration = 0.42f;

        private void Awake()
        {
            _visualizer = GetComponent<PizzeriaItemVisualizer>();
            LoadAssets();
        }

        private void LoadAssets()
        {
#if UNITY_EDITOR
            OnValidate();
#endif
            if (originalMess != null) _messAsset = originalMess;
            if (originalSmoke != null) _smokeAsset = originalSmoke;
            if (_messAsset == null) _messAsset = Resources.Load<VisualEffectAsset>("PizzeriaVFX/VFX_Mess");
            if (_smokeAsset == null) _smokeAsset = Resources.Load<VisualEffectAsset>("PizzeriaVFX/VFX_Smoke");
#if UNITY_EDITOR
            if (_messAsset == null)
                _messAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Resources/PizzeriaVFX/VFX_Mess.vfx");
            if (_smokeAsset == null)
                _smokeAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<VisualEffectAsset>("Assets/Resources/PizzeriaVFX/VFX_Smoke.vfx");
#endif
        }

        private void LateUpdate()
        {
            var state = PizzeriaNetworkState.Instance;
            if (_state != state)
            {
                Clear();
                _state = state;
            }
            if (state == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            {
                Clear();
                return;
            }

            _seen.Clear();
            double now = NetworkManager.Singleton.ServerTime.Time;
            for (int i = 0; i < state.StationSlots.Count; i++)
            {
                var slot = state.StationSlots[i];
                if (slot.Phase != (byte)OperationPhase.Running || slot.HeldItemId == 0 ||
                    (slot.Kind != (byte)OperationKind.Assemble && slot.Kind != (byte)OperationKind.Process) ||
                    (slot.ProcessId == 0 && slot.Kind != (byte)OperationKind.Assemble))
                    continue;

                var key = (slot.StationId, slot.SlotIndex);
                _seen.Add(key);
                if (!_active.TryGetValue(key, out var effect) || effect.Primary != slot.HeldItemId ||
                    effect.Secondary != slot.SecondaryItemId || effect.Start != slot.ServerTime)
                {
                    Remove(key);
                    effect = Create(slot);
                    if (effect == null) continue;
                    _active[key] = effect;
                }

                double elapsed = now - effect.Start + effect.Accumulated;
                if (elapsed < 0) elapsed = 0;
                if (effect.EndingAt >= 0f) continue;
                float flight = Mathf.Clamp01((float)(elapsed / FlightDuration));
                if (effect.ToolClone != null)
                {
                    effect.ToolClone.position = Vector3.Lerp(effect.ToolOrigin, effect.Center, flight) +
                        Vector3.up * (4f * flight * (1f - flight) * 0.4f);
                    effect.ToolClone.Rotate(Vector3.up, Time.deltaTime * 180f, Space.World);
                }
                if (effect.Root != null && elapsed < effect.Duration && !effect.Root.activeSelf)
                    effect.Root.SetActive(true);
                for (int f = 0; f < effect.Flying.Length; f++)
                {
                    var visual = effect.Flying[f];
                    if (visual == null) continue;
                    if (flight >= 1f || elapsed >= effect.Duration)
                    {
                        visual.gameObject.SetActive(false);
                        continue;
                    }
                    visual.gameObject.SetActive(true);
                    visual.position = Vector3.Lerp(effect.Origins[f], effect.Center, flight) +
                                      Vector3.up * (4f * flight * (1f - flight) * (0.3f + f * 0.12f));
                    visual.Rotate(Vector3.up, Time.deltaTime * 330f, Space.World);
                }
                if (elapsed >= effect.Duration)
                {
                    // Keep inputs hidden until the authoritative result arrives.
                    foreach (var vfx in effect.Root.GetComponentsInChildren<VisualEffect>()) vfx.Stop();
                }
            }

            _expired.Clear();
            foreach (var pair in _active)
            {
                var effect = pair.Value;
                if (!_seen.Contains(pair.Key) && effect.EndingAt < 0f)
                {
                    effect.Interrupted = false;
                    for (int s = 0; s < state.StationSlots.Count; s++)
                    {
                        var current = state.StationSlots[s];
                        if (current.StationId == pair.Key.Item1 && current.SlotIndex == pair.Key.Item2 &&
                            current.Phase == (byte)OperationPhase.Paused)
                            effect.Interrupted = true;
                    }
                    // Reset-on-cancel removes the slot operation entirely before its deadline.
                    effect.Interrupted |= now - effect.Start + effect.Accumulated < effect.Duration;
                    effect.EndingAt = Time.unscaledTime;
                    if (effect.ToolClone != null) effect.ToolReturnOrigin = effect.ToolClone.position;
                    if (effect.Root != null)
                        foreach (var vfx in effect.Root.GetComponentsInChildren<VisualEffect>()) vfx.Stop();
                    foreach (var flying in effect.Flying)
                        if (flying != null) flying.gameObject.SetActive(false);
                    if (effect.Interrupted)
                    {
                        FadeInput(effect.Primary);
                        FadeInput(effect.Secondary);
                    }
                }
                if (effect.EndingAt >= 0f)
                {
                    float duration = effect.Interrupted ? transitionDuration : completionTransitionDuration;
                    float t = Mathf.Clamp01((Time.unscaledTime - effect.EndingAt) / Mathf.Max(0.01f, duration));
                    var tool = _visualizer.GetToolVisual(effect.ToolId);
                    if (effect.ToolClone != null && tool != null && tool.VisualInstance != null)
                    {
                        effect.ToolClone.position = Vector3.Lerp(effect.ToolReturnOrigin, tool.VisualInstance.transform.position, t) +
                            Vector3.up * (4f * t * (1f - t) * 0.25f);
                        effect.ToolClone.rotation = Quaternion.Slerp(effect.ToolClone.rotation, tool.VisualInstance.transform.rotation, t);
                    }
                    if (t >= 1f) _expired.Add(pair.Key);
                }
            }
            foreach (var key in _expired) Remove(key);
            ApplyVisibility();
        }

        private ActiveEffect Create(NetStationSlotDto slot)
        {
            LoadAssets();

            Transform stationTr = null;
            if (StationRegistry.TryGetStation(slot.StationId, out var tr) && tr != null)
                stationTr = tr;
            else
            {
                var allStations = FindObjectsByType<StationView>(FindObjectsSortMode.None);
                for (int s = 0; s < allStations.Length; s++)
                {
                    if (allStations[s] != null && allStations[s].stationId == slot.StationId)
                    {
                        stationTr = allStations[s].transform;
                        break;
                    }
                }
            }

            if (stationTr == null) return null;
            var station = stationTr.GetComponent<StationView>();
            Vector3 center = station != null ? station.GetSlotPosition(slot.SlotIndex) + Vector3.up * 0.18f : stationTr.position + Vector3.up * 0.5f;

            var root = CreateEffectPreview(center);
            root.name = $"KitchenEffect_{slot.StationId}_{slot.SlotIndex}";
            var mess = root.GetComponent<VisualEffect>();

            var effect = new ActiveEffect
            {
                Primary = slot.HeldItemId,
                Secondary = slot.SecondaryItemId,
                Root = root,
                Mess = mess,
                Center = center,
                Start = slot.ServerTime,
                Duration = slot.Duration,
                Accumulated = slot.Accumulated,
                Flying = new Transform[slot.SecondaryItemId != 0 ? 2 : 1],
                Origins = new Vector3[slot.SecondaryItemId != 0 ? 2 : 1]
            };
            for (int i = 0; i < effect.Flying.Length; i++)
            {
                ulong id = i == 0 ? slot.HeldItemId : slot.SecondaryItemId;
                var wrapper = _visualizer.GetItemVisual(id);
                if (wrapper == null || wrapper.VisualInstance == null) continue;
                var clone = Instantiate(wrapper.VisualInstance, root.transform, true);
                ItemVisualWrapper.SetLayerRecursively(clone, 0);
                clone.SetActive(true);
                clone.name = "FlyingInput";
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                effect.Flying[i] = clone.transform;
                effect.Origins[i] = Time.unscaledTime - wrapper.LastHandTime < 1f
                    ? wrapper.LastHandPosition : wrapper.VisualInstance.transform.position;
                clone.transform.position = effect.Origins[i];
            }
            effect.ToolId = slot.ToolItemId;
            var toolWrapper = _visualizer.GetToolVisual(effect.ToolId);
            if (toolWrapper != null && toolWrapper.VisualInstance != null)
            {
                var clone = Instantiate(toolWrapper.VisualInstance, root.transform, true);
                clone.name = "FlyingTool";
                clone.SetActive(true);
                ItemVisualWrapper.SetLayerRecursively(clone, 0);
                foreach (var collider in clone.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                effect.ToolClone = clone.transform;
                effect.ToolOrigin = toolWrapper.VisualInstance.transform.position;
                toolWrapper.VisualInstance.SetActive(false);
            }
            return effect;
        }

        private void ApplyVisibility()
        {
            _hidden.Clear();
            foreach (var effect in _active.Values)
            {
                if (effect.EndingAt >= 0f && effect.Interrupted) continue;
                _hidden.Add(effect.Primary);
                if (effect.Secondary != 0) _hidden.Add(effect.Secondary);
            }
            _visualizer.SetProcessHiddenItems(_hidden);
        }

        private void FadeInput(ulong id)
        {
            var wrapper = _visualizer.GetItemVisual(id);
            if (wrapper == null || wrapper.VisualInstance == null) return;
            var fade = wrapper.VisualInstance.GetComponent<KitchenItemFade>();
            if (fade == null) fade = wrapper.VisualInstance.AddComponent<KitchenItemFade>();
            fade.Begin(transitionDuration);
        }

        private void Remove((int, int) key)
        {
            if (!_active.TryGetValue(key, out var effect)) return;
            RestoreTool(effect);
            if (effect.Root != null) Destroy(effect.Root);
            _active.Remove(key);
        }

        private void Clear()
        {
            foreach (var effect in _active.Values)
            {
                RestoreTool(effect);
                if (effect.Root != null) Destroy(effect.Root);
            }
            _active.Clear();
            _hidden.Clear();
            if (_visualizer != null) _visualizer.SetProcessHiddenItems(_hidden);
        }

        private void OnDisable() => Clear();
        private void RestoreTool(ActiveEffect effect)
        {
            var tool = _visualizer != null ? _visualizer.GetToolVisual(effect.ToolId) : null;
            if (tool != null && tool.VisualInstance != null) tool.VisualInstance.SetActive(true);
        }

        private void OnDestroy() => Clear();
    }
}
