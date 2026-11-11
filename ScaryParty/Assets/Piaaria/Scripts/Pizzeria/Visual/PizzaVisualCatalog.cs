using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Catálogo central de perfis visuais de ingredientes de pizza.
    /// Resolve o perfil visual pelo ID do ingrediente e estágio, fornecendo perfis procedurais elegantes como fallback.
    /// </summary>
    [CreateAssetMenu(fileName = "PizzaVisualCatalog", menuName = "Scary Party/Pizzeria/Pizza Visual Catalog")]
    public class PizzaVisualCatalog : ScriptableObject
    {
        private static PizzaVisualCatalog _instance;
        public static PizzaVisualCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<PizzaVisualCatalog>("PizzaVisualCatalog");
#if UNITY_EDITOR
                    if (_instance == null)
                    {
                        var guids = UnityEditor.AssetDatabase.FindAssets("t:PizzaVisualCatalog");
                        if (guids.Length > 0)
                        {
                            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                            _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<PizzaVisualCatalog>(path);
                        }
                    }
#endif
                    if (_instance == null)
                    {
                        _instance = CreateInstance<PizzaVisualCatalog>();
                        _instance.name = "PizzaVisualCatalog_RuntimeFallback";
                    }
                }
                return _instance;
            }
        }

        [Header("Perfis de Ingredientes Configurados")]
        public List<PizzaIngredientVisualProfile> profiles = new List<PizzaIngredientVisualProfile>();

        private readonly Dictionary<int, PizzaIngredientVisualProfile> _runtimeCache = new Dictionary<int, PizzaIngredientVisualProfile>();

        public PizzaIngredientVisualProfile GetProfile(int ingredientDefId, ProcessingStage stage)
        {
            if (_runtimeCache.TryGetValue(ingredientDefId, out var cached) && cached != null)
            {
                return cached;
            }

            if (profiles != null)
            {
                for (int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i] != null && profiles[i].ingredientId == ingredientDefId && profiles[i].stage == stage)
                    {
                        _runtimeCache[ingredientDefId] = profiles[i];
                        return profiles[i];
                    }
                }

                // Fallback por apenas ID
                for (int i = 0; i < profiles.Count; i++)
                {
                    if (profiles[i] != null && profiles[i].ingredientId == ingredientDefId)
                    {
                        _runtimeCache[ingredientDefId] = profiles[i];
                        return profiles[i];
                    }
                }
            }

            // Fallback procedural configurado com as cores e modos gastronômicos padronizados
            var fallback = CreateProceduralProfile(ingredientDefId, stage);
            _runtimeCache[ingredientDefId] = fallback;
            return fallback;
        }

        private static PizzaIngredientVisualProfile CreateProceduralProfile(int ingredientDefId, ProcessingStage stage)
        {
            var p = CreateInstance<PizzaIngredientVisualProfile>();
            p.ingredientId = ingredientDefId;
            p.stage = stage;

            switch (ingredientDefId)
            {
                case 3: // Molho de Tomate
                    p.displayName = "Molho de Tomate";
                    p.placementMode = PizzaPlacementMode.Total;
                    p.tintColor = new Color(0.82f, 0.16f, 0.10f);
                    p.bakedTintColor = new Color(0.58f, 0.10f, 0.06f);
                    p.burnedTintColor = new Color(0.18f, 0.08f, 0.06f);
                    p.layerHeight = 0.002f;
                    p.coatingScale = 1.0f;
                    p.thickness = 1.0f;
                    break;

                case 4: // Queijo em Bloco
                case 5: // Queijo Ralado
                    p.displayName = "Queijo Ralado";
                    p.placementMode = PizzaPlacementMode.Total;
                    p.tintColor = new Color(0.98f, 0.92f, 0.65f);
                    p.bakedTintColor = new Color(0.94f, 0.72f, 0.22f);
                    p.burnedTintColor = new Color(0.24f, 0.16f, 0.10f);
                    p.layerHeight = 0.003f;
                    p.coatingScale = 0.96f; // 96% para deixar borda sutil de molho visível
                    p.thickness = 1.0f;
                    break;

                case 6: // Calabresa Inteira
                case 7: // Calabresa Fatiada
                    p.displayName = "Calabresa Fatiada";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.72f, 0.18f, 0.14f);
                    p.bakedTintColor = new Color(0.54f, 0.12f, 0.10f);
                    p.burnedTintColor = new Color(0.16f, 0.10f, 0.08f);
                    p.piecesPerPortion = 5;
                    p.minRadius = 0.035f;
                    p.maxRadius = 0.115f;
                    p.occupancyRadius = 0.028f;
                    p.minDistanceBetweenPieces = 0.055f;
                    p.yOffset = 0.015f;
                    p.scaleRange = new Vector2(0.95f, 1.05f);
                    break;

                case 8: // Cogumelo Inteiro
                case 9: // Cogumelo Fatiado
                    p.displayName = "Cogumelo Fatiado";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.85f, 0.78f, 0.68f);
                    p.bakedTintColor = new Color(0.62f, 0.48f, 0.35f);
                    p.burnedTintColor = new Color(0.18f, 0.14f, 0.12f);
                    p.piecesPerPortion = 4;
                    p.minRadius = 0.030f;
                    p.maxRadius = 0.110f;
                    p.occupancyRadius = 0.025f;
                    p.minDistanceBetweenPieces = 0.050f;
                    p.yOffset = 0.016f;
                    p.scaleRange = new Vector2(0.95f, 1.05f);
                    break;

                case 11: // Manjericão
                    p.displayName = "Manjericão Fresco";
                    p.placementMode = PizzaPlacementMode.Central;
                    p.tintColor = new Color(0.22f, 0.68f, 0.24f);
                    p.bakedTintColor = new Color(0.18f, 0.42f, 0.16f);
                    p.burnedTintColor = new Color(0.10f, 0.12f, 0.08f);
                    p.centralYOffset = 0.016f;
                    p.reservedCenterRadius = 0.035f;
                    break;

                default:
                    p.displayName = $"Ingrediente #{ingredientDefId}";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.5f, 0.7f, 0.3f);
                    p.bakedTintColor = new Color(0.4f, 0.5f, 0.2f);
                    p.burnedTintColor = new Color(0.15f, 0.12f, 0.10f);
                    p.piecesPerPortion = 4;
                    p.minRadius = 0.030f;
                    p.maxRadius = 0.115f;
                    p.occupancyRadius = 0.028f;
                    p.minDistanceBetweenPieces = 0.055f;
                    p.yOffset = 0.015f;
                    break;
            }

            return p;
        }
    }
}
