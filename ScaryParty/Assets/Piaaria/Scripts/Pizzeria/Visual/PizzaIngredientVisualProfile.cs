using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Perfil ScriptableObject que define o comportamento visual e regra espacial de um ingrediente na pizza.
    /// Compatível com o Odin Inspector para edição precisa de parâmetros condicionais.
    /// </summary>
    [CreateAssetMenu(fileName = "PizzaVisualProfile_", menuName = "Scary Party/Pizzeria/Pizza Visual Profile")]
    public class PizzaIngredientVisualProfile : ScriptableObject
    {
        [Header("Identificação do Ingrediente")]
        public int ingredientId = 5;
        public string displayName = "Ingrediente";
        public ProcessingStage stage = ProcessingStage.Prepared;

        [Header("Modo de Posicionamento Espacial")]
        public PizzaPlacementMode placementMode = PizzaPlacementMode.Sliced;

        [Header("Prefab e Materiais")]
        public GameObject unitPrefab;
        public Material overrideMaterial;
        public Color tintColor = Color.white;
        public Color bakedTintColor = new Color(0.85f, 0.65f, 0.40f);
        public Color burnedTintColor = new Color(0.15f, 0.12f, 0.12f);
        public bool tintOnBake = true;

        [Header("Parâmetros do Modo Sliced (Fatiado)")]
        [Min(1)] public int piecesPerPortion = 4;
        [Range(0.01f, 0.20f)] public float minRadius = 0.035f;
        [Range(0.05f, 0.20f)] public float maxRadius = 0.125f;
        [Range(0.01f, 0.10f)] public float occupancyRadius = 0.028f;
        [Range(0.01f, 0.15f)] public float minDistanceBetweenPieces = 0.055f;
        public Vector2 scaleRange = new Vector2(0.95f, 1.05f);
        public float yOffset = 0.015f;

        [Header("Parâmetros do Modo Total (Cobertura Contínua)")]
        public float layerHeight = 0.003f;
        public float coatingScale = 1.0f;
        public float thickness = 1.0f;

        [Header("Parâmetros do Modo Central")]
        public Vector3 centerOffset = Vector3.zero;
        public Vector3 centerScale = Vector3.one;
        public float centralYOffset = 0.016f;
        [Range(0.01f, 0.10f)] public float reservedCenterRadius = 0.04f;

        [Header("Parâmetros do Modo Specific (Poses Manuais)")]
        public List<SpecificPlacementPose> specificPoses = new List<SpecificPlacementPose>();
        public bool allowManualOverlap = false;

        [Header("Parâmetros do Modo Ring (Anel)")]
        [Range(0.02f, 0.20f)] public float ringRadius = 0.095f;
        [Min(1)] public int piecesInRing = 8;
        [Range(0f, 360f)] public float startAngleDegrees = 0f;
        [Range(0f, 30f)] public float angleJitterDegrees = 5f;
        [Range(0.01f, 0.10f)] public float ringOccupancyRadius = 0.025f;
        public float ringYOffset = 0.015f;

        [Header("Parâmetros do Modo Scatter (Disperso)")]
        [Range(1, 50)] public int scatterDensity = 12;
        [Min(1)] public int maxPiecesBudget = 24;
        public float scatterScale = 0.025f;
        public float minSpacing = 0.025f;
        public float scatterYOffset = 0.015f;

        [Header("Parâmetros do Modo None (Sem Visual)")]
        public string omissionReason = "Ingrediente puramente lógico/sabor";
    }
}
