using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Layout puro de um pavimento individual.
    /// Contém os módulos alocados para cada uma das 4 fachadas do andar.
    /// </summary>
    [Serializable]
    public class FloorLayout
    {
        public int floorIndex;
        public List<CityModuleId> frontModules = new List<CityModuleId>();
        public List<CityModuleId> backModules = new List<CityModuleId>();
        public List<CityModuleId> leftModules = new List<CityModuleId>();
        public List<CityModuleId> rightModules = new List<CityModuleId>();
        public List<bool> frontAwnings = new List<bool>();
    }

    /// <summary>
    /// Estrutura de dados pura representando a composição completa de um edifício modular.
    /// Desacoplada da cena para permitir planejamento, testes de contenção e determinismo puro.
    /// </summary>
    [Serializable]
    public class BuildingLayout
    {
        [Header("Identificação")]
        public string buildingId;
        public int blockId = 0;
        public int buildingIndex = 0;
        public int localSeed = 0;
        public ZoneType zone = ZoneType.Commercial;
        public ModularBuildingRecipe recipe = ModularBuildingRecipe.Custom;

        [Header("Transformação no Mundo")]
        [Tooltip("Posição da base do centro da fachada frontal (piso térreo).")]
        public Vector3 worldPosition = Vector3.zero;

        [Tooltip("Rotação do edifício. O vetor forward aponta para a rua (+Z).")]
        public Quaternion worldRotation = Quaternion.identity;

        [Header("Dimensões Estruturais (em Módulos de 2m)")]
        [Tooltip("Largura da fachada em módulos (Largura em metros = widthModules * 2m).")]
        public int widthModules = 3; // 6m

        [Tooltip("Profundidade lateral em módulos (Profundidade em metros = depthModules * 2m).")]
        public int depthModules = 3; // 6m

        [Tooltip("Número de pavimentos habitáveis (3m cada).")]
        public int floorCount = 1;

        [Header("Composição dos Andares")]
        public List<FloorLayout> floors = new List<FloorLayout>();

        [Header("Âncoras de Navegação (Mundo)")]
        public Vector3 entranceWorldPos;
        public Vector3 approachWorldPos;
        public Vector3 interiorWorldPos;
        public Vector3 deliveryWorldPos;
        public Vector3 entranceForwardWorld;

        [Header("Visual")]
        public Color buildingColor = Color.white;

        // Propriedades calculadas em metros
        public float WidthMeters => widthModules * 2.0f;
        public float DepthMeters => depthModules * 2.0f;
        public float WallHeightMeters => floorCount * 3.0f;
        public float TotalHeightMeters => WallHeightMeters + 0.2f + 0.4f; // Paredes + Cobertura E (0.2m) + Platibanda F (0.4m)

        /// <summary>
        /// Retorna os 4 cantos da base do edifício no plano XZ em coordenadas de mundo.
        /// </summary>
        public Vector3[] GetBaseCornersWorld()
        {
            float halfW = WidthMeters * 0.5f;
            float depth = DepthMeters;

            // No espaço local do edifício, a fachada frontal está em Z = 0, estendendo-se de X = -halfW a +halfW.
            // O fundo está em Z = -depth.
            Vector3[] localCorners = new Vector3[4]
            {
                new Vector3(-halfW, 0f, 0f),      // Front-Left
                new Vector3(halfW, 0f, 0f),       // Front-Right
                new Vector3(halfW, 0f, -depth),   // Back-Right
                new Vector3(-halfW, 0f, -depth)   // Back-Left
            };

            Vector3[] worldCorners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                worldCorners[i] = worldPosition + (worldRotation * localCorners[i]);
            }

            return worldCorners;
        }

        /// <summary>
        /// Retorna o centro geométrico da base no mundo.
        /// </summary>
        public Vector3 GetBaseCenterWorld()
        {
            Vector3 localCenter = new Vector3(0f, 0f, -DepthMeters * 0.5f);
            return worldPosition + (worldRotation * localCenter);
        }

        /// <summary>
        /// Retorna a OBB 2D para testes de SAT e colisão contra outros lotes e reservas.
        /// </summary>
        public BlockFiller.OBB2D GetOBB2D(float safetyMargin = 0f)
        {
            Vector3 center = GetBaseCenterWorld();
            Vector3 fwd = worldRotation * Vector3.forward;
            Vector3 right = worldRotation * Vector3.right;

            Vector2 center2D = new Vector2(center.x, center.z);
            Vector2 right2D = new Vector2(right.x, right.z).normalized;
            Vector2 fwd2D = new Vector2(fwd.x, fwd.z).normalized;

            float halfW = Mathf.Max(0.1f, (WidthMeters * 0.5f) - (safetyMargin * 0.5f));
            float halfD = Mathf.Max(0.1f, (DepthMeters * 0.5f) - (safetyMargin * 0.5f));

            return new BlockFiller.OBB2D(center2D, new Vector2(halfW, halfD), right2D, fwd2D);
        }
    }
}
