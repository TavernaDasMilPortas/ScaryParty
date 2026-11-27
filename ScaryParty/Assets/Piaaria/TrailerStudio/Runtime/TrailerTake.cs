using System;
using System.Collections.Generic;
using Cinemachine;
using ScaryParty.City;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.VisualStyles;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Timeline;

namespace Piaaria.TrailerStudio
{
    public enum TrailerTakeId
    {
        T01_CityReveal = 0,
        T02_KitchenTeam = 1,
        T03_Dough = 2,
        T04_Ingredients = 3,
        T05_Assembly = 4,
        T06_Oven = 5,
        T07_Packaging = 6,
        T08_Tickets = 7,
        T09_KitchenChaos = 8,
        T10_BurntPizza = 9,
        T11_Backpack = 10,
        T12_MotorcycleStart = 11,
        T13_CityRide = 12,
        T14_Delivery = 13,
        T15_FinalGag = 14
    }

    public enum TrailerCameraMode
    {
        CameraA = 0,
        CameraB_Alt = 1
    }

    public enum TrailerActorId
    {
        ActorA = 0,
        ActorB = 1,
        ActorC = 2,
        ActorD = 3
    }

    public enum TrailerAnchorKey
    {
        None = 0,
        PREP_DOUGH = 1,
        PREP_CUT = 2,
        ASSEMBLY = 3,
        OVEN_SLOT = 4,
        OVEN_ENTRY = 5,
        PACKAGING = 6,
        TICKETS = 7,
        STORAGE = 8,
        TOOLS = 9,
        DISPATCH = 10,
        BAG = 11,
        ENTRANCE = 12,
        FACADE = 13,
        PARKING = 14,
        STREET_EXIT = 15,
        DELIVERY = 16,
        ROUTE_START = 17,
        ROUTE_END = 18,
        PHONE = 19,
        COUNTER = 20,
        STAGING = 21,
        DISMOUNT_LEFT = 22,
        OVEN_FRONT = 23
    }

    public enum TrailerActionType
    {
        None = 0,
        MoveToAnchor = 1,
        FaceAnchor = 2,
        SetPreparing = 3,
        SetSitting = 4,
        AttachProp = 5,
        PlaceProp = 6,
        SwapProp = 7,
        OvenTransfer = 8,
        PackPizza = 9,
        BagEquip = 10,
        MountMoto = 11,
        DriveMoto = 12,
        PlayReaction = 13,
        PlayFx = 14,
        PlaySound = 15,
        DropProp = 16,
        OvenDoor = 17,
        DismountMoto = 18,
        PostTicket = 19,
        DeliverAtPoint = 20,
        SetPizzaStage = 21,
        StoreBoxInBag = 22,
        TakeBoxFromBag = 23,
        LookAtTarget = 24
    }

    [Serializable]
    public class TrailerActionCue
    {
        public float time;
        public float duration;
        public TrailerActorId actor;
        public TrailerActionType actionType;
        public TrailerAnchorKey anchorKey;
        public TrailerAnchorKey targetAnchorKey;
        public string propKey;
        public string soundKey;
        public string fxKey;
        public bool boolValue;
        public float floatValue;
        public Vector3 vectorValue;
    }

    [Serializable]
    public class TrailerTakeDefinition
    {
        public TrailerTakeId id;
        public string displayName;
        public float actionDuration = 6f;
        public float preRoll = 0.75f;
        public float postRoll = 0.75f;
        public TimelineAsset timelineAsset;
        public bool hasCameraB;
        public Vector3 cameraAStartOffset;
        public Vector3 cameraAEndOffset;
        public float cameraAFov = 45f;
        public Vector3 cameraBOffset;
        public float cameraBFov = 60f;
        public TrailerAnchorKey focusAnchor = TrailerAnchorKey.FACADE;
        public List<TrailerActorId> activeActors = new List<TrailerActorId>();
        public bool showMotorcycle;
        public bool showTrafficDecor;
        public List<TrailerAnchorKey> requiredAnchors = new List<TrailerAnchorKey>();
        public List<TrailerActionCue> cues = new List<TrailerActionCue>();

        public float TotalDuration => preRoll + actionDuration + postRoll;
    }
    public struct TrailerResolvedAnchor
    {
        public TrailerAnchorKey Key;
        public string SourceName;
        public Vector3 ObjectPoint;
        public Quaternion ObjectRotation;
        public Vector3 StandPoint;
        public Quaternion StandRotation;
        public Vector3 FocusPoint;
        public Vector3 FrameForward;
        public Component StationComponent;
        public Transform SlotTransform;
    }

    public sealed class TrailerAnchorEnvironment
    {
        public PizzeriaRoot Pizzeria;
        public PizzeriaFootprint Footprint;
        public PizzeriaAuthoringBindings Bindings;
        public PizzeriaExteriorAnchors ExteriorAnchors;
        public OvenStation Oven;
        public StationDoorPresenter OvenDoorPresenter;
        public TicketBoardStation TicketBoard;
        public DeliveryPoint DeliveryPoint;
        public int DeliveryAddressId;
        public float FloorSurfaceWorldY;

        public readonly Dictionary<TrailerAnchorKey, TrailerResolvedAnchor> Anchors = new Dictionary<TrailerAnchorKey, TrailerResolvedAnchor>();
        public readonly List<Vector3> MotoStartRoute = new List<Vector3>();
        public readonly List<Vector3> CityRideRoute = new List<Vector3>();
        public Vector3 DeliveryCurbBikePos;
        public Quaternion DeliveryCurbBikeRot = Quaternion.identity;

        public bool TryGetAnchor(TrailerAnchorKey key, out TrailerResolvedAnchor anchor)
            => Anchors.TryGetValue(key, out anchor);

        public static bool ResolveAll(
            CityGenerator cityGen,
            out TrailerAnchorEnvironment env,
            out string error)
        {
            env = new TrailerAnchorEnvironment();
            error = string.Empty;

            var pizzerias = UnityEngine.Object.FindObjectsByType<PizzeriaRoot>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (pizzerias == null || pizzerias.Length == 0)
            {
                error = "Nenhuma instÃ¢ncia de PizzeriaRoot ativa encontrada apÃ³s verificaÃ§Ã£o/geraÃ§Ã£o da cidade.";
                return false;
            }
            if (pizzerias.Length > 1)
            {
                error = $"Encontradas {pizzerias.Length} instÃ¢ncias de PizzeriaRoot na cena; esperada exatamente 1.";
                return false;
            }

            PizzeriaRoot root = pizzerias[0];
            env.Pizzeria = root;
            env.Footprint = root.GetComponent<PizzeriaFootprint>();
            env.Bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            root.EnsureAnchorsResolved();

            // Garante pÃ¡tio externo e portinhola de passagem do balcÃ£o offline
            PizzeriaParkingLotBuilder.EnsureBuilt(root);
            CounterPassageDoorController.EnsureCounterPassageDoorsConfigured(root.transform);

            // Desativa a moto autÃ´noma do pÃ¡tio para que apenas a moto controlada pelo TrailerDirector fique visÃ­vel
            var builtBike = root.transform.Find("__PizzeriaExteriorFacilities/ExteriorExitFrame/DeliveryMotorcycle");
            if (builtBike != null)
            {
                builtBike.gameObject.SetActive(false);
            }

            env.ExteriorAnchors = root.GetComponent<PizzeriaExteriorAnchors>();
            env.FloorSurfaceWorldY = env.Footprint != null
                ? env.Footprint.GetFloorSurfaceWorldY()
                : root.transform.TransformPoint(new Vector3(0f, -1.6f, 0f)).y;

            ResolveInteriorStations(env);
            ResolveExteriorAndFacade(env);
            ResolveRoutesAndDelivery(env, cityGen);

            return true;
        }

        private static void ResolveInteriorStations(TrailerAnchorEnvironment env)
        {
            PizzeriaRoot root = env.Pizzeria;
            PizzeriaAuthoringBindings bindings = env.Bindings;

            // PrepStations: separar massa vs corte por bindings ou stationId ordenado
            PrepStation[] preps = root.GetComponentsInChildren<PrepStation>(true);
            Array.Sort(preps, (a, b) => a.stationId.CompareTo(b.stationId));

            PrepStation doughStation = null;
            PrepStation cutStation = null;

            if (bindings != null && bindings.stationRecords != null)
            {
                for (int i = 0; i < bindings.stationRecords.Count; i++)
                {
                    var rec = bindings.stationRecords[i];
                    if (rec == null || !(rec.stationComponent is PrepStation ps)) continue;
                    if (doughStation == null && rec.configuredProcessIds != null && rec.configuredProcessIds.Contains(1))
                        doughStation = ps;
                    else if (cutStation == null && rec.configuredProcessIds != null &&
                             (rec.configuredProcessIds.Contains(3) || rec.configuredProcessIds.Contains(4)))
                        cutStation = ps;
                }
            }

            if (doughStation == null && preps.Length > 0) doughStation = preps[0];
            if (cutStation == null)
            {
                for (int i = 0; i < preps.Length; i++)
                {
                    if (preps[i] != doughStation)
                    {
                        cutStation = preps[i];
                        break;
                    }
                }
                if (cutStation == null) cutStation = doughStation;
            }

            if (doughStation != null)
            {
                env.Anchors[TrailerAnchorKey.PREP_DOUGH] = BuildStationAnchor(env, TrailerAnchorKey.PREP_DOUGH, doughStation, 0, 0.78f);
            }

            if (cutStation != null)
            {
                int cutSlot = (cutStation == doughStation && cutStation.slotAnchors != null && cutStation.slotAnchors.Length > 1) ? 1 : 0;
                env.Anchors[TrailerAnchorKey.PREP_CUT] = BuildStationAnchor(env, TrailerAnchorKey.PREP_CUT, cutStation, cutSlot, 0.78f);
            }

            // Assembly
            AssemblyStation assembly = root.GetComponentInChildren<AssemblyStation>(true);
            if (assembly != null)
                env.Anchors[TrailerAnchorKey.ASSEMBLY] = BuildStationAnchor(env, TrailerAnchorKey.ASSEMBLY, assembly, 0, 0.80f);

            // Oven
            OvenStation oven = root.GetComponentInChildren<OvenStation>(true);
            if (oven != null)
            {
                env.Oven = oven;
                env.OvenDoorPresenter = oven.GetComponent<StationDoorPresenter>();
                if (env.OvenDoorPresenter != null) env.OvenDoorPresenter.SnapClosed();

                var ovenSlot = BuildStationAnchor(env, TrailerAnchorKey.OVEN_SLOT, oven, 0, 1.05f);
                env.Anchors[TrailerAnchorKey.OVEN_SLOT] = ovenSlot;

                Transform entryT = oven.GetEntryAnchor();
                Vector3 entryPos = entryT != null ? entryT.position : ovenSlot.ObjectPoint;
                Vector3 kitchenCenter = assembly != null ? assembly.transform.position : root.transform.position;
                Vector3 fwd = entryPos - ovenSlot.ObjectPoint;
                fwd.y = 0f;
                if (fwd.sqrMagnitude < 0.01f)
                {
                    fwd = kitchenCenter - oven.transform.position;
                    fwd.y = 0f;
                }
                if (fwd.sqrMagnitude < 0.001f) fwd = ovenSlot.FrameForward;
                fwd.Normalize();
                // Garante que aponta para o interior da cozinha, nunca para trás da parede
                Vector3 toKitchen = kitchenCenter - entryPos;
                toKitchen.y = 0f;
                if (toKitchen.sqrMagnitude > 0.05f && Vector3.Dot(fwd, toKitchen.normalized) < -0.15f)
                    fwd = -fwd;

                Vector3 ovenRight = Vector3.Cross(Vector3.up, fwd).normalized;
                Vector3 standPos = SnapToFloor(env, entryPos + fwd * 0.85f - ovenRight * 0.58f);
                Quaternion standRot = LookHorizontal(entryPos - standPos, -fwd);
                env.Anchors[TrailerAnchorKey.OVEN_ENTRY] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.OVEN_ENTRY,
                    SourceName = $"{oven.name}/EntryAnchor",
                    ObjectPoint = entryPos,
                    ObjectRotation = entryT != null ? entryT.rotation : oven.transform.rotation,
                    StandPoint = standPos,
                    StandRotation = standRot,
                    FocusPoint = entryPos + Vector3.up * 0.06f,
                    FrameForward = fwd,
                    StationComponent = oven,
                    SlotTransform = entryT
                };

                env.Anchors[TrailerAnchorKey.OVEN_FRONT] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.OVEN_FRONT,
                    SourceName = $"{oven.name}/Front",
                    ObjectPoint = entryPos,
                    ObjectRotation = oven.transform.rotation,
                    StandPoint = standPos,
                    StandRotation = standRot,
                    FocusPoint = entryPos + Vector3.up * 0.06f,
                    FrameForward = fwd,
                    StationComponent = oven,
                    SlotTransform = entryT
                };
            }

            // Packaging
            PackagingStation packaging = root.GetComponentInChildren<PackagingStation>(true);
            if (packaging != null)
                env.Anchors[TrailerAnchorKey.PACKAGING] = BuildStationAnchor(env, TrailerAnchorKey.PACKAGING, packaging, 0, 0.78f);

            // Staging / Counter
            StagingStation staging = bindings != null && bindings.primaryStagingStation != null
                ? bindings.primaryStagingStation
                : root.GetComponentInChildren<StagingStation>(true);
            if (staging != null)
                env.Anchors[TrailerAnchorKey.STAGING] = BuildStationAnchor(env, TrailerAnchorKey.STAGING, staging, 0, 0.80f);
            else
            {
                CounterStation counterSt = root.GetComponentInChildren<CounterStation>(true);
                if (counterSt != null)
                    env.Anchors[TrailerAnchorKey.STAGING] = BuildStationAnchor(env, TrailerAnchorKey.STAGING, counterSt, 0, 0.80f);
            }

            // Counter & Phone
            CounterStation counterStation = root.GetComponentInChildren<CounterStation>(true);
            if (counterStation != null)
                env.Anchors[TrailerAnchorKey.COUNTER] = BuildStationAnchor(env, TrailerAnchorKey.COUNTER, counterStation, 0, 0.85f);
            else
            {
                CustomerCounterStation custCounter = root.GetComponentInChildren<CustomerCounterStation>(true);
                if (custCounter != null)
                {
                    Vector3 objP = custCounter.transform.position;
                    Vector3 fwd = ResolveWalkableCorridorDirection(env, custCounter, objP, 0.85f, out Vector3 standP);
                    env.Anchors[TrailerAnchorKey.COUNTER] = new TrailerResolvedAnchor
                    {
                        Key = TrailerAnchorKey.COUNTER,
                        SourceName = custCounter.name,
                        ObjectPoint = objP,
                        ObjectRotation = custCounter.transform.rotation,
                        StandPoint = standP,
                        StandRotation = LookHorizontal(objP - standP, -fwd),
                        FocusPoint = objP + Vector3.up * 0.25f,
                        FrameForward = fwd,
                        StationComponent = custCounter
                    };
                }
                else if (env.Anchors.TryGetValue(TrailerAnchorKey.STAGING, out var stgCopy))
                {
                    stgCopy.Key = TrailerAnchorKey.COUNTER;
                    env.Anchors[TrailerAnchorKey.COUNTER] = stgCopy;
                }
            }

            PhoneStation phone = bindings != null && bindings.primaryPhoneStation != null
                ? bindings.primaryPhoneStation
                : root.GetComponentInChildren<PhoneStation>(true);
            if (phone != null)
                env.Anchors[TrailerAnchorKey.PHONE] = BuildStationAnchor(env, TrailerAnchorKey.PHONE, phone, 0, 0.82f);

            // TicketBoard
            TicketBoardStation board = bindings != null && bindings.primaryTicketBoardStation != null
                ? bindings.primaryTicketBoardStation
                : root.GetComponentInChildren<TicketBoardStation>(true);
            if (board != null)
            {
                env.TicketBoard = board;
                var baseTicketAnchor = BuildStationAnchor(env, TrailerAnchorKey.TICKETS, board, 0, 1.05f);
                if (board.ticketSlots != null && board.ticketSlots.Length > 0)
                {
                    Vector3 sum = Vector3.zero;
                    int validSlots = 0;
                    for (int s = 0; s < board.ticketSlots.Length; s++)
                    {
                        if (board.ticketSlots[s] != null)
                        {
                            sum += board.ticketSlots[s].position;
                            validSlots++;
                        }
                    }
                    if (validSlots > 0)
                    {
                        Vector3 avgSlot = sum / validSlots;
                        Vector3 kitchenRef = assembly != null ? assembly.transform.position : root.transform.position;
                        Vector3 toOpenKitchen = kitchenRef - avgSlot;
                        toOpenKitchen.y = 0f;
                        if (toOpenKitchen.sqrMagnitude > 0.05f)
                        {
                            Vector3 boardFwd = baseTicketAnchor.FrameForward;
                            if (Vector3.Dot(boardFwd, toOpenKitchen.normalized) < 0f)
                                boardFwd = -boardFwd;
                            baseTicketAnchor.FrameForward = boardFwd.normalized;
                        }
                        baseTicketAnchor.ObjectPoint = avgSlot;
                        baseTicketAnchor.FocusPoint = avgSlot - Vector3.up * 0.08f;
                        Vector3 boardRight = Vector3.Cross(Vector3.up, baseTicketAnchor.FrameForward).normalized;
                        baseTicketAnchor.StandPoint = SnapToFloor(env, avgSlot + baseTicketAnchor.FrameForward * 0.95f - boardRight * 0.65f);
                        baseTicketAnchor.StandRotation = LookHorizontal(avgSlot - baseTicketAnchor.StandPoint, -baseTicketAnchor.FrameForward);
                    }
                }
                env.Anchors[TrailerAnchorKey.TICKETS] = baseTicketAnchor;
            }

            // Storage & ToolRack
            StorageStation storage = bindings != null && bindings.primaryFridgeStation != null
                ? bindings.primaryFridgeStation
                : root.GetComponentInChildren<StorageStation>(true);
            if (storage != null)
                env.Anchors[TrailerAnchorKey.STORAGE] = BuildStationAnchor(env, TrailerAnchorKey.STORAGE, storage, 0, 0.95f);

            ToolRackStation tools = bindings != null && bindings.primaryToolRackStation != null
                ? bindings.primaryToolRackStation
                : root.GetComponentInChildren<ToolRackStation>(true);
            if (tools != null)
                env.Anchors[TrailerAnchorKey.TOOLS] = BuildStationAnchor(env, TrailerAnchorKey.TOOLS, tools, 0, 0.85f);
        }

        private static void ResolveExteriorAndFacade(TrailerAnchorEnvironment env)
        {
            PizzeriaRoot root = env.Pizzeria;
            PizzeriaFootprint fp = env.Footprint;
            PizzeriaExteriorAnchors ext = env.ExteriorAnchors;

            Vector3 facadeDir = fp != null ? fp.GetEntranceDirectionWorld() : -root.transform.right;
            facadeDir.y = 0f;
            if (facadeDir.sqrMagnitude < 0.001f) facadeDir = Vector3.forward;
            facadeDir.Normalize();

            Transform entranceT = fp != null && fp.entranceAnchor != null ? fp.entranceAnchor : root.entranceAnchor;
            if (entranceT != null)
            {
                Vector3 entPos = SnapToFloor(env, entranceT.position);
                Vector3 facadeFocus = entPos + Vector3.up * 1.6f;
                env.Anchors[TrailerAnchorKey.ENTRANCE] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.ENTRANCE,
                    SourceName = entranceT.name,
                    ObjectPoint = entPos,
                    ObjectRotation = Quaternion.LookRotation(facadeDir, Vector3.up),
                    StandPoint = entPos,
                    StandRotation = Quaternion.LookRotation(facadeDir, Vector3.up),
                    FocusPoint = facadeFocus,
                    FrameForward = facadeDir,
                    SlotTransform = entranceT
                };
                env.Anchors[TrailerAnchorKey.FACADE] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.FACADE,
                    SourceName = $"{root.name}/Facade",
                    ObjectPoint = facadeFocus,
                    ObjectRotation = Quaternion.LookRotation(facadeDir, Vector3.up),
                    StandPoint = SnapToFloor(env, entPos + facadeDir * 2.2f),
                    StandRotation = Quaternion.LookRotation(-facadeDir, Vector3.up),
                    FocusPoint = facadeFocus,
                    FrameForward = facadeDir,
                    SlotTransform = entranceT
                };
            }

            if (ext != null)
            {
                if (ext.dispatch != null)
                {
                    Vector3 dispObj = ext.dispatch.position;
                    Vector3 dispFwd = (ext.streetExit != null ? ext.streetExit.position - dispObj : facadeDir);
                    dispFwd.y = 0f;
                    if (dispFwd.sqrMagnitude < 0.001f) dispFwd = facadeDir;
                    dispFwd.Normalize();

                    Vector3 stand = SnapToFloor(env, dispObj - dispFwd * 0.45f);
                    env.Anchors[TrailerAnchorKey.DISPATCH] = new TrailerResolvedAnchor
                    {
                        Key = TrailerAnchorKey.DISPATCH,
                        SourceName = ext.dispatch.name,
                        ObjectPoint = dispObj + Vector3.up * 0.85f,
                        ObjectRotation = Quaternion.LookRotation(dispFwd, Vector3.up),
                        StandPoint = stand,
                        StandRotation = Quaternion.LookRotation(dispFwd, Vector3.up),
                        FocusPoint = stand + Vector3.up * 1.1f,
                        FrameForward = dispFwd,
                        SlotTransform = ext.dispatch
                    };
                }

                if (ext.motorcycleSpawn_01 != null)
                {
                    Vector3 parkPos = SnapToFloor(env, ext.motorcycleSpawn_01.position);
                    Vector3 parkFwd = ext.motorcycleSpawn_01.forward;
                    parkFwd.y = 0f;
                    if (parkFwd.sqrMagnitude < 0.001f) parkFwd = facadeDir;
                    parkFwd.Normalize();

                    env.Anchors[TrailerAnchorKey.PARKING] = new TrailerResolvedAnchor
                    {
                        Key = TrailerAnchorKey.PARKING,
                        SourceName = ext.motorcycleSpawn_01.name,
                        ObjectPoint = parkPos,
                        ObjectRotation = Quaternion.LookRotation(parkFwd, Vector3.up),
                        StandPoint = SnapToFloor(env, ext.dismountLeft != null ? ext.dismountLeft.position : parkPos - Vector3.Cross(Vector3.up, parkFwd) * 1.35f),
                        StandRotation = Quaternion.LookRotation(parkFwd, Vector3.up),
                        FocusPoint = parkPos + Vector3.up * 0.72f,
                        FrameForward = parkFwd,
                        SlotTransform = ext.motorcycleSpawn_01
                    };
                }

                if (ext.dismountLeft != null)
                {
                    Vector3 dmPos = SnapToFloor(env, ext.dismountLeft.position);
                    Vector3 lookTarget = ext.motorcycleSpawn_01 != null ? ext.motorcycleSpawn_01.position : dmPos + facadeDir;
                    Vector3 bikeFwd = ext.motorcycleSpawn_01 != null ? ext.motorcycleSpawn_01.forward : facadeDir;
                    bikeFwd.y = 0f;
                    if (bikeFwd.sqrMagnitude < 0.001f) bikeFwd = facadeDir;
                    bikeFwd.Normalize();

                    env.Anchors[TrailerAnchorKey.DISMOUNT_LEFT] = new TrailerResolvedAnchor
                    {
                        Key = TrailerAnchorKey.DISMOUNT_LEFT,
                        SourceName = ext.dismountLeft.name,
                        ObjectPoint = dmPos,
                        ObjectRotation = LookHorizontal(lookTarget - dmPos, bikeFwd),
                        StandPoint = dmPos,
                        StandRotation = LookHorizontal(lookTarget - dmPos, bikeFwd),
                        FocusPoint = dmPos + Vector3.up * 1.0f,
                        FrameForward = bikeFwd,
                        SlotTransform = ext.dismountLeft
                    };
                }

                if (ext.streetExit != null)
                {
                    Vector3 exitPos = SnapToFloor(env, ext.streetExit.position);
                    Vector3 exitFwd = ext.streetExit.forward;
                    exitFwd.y = 0f;
                    if (exitFwd.sqrMagnitude < 0.001f) exitFwd = facadeDir;
                    exitFwd.Normalize();

                    env.Anchors[TrailerAnchorKey.STREET_EXIT] = new TrailerResolvedAnchor
                    {
                        Key = TrailerAnchorKey.STREET_EXIT,
                        SourceName = ext.streetExit.name,
                        ObjectPoint = exitPos,
                        ObjectRotation = Quaternion.LookRotation(exitFwd, Vector3.up),
                        StandPoint = exitPos,
                        StandRotation = Quaternion.LookRotation(exitFwd, Vector3.up),
                        FocusPoint = exitPos + Vector3.up * 0.8f,
                        FrameForward = exitFwd,
                        SlotTransform = ext.streetExit
                    };
                }
            }
        }

        private static void ResolveRoutesAndDelivery(TrailerAnchorEnvironment env, CityGenerator cityGen)
        {
            CityGraphPathfinder pathfinder = cityGen != null ? cityGen.GetPathfinder() : null;
            if (pathfinder == null)
                pathfinder = UnityEngine.Object.FindFirstObjectByType<CityGraphPathfinder>();

            Vector3 parkPos = env.Anchors.TryGetValue(TrailerAnchorKey.PARKING, out var pk)
                ? pk.ObjectPoint
                : env.Pizzeria.transform.position;
            Vector3 exitPos = env.Anchors.TryGetValue(TrailerAnchorKey.STREET_EXIT, out var se)
                ? se.ObjectPoint
                : parkPos;
            Vector3 exitFwd = env.Anchors.TryGetValue(TrailerAnchorKey.STREET_EXIT, out se)
                ? se.FrameForward
                : (env.Footprint != null ? env.Footprint.GetEntranceDirectionWorld() : Vector3.forward);

            // Rota T12: Parking -> StreetExit -> eixo da rua conectada
            env.MotoStartRoute.Clear();
            env.MotoStartRoute.Add(parkPos);
            if (Vector3.Distance(parkPos, exitPos) > 0.25f)
                env.MotoStartRoute.Add(exitPos);

            StreetGraph graph = pathfinder != null ? pathfinder.Graph : null;
            if (graph != null && graph.nodes != null && graph.edges != null && graph.edges.Count > 0)
            {
                Vector3 streetEntry = FindClosestPointOnStreetGraph(graph, exitPos + exitFwd * 2.5f, out int bestEdgeIdx);
                streetEntry.y = exitPos.y;
                env.MotoStartRoute.Add(streetEntry);

                if (bestEdgeIdx >= 0 && bestEdgeIdx < graph.edges.Count)
                {
                    var edge = graph.edges[bestEdgeIdx];
                    Vector3 nA = graph.nodes[edge.nodeA].worldPosition;
                    Vector3 nB = graph.nodes[edge.nodeB].worldPosition;
                    Vector3 dirA = (nA - streetEntry);
                    Vector3 dirB = (nB - streetEntry);
                    dirA.y = 0f;
                    dirB.y = 0f;
                    Vector3 chosenEnd = Vector3.Dot(dirA.normalized, exitFwd) >= Vector3.Dot(dirB.normalized, exitFwd) ? nA : nB;
                    Vector3 laneDir = (chosenEnd - streetEntry);
                    laneDir.y = 0f;
                    if (laneDir.sqrMagnitude > 0.1f)
                    {
                        float dist = Mathf.Min(24f, laneDir.magnitude);
                        Vector3 pEnd = streetEntry + laneDir.normalized * dist;
                        pEnd.y = streetEntry.y;
                        env.MotoStartRoute.Add(pEnd);
                    }
                }
            }
            else
            {
                env.MotoStartRoute.Add(exitPos + exitFwd * 18f);
            }

            // Rota T13 e Delivery T14
            DeliveryPoint[] dps = UnityEngine.Object.FindObjectsByType<DeliveryPoint>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            DeliveryPoint chosenDp = null;
            List<Vector3> chosenPath = null;
            float bestScore = float.MinValue;

            Vector3 routeOrigin = env.MotoStartRoute[env.MotoStartRoute.Count - 1];
            if (dps != null && dps.Length > 0)
            {
                Array.Sort(dps, (a, b) => a.AddressId.CompareTo(b.AddressId));
                for (int i = 0; i < dps.Length; i++)
                {
                    var dp = dps[i];
                    if (dp == null) continue;
                    if (pathfinder != null && pathfinder.IsReady &&
                        pathfinder.TryFindPath(routeOrigin, dp.transform.position, out var candidatePath) &&
                        candidatePath != null && candidatePath.Count >= 2)
                    {
                        float pathLen = ComputePathLength(candidatePath);
                        // Prefere rota ~60-95m com pelo menos 1 esquina
                        float score = 100f - Mathf.Abs(pathLen - 75f);
                        if (candidatePath.Count >= 3) score += 25f;
                        if (score > bestScore)
                        {
                            bestScore = score;
                            chosenDp = dp;
                            chosenPath = candidatePath;
                        }
                    }
                    else if (chosenDp == null)
                    {
                        chosenDp = dp;
                    }
                }
            }

            if (chosenDp != null)
            {
                env.DeliveryPoint = chosenDp;
                env.DeliveryAddressId = chosenDp.AddressId;

                Vector3 dpWorld = chosenDp.transform.position;
                Vector3 streetFront = graph != null ? FindClosestPointOnStreetGraph(graph, dpWorld, out _) : (dpWorld + exitFwd * 4f);
                Vector3 toStreet = streetFront - dpWorld;
                toStreet.y = 0f;
                if (toStreet.sqrMagnitude < 0.05f) toStreet = exitFwd;
                toStreet.Normalize();

                Vector3 dpFloor = SnapToFloor(env, dpWorld);
                Vector3 curbBike = SnapToFloor(env, dpWorld + toStreet * 3.6f);
                Vector3 curbTangent = Vector3.Cross(Vector3.up, toStreet).normalized;
                if (curbTangent.sqrMagnitude < 0.01f) curbTangent = Vector3.right;

                env.DeliveryCurbBikePos = curbBike;
                env.DeliveryCurbBikeRot = Quaternion.LookRotation(curbTangent, Vector3.up);

                env.Anchors[TrailerAnchorKey.DELIVERY] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.DELIVERY,
                    SourceName = chosenDp.name,
                    ObjectPoint = dpFloor + Vector3.up * 0.25f,
                    ObjectRotation = Quaternion.LookRotation(toStreet, Vector3.up),
                    StandPoint = SnapToFloor(env, dpFloor + toStreet * 0.9f),
                    StandRotation = Quaternion.LookRotation(-toStreet, Vector3.up),
                    FocusPoint = dpFloor + Vector3.up * 1.15f,
                    FrameForward = toStreet,
                    StationComponent = chosenDp,
                    SlotTransform = chosenDp.transform
                };
            }

            env.CityRideRoute.Clear();
            if (chosenPath != null && chosenPath.Count >= 2)
            {
                float streetY = routeOrigin.y;
                for (int i = 0; i < chosenPath.Count; i++)
                {
                    Vector3 pt = chosenPath[i];
                    pt.y = streetY;
                    if (env.CityRideRoute.Count == 0 || Vector3.Distance(env.CityRideRoute[env.CityRideRoute.Count - 1], pt) > 0.35f)
                        env.CityRideRoute.Add(pt);
                }
            }
            else if (env.MotoStartRoute.Count >= 2)
            {
                env.CityRideRoute.AddRange(env.MotoStartRoute);
            }

            if (env.CityRideRoute.Count >= 2)
            {
                Vector3 startPt = env.CityRideRoute[0];
                Vector3 startFwd = (env.CityRideRoute[1] - startPt);
                startFwd.y = 0f;
                if (startFwd.sqrMagnitude < 0.001f) startFwd = exitFwd;
                startFwd.Normalize();

                env.Anchors[TrailerAnchorKey.ROUTE_START] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.ROUTE_START,
                    SourceName = "CityRideRoute/Start",
                    ObjectPoint = startPt,
                    ObjectRotation = Quaternion.LookRotation(startFwd, Vector3.up),
                    StandPoint = startPt,
                    StandRotation = Quaternion.LookRotation(startFwd, Vector3.up),
                    FocusPoint = startPt + Vector3.up * 0.75f,
                    FrameForward = startFwd
                };

                int last = env.CityRideRoute.Count - 1;
                Vector3 endPt = env.CityRideRoute[last];
                Vector3 endFwd = (endPt - env.CityRideRoute[last - 1]);
                endFwd.y = 0f;
                if (endFwd.sqrMagnitude < 0.001f) endFwd = startFwd;
                endFwd.Normalize();

                env.Anchors[TrailerAnchorKey.ROUTE_END] = new TrailerResolvedAnchor
                {
                    Key = TrailerAnchorKey.ROUTE_END,
                    SourceName = "CityRideRoute/End",
                    ObjectPoint = endPt,
                    ObjectRotation = Quaternion.LookRotation(endFwd, Vector3.up),
                    StandPoint = endPt,
                    StandRotation = Quaternion.LookRotation(endFwd, Vector3.up),
                    FocusPoint = endPt + Vector3.up * 0.75f,
                    FrameForward = endFwd
                };
            }
        }

        private static TrailerResolvedAnchor BuildStationAnchor(
            TrailerAnchorEnvironment env,
            TrailerAnchorKey key,
            StationView station,
            int slotIndex,
            float defaultOffset)
        {
            Transform slotT = (station.slotAnchors != null && slotIndex >= 0 && slotIndex < station.slotAnchors.Length && station.slotAnchors[slotIndex] != null)
                ? station.slotAnchors[slotIndex]
                : null;

            Vector3 objPos = slotT != null ? slotT.position : station.GetSlotPosition(slotIndex);
            Quaternion objRot = slotT != null ? slotT.rotation : station.GetSlotRotation(slotIndex);

            Vector3 standPos;
            Vector3 frameFwd;

            if (NpcWorkPointRegistry.TryResolve(station, slotIndex, 0UL, 0.0, out NpcWorkPoint wp) && wp != null)
            {
                standPos = SnapToFloor(env, wp.transform.position);
                Vector3 toStand = standPos - objPos;
                toStand.y = 0f;
                frameFwd = toStand.sqrMagnitude > 0.01f ? toStand.normalized : (-wp.transform.forward);
            }
            else
            {
                frameFwd = ResolveWalkableCorridorDirection(env, station, objPos, defaultOffset, out standPos);
            }

            frameFwd.y = 0f;
            if (frameFwd.sqrMagnitude < 0.001f) frameFwd = Vector3.forward;
            frameFwd.Normalize();

            Quaternion standRot = LookHorizontal(objPos - standPos, -frameFwd);
            return new TrailerResolvedAnchor
            {
                Key = key,
                SourceName = slotT != null ? $"{station.name}/{slotT.name}" : station.name,
                ObjectPoint = objPos,
                ObjectRotation = objRot,
                StandPoint = standPos,
                StandRotation = standRot,
                FocusPoint = objPos + Vector3.up * 0.12f,
                FrameForward = frameFwd,
                StationComponent = station,
                SlotTransform = slotT
            };
        }

        private static Vector3 ResolveWalkableCorridorDirection(
            TrailerAnchorEnvironment env,
            Component station,
            Vector3 objPos,
            float offset,
            out Vector3 bestStandPos)
        {
            var candidates = new List<Vector3>(5);
            var rec = env.Bindings != null && station is StationView sv ? env.Bindings.FindRecordByStation(sv) : null;
            if (rec != null && rec.operationalForwardLocal.sqrMagnitude > 0.01f)
            {
                Vector3 opWorld = station.transform.TransformDirection(rec.operationalForwardLocal);
                opWorld.y = 0f;
                if (opWorld.sqrMagnitude > 0.01f) candidates.Add(opWorld.normalized);
            }

            Vector3 f = station.transform.forward; f.y = 0f; if (f.sqrMagnitude > 0.01f) { f.Normalize(); candidates.Add(f); candidates.Add(-f); }
            Vector3 r = station.transform.right; r.y = 0f; if (r.sqrMagnitude > 0.01f) { r.Normalize(); candidates.Add(r); candidates.Add(-r); }

            float bestScore = float.MinValue;
            Vector3 bestDir = candidates.Count > 0 ? candidates[0] : Vector3.forward;
            bestStandPos = SnapToFloor(env, objPos + bestDir * offset);

            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 dir = candidates[i];
                Vector3 candPos = objPos + dir * offset;
                candPos.y = env.FloorSurfaceWorldY + 0.05f;

                float score = 0f;
                if (env.Footprint == null || env.Footprint.IsWorldPositionInsideInterior(candPos)) score += 10f;
                if (NavMesh.SamplePosition(candPos, out NavMeshHit navHit, 0.75f, NavMesh.AllAreas))
                {
                    score += 20f - Vector3.Distance(candPos, navHit.position) * 4f;
                    candPos = navHit.position;
                }
                // Evita pontos obstruÃ­dos por collider da prÃ³pria estaÃ§Ã£o
                Vector3 rayFrom = candPos + Vector3.up * 0.9f;
                if (!Physics.CheckSphere(rayFrom, 0.24f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    score += 6f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestDir = dir;
                    bestStandPos = SnapToFloor(env, candPos);
                }
            }

            return bestDir;
        }

        private static Vector3 SnapToFloor(TrailerAnchorEnvironment env, Vector3 pos)
        {
            if (env.Footprint != null && env.Footprint.IsWorldPositionInsideInterior(pos))
            {
                pos.y = env.FloorSurfaceWorldY;
                return pos;
            }

            Vector3 probe = pos + Vector3.up * 2.2f;
            RaycastHit[] hits = Physics.RaycastAll(probe, Vector3.down, 6.5f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = float.MinValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var h = hits[i];
                if (h.collider == null) continue;
                string n = h.collider.name.ToLowerInvariant();
                if (n.Contains("counter") || n.Contains("table") || n.Contains("station") ||
                    n.Contains("bench") || n.Contains("oven") || n.Contains("shelf") || n.Contains("chair"))
                    continue;
                bool isSurface = n.Contains("floor") || n.Contains("piso") || n.Contains("ground") ||
                                 n.Contains("chao") || n.Contains("street") || n.Contains("sidewalk") ||
                                 n.Contains("parkinglotpad") || n.Contains("intersection") || h.normal.y > 0.75f;
                if (!isSurface) continue;
                if (h.point.y > env.FloorSurfaceWorldY + 0.30f) continue;
                if (h.point.y > bestY)
                {
                    bestY = h.point.y;
                    found = true;
                }
            }
            pos.y = found ? bestY : env.FloorSurfaceWorldY;
            return pos;
        }

        private static Quaternion LookHorizontal(Vector3 dir, Vector3 fallback)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = fallback;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            return Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        private static Vector3 FindClosestPointOnStreetGraph(StreetGraph graph, Vector3 worldPos, out int bestEdgeIdx)
        {
            bestEdgeIdx = -1;
            Vector3 best = worldPos;
            float bestDistSqr = float.MaxValue;
            for (int i = 0; i < graph.edges.Count; i++)
            {
                var e = graph.edges[i];
                if (e.nodeA < 0 || e.nodeA >= graph.nodes.Count || e.nodeB < 0 || e.nodeB >= graph.nodes.Count) continue;
                Vector3 a = graph.nodes[e.nodeA].worldPosition;
                Vector3 b = graph.nodes[e.nodeB].worldPosition;
                Vector3 ab = b - a; ab.y = 0f;
                float denom = ab.sqrMagnitude;
                float t = denom > 0.0001f ? Mathf.Clamp01(((worldPos.x - a.x) * ab.x + (worldPos.z - a.z) * ab.z) / denom) : 0f;
                Vector3 p = Vector3.Lerp(a, b, t);
                float d = (worldPos.x - p.x) * (worldPos.x - p.x) + (worldPos.z - p.z) * (worldPos.z - p.z);
                if (d < bestDistSqr)
                {
                    bestDistSqr = d;
                    best = p;
                    bestEdgeIdx = i;
                }
            }
            return best;
        }

        private static float ComputePathLength(List<Vector3> pts)
        {
            float sum = 0f;
            for (int i = 1; i < pts.Count; i++) sum += Vector3.Distance(pts[i - 1], pts[i]);
            return sum;
        }
    }

    public static class TrailerPropFactory
    {
        private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

        public static ItemPresentationCatalog ResolveCatalog(TrailerStudioProfile profile)
        {
            var styled = VisualStyleManager.Active?.activeLibrary?.styledItemCatalogAsset as ItemPresentationCatalog;
            if (styled != null) return styled;
            if (profile != null && profile.itemCatalog != null) return profile.itemCatalog;
            return Resources.Load<ItemPresentationCatalog>("ItemPresentationCatalog");
        }

        public static GameObject CreateProp(
            string propKey,
            TrailerStudioProfile profile,
            Transform parent,
            int deliveryAddressId,
            out ItemPresentationProfile usedProfile)
        {
            usedProfile = null;
            ItemPresentationCatalog catalog = ResolveCatalog(profile);

            if (propKey == "rolling_pin")
            {
                return CreateRollingPin(parent);
            }

            if (propKey != null && propKey.StartsWith("pizza_", StringComparison.Ordinal) && propKey != "pizza_box")
            {
                usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Pizza, 0, false, false) : null;
                GameObject raw = (usedProfile != null && usedProfile.visualPrefab != null)
                    ? UnityEngine.Object.Instantiate(usedProfile.visualPrefab)
                    : (profile != null && profile.pizzaWipPrefab != null ? UnityEngine.Object.Instantiate(profile.pizzaWipPrefab) : null);

                GameObject wrapperGo = WrapVisual("TrailerPizza_" + propKey, raw, usedProfile, parent);
                ApplyPizzaStage(wrapperGo, propKey);
                return wrapperGo;
            }

            GameObject prefab = null;
            VisualSemanticRole role = VisualSemanticRole.FoodGeneric;
            string catId = VisualCategoryIds.Food;

            switch (propKey)
            {
                case "dough_raw":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Dough, 1, false, false) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.doughPrefab;
                    role = VisualSemanticRole.PizzaDough;
                    break;
                case "dough_open":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Dough, 2, false, false) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.openDoughPrefab;
                    role = VisualSemanticRole.PizzaDough;
                    break;
                case "knife":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Other, 2, true, false) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.knifePrefab;
                    role = VisualSemanticRole.KitchenTool;
                    catId = VisualCategoryIds.Props;
                    break;
                case "calabresa_whole":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Topping, 6, false, false) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.calabresaPrefab;
                    role = VisualSemanticRole.PizzaPepperoni;
                    break;
                case "calabresa_sliced":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Topping, 7, false, false) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.slicedCalabresaPrefab;
                    role = VisualSemanticRole.PizzaPepperoni;
                    break;
                case "pizza_box":
                    usedProfile = catalog != null ? catalog.FindProfile(ItemCategory.Box, 0, false, true) : null;
                    prefab = usedProfile?.visualPrefab ?? profile?.pizzaBoxPrefab;
                    role = VisualSemanticRole.PizzaBox;
                    catId = VisualCategoryIds.Props;
                    break;
            }

            GameObject inst = prefab != null ? UnityEngine.Object.Instantiate(prefab) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            if (prefab == null) inst.transform.localScale = Vector3.one * 0.22f;

            VisualStyleManager.ApplyHierarchyOrFallback(inst, role, catId, usedProfile != null ? usedProfile.presentationKey : propKey);
            EnsureUrpMaterialAndTint(inst, propKey);
            GameObject wrapped = WrapVisual("TrailerProp_" + propKey, inst, usedProfile, parent);
            if (propKey == "pizza_box")
            {
                TintAndLabelBox(wrapped, deliveryAddressId);
            }
            return wrapped;
        }

        public static void ApplyPizzaStage(GameObject wrapperGo, string stageKey)
        {
            if (wrapperGo == null) return;
            var wrapper = wrapperGo.GetComponent<ItemVisualWrapper>();
            GameObject target = wrapper != null && wrapper.VisualInstance != null ? wrapper.VisualInstance : wrapperGo;

            NetItemDto dto = BuildStageDto(stageKey);
            PizzaVisualComposer.EnsureDoughBase(target, dto.CookProgress, dto.BurnProgress);
            var comp = PizzaVisualComposition.FromNetItemDto(dto);
            var layout = PizzaLayoutGenerator.Generate(comp, PizzaVisualCatalog.Instance);
            PizzaVisualComposer.ApplyLayout(target, layout, dto);

            Color doughColor = new Color(0.95f, 0.88f, 0.72f);
            Color bakedColor = new Color(0.80f, 0.48f, 0.15f);
            Color burnedColor = new Color(0.12f, 0.10f, 0.10f);
            Color baseTint = dto.CookingStage == (byte)CookingStage.Burned
                ? burnedColor
                : (dto.CookingStage == (byte)CookingStage.Baked ? bakedColor : doughColor);

            Transform doughRoot = target.transform.Find("ModularPizzaVisual/Dough_Root");
            if (doughRoot != null)
            {
                foreach (var r in doughRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    VisualStyleShaderUtility.ApplyColorBlockToSlot(r, 0, baseTint, _mpb);
                }
            }

            target.transform.localScale = dto.CookingStage switch
            {
                (byte)CookingStage.Baked => new Vector3(1.04f, 1.16f, 1.04f),
                (byte)CookingStage.Burned => new Vector3(0.95f, 0.85f, 0.95f),
                _ => Vector3.one
            };
        }

        private static NetItemDto BuildStageDto(string stageKey)
        {
            var dto = new NetItemDto
            {
                ItemId = 1337UL,
                BaseDoughDefinitionId = 2,
                Category = (byte)ItemCategory.Pizza,
                PrepStage = (byte)ProcessingStage.Prepared,
                CookingStage = 0
            };

            switch (stageKey)
            {
                case "pizza_stage_0_base":
                    dto.DistinctIngredientCount = 0;
                    break;
                case "pizza_stage_1_sauce":
                    dto.DistinctIngredientCount = 1;
                    dto.SetIngredient(0, new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.IngredientMask = 1;
                    dto.IngredientCount = 1;
                    break;
                case "pizza_stage_2_cheese":
                    dto.DistinctIngredientCount = 2;
                    dto.SetIngredient(0, new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(1, new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.IngredientMask = 3;
                    dto.IngredientCount = 2;
                    break;
                case "pizza_stage_3_calabresa":
                    dto.DistinctIngredientCount = 3;
                    dto.SetIngredient(0, new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(1, new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(2, new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.IngredientMask = 7;
                    dto.IngredientCount = 3;
                    break;
                case "pizza_stage_4_full":
                    dto.DistinctIngredientCount = 4;
                    dto.SetIngredient(0, new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(1, new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(2, new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(3, new NetIngredientDto { DefinitionId = 9, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.IngredientMask = 15;
                    dto.IngredientCount = 4;
                    break;
                case "pizza_burned":
                    dto.CookingStage = (byte)CookingStage.Burned;
                    dto.CookProgress = 1f;
                    dto.BurnProgress = 1f;
                    goto case "pizza_stage_3_calabresa";
                default: // pizza_baked
                    dto.CookingStage = (byte)CookingStage.Baked;
                    dto.CookProgress = 1f;
                    dto.DistinctIngredientCount = 3;
                    dto.SetIngredient(0, new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(1, new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.SetIngredient(2, new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 });
                    dto.IngredientMask = 7;
                    dto.IngredientCount = 3;
                    break;
            }
            return dto;
        }

        private static GameObject WrapVisual(string name, GameObject visualInst, ItemPresentationProfile profile, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var wrapper = go.AddComponent<ItemVisualWrapper>();
            wrapper.BindVisual(profile, visualInst);
            return go;
        }

        private static GameObject CreateRollingPin(Transform parent)
        {
            var root = new GameObject("TrailerProp_RollingPin");
            if (parent != null) root.transform.SetParent(parent, false);

            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Cylinder";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            body.transform.localScale = new Vector3(0.065f, 0.16f, 0.065f);
            UnityEngine.Object.Destroy(body.GetComponent<Collider>());

            var mr = body.GetComponent<Renderer>();
            if (mr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null) mr.sharedMaterial = new Material(sh);
                VisualStyleShaderUtility.ApplyColorBlockToSlot(mr, 0, new Color(0.76f, 0.56f, 0.34f), _mpb);
            }
            return root;
        }

        public static void EnsureUrpMaterialAndTint(GameObject root, string propKey)
        {
            if (root == null) return;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            Color? desiredTint = propKey switch
            {
                "pizza_box" => new Color(0.80f, 0.60f, 0.38f),
                "dough_raw" => new Color(0.95f, 0.87f, 0.70f),
                "dough_open" => new Color(0.96f, 0.89f, 0.73f),
                "calabresa_whole" => new Color(0.68f, 0.18f, 0.15f),
                "calabresa_sliced" => new Color(0.75f, 0.22f, 0.18f),
                _ => null
            };

            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r is ParticleSystemRenderer) continue;
                if (r.GetComponent<TMPro.TextMeshPro>() != null) continue;

                bool brokenMat = r.sharedMaterial == null ||
                                 r.sharedMaterial.shader == null ||
                                 r.sharedMaterial.shader.name.Contains("InternalError") ||
                                 r.sharedMaterial.shader.name == "Standard";
                if (brokenMat && urpLit != null)
                {
                    var mat = new Material(urpLit);
                    mat.color = desiredTint ?? new Color(0.78f, 0.78f, 0.78f);
                    r.sharedMaterial = mat;
                }

                if (desiredTint.HasValue)
                {
                    VisualStyleShaderUtility.ApplyColorBlockToSlot(r, 0, desiredTint.Value, _mpb);
                }
            }
        }

        private static void TintAndLabelBox(GameObject boxRoot, int addressId)
        {
            EnsureUrpMaterialAndTint(boxRoot, "pizza_box");

            Transform anchor = ItemVisualWrapper.FindDeepChild(boxRoot.transform, "BoxLabelAnchor") ?? boxRoot.transform;
            if (anchor.Find("BoxLabel") == null)
            {
                var labelGo = new GameObject("BoxLabel");
                labelGo.transform.SetParent(anchor, false);
                labelGo.transform.localPosition = anchor == boxRoot.transform ? new Vector3(0f, 0.055f, 0f) : Vector3.zero;
                labelGo.transform.localRotation = anchor == boxRoot.transform ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                labelGo.transform.localScale = Vector3.one * 0.08f;
                var tmp = labelGo.AddComponent<TMPro.TextMeshPro>();
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                tmp.fontSize = 2.4f;
                tmp.color = Color.black;
                tmp.text = addressId >= 0 ? $"<b>{AddressFormat.Display(addressId)}</b>\n<size=65%>Calabresa</size>" : "<b>PIARIA</b>";
                var rect = labelGo.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(3.5f, 3.5f);
            }
        }
    }
}
