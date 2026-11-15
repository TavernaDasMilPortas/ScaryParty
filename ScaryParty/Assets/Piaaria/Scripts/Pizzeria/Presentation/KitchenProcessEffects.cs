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
            public Vector3[] BaseScales;
            public Vector3 Center;
            public float Duration;
            public float Accumulated;
            public float EndingAt = -1f;
            public bool Interrupted;
            public int ProcessId;
            public ulong ToolId;
            public Transform ToolClone;
            public Vector3 ToolOrigin;
            public Vector3 ToolReturnOrigin;
            public Transform[] DecorativeSlices;
            public Vector3[] DecorativeTargets;
        }

        private readonly Queue<GameObject> _effectRootPool = new Queue<GameObject>();
        private const int MaxPooledRoots = 6;

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
                float procProgress = effect.Duration > 0.01f ? Mathf.Clamp01((float)(elapsed / effect.Duration)) : 0f;
                bool useSpecificProcessFeel = slot.Kind == (byte)OperationKind.Process && effect.ProcessId >= 1 && effect.ProcessId <= 4
                    && !ScaryParty.Pizzeria.Presentation.Feedback.GameFeelSettings.ReduceMotion;

                if (effect.ToolClone != null)
                {
                    if (flight < 1f || !useSpecificProcessFeel)
                    {
                        effect.ToolClone.position = Vector3.Lerp(effect.ToolOrigin, effect.Center, flight) +
                            Vector3.up * (4f * flight * (1f - flight) * 0.4f);
                        effect.ToolClone.Rotate(Vector3.up, Time.deltaTime * 180f, Space.World);
                    }
                    else
                    {
                        AnimateToolForProcess(effect, (float)elapsed, procProgress);
                    }
                }
                if (effect.Root != null && elapsed < effect.Duration && !effect.Root.activeSelf)
                    effect.Root.SetActive(true);
                for (int f = 0; f < effect.Flying.Length; f++)
                {
                    var visual = effect.Flying[f];
                    if (visual == null) continue;
                    if (useSpecificProcessFeel && f == 0)
                    {
                        if (flight < 1f)
                        {
                            visual.gameObject.SetActive(true);
                            visual.position = Vector3.Lerp(effect.Origins[f], effect.Center, flight) +
                                              Vector3.up * (4f * flight * (1f - flight) * 0.3f);
                        }
                        else
                        {
                            AnimateIngredientForProcess(effect, visual, f, (float)elapsed, procProgress);
                        }
                        continue;
                    }

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
                AnimateDecorativeSlices(effect, (float)elapsed);
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

            var root = _effectRootPool.Count > 0 ? _effectRootPool.Dequeue() : CreateEffectPreview(center);
            if (root == null) root = CreateEffectPreview(center);
            root.transform.position = center;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * Mathf.Max(0.01f, effectScale);
            root.SetActive(true);
            root.name = $"KitchenEffect_{slot.StationId}_{slot.SlotIndex}";
            var mess = root.GetComponent<VisualEffect>();
            foreach (var vfx in root.GetComponentsInChildren<VisualEffect>(true)) vfx.Play();

            var effect = new ActiveEffect
            {
                Primary = slot.HeldItemId,
                Secondary = slot.SecondaryItemId,
                ProcessId = slot.ProcessId,
                Root = root,
                Mess = mess,
                Center = center,
                Start = slot.ServerTime,
                Duration = slot.Duration,
                Accumulated = slot.Accumulated,
                Flying = new Transform[slot.SecondaryItemId != 0 ? 2 : 1],
                Origins = new Vector3[slot.SecondaryItemId != 0 ? 2 : 1],
                BaseScales = new Vector3[slot.SecondaryItemId != 0 ? 2 : 1]
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
                effect.BaseScales[i] = clone.transform.localScale;
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
            if (slot.Kind == (byte)OperationKind.Process && (slot.ProcessId == 3 || slot.ProcessId == 4))
            {
                CreateDecorativeSlices(effect, center, slot.ProcessId);
            }
            return effect;
        }

        private void CreateDecorativeSlices(ActiveEffect effect, Vector3 center, int processId)
        {
            const int sliceCount = 3;
            effect.DecorativeSlices = new Transform[sliceCount];
            effect.DecorativeTargets = new Vector3[sliceCount];
            Color sliceColor = processId == 4 ? new Color(0.76f, 0.68f, 0.56f) : new Color(0.78f, 0.18f, 0.14f);

            for (int i = 0; i < sliceCount; i++)
            {
                var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disc.name = $"DecorSlice_{i}";
                disc.transform.SetParent(effect.Root.transform, false);
                var col = disc.GetComponent<Collider>();
                if (col != null) Destroy(col);
                disc.transform.localScale = new Vector3(0.055f, 0.008f, 0.055f);
                disc.transform.position = center;

                var mr = disc.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    var block = new MaterialPropertyBlock();
                    ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(mr, 0, sliceColor, block);
                }

                float angle = (i * 120f + 25f) * Mathf.Deg2Rad;
                effect.DecorativeTargets[i] = center + new Vector3(Mathf.Cos(angle) * 0.08f, -0.06f, Mathf.Sin(angle) * 0.08f);
                disc.SetActive(false);
                effect.DecorativeSlices[i] = disc.transform;
            }
        }

        private void AnimateToolForProcess(ActiveEffect effect, float elapsed, float progress)
        {
            const float cycleDur = 0.32f;
            float phase = (elapsed % cycleDur) / cycleDur;
            Vector3 center = effect.Center + Vector3.up * 0.03f;

            switch (effect.ProcessId)
            {
                case 2: // Ralar queijo — vaivém lateral
                {
                    float scrub = Mathf.Sin(elapsed * 22f) * 0.045f;
                    effect.ToolClone.position = center + new Vector3(scrub, 0.05f, 0f);
                    effect.ToolClone.rotation = Quaternion.Euler(25f, 0f, Mathf.Sin(elapsed * 22f) * 8f);
                    break;
                }
                case 3: // Fatiar tomate/calabresa — ciclo 320ms (Receita B)
                case 4: // Cortar cogumelo com inclinação
                {
                    float lift = phase < 0.18f
                        ? Mathf.Lerp(0f, 0.085f, phase / 0.18f)
                        : (phase < 0.34f ? Mathf.Lerp(0.085f, 0f, (phase - 0.18f) / 0.16f) : Mathf.Lerp(0f, 0.02f, (phase - 0.34f) / 0.66f));
                    float xOff = ((Mathf.FloorToInt(elapsed / cycleDur) % 3) - 1) * 0.022f;
                    float rollAngle = effect.ProcessId == 4 ? Mathf.Sin(Mathf.FloorToInt(elapsed / cycleDur) * 2.1f) * 16f : 0f;
                    effect.ToolClone.position = center + new Vector3(xOff, lift, 0f);
                    effect.ToolClone.rotation = Quaternion.Euler(-12f + lift * 120f, 0f, rollAngle);
                    break;
                }
                default:
                    effect.ToolClone.position = center + new Vector3(Mathf.Sin(elapsed * 8f) * 0.05f, 0.03f, 0f);
                    break;
            }
        }

        private void AnimateIngredientForProcess(ActiveEffect effect, Transform visual, int index, float elapsed, float progress)
        {
            visual.gameObject.SetActive(true);
            Vector3 baseScale = index < effect.BaseScales.Length && effect.BaseScales[index] != Vector3.zero
                ? effect.BaseScales[index] : Vector3.one;
            const float cycleDur = 0.32f;
            float phase = (elapsed % cycleDur) / cycleDur;

            switch (effect.ProcessId)
            {
                case 1: // Abrir massa — achata Y e alarga XZ com vaivém suave
                {
                    float xzMult = Mathf.Lerp(1f, 1.18f, progress);
                    float yMult = Mathf.Lerp(1f, 0.58f, progress);
                    float pulse = Mathf.Sin(elapsed * 10f) * 0.03f;
                    visual.position = effect.Center + new Vector3(pulse * 0.4f, -0.05f, 0f);
                    visual.localScale = new Vector3(baseScale.x * (xzMult + pulse), baseScale.y * Mathf.Max(0.45f, yMult - pulse * 0.5f), baseScale.z * (xzMult - pulse));
                    break;
                }
                case 2: // Ralar queijo
                {
                    visual.position = effect.Center + new Vector3(Mathf.Sin(elapsed * 22f) * 0.025f, 0.02f, 0f);
                    float shrink = Mathf.Lerp(1f, 0.65f, progress);
                    visual.localScale = baseScale * shrink;
                    break;
                }
                case 3: // Tomate/Calabresa (squash no impacto de 320ms)
                case 4: // Cogumelo
                {
                    visual.position = effect.Center + Vector3.down * 0.05f;
                    bool isSquash = phase >= 0.30f && phase < 0.52f;
                    float squashAmt = isSquash ? Mathf.Sin(((phase - 0.30f) / 0.22f) * Mathf.PI) : 0f;
                    visual.localScale = new Vector3(
                        baseScale.x * (1f + 0.14f * squashAmt),
                        baseScale.y * (1f - 0.18f * squashAmt),
                        baseScale.z * (1f + 0.14f * squashAmt));
                    break;
                }
            }
        }

        private void AnimateDecorativeSlices(ActiveEffect effect, float elapsed)
        {
            if (effect.DecorativeSlices == null) return;
            const float cycleDur = 0.32f;
            float cycleTime = elapsed - FlightDuration;
            if (cycleTime < 0f) return;

            for (int i = 0; i < effect.DecorativeSlices.Length; i++)
            {
                var slice = effect.DecorativeSlices[i];
                if (slice == null) continue;
                int cycleIndex = Mathf.FloorToInt(cycleTime / cycleDur);
                if (cycleIndex >= i)
                {
                    float localT = Mathf.Clamp01((cycleTime - i * cycleDur) / 0.18f);
                    slice.gameObject.SetActive(true);
                    float arc = 4f * localT * (1f - localT) * 0.045f;
                    slice.position = Vector3.Lerp(effect.Center, effect.DecorativeTargets[i], localT) + Vector3.up * arc;
                    slice.localRotation = Quaternion.Euler(localT * 140f, i * 75f, 0f);
                }
            }
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
            RecycleOrDestroyRoot(effect);
            _active.Remove(key);
        }

        private void Clear()
        {
            foreach (var effect in _active.Values)
            {
                RestoreTool(effect);
                RecycleOrDestroyRoot(effect);
            }
            _active.Clear();
            _hidden.Clear();
            if (_visualizer != null) _visualizer.SetProcessHiddenItems(_hidden);
        }

        private void RecycleOrDestroyRoot(ActiveEffect effect)
        {
            if (effect == null || effect.Root == null) return;

            for (int i = 0; i < effect.Flying.Length; i++)
            {
                if (effect.Flying[i] != null) Destroy(effect.Flying[i].gameObject);
            }
            if (effect.ToolClone != null) Destroy(effect.ToolClone.gameObject);
            if (effect.DecorativeSlices != null)
            {
                for (int i = 0; i < effect.DecorativeSlices.Length; i++)
                {
                    if (effect.DecorativeSlices[i] != null) Destroy(effect.DecorativeSlices[i].gameObject);
                }
            }

            if (_effectRootPool.Count < MaxPooledRoots)
            {
                foreach (var vfx in effect.Root.GetComponentsInChildren<VisualEffect>(true)) vfx.Stop();
                effect.Root.SetActive(false);
                _effectRootPool.Enqueue(effect.Root);
            }
            else
            {
                Destroy(effect.Root);
            }
        }

        private void OnDisable() => Clear();
        private void RestoreTool(ActiveEffect effect)
        {
            var tool = _visualizer != null ? _visualizer.GetToolVisual(effect.ToolId) : null;
            if (tool != null && tool.VisualInstance != null) tool.VisualInstance.SetActive(true);
        }

        private void OnDestroy()
        {
            Clear();
            while (_effectRootPool.Count > 0)
            {
                var pooled = _effectRootPool.Dequeue();
                if (pooled != null) Destroy(pooled);
            }
        }
    }
}
