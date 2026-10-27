#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public enum MigrationStationCategory
    {
        StorageFridge = 1,
        StorageCupboard = 2,
        ToolRack = 3,
        Counter = 4,
        Prep = 5,
        Assembly = 7,
        Oven = 8,
        Packaging = 9,
        Staging = 10,
        SupplierTerminal = 11,
        Phone = 12,
        Receiving = 13,
        Trash = 14,
        TicketBoard = 15,
        Stack = 16
    }

    [Serializable]
    public class ItemScaleRule
    {
        public string nodePrefix;
        public string targetPrefabName;
        public float scaleFactor = 1.0f;
        public Vector3 gripPointOffset = Vector3.zero;
        public Vector3 supportPointOffset = Vector3.zero;
        public Vector3 visualRotationEuler = Vector3.zero;
    }

    [Serializable]
    public class StationNodeMapping
    {
        public int stationId;
        public string stationName;
        public MigrationStationCategory category;
        public string primaryNodeName;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 interactionOffset = new Vector3(0f, 0f, -1.0f);
        public List<Vector3> slotOffsets = new List<Vector3>();
    }

    [CreateAssetMenu(fileName = "PizzeriaFbxImportProfile", menuName = "Scary Party/Pizzeria/FBX Import Profile")]
    public class PizzeriaFbxImportProfile : ScriptableObject
    {
        [Header("Origem do Modelo")]
        public string fbxAssetPath = "Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx";
        public string fbxGuid = "f41c1a5c11441dc458d5fb3728a59962";

        [Header("Normalização Geral de Geometria")]
        [Tooltip("Offset vertical para garantir que o piso do FBX fique normalizado em Y = 0.")]
        public float floorElevationY = 1.45f;

        [Header("Planta em L e Pisos Procedurais")]
        public Rect diningHallRect = Rect.MinMaxRect(0f, -13.2f, 16.4f, 9.2f);
        public Rect kitchenRect = Rect.MinMaxRect(16.4f, -27.6f, 33.0f, 9.2f);
        public float floorThickness = 0.2f;

        [Header("Âncoras do Prédio (Coordenadas Locais com Y normalizado)")]
        public Vector3 entranceAnchorPos = new Vector3(0f, 0f, -1.98f);
        public Vector3 receivingAnchorPos = new Vector3(33.0f, 0f, -27.0f);
        public Vector3 recoveryAnchorPos = new Vector3(5.5f, 0f, -2.5f);
        public Vector3[] playerSpawnPositions = new Vector3[]
        {
            new Vector3(4.0f, 0.1f, -1.5f),
            new Vector3(4.0f, 0.1f, -3.0f),
            new Vector3(2.5f, 0.1f, -1.5f),
            new Vector3(2.5f, 0.1f, -3.0f)
        };

        [Header("Regras de Escala de Itens")]
        public List<ItemScaleRule> itemScaleRules = new List<ItemScaleRule>();

        [Header("Mapeamento Canônico de Estações")]
        public List<StationNodeMapping> stationMappings = new List<StationNodeMapping>();

        [Header("Nós Ignorados")]
        public List<string> ignoredNodeNames = new List<string>
        {
            "paredecomum.019", // Geometria vazia no FBX
            "player"           // Manequim auxiliar do autor 3D
        };

        public static PizzeriaFbxImportProfile CreateDefaultProfile()
        {
            var profile = CreateInstance<PizzeriaFbxImportProfile>();

            // Regras de Escala de Itens Reutilizáveis
            profile.itemScaleRules = new List<ItemScaleRule>
            {
                new ItemScaleRule
                {
                    nodePrefix = "faca",
                    targetPrefabName = "Visual_Knife",
                    scaleFactor = 0.577f, // ~0.30m
                    gripPointOffset = new Vector3(0f, 0f, -0.08f),
                    supportPointOffset = new Vector3(0f, -0.02f, 0f),
                    visualRotationEuler = Vector3.zero
                },
                new ItemScaleRule
                {
                    nodePrefix = "cortador de pizza",
                    targetPrefabName = "Visual_PizzaCutter",
                    scaleFactor = 0.266f, // ~0.25m
                    gripPointOffset = new Vector3(0f, 0f, -0.06f),
                    supportPointOffset = new Vector3(0f, -0.02f, 0f),
                    visualRotationEuler = Vector3.zero
                },
                new ItemScaleRule
                {
                    nodePrefix = "prato/forma",
                    targetPrefabName = "Visual_PizzaPan",
                    scaleFactor = 0.447f, // ~0.42m
                    gripPointOffset = new Vector3(0f, 0f, 0f),
                    supportPointOffset = new Vector3(0f, 0f, 0f),
                    visualRotationEuler = Vector3.zero
                },
                new ItemScaleRule
                {
                    nodePrefix = "deliverybag",
                    targetPrefabName = "Visual_DeliveryBag",
                    scaleFactor = 0.617f, // ~0.50m
                    gripPointOffset = new Vector3(0f, 0f, 0f),
                    supportPointOffset = new Vector3(0f, -0.25f, 0f),
                    visualRotationEuler = Vector3.zero
                },
                new ItemScaleRule
                {
                    nodePrefix = "tabua",
                    targetPrefabName = "Visual_CuttingBoard",
                    scaleFactor = 1.0f,
                    gripPointOffset = Vector3.zero,
                    supportPointOffset = Vector3.zero,
                    visualRotationEuler = Vector3.zero
                },
                new ItemScaleRule
                {
                    nodePrefix = "caixa",
                    targetPrefabName = "Visual_SupplyCrate",
                    scaleFactor = 1.0f,
                    gripPointOffset = Vector3.zero,
                    supportPointOffset = Vector3.zero,
                    visualRotationEuler = Vector3.zero
                }
            };

            // Mapeamentos de Estações (1 a 18)
            profile.stationMappings = new List<StationNodeMapping>
            {
                // Station 1: Geladeira 1 (Storage_Fridge)
                new StationNodeMapping
                {
                    stationId = 1,
                    stationName = "Station_Storage_Fridge",
                    category = MigrationStationCategory.StorageFridge,
                    primaryNodeName = "geladeira.001",
                    localPosition = new Vector3(20.5f, 0f, 8.8f),
                    interactionOffset = new Vector3(0f, 0f, -1.2f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.3f, 0.8f, 0f),
                        new Vector3(0.3f, 0.8f, 0f),
                        new Vector3(-0.3f, 1.3f, 0f),
                        new Vector3(0.3f, 1.3f, 0f)
                    }
                },
                // Station 2: Armário de Massa (Storage_Cupboard)
                new StationNodeMapping
                {
                    stationId = 2,
                    stationName = "Station_Storage_Cupboard",
                    category = MigrationStationCategory.StorageCupboard,
                    primaryNodeName = "armario.001",
                    localPosition = new Vector3(24.0f, 0f, 8.8f),
                    interactionOffset = new Vector3(0f, 0f, -1.2f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(0f, 0.9f, 0f)
                    }
                },
                // Station 3: Suporte de Utensílios (ToolRack)
                new StationNodeMapping
                {
                    stationId = 3,
                    stationName = "Station_ToolRack",
                    category = MigrationStationCategory.ToolRack,
                    primaryNodeName = "suportefaca.001",
                    localPosition = new Vector3(17.2f, 0.8f, 3.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.3f, 0f, 0f),
                        new Vector3(-0.1f, 0f, 0f),
                        new Vector3(0.1f, 0f, 0f),
                        new Vector3(0.3f, 0f, 0f)
                    }
                },
                // Station 4: Balcão de Espera / Neutro
                new StationNodeMapping
                {
                    stationId = 4,
                    stationName = "Station_Counter",
                    category = MigrationStationCategory.Counter,
                    primaryNodeName = "balcao.001",
                    localPosition = new Vector3(14.0f, 0f, -2.0f),
                    interactionOffset = new Vector3(-1.0f, 0f, 0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(0f, 0.85f, -0.4f),
                        new Vector3(0f, 0.85f, 0.4f)
                    }
                },
                // Station 5: Preparo A (Mesa Média 1, Lado Esquerdo)
                new StationNodeMapping
                {
                    stationId = 5,
                    stationName = "Station_Prep_A",
                    category = MigrationStationCategory.Prep,
                    primaryNodeName = "mesa media.001",
                    localPosition = new Vector3(19.0f, 0f, 2.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-1.5f, 0.82f, 0f),
                        new Vector3(-0.5f, 0.82f, 0f)
                    }
                },
                // Station 6: Preparo B (Mesa Média 1, Lado Direito)
                new StationNodeMapping
                {
                    stationId = 6,
                    stationName = "Station_Prep_B",
                    category = MigrationStationCategory.Prep,
                    primaryNodeName = "mesa media.001",
                    localPosition = new Vector3(23.0f, 0f, 2.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(0.5f, 0.82f, 0f),
                        new Vector3(1.5f, 0.82f, 0f)
                    }
                },
                // Station 7: Montagem (Mesa Média 2)
                new StationNodeMapping
                {
                    stationId = 7,
                    stationName = "Station_Assembly",
                    category = MigrationStationCategory.Assembly,
                    primaryNodeName = "mesa media.002",
                    localPosition = new Vector3(21.0f, 0f, -5.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.6f, 0.82f, 0f),
                        new Vector3(0.6f, 0.82f, 0f)
                    }
                },
                // Station 8: Forno 1
                new StationNodeMapping
                {
                    stationId = 8,
                    stationName = "Station_Oven_1",
                    category = MigrationStationCategory.Oven,
                    primaryNodeName = "forno.001",
                    localPosition = new Vector3(31.8f, 0f, 4.0f),
                    interactionOffset = new Vector3(-1.2f, 0f, 0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.2f, 0.85f, -0.3f),
                        new Vector3(-0.2f, 0.85f, 0.3f)
                    }
                },
                // Station 17: Forno 2
                new StationNodeMapping
                {
                    stationId = 17,
                    stationName = "Station_Oven_2",
                    category = MigrationStationCategory.Oven,
                    primaryNodeName = "forno.002",
                    localPosition = new Vector3(31.8f, 0f, 0.5f),
                    interactionOffset = new Vector3(-1.2f, 0f, 0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.2f, 0.85f, -0.3f),
                        new Vector3(-0.2f, 0.85f, 0.3f)
                    }
                },
                // Station 18: Forno 3
                new StationNodeMapping
                {
                    stationId = 18,
                    stationName = "Station_Oven_3",
                    category = MigrationStationCategory.Oven,
                    primaryNodeName = "forno.003",
                    localPosition = new Vector3(31.8f, 0f, -3.0f),
                    interactionOffset = new Vector3(-1.2f, 0f, 0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.2f, 0.85f, -0.3f),
                        new Vector3(-0.2f, 0.85f, 0.3f)
                    }
                },
                // Station 9: Empacotamento (Mesa de Empacotamento)
                new StationNodeMapping
                {
                    stationId = 9,
                    stationName = "Station_Packaging",
                    category = MigrationStationCategory.Packaging,
                    primaryNodeName = "mesa_empacotamento",
                    localPosition = new Vector3(21.0f, 0f, -12.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.5f, 0.82f, 0f),
                        new Vector3(0.5f, 0.82f, 0f)
                    }
                },
                // Station 10: Expedição / Staging
                new StationNodeMapping
                {
                    stationId = 10,
                    stationName = "Station_Staging",
                    category = MigrationStationCategory.Staging,
                    primaryNodeName = "balcao_expedicao",
                    localPosition = new Vector3(15.0f, 0f, -12.0f),
                    interactionOffset = new Vector3(-1.0f, 0f, 0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(0f, 0.85f, -0.6f),
                        new Vector3(0f, 0.85f, -0.2f),
                        new Vector3(0f, 0.85f, 0.2f),
                        new Vector3(0f, 0.85f, 0.6f)
                    }
                },
                // Station 11: Terminal de Compras (SupplierTerminal)
                new StationNodeMapping
                {
                    stationId = 11,
                    stationName = "Station_SupplierTerminal",
                    category = MigrationStationCategory.SupplierTerminal,
                    primaryNodeName = "computador_terminal",
                    localPosition = new Vector3(28.0f, 0.82f, -12.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>()
                },
                // Station 12: Telefone de Pedidos (PhoneStation)
                new StationNodeMapping
                {
                    stationId = 12,
                    stationName = "Station_Phone",
                    category = MigrationStationCategory.Phone,
                    primaryNodeName = "telefone",
                    localPosition = new Vector3(14.0f, 0.85f, -4.0f),
                    interactionOffset = new Vector3(-0.8f, 0f, 0f),
                    slotOffsets = new List<Vector3>()
                },
                // Station 13: Pátio de Recebimento de Cargas (ReceivingStation)
                new StationNodeMapping
                {
                    stationId = 13,
                    stationName = "Station_Receiving",
                    category = MigrationStationCategory.Receiving,
                    primaryNodeName = "porta_fundos",
                    localPosition = new Vector3(32.5f, 0f, -26.0f),
                    interactionOffset = new Vector3(-1.0f, 0f, 0f),
                    slotOffsets = new List<Vector3>()
                },
                // Station 14: Lixeira (TrashStation)
                new StationNodeMapping
                {
                    stationId = 14,
                    stationName = "Station_Trash",
                    category = MigrationStationCategory.Trash,
                    primaryNodeName = "lixeira",
                    localPosition = new Vector3(18.0f, 0f, -25.0f),
                    interactionOffset = new Vector3(0f, 0f, 1.0f),
                    slotOffsets = new List<Vector3>()
                },
                // Station 15: Quadro de Comandas (TicketBoardStation)
                new StationNodeMapping
                {
                    stationId = 15,
                    stationName = "Station_TicketBoard",
                    category = MigrationStationCategory.TicketBoard,
                    primaryNodeName = "quadro_comandas",
                    localPosition = new Vector3(21.0f, 1.6f, -3.5f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>()
                },
                // Station 16: Palete de Caixas / Stack (StackStation)
                new StationNodeMapping
                {
                    stationId = 16,
                    stationName = "Station_Stack",
                    category = MigrationStationCategory.Stack,
                    primaryNodeName = "palete_caixas",
                    localPosition = new Vector3(17.5f, 0f, -12.0f),
                    interactionOffset = new Vector3(0f, 0f, -1.0f),
                    slotOffsets = new List<Vector3>
                    {
                        new Vector3(0f, 0.1f, 0f)
                    }
                }
            };

            return profile;
        }
    }
}
#endif
