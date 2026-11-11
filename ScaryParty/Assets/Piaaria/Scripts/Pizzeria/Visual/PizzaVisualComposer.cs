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
            ClearChildren(coatingRoot);
            ClearChildren(toppingsRoot);

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") 
                                ?? Shader.Find("Distant Lands/Toon Lit") 
                                ?? Shader.Find("Standard");

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
                }
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
    }
}
