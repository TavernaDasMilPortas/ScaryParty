using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Compositor de instâncias visuais da pizza na hierarquia do GameObject da pizza.
    /// Reutiliza/gerencia filhos sem destruir a raiz lógica da pizza nem criar vazamento de materiais.
    /// </summary>
    public static class PizzaVisualComposer
    {
        private const string ModularVisualRootName = "ModularPizzaVisual";
        private const string CoatingRootName = "Coating_Root";
        private const string ToppingsRootName = "Toppings_Root";

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

            // Limpar peças anteriores dos roots
            ClearChildren(coatingRoot);
            ClearChildren(toppingsRoot);

            Shader defaultShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

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
                    // Fallback procedural dependendo do modo
                    if (p.IsCoating)
                    {
                        piece = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    }
                    else if (p.Mode == PizzaPlacementMode.Scatter)
                    {
                        piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    }
                    else
                    {
                        piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    }
                    piece.transform.SetParent(parent, false);
                    var col = piece.GetComponent<Collider>();
                    if (col != null) UnityEngine.Object.Destroy(col);
                }

                piece.name = $"Piece_{p.IngredientId}_{p.Mode}_{i}";
                piece.transform.localPosition = p.LocalPosition;
                piece.transform.localRotation = p.LocalRotation;
                piece.transform.localScale = p.LocalScale;
                piece.layer = pizzaGo.layer;

                // Aplicar cor / material
                var rends = piece.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < rends.Length; r++)
                {
                    var rend = rends[r];
                    if (rend == null) continue;

                    if (p.MaterialOverride != null)
                    {
                        rend.sharedMaterial = p.MaterialOverride;
                    }
                    else if (defaultShader != null && rend.sharedMaterial == null)
                    {
                        rend.material = new Material(defaultShader);
                    }

                    if (rend.material != null)
                    {
                        rend.material.color = p.TintColor;
                    }
                }
            }
        }

        private static Transform FindLayerAnchor(Transform root)
        {
            if (root == null) return null;
            var anchor = root.Find("IngredientLayerAnchor");
            if (anchor != null) return anchor;

            // Busca profunda
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
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }
        }
    }
}
