using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Authoring
{
    public enum PizzeriaStationKind
    {
        None = 0,
        Counter = 1,          // Bancada de apoio/espera
        Prep = 2,             // Bancada de preparo
        Assembly = 3,         // Bancada de montagem
        Oven = 4,             // Forno
        Packaging = 5,        // Bancada de embalagem
        Staging = 6,          // Bancada de retirada
        Pantry = 7,           // Dispensa (StorageStation - seco)
        Fridge = 8,           // Geladeira (StorageStation - frio)
        ToolRack = 9,         // Suporte de utensílios
        Phone = 10,           // Telefone
        TicketBoard = 11,     // Quadro de comandas
        SupplierTerminal = 12,// Terminal de fornecedor
        Receiving = 13,       // Área de recebimento
        Trash = 14,           // Lixeira
        BoxStack = 15         // Pilha de caixas
    }

    public enum PizzeriaStoragePreset
    {
        None = 0,
        DryPantry = 1,
        ColdFridge = 2,
        Custom = 3
    }

    public enum PizzeriaHierarchyCategory
    {
        Unclassified = 0,
        Architecture_Walls = 1,
        Architecture_Walls_Internal = 2,
        Architecture_Walls_External = 3,
        Architecture_Floors = 4,
        Architecture_Ceilings = 5,
        Architecture_Structure = 6,
        Architecture_Openings = 7,
        Equipment = 8,
        Furniture = 9,
        Utensils = 10,
        Decoration = 11,
        Review = 12,
        Anchors = 13
    }

    public enum PizzeriaVisualRole
    {
        None = 0,
        DecorationOnly = 1,
        UtensilVisual = 2,
        IngredientVisual = 3,
        PizzaVisual = 4,
        BoxVisual = 5,
        PanVisual = 6,
        BackpackVisual = 7
    }

    [Serializable]
    public class PizzeriaStationBindingRecord
    {
        public string bindingGuid;
        public string nodeGuid;
        public int stationId;
        public PizzeriaStationKind stationKind;
        public PizzeriaStoragePreset storagePreset;
        public StationView stationComponent;
        public StationDef stationDef;
        public Vector3 operationalForwardLocal = Vector3.forward;
        public float maxInteractionDistance = 3.0f;
        public List<int> configuredIngredientIds = new List<int>();
        public List<int> configuredProcessIds = new List<int>();
        public List<int> configuredToolDefIds = new List<int>();
    }

    /// <summary>
    /// Componente persistido na raiz do prefab editável da pizzaria.
    /// Guarda os vínculos de autoria manual sem referenciar objetos temporários fora do prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class PizzeriaAuthoringBindings : MonoBehaviour
    {
        public const int CurrentToolVersion = 1;

        [Header("Metadados de Autoria")]
        public bool isManualAuthoring = true;
        public int toolVersion = CurrentToolVersion;
        public string sourceModelAssetGuid;
        public string sourceModelAssetPath;
        public float appliedScaleFactor = 1.0f;

        [Header("Status de Âncoras")]
        public bool anchorsVerifiedByUser;
        public bool entranceAnchorProvisional;
        public bool receivingAnchorProvisional;
        public bool recoveryAnchorProvisional;
        public bool playerSpawnsProvisional;

        [Header("Referências Primárias Explícitas")]
        public StorageStation primaryPantryStation;
        public StorageStation primaryFridgeStation;
        public ToolRackStation primaryToolRackStation;
        public StagingStation primaryStagingStation;
        public StackStation primaryBoxStackStation;
        public PhoneStation primaryPhoneStation;
        public TicketBoardStation primaryTicketBoardStation;
        public SupplierTerminal primarySupplierTerminal;
        public ReceivingStation primaryReceivingStation;

        [Header("Estações Configuradas")]
        public List<PizzeriaStationBindingRecord> stationRecords = new List<PizzeriaStationBindingRecord>();

        public PizzeriaStationBindingRecord FindRecordByStation(StationView station)
        {
            if (station == null || stationRecords == null) return null;
            for (int i = 0; i < stationRecords.Count; i++)
            {
                var r = stationRecords[i];
                if (r != null && (r.stationComponent == station || (station.stationId > 0 && r.stationId == station.stationId)))
                    return r;
            }
            return null;
        }

        public PizzeriaStationBindingRecord FindRecordByStationId(int stationId)
        {
            if (stationId <= 0 || stationRecords == null) return null;
            for (int i = 0; i < stationRecords.Count; i++)
            {
                var r = stationRecords[i];
                if (r != null && r.stationId == stationId)
                    return r;
            }
            return null;
        }

        public PizzeriaStationKind GetStationKind(StationView station)
        {
            if (station == null) return PizzeriaStationKind.None;
            var rec = FindRecordByStation(station);
            if (rec != null && rec.stationKind != PizzeriaStationKind.None)
                return rec.stationKind;

            if (station is PrepStation) return PizzeriaStationKind.Prep;
            if (station is AssemblyStation) return PizzeriaStationKind.Assembly;
            if (station is OvenStation) return PizzeriaStationKind.Oven;
            if (station is PackagingStation) return PizzeriaStationKind.Packaging;
            if (station is StagingStation) return PizzeriaStationKind.Staging;
            if (station is ToolRackStation) return PizzeriaStationKind.ToolRack;
            if (station is PhoneStation) return PizzeriaStationKind.Phone;
            if (station is TicketBoardStation) return PizzeriaStationKind.TicketBoard;
            if (station is SupplierTerminal) return PizzeriaStationKind.SupplierTerminal;
            if (station is ReceivingStation) return PizzeriaStationKind.Receiving;
            if (station is TrashStation) return PizzeriaStationKind.Trash;
            if (station is StackStation) return PizzeriaStationKind.BoxStack;
            if (station is CounterStation) return PizzeriaStationKind.Counter;
            if (station is StorageStation storage)
            {
                if (primaryFridgeStation == storage) return PizzeriaStationKind.Fridge;
                if (primaryPantryStation == storage) return PizzeriaStationKind.Pantry;
                if (station.stationId == 1) return PizzeriaStationKind.Fridge;
                if (station.stationId == 2) return PizzeriaStationKind.Pantry;
                return PizzeriaStationKind.Pantry;
            }

            return PizzeriaStationKind.None;
        }

        public int ResolvePrimaryToolRackId(int fallbackId = 3)
        {
            if (primaryToolRackStation != null && primaryToolRackStation.stationId > 0)
                return primaryToolRackStation.stationId;

            if (stationRecords != null)
            {
                for (int i = 0; i < stationRecords.Count; i++)
                {
                    var r = stationRecords[i];
                    if (r != null && r.stationKind == PizzeriaStationKind.ToolRack && r.stationId > 0)
                        return r.stationId;
                }
            }

            var rack = GetComponentInChildren<ToolRackStation>(true);
            if (rack != null && rack.stationId > 0)
                return rack.stationId;

            return fallbackId;
        }

        public int ResolvePrimaryStagingId(int fallbackId = 10)
        {
            if (primaryStagingStation != null && primaryStagingStation.stationId > 0)
                return primaryStagingStation.stationId;

            if (stationRecords != null)
            {
                for (int i = 0; i < stationRecords.Count; i++)
                {
                    var r = stationRecords[i];
                    if (r != null && r.stationKind == PizzeriaStationKind.Staging && r.stationId > 0)
                        return r.stationId;
                }
            }

            var stg = GetComponentInChildren<StagingStation>(true);
            if (stg != null && stg.stationId > 0)
                return stg.stationId;

            return fallbackId;
        }

        public int ResolvePrimaryBoxStackStationId(int fallbackId = 16)
        {
            if (primaryBoxStackStation != null && primaryBoxStackStation.stationId > 0)
                return primaryBoxStackStation.stationId;

            if (stationRecords != null)
            {
                for (int i = 0; i < stationRecords.Count; i++)
                {
                    var r = stationRecords[i];
                    if (r != null && r.stationKind == PizzeriaStationKind.BoxStack && r.stationId > 0)
                        return r.stationId;
                }
            }

            var stk = GetComponentInChildren<StackStation>(true);
            if (stk != null && stk.stationId > 0)
                return stk.stationId;

            return fallbackId;
        }

        public int ResolvePrimaryFridgeId(int fallbackId = 1)
        {
            if (primaryFridgeStation != null && primaryFridgeStation.stationId > 0)
                return primaryFridgeStation.stationId;

            if (stationRecords != null)
            {
                for (int i = 0; i < stationRecords.Count; i++)
                {
                    var r = stationRecords[i];
                    if (r != null && r.stationKind == PizzeriaStationKind.Fridge && r.stationId > 0)
                        return r.stationId;
                }
            }

            return fallbackId;
        }

        public int ResolvePrimaryPantryId(int fallbackId = 2)
        {
            if (primaryPantryStation != null && primaryPantryStation.stationId > 0)
                return primaryPantryStation.stationId;

            if (stationRecords != null)
            {
                for (int i = 0; i < stationRecords.Count; i++)
                {
                    var r = stationRecords[i];
                    if (r != null && r.stationKind == PizzeriaStationKind.Pantry && r.stationId > 0)
                        return r.stationId;
                }
            }

            return fallbackId;
        }

        public void SyncPrimaryStationsFromRecords()
        {
            if (stationRecords == null) return;
            foreach (var r in stationRecords)
            {
                if (r == null || r.stationComponent == null) continue;
                switch (r.stationKind)
                {
                    case PizzeriaStationKind.Pantry:
                        if (primaryPantryStation == null) primaryPantryStation = r.stationComponent as StorageStation;
                        break;
                    case PizzeriaStationKind.Fridge:
                        if (primaryFridgeStation == null) primaryFridgeStation = r.stationComponent as StorageStation;
                        break;
                    case PizzeriaStationKind.ToolRack:
                        if (primaryToolRackStation == null) primaryToolRackStation = r.stationComponent as ToolRackStation;
                        break;
                    case PizzeriaStationKind.Staging:
                        if (primaryStagingStation == null) primaryStagingStation = r.stationComponent as StagingStation;
                        break;
                    case PizzeriaStationKind.BoxStack:
                        if (primaryBoxStackStation == null) primaryBoxStackStation = r.stationComponent as StackStation;
                        break;
                    case PizzeriaStationKind.Phone:
                        if (primaryPhoneStation == null) primaryPhoneStation = r.stationComponent as PhoneStation;
                        break;
                    case PizzeriaStationKind.TicketBoard:
                        if (primaryTicketBoardStation == null) primaryTicketBoardStation = r.stationComponent as TicketBoardStation;
                        break;
                    case PizzeriaStationKind.SupplierTerminal:
                        if (primarySupplierTerminal == null) primarySupplierTerminal = r.stationComponent as SupplierTerminal;
                        break;
                    case PizzeriaStationKind.Receiving:
                        if (primaryReceivingStation == null) primaryReceivingStation = r.stationComponent as ReceivingStation;
                        break;
                }
            }
        }
    }
}
