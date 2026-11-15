using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Compositor de instâncias visuais da pizza na hierarquia do GameObject da pizza.
    /// Reutiliza/gerencia filhos sem destruir a raiz lógica da pizza nem criar vazamento de materiais.
    /// Integra-se com PizzaModularGeometryFactory para fornecer geometria gastronômica anatômica real.
    /// </summary>
    public static class PizzaVisualComposer
    {
        private const string ModularVisualRootName = "ModularPizzaVisual";
        private const string DoughRootName = "Dough_Root";
        private const string CoatingRootName = "Coating_Root";
        private const string ToppingsRootName = "Toppings_Root";
        private static MaterialPropertyBlock _sharedColorBlock;
        private static MaterialPropertyBlock SharedColorBlock => _sharedColorBlock ??= new MaterialPropertyBlock();
        private static Material _cachedFallbackLitMaterial;

        private static Material ResolveFoodMaterial(
            Material overrideMat,
            ScaryParty.VisualStyles.VisualSemanticRole role,
            Shader fallbackShader)
        {
            Material resolved = ScaryParty.VisualStyles.VisualStyleManager.ResolveMaterialOrFallback(
                overrideMat,
                role,
                ScaryParty.VisualStyles.VisualCategoryIds.Food);
            if (resolved != null) return resolved;

            if (_cachedFallbackLitMaterial == null && fallbackShader != null)
            {
                _cachedFallbackLitMaterial = new Material(fallbackShader) { name = "PizzaComposer_FallbackLit" };
            }
            return _cachedFallbackLitMaterial;
        }

        private static ScaryParty.VisualStyles.VisualSemanticRole ResolveIngredientRole(int ingredientId, bool isCoating)
        {
            if (isCoating)
                return ingredientId == 3 ? ScaryParty.VisualStyles.VisualSemanticRole.PizzaSauce : ScaryParty.VisualStyles.VisualSemanticRole.PizzaCheese;
            if (ingredientId == 8 || ingredientId == 9)
                return ScaryParty.VisualStyles.VisualSemanticRole.PizzaMushroom;
            if (ingredientId == 4 || ingredientId == 5)
                return ScaryParty.VisualStyles.VisualSemanticRole.PizzaCheese;
            if (ingredientId == 1 || ingredientId == 2)
                return ScaryParty.VisualStyles.VisualSemanticRole.PizzaDough;
            return ScaryParty.VisualStyles.VisualSemanticRole.PizzaPepperoni;
        }

        public static void ApplyLayout(GameObject pizzaGo, PizzaLayoutResult layout, NetItemDto item)
        {
            if (pizzaGo == null || layout == null) return;

            Transform anchor = FindLayerAnchor(pizzaGo.transform);

            // Obter ou criar a raiz modular
            Transform modularRoot = anchor.Find(ModularVisualRootName);
            if (modularRoot == null)
            {
                var mGo = new GameObject(ModularVisualRootName);
                modularRoot = mGo.transform;
                modularRoot.SetParent(anchor, false);
                modularRoot.localPosition = Vector3.zero;
                modularRoot.localRotation = Quaternion.identity;
                modularRoot.localScale = Vector3.one;
            }

            // Raiz de massa
            Transform doughRoot = modularRoot.Find(DoughRootName);
            if (doughRoot == null)
            {
                var dGo = new GameObject(DoughRootName);
                doughRoot = dGo.transform;
                doughRoot.SetParent(modularRoot, false);
                doughRoot.localPosition = Vector3.zero;
                doughRoot.localRotation = Quaternion.identity;
                doughRoot.localScale = Vector3.one;
            }

            Transform coatingRoot = modularRoot.Find(CoatingRootName);
            if (coatingRoot == null)
            {
                var cGo = new GameObject(CoatingRootName);
                coatingRoot = cGo.transform;
                coatingRoot.SetParent(modularRoot, false);
                coatingRoot.localPosition = Vector3.zero;
                coatingRoot.localRotation = Quaternion.identity;
                coatingRoot.localScale = Vector3.one;
            }

            Transform toppingsRoot = modularRoot.Find(ToppingsRootName);
            if (toppingsRoot == null)
            {
                var tGo = new GameObject(ToppingsRootName);
                toppingsRoot = tGo.transform;
                toppingsRoot.SetParent(modularRoot, false);
                toppingsRoot.localPosition = Vector3.zero;
                toppingsRoot.localRotation = Quaternion.identity;
                toppingsRoot.localScale = Vector3.one;
            }

            // Limpar peças anteriores dos roots com segurança (Editor & Runtime)
            var feelTracker = modularRoot.GetComponent<PizzaLayerFeelTracker>();
            if (Application.isPlaying && feelTracker == null)
            {
                feelTracker = modularRoot.gameObject.AddComponent<PizzaLayerFeelTracker>();
            }

            Dictionary<int, int> newCounts = feelTracker != null ? feelTracker.BeginBuildAndCollectNewCounts(item, layout) : null;
            bool shouldAnimateNewLayers = Application.isPlaying && feelTracker != null && feelTracker.HasInitializedBaseline
                && !ScaryParty.Pizzeria.Presentation.Feedback.GameFeelSettings.ReduceMotion;

            ClearChildren(coatingRoot);
            ClearChildren(toppingsRoot);

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") 
                                ?? Shader.Find("Distant Lands/Toon Lit") 
                                ?? Shader.Find("Standard");

            int toppingAnimIdx = 0;
            // Instanciar / posicionar cada peça
            for (int i = 0; i < layout.Placements.Count; i++)
            {
                var p = layout.Placements[i];
                Transform parent = p.IsCoating ? coatingRoot : toppingsRoot;

                GameObject piece;
                if (p.Prefab != null)
                {
                    piece = UnityEngine.Object.Instantiate(p.Prefab, parent);
                }
                else
                {
                    // Geometria específica de alimentos anatômicos via PizzaModularGeometryFactory
                    piece = new GameObject();
                    piece.transform.SetParent(parent, false);

                    var mf = piece.AddComponent<MeshFilter>();
                    var mr = piece.AddComponent<MeshRenderer>();

                    if (p.IsCoating)
                    {
                        mf.sharedMesh = (p.IngredientId == 3)
                            ? PizzaModularGeometryFactory.GetSauceMesh()
                            : PizzaModularGeometryFactory.GetCheeseCoatingMesh();
                    }
                    else if (p.Mode == PizzaPlacementMode.Central)
                    {
                        mf.sharedMesh = PizzaModularGeometryFactory.GetBasilLeafMesh();
                    }
                    else if (p.Mode == PizzaPlacementMode.Scatter)
                    {
                        mf.sharedMesh = PizzaModularGeometryFactory.GetCheeseStrandMesh();
                    }
                    else if (p.Mode == PizzaPlacementMode.Sliced)
                    {
                        mf.sharedMesh = (p.IngredientId == 8 || p.IngredientId == 9)
                            ? PizzaModularGeometryFactory.GetMushroomSliceMesh()
                            : PizzaModularGeometryFactory.GetPepperoniSliceMesh();
                    }
                    else
                    {
                        mf.sharedMesh = PizzaModularGeometryFactory.GetPepperoniSliceMesh();
                    }
                }

                piece.name = $"Piece_{p.IngredientId}_{p.Mode}_{i}";
                piece.transform.localPosition = p.LocalPosition;
                piece.transform.localRotation = p.LocalRotation;
                piece.transform.localScale = p.LocalScale;
                piece.layer = pizzaGo.layer;

                // Aplicar cor / material com suporte a URP e Distant Lands via MaterialPropertyBlock (zero vazamento)
                var rends = piece.GetComponentsInChildren<Renderer>(true);
                var role = ResolveIngredientRole(p.IngredientId, p.IsCoating);
                for (int r = 0; r < rends.Length; r++)
                {
                    var rend = rends[r];
                    if (rend == null) continue;

                    Material targetMat = ResolveFoodMaterial(p.MaterialOverride ?? rend.sharedMaterial, role, defaultShader);
                    if (targetMat != null)
                    {
                        rend.sharedMaterial = targetMat;
                    }

                    ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, 0, p.TintColor, SharedColorBlock);
                    ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                        piece, role, ScaryParty.VisualStyles.VisualCategoryIds.Food);
                    ScaryParty.Lighting.PizzeriaLightingController.Active?.ApplyZoneFillToHierarchy(piece);
                }

                if (shouldAnimateNewLayers && newCounts != null && newCounts.TryGetValue(p.IngredientId, out int addedCount) && addedCount > 0)
                {
                    var anim = piece.AddComponent<PizzaPieceEntryAnimator>();
                    if (p.IsCoating && p.IngredientId == 3)
                    {
                        anim.Begin(PizzaPieceEntryAnimator.EntryKind.SauceSpread, p.LocalPosition, p.LocalRotation, p.LocalScale, 0f);
                    }
                    else if (p.IsCoating || p.Mode == PizzaPlacementMode.Scatter)
                    {
                        anim.Begin(PizzaPieceEntryAnimator.EntryKind.CheeseDrop, p.LocalPosition, p.LocalRotation, p.LocalScale, (toppingAnimIdx % 5) * 0.025f);
                        toppingAnimIdx++;
                    }
                    else
                    {
                        anim.Begin(PizzaPieceEntryAnimator.EntryKind.ToppingHop, p.LocalPosition, p.LocalRotation, p.LocalScale, toppingAnimIdx * 0.028f);
                        toppingAnimIdx++;
                    }
                }
            }

            if (feelTracker != null)
            {
                if (shouldAnimateNewLayers && newCounts != null && newCounts.Count > 0)
                {
                    TriggerAssemblyEventFromChanges(pizzaGo, item, newCounts);
                }
                feelTracker.CommitBaseline(item);
            }
        }

        /// <summary>
        /// Garante a base de massa da pizza com geometria de cornicione e tint de cozimento.
        /// </summary>
        public static GameObject EnsureDoughBase(GameObject pizzaGo, float cookProgress, float burnProgress)
        {
            if (pizzaGo == null) return null;
            Transform anchor = FindLayerAnchor(pizzaGo.transform);

            Transform modularRoot = anchor.Find(ModularVisualRootName);
            if (modularRoot == null)
            {
                var mGo = new GameObject(ModularVisualRootName);
                modularRoot = mGo.transform;
                modularRoot.SetParent(anchor, false);
                modularRoot.localPosition = Vector3.zero;
                modularRoot.localRotation = Quaternion.identity;
                modularRoot.localScale = Vector3.one;
            }

            Transform doughRoot = modularRoot.Find(DoughRootName);
            if (doughRoot == null)
            {
                var dGo = new GameObject(DoughRootName);
                doughRoot = dGo.transform;
                doughRoot.SetParent(modularRoot, false);
                doughRoot.localPosition = Vector3.zero;
                doughRoot.localRotation = Quaternion.identity;
                doughRoot.localScale = Vector3.one;
            }

            Transform baseDough = doughRoot.Find("BaseDough");
            if (baseDough == null)
            {
                var go = new GameObject("BaseDough");
                go.transform.SetParent(doughRoot, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                baseDough = go.transform;
            }

            var mf = baseDough.GetComponent<MeshFilter>();
            if (mf == null) mf = baseDough.gameObject.AddComponent<MeshFilter>();

            var mr = baseDough.GetComponent<MeshRenderer>();
            if (mr == null) mr = baseDough.gameObject.AddComponent<MeshRenderer>();

            mf.sharedMesh = PizzaModularGeometryFactory.GetDoughMesh();

            Color rawDough = new Color(0.96f, 0.90f, 0.76f);
            Color bakedDough = new Color(0.82f, 0.52f, 0.18f);
            Color burnedDough = new Color(0.12f, 0.10f, 0.09f);

            Color currentDough = burnProgress > 0f
                ? Color.Lerp(bakedDough, burnedDough, Mathf.Clamp01(burnProgress))
                : Color.Lerp(rawDough, bakedDough, Mathf.Clamp01(cookProgress));

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit")
                                ?? Shader.Find("Distant Lands/Toon Lit")
                                ?? Shader.Find("Standard");

            Material doughMat = ResolveFoodMaterial(
                mr.sharedMaterial,
                ScaryParty.VisualStyles.VisualSemanticRole.PizzaDough,
                defaultShader);
            if (doughMat != null)
            {
                mr.sharedMaterial = doughMat;
            }
            ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(mr, 0, currentDough, SharedColorBlock);
            ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                baseDough.gameObject,
                ScaryParty.VisualStyles.VisualSemanticRole.PizzaDough,
                ScaryParty.VisualStyles.VisualCategoryIds.Food);
            ScaryParty.Lighting.PizzeriaLightingController.Active?.ApplyZoneFillToHierarchy(baseDough.gameObject);

            return baseDough.gameObject;
        }

        private static Transform FindLayerAnchor(Transform root)
        {
            if (root == null) return null;
            var anchor = root.Find("IngredientLayerAnchor");
            if (anchor != null) return anchor;

            var all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == "IngredientLayerAnchor") return all[i];
            }

            return root;
        }

        private static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                if (child != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                        continue;
                    }
#endif
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }

        private static void TriggerAssemblyEventFromChanges(GameObject pizzaGo, NetItemDto item, Dictionary<int, int> newCounts)
        {
            var presenter = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelPresenter.Instance;
            if (presenter == null) return;

            var eventId = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelEventId.PlaceToppings;
            var family = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelMaterialFamily.SmallBounceIngredient;
            if (newCounts.ContainsKey(3))
            {
                eventId = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelEventId.SpreadSauce;
                family = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelMaterialFamily.FluidGranular;
            }
            else if (newCounts.ContainsKey(4) || newCounts.ContainsKey(5))
            {
                eventId = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelEventId.SprinkleCheese;
                family = ScaryParty.Pizzeria.Presentation.Feedback.GameFeelMaterialFamily.FluidGranular;
            }

            presenter.TriggerSimple(
                eventId,
                target: pizzaGo != null ? pizzaGo.transform : null,
                entityId: item.ItemId,
                stationId: (int)item.HolderId,
                revision: item.Revision,
                family: family,
                intensity: 0.9f,
                isLocal: true,
                slotIndex: item.SlotId);
        }
    }

    /// <summary>
    /// Rastreia camadas já renderizadas em uma pizza para animar somente ingredientes recém-adicionados.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PizzaLayerFeelTracker : MonoBehaviour
    {
        private readonly Dictionary<int, int> _lastIngredientCounts = new Dictionary<int, int>();
        private ulong _trackedItemId;

        public bool HasInitializedBaseline { get; private set; }

        public Dictionary<int, int> BeginBuildAndCollectNewCounts(NetItemDto item, PizzaLayoutResult layout)
        {
            if (_trackedItemId != item.ItemId)
            {
                _trackedItemId = item.ItemId;
                _lastIngredientCounts.Clear();
                HasInitializedBaseline = false;
                return null;
            }

            if (!HasInitializedBaseline) return null;

            var current = new Dictionary<int, int>();
            for (int i = 0; i < layout.Placements.Count; i++)
            {
                int id = layout.Placements[i].IngredientId;
                current.TryGetValue(id, out int count);
                current[id] = count + 1;
            }

            var added = new Dictionary<int, int>();
            foreach (var kv in current)
            {
                _lastIngredientCounts.TryGetValue(kv.Key, out int prev);
                if (kv.Value > prev)
                {
                    added[kv.Key] = kv.Value - prev;
                }
            }
            return added;
        }

        public void CommitBaseline(NetItemDto item)
        {
            _trackedItemId = item.ItemId;
            _lastIngredientCounts.Clear();

            int count = Mathf.Clamp(item.DistinctIngredientCount, 0, NetItemDto.MaxDistinctIngredients);
            for (int i = 0; i < count; i++)
            {
                var ing = item.GetIngredient(i);
                if (ing.DefinitionId > 0)
                {
                    _lastIngredientCounts[ing.DefinitionId] = ing.Count;
                }
            }

            // Fallback baseado em layout caso o tracker venha por DistinctIngredientCount vazio em teste visual
            var children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] == null) continue;
                string n = children[i].name;
                if (n.StartsWith("Piece_") && TryParseIngredientId(n, out int id))
                {
                    _lastIngredientCounts.TryGetValue(id, out int prev);
                    _lastIngredientCounts[id] = prev + 1;
                }
            }

            HasInitializedBaseline = true;
        }

        private static bool TryParseIngredientId(string pieceName, out int id)
        {
            id = 0;
            int firstUnderscore = pieceName.IndexOf('_');
            if (firstUnderscore < 0) return false;
            int secondUnderscore = pieceName.IndexOf('_', firstUnderscore + 1);
            if (secondUnderscore <= firstUnderscore + 1) return false;
            return int.TryParse(pieceName.Substring(firstUnderscore + 1, secondUnderscore - firstUnderscore - 1), out id);
        }
    }

    /// <summary>
    /// Micro-animador de entrada para camadas recém-adicionadas na pizza (espalhar molho, chuva de queijo e saltos de toppings).
    /// Ao terminar, restaura a pose determinística exata e desativa a si mesmo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PizzaPieceEntryAnimator : MonoBehaviour
    {
        public enum EntryKind { SauceSpread, CheeseDrop, ToppingHop }

        private EntryKind _kind;
        private Vector3 _targetPos;
        private Quaternion _targetRot;
        private Vector3 _targetScale;
        private float _delay;
        private float _duration;
        private float _elapsed;
        private bool _active;

        public void Begin(EntryKind kind, Vector3 targetPos, Quaternion targetRot, Vector3 targetScale, float delay)
        {
            _kind = kind;
            _targetPos = targetPos;
            _targetRot = targetRot;
            _targetScale = targetScale;
            _delay = Mathf.Max(0f, delay);
            _duration = kind == EntryKind.SauceSpread ? 0.22f : (kind == EntryKind.CheeseDrop ? 0.20f : 0.23f);
            _elapsed = -_delay;
            _active = true;
            ApplyAt(0f);
        }

        private void Update()
        {
            if (!_active)
            {
                enabled = false;
                return;
            }

            _elapsed += Time.deltaTime;
            if (_elapsed < 0f) return;

            float t = Mathf.Clamp01(_elapsed / _duration);
            ApplyAt(t);

            if (t >= 1f)
            {
                transform.localPosition = _targetPos;
                transform.localRotation = _targetRot;
                transform.localScale = _targetScale;
                _active = false;
                enabled = false;
            }
        }

        private void ApplyAt(float t)
        {
            switch (_kind)
            {
                case EntryKind.SauceSpread:
                {
                    float s = t < 0.72f
                        ? Mathf.Lerp(0.22f, 1.05f, Mathf.SmoothStep(0f, 1f, t / 0.72f))
                        : Mathf.Lerp(1.05f, 1f, (t - 0.72f) / 0.28f);
                    transform.localScale = new Vector3(_targetScale.x * s, _targetScale.y, _targetScale.z * s);
                    break;
                }
                case EntryKind.CheeseDrop:
                {
                    float drop = (1f - Mathf.SmoothStep(0f, 1f, t)) * 0.035f;
                    transform.localPosition = _targetPos + Vector3.up * drop;
                    float squash = t > 0.7f ? 1f - Mathf.Sin(((t - 0.7f) / 0.3f) * Mathf.PI) * 0.12f : 1f;
                    transform.localScale = new Vector3(_targetScale.x * (2f - squash), _targetScale.y * squash, _targetScale.z * (2f - squash));
                    break;
                }
                case EntryKind.ToppingHop:
                {
                    float hop = Mathf.Sin(t * Mathf.PI) * 0.025f;
                    float startDrop = (1f - t) * 0.028f;
                    transform.localPosition = _targetPos + Vector3.up * (hop + startDrop);
                    float tilt = (1f - t) * 14f;
                    transform.localRotation = _targetRot * Quaternion.Euler(tilt, 0f, -tilt * 0.6f);
                    float landSquash = t > 0.75f ? Mathf.Sin(((t - 0.75f) / 0.25f) * Mathf.PI) : 0f;
                    transform.localScale = new Vector3(
                        _targetScale.x * (1f + landSquash * 0.08f),
                        _targetScale.y * (1f - landSquash * 0.14f),
                        _targetScale.z * (1f + landSquash * 0.08f));
                    break;
                }
            }
        }
    }
}
