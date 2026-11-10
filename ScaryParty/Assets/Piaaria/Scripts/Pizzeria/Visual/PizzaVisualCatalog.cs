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

            // Fallback procedural configurado com as cores e modos padronizados
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
                    p.tintColor = new Color(0.80f, 0.15f, 0.10f);
                    p.layerHeight = 0.012f;
                    p.coatingScale = 0.38f;
                    p.thickness = 0.005f;
                    break;

                case 4: // Queijo em Bloco
                case 5: // Queijo Ralado
                    p.displayName = "Queijo Ralado";
                    p.placementMode = PizzaPlacementMode.Total;
                    p.tintColor = new Color(0.98f, 0.88f, 0.35f);
                    p.layerHeight = 0.022f;
                    p.coatingScale = 0.36f;
                    p.thickness = 0.005f;
                    break;

                case 6: // Calabresa Inteira
                case 7: // Calabresa Fatiada
                    p.displayName = "Calabresa Fatiada";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.65f, 0.15f, 0.15f);
                    p.piecesPerPortion = 5;
                    p.occupancyRadius = 0.045f;
                    p.minDistanceBetweenPieces = 0.07f;
                    p.yOffset = 0.035f;
                    p.scaleRange = new Vector2(0.045f, 0.055f);
                    break;

                case 8: // Cogumelo Inteiro
                case 9: // Cogumelo Fatiado
                    p.displayName = "Cogumelo Fatiado";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.75f, 0.65f, 0.52f);
                    p.piecesPerPortion = 4;
                    p.occupancyRadius = 0.040f;
                    p.minDistanceBetweenPieces = 0.065f;
                    p.yOffset = 0.035f;
                    p.scaleRange = new Vector2(0.04f, 0.05f);
                    break;

                default:
                    p.displayName = $"Ingrediente #{ingredientDefId}";
                    p.placementMode = PizzaPlacementMode.Sliced;
                    p.tintColor = new Color(0.5f, 0.7f, 0.3f);
                    p.piecesPerPortion = 4;
                    p.occupancyRadius = 0.040f;
                    p.minDistanceBetweenPieces = 0.06f;
                    p.yOffset = 0.035f;
                    break;
            }

            return p;
        }
    }
}
