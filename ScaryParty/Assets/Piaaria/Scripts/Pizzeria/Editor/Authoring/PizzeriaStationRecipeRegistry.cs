using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public enum PizzeriaPrepPreset
    {
        AllProcesses = 0,
        RollDoughOnly = 1,
        GrateCheeseOnly = 2,
        SliceToppingsOnly = 3,
        Custom = 4
    }

    [Serializable]
    public class PizzeriaStationConversionOptions
    {
        public PizzeriaStationKind kind = PizzeriaStationKind.Counter;
        public string customDisplayName = "";
        public int slotCount = 1;
        public Vector3 operationalForwardLocal = Vector3.forward;
        public float maxInteractionDistance = 3.0f;

        // Preparo
        public PizzeriaPrepPreset prepPreset = PizzeriaPrepPreset.AllProcesses;
        public List<int> customAcceptedProcessIds = new List<int> { 1, 2, 3, 4 };
        public int defaultProcessId = 1;

        // Dispensa / Geladeira
        public PizzeriaStoragePreset storagePreset = PizzeriaStoragePreset.DryPantry;
        public List<int> storageIngredientIds = new List<int>();
        public int initialStockPerIngredient = 10;
        public int capacityPerIngredient = 25;

        // Suporte de utensílios
        public List<int> toolDefIds = new List<int> { 1, 2 };

        // Quadro de comandas
        public int ticketSlotCount = 6;

        // Pilha de caixas
        public ulong stackId = 1;
        public int stackMaxCapacity = 5;

        // Forno
        public float defaultBakeDuration = 15f;
        public float burnGraceDuration = 10f;
    }

    public class PizzeriaStationRecipeDescriptor
    {
        public PizzeriaStationKind kind;
        public string displayName;
        public string actionButtonLabel;
        public string shortDescription;
        public Type runtimeComponentType;
        public int defaultSlotCount;
        public Color gizmoColor;
        public PizzeriaStoragePreset defaultStoragePreset;
    }

    public static class PizzeriaStationRecipeRegistry
    {
        private static readonly List<PizzeriaStationRecipeDescriptor> _recipes = new List<PizzeriaStationRecipeDescriptor>
        {
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Counter,
                displayName = "Bancada de Apoio / Espera",
                actionButtonLabel = "Criar Bancada de Apoio",
                shortDescription = "Superfície neutra para apoiar ingredientes, pizzas ou utensílios (CounterStation).",
                runtimeComponentType = typeof(CounterStation),
                defaultSlotCount = 2,
                gizmoColor = new Color(0.75f, 0.75f, 0.75f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Prep,
                displayName = "Bancada de Preparo",
                actionButtonLabel = "Criar Bancada de Preparo",
                shortDescription = "Permite abrir massa, ralar queijo e fatiar ingredientes com utensílios (PrepStation).",
                runtimeComponentType = typeof(PrepStation),
                defaultSlotCount = 2,
                gizmoColor = new Color(0.25f, 0.85f, 0.45f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Assembly,
                displayName = "Bancada de Montagem",
                actionButtonLabel = "Criar Bancada de Montagem",
                shortDescription = "Combina massa aberta, molho, queijo e coberturas para montar pizzas (AssemblyStation).",
                runtimeComponentType = typeof(AssemblyStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.95f, 0.75f, 0.25f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Oven,
                displayName = "Forno",
                actionButtonLabel = "Criar Forno nos Selecionados",
                shortDescription = "Assa pizzas com luz de fogo, fumaça, áudio e slots acessíveis sem bloqueio de porta (OvenStation).",
                runtimeComponentType = typeof(OvenStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(1.0f, 0.35f, 0.15f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Packaging,
                displayName = "Bancada de Embalagem",
                actionButtonLabel = "Criar Bancada de Embalagem",
                shortDescription = "Empacota pizzas assadas em caixas fechadas ao segurar [F] (PackagingStation).",
                runtimeComponentType = typeof(PackagingStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.85f, 0.55f, 0.25f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Staging,
                displayName = "Bancada de Retirada / Expedição",
                actionButtonLabel = "Criar Bancada de Retirada",
                shortDescription = "Permite etiquetar caixas com o endereço do pedido e retirá-las para entrega (StagingStation).",
                runtimeComponentType = typeof(StagingStation),
                defaultSlotCount = 2,
                gizmoColor = new Color(0.3f, 0.75f, 0.95f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Pantry,
                displayName = "Dispensa (Estoque Seco)",
                actionButtonLabel = "Criar Dispensa",
                shortDescription = "Armazenamento seco (ex: Massa) com slots de estoque configurados no PizzeriaConfig (StorageStation).",
                runtimeComponentType = typeof(StorageStation),
                defaultSlotCount = 1,
                defaultStoragePreset = PizzeriaStoragePreset.DryPantry,
                gizmoColor = new Color(0.85f, 0.70f, 0.45f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Fridge,
                displayName = "Geladeira (Estoque Frio)",
                actionButtonLabel = "Criar Geladeira",
                shortDescription = "Armazenamento refrigerado (Molho, Queijo, Calabresa, Cogumelo) com portas preservadas (StorageStation).",
                runtimeComponentType = typeof(StorageStation),
                defaultSlotCount = 1,
                defaultStoragePreset = PizzeriaStoragePreset.ColdFridge,
                gizmoColor = new Color(0.35f, 0.85f, 1.0f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.ToolRack,
                displayName = "Suporte de Utensílios",
                actionButtonLabel = "Criar Suporte de Utensílios",
                shortDescription = "Guarda facas, raladores e utensílios compartilhados nos slots iniciais (ToolRackStation).",
                runtimeComponentType = typeof(ToolRackStation),
                defaultSlotCount = 4,
                gizmoColor = new Color(0.75f, 0.55f, 0.95f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Phone,
                displayName = "Telefone",
                actionButtonLabel = "Criar Telefone",
                shortDescription = "Atende chamadas e gera pedidos, incluindo PhoneRinger, AudioSource e luz indicadora (PhoneStation).",
                runtimeComponentType = typeof(PhoneStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.2f, 1.0f, 0.6f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.TicketBoard,
                displayName = "Quadro de Comandas",
                actionButtonLabel = "Criar Quadro de Comandas",
                shortDescription = "Exibe as fichas de pedidos ativos com TicketSlots e TicketSheetView (TicketBoardStation).",
                runtimeComponentType = typeof(TicketBoardStation),
                defaultSlotCount = 6,
                gizmoColor = new Color(1.0f, 0.9f, 0.3f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.SupplierTerminal,
                displayName = "Terminal de Fornecedor",
                actionButtonLabel = "Criar Terminal de Fornecedor",
                shortDescription = "Abre o catálogo de compra de insumos para reabastecer o estoque (SupplierTerminal).",
                runtimeComponentType = typeof(SupplierTerminal),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.4f, 0.9f, 0.8f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Receiving,
                displayName = "Área de Recebimento",
                actionButtonLabel = "Criar Área de Recebimento",
                shortDescription = "Recebe entregas de suprimentos comprados no terminal (ReceivingStation + crateSpawnAnchor).",
                runtimeComponentType = typeof(ReceivingStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.55f, 0.85f, 0.35f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.Trash,
                displayName = "Lixeira",
                actionButtonLabel = "Criar Lixeira",
                shortDescription = "Permite descartar itens segurados nas mãos (TrashStation).",
                runtimeComponentType = typeof(TrashStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.9f, 0.3f, 0.3f, 0.9f)
            },
            new PizzeriaStationRecipeDescriptor
            {
                kind = PizzeriaStationKind.BoxStack,
                displayName = "Pilha de Caixas",
                actionButtonLabel = "Criar Pilha de Caixas",
                shortDescription = "Empilha caixas de pizza prontas e etiquetadas para coleta rápida (StackStation).",
                runtimeComponentType = typeof(StackStation),
                defaultSlotCount = 1,
                gizmoColor = new Color(0.9f, 0.65f, 0.35f, 0.9f)
            }
        };

        public static IReadOnlyList<PizzeriaStationRecipeDescriptor> AllRecipes => _recipes;

        public static PizzeriaStationRecipeDescriptor GetDescriptor(PizzeriaStationKind kind)
        {
            for (int i = 0; i < _recipes.Count; i++)
            {
                if (_recipes[i].kind == kind)
                    return _recipes[i];
            }
            return _recipes[0];
        }

        public static PizzeriaStationConversionOptions CreateDefaultOptions(PizzeriaStationKind kind, PizzeriaConfig config)
        {
            var desc = GetDescriptor(kind);
            var opts = new PizzeriaStationConversionOptions
            {
                kind = kind,
                customDisplayName = desc.displayName,
                slotCount = desc.defaultSlotCount,
                operationalForwardLocal = Vector3.forward,
                maxInteractionDistance = config != null && config.interactDistance > 0f ? config.interactDistance : 3.0f,
                storagePreset = desc.defaultStoragePreset,
                defaultBakeDuration = config != null && config.defaultBakeDuration > 0f ? config.defaultBakeDuration : 15f,
                burnGraceDuration = config != null && config.ovenBurnGraceDuration > 0f ? config.ovenBurnGraceDuration : 10f,
                stackMaxCapacity = 6
            };

            if (kind == PizzeriaStationKind.Pantry)
            {
                opts.storagePreset = PizzeriaStoragePreset.DryPantry;
                opts.storageIngredientIds = new List<int> { 1 }; // Massa
                opts.initialStockPerIngredient = config != null ? config.initialDoughStock : 10;
                opts.capacityPerIngredient = config != null ? config.cupboardCapacityPerIngredient : 20;
            }
            else if (kind == PizzeriaStationKind.Fridge)
            {
                opts.storagePreset = PizzeriaStoragePreset.ColdFridge;
                opts.storageIngredientIds = new List<int> { 3, 4, 6, 8 }; // Molho, Queijo, Calabresa, Cogumelo
                opts.initialStockPerIngredient = config != null ? config.initialCheeseStock : 10;
                opts.capacityPerIngredient = config != null ? config.fridgeCapacityPerIngredient : 25;
            }
            else if (kind == PizzeriaStationKind.Prep)
            {
                opts.prepPreset = PizzeriaPrepPreset.AllProcesses;
                opts.customAcceptedProcessIds = new List<int> { 1, 2, 3, 4 };
                opts.defaultProcessId = 1;
            }

            return opts;
        }
    }
}
