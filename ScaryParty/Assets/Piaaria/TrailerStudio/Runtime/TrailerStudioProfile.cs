using System.Collections.Generic;
using ScaryParty.Pizzeria.Presentation;
using UnityEngine;

namespace Piaaria.TrailerStudio
{
    [CreateAssetMenu(fileName = "TrailerStudioProfile", menuName = "P.I.A.R.I.A/Trailer Studio Profile")]
    public class TrailerStudioProfile : ScriptableObject
    {
        [Header("Environment & Seed")]
        public int seed = 20261125;
        public float defaultLightingHour = 19f;
        public CityConfig cityConfig;
        public GameObject pizzeriaPrefab;

        [Header("Cast & Vehicles")]
        public GameObject characterVisualPrefab;
        public GameObject motorcyclePrefab;

        [Header("Props & Catalog")]
        public GameObject doughPrefab;
        public GameObject pizzaWipPrefab;
        public GameObject pizzaBoxPrefab;
        public GameObject knifePrefab;
        public GameObject calabresaPrefab;
        public GameObject cheesePrefab;
        public ItemPresentationCatalog itemCatalog;
        public GameObject openDoughPrefab;
        public GameObject saucePrefab;
        public GameObject gratedCheesePrefab;
        public GameObject slicedCalabresaPrefab;
        public GameObject deliveryBagVisualPrefab;
        public Color[] actorColors = new Color[]
        {
            new Color(0.20f, 0.55f, 0.95f, 1f), // A: Azul
            new Color(0.92f, 0.25f, 0.25f, 1f), // B: Vermelho
            new Color(0.22f, 0.78f, 0.38f, 1f), // C: Verde
            new Color(0.95f, 0.72f, 0.18f, 1f)  // D: Amarelo/Laranja
        };

        [Header("Takes (T01 - T15)")]
        public List<TrailerTakeDefinition> takes = new List<TrailerTakeDefinition>();

        public TrailerTakeDefinition GetTake(TrailerTakeId id)
        {
            for (int i = 0; i < takes.Count; i++)
            {
                if (takes[i] != null && takes[i].id == id)
                    return takes[i];
            }
            return null;
        }

        /// <summary>
        /// Preenche de forma não-destrutiva metadados, câmeras zeradas e cues ausentes/incompletos.
        /// Preserva ajustes manuais já salvos pelo usuário.
        /// </summary>
        public void EnsureRuntimeDefaults()
        {
            if (takes == null) takes = new List<TrailerTakeDefinition>();
            for (int i = 0; i < takes.Count; i++)
            {
                var def = takes[i];
                if (def != null) ApplyTakeDefaultsIfMissing(def);
            }
        }

        public static void ApplyTakeDefaultsIfMissing(TrailerTakeDefinition def)
        {
            if (def == null) return;
            def.preRoll = 0.75f;
            def.postRoll = 0.75f;

            switch (def.id)
            {
                case TrailerTakeId.T01_CityReveal:
                    SetMetadataIfEmpty(def, 8f, false, 58f, 60f, TrailerAnchorKey.FACADE,
                        new[] { TrailerActorId.ActorD }, true, true,
                        new[] { TrailerAnchorKey.FACADE, TrailerAnchorKey.ENTRANCE, TrailerAnchorKey.PARKING });
                    SetCameraIfZero(def, new Vector3(-6.0f, 10.5f, 14.5f), new Vector3(3.2f, 2.6f, 9.8f), Vector3.zero);
                    break;
                case TrailerTakeId.T02_KitchenTeam:
                    SetMetadataIfEmpty(def, 10f, true, 60f, 68f, TrailerAnchorKey.ASSEMBLY,
                        new[] { TrailerActorId.ActorA, TrailerActorId.ActorB, TrailerActorId.ActorC, TrailerActorId.ActorD }, false, false,
                        new[] { TrailerAnchorKey.PREP_DOUGH, TrailerAnchorKey.ASSEMBLY, TrailerAnchorKey.OVEN_SLOT, TrailerAnchorKey.PACKAGING, TrailerAnchorKey.TICKETS, TrailerAnchorKey.STAGING });
                    SetCameraIfZero(def, new Vector3(-2.6f, 1.85f, 4.2f), new Vector3(2.4f, 1.55f, 4.2f), new Vector3(0f, 1.52f, 0.25f));
                    break;
                case TrailerTakeId.T03_Dough:
                    SetMetadataIfEmpty(def, 6f, true, 44f, 44f, TrailerAnchorKey.PREP_DOUGH,
                        new[] { TrailerActorId.ActorA }, false, false,
                        new[] { TrailerAnchorKey.PREP_DOUGH });
                    SetCameraIfZero(def, new Vector3(1.25f, 0.72f, 0.28f), new Vector3(0.98f, 0.62f, 0.22f), new Vector3(0f, 1.48f, -0.10f));
                    break;
                case TrailerTakeId.T04_Ingredients:
                    SetMetadataIfEmpty(def, 6f, false, 44f, 45f, TrailerAnchorKey.PREP_CUT,
                        new[] { TrailerActorId.ActorA }, false, false,
                        new[] { TrailerAnchorKey.PREP_CUT });
                    SetCameraIfZero(def, new Vector3(-1.22f, 0.70f, 0.26f), new Vector3(-0.95f, 0.60f, 0.20f), Vector3.zero);
                    break;
                case TrailerTakeId.T05_Assembly:
                    SetMetadataIfEmpty(def, 8f, true, 42f, 65f, TrailerAnchorKey.ASSEMBLY,
                        new[] { TrailerActorId.ActorB }, false, false,
                        new[] { TrailerAnchorKey.ASSEMBLY });
                    SetCameraIfZero(def, new Vector3(0.04f, 1.72f, -0.08f), new Vector3(0.04f, 1.48f, -0.08f), new Vector3(0f, 1.50f, 0.18f));
                    break;
                case TrailerTakeId.T06_Oven:
                    SetMetadataIfEmpty(def, 6f, false, 46f, 60f, TrailerAnchorKey.OVEN_ENTRY,
                        new[] { TrailerActorId.ActorC }, false, false,
                        new[] { TrailerAnchorKey.OVEN_ENTRY, TrailerAnchorKey.OVEN_SLOT, TrailerAnchorKey.OVEN_FRONT });
                    SetCameraIfZero(def, new Vector3(0.68f, 0.42f, 1.85f), new Vector3(0.42f, 0.30f, 1.42f), Vector3.zero);
                    break;
                case TrailerTakeId.T07_Packaging:
                    SetMetadataIfEmpty(def, 6f, true, 46f, 52f, TrailerAnchorKey.PACKAGING,
                        new[] { TrailerActorId.ActorC }, false, false,
                        new[] { TrailerAnchorKey.PACKAGING, TrailerAnchorKey.DISPATCH });
                    SetCameraIfZero(def, new Vector3(1.32f, 0.75f, 0.30f), new Vector3(1.12f, 0.68f, 0.26f), new Vector3(-1.28f, 0.82f, 0.32f));
                    break;
                case TrailerTakeId.T08_Tickets:
                    SetMetadataIfEmpty(def, 5f, false, 46f, 60f, TrailerAnchorKey.TICKETS,
                        new[] { TrailerActorId.ActorD }, false, false,
                        new[] { TrailerAnchorKey.TICKETS, TrailerAnchorKey.ASSEMBLY });
                    SetCameraIfZero(def, new Vector3(0.28f, -0.04f, 1.28f), new Vector3(0.12f, -0.02f, 0.92f), Vector3.zero);
                    break;
                case TrailerTakeId.T09_KitchenChaos:
                    SetMetadataIfEmpty(def, 10f, true, 58f, 68f, TrailerAnchorKey.ASSEMBLY,
                        new[] { TrailerActorId.ActorA, TrailerActorId.ActorB, TrailerActorId.ActorC, TrailerActorId.ActorD }, false, false,
                        new[] { TrailerAnchorKey.STORAGE, TrailerAnchorKey.PREP_CUT, TrailerAnchorKey.ASSEMBLY, TrailerAnchorKey.OVEN_SLOT, TrailerAnchorKey.PACKAGING, TrailerAnchorKey.TICKETS, TrailerAnchorKey.STAGING });
                    SetCameraIfZero(def, new Vector3(1.75f, 1.15f, 2.35f), new Vector3(1.55f, 1.05f, 2.15f), new Vector3(0f, 1.52f, 0.22f));
                    break;
                case TrailerTakeId.T10_BurntPizza:
                    SetMetadataIfEmpty(def, 6f, false, 48f, 60f, TrailerAnchorKey.OVEN_ENTRY,
                        new[] { TrailerActorId.ActorB, TrailerActorId.ActorC }, false, false,
                        new[] { TrailerAnchorKey.OVEN_ENTRY, TrailerAnchorKey.OVEN_SLOT, TrailerAnchorKey.OVEN_FRONT, TrailerAnchorKey.ASSEMBLY });
                    SetCameraIfZero(def, new Vector3(0.75f, 0.48f, 1.95f), new Vector3(1.05f, 0.68f, 2.35f), Vector3.zero);
                    break;
                case TrailerTakeId.T11_Backpack:
                    SetMetadataIfEmpty(def, 6f, false, 52f, 60f, TrailerAnchorKey.DISPATCH,
                        new[] { TrailerActorId.ActorD }, false, false,
                        new[] { TrailerAnchorKey.DISPATCH, TrailerAnchorKey.ENTRANCE });
                    SetCameraIfZero(def, new Vector3(2.15f, 0.62f, 1.15f), new Vector3(1.85f, 0.68f, -0.45f), Vector3.zero);
                    break;
                case TrailerTakeId.T12_MotorcycleStart:
                    SetMetadataIfEmpty(def, 8f, false, 55f, 60f, TrailerAnchorKey.PARKING,
                        new[] { TrailerActorId.ActorD }, true, false,
                        new[] { TrailerAnchorKey.PARKING, TrailerAnchorKey.DISMOUNT_LEFT, TrailerAnchorKey.STREET_EXIT });
                    SetCameraIfZero(def, new Vector3(-2.55f, 0.45f, 2.45f), new Vector3(-1.85f, 0.65f, 1.95f), Vector3.zero);
                    break;
                case TrailerTakeId.T13_CityRide:
                    SetMetadataIfEmpty(def, 10f, true, 58f, 60f, TrailerAnchorKey.ROUTE_START,
                        new[] { TrailerActorId.ActorD }, true, true,
                        new[] { TrailerAnchorKey.ROUTE_START, TrailerAnchorKey.ROUTE_END });
                    SetCameraIfZero(def, new Vector3(3.40f, 1.15f, -1.60f), new Vector3(3.40f, 1.15f, -1.60f), new Vector3(0.55f, 1.55f, -4.10f));
                    break;
                case TrailerTakeId.T14_Delivery:
                    SetMetadataIfEmpty(def, 8f, false, 50f, 60f, TrailerAnchorKey.DELIVERY,
                        new[] { TrailerActorId.ActorC, TrailerActorId.ActorD }, true, true,
                        new[] { TrailerAnchorKey.DELIVERY });
                    SetCameraIfZero(def, new Vector3(2.25f, 0.85f, 2.65f), new Vector3(1.65f, 0.72f, 2.15f), Vector3.zero);
                    break;
                case TrailerTakeId.T15_FinalGag:
                    SetMetadataIfEmpty(def, 6f, false, 48f, 60f, TrailerAnchorKey.DISPATCH,
                        new[] { TrailerActorId.ActorD }, false, false,
                        new[] { TrailerAnchorKey.DISPATCH, TrailerAnchorKey.ENTRANCE });
                    SetCameraIfZero(def, new Vector3(1.45f, 0.58f, 2.25f), new Vector3(1.15f, 0.48f, 1.85f), Vector3.zero);
                    break;
            }

            if (IsCueListMissingOrIncomplete(def))
            {
                def.cues = BuildCanonicalCues(def.id);
            }
        }

        private static void SetMetadataIfEmpty(
            TrailerTakeDefinition def,
            float actionDuration,
            bool hasCameraB,
            float fovA,
            float fovB,
            TrailerAnchorKey focusAnchor,
            TrailerActorId[] actors,
            bool showMotorcycle,
            bool showTrafficDecor,
            TrailerAnchorKey[] required)
        {
            if (def.actionDuration <= 0.1f) def.actionDuration = actionDuration;
            def.hasCameraB = hasCameraB;
            if (def.cameraAFov <= 1f) def.cameraAFov = fovA;
            if (def.cameraBFov <= 1f) def.cameraBFov = fovB;
            if (def.focusAnchor == TrailerAnchorKey.None) def.focusAnchor = focusAnchor;
            if (def.activeActors == null || def.activeActors.Count == 0)
            {
                def.activeActors = new List<TrailerActorId>(actors);
                def.showMotorcycle = showMotorcycle;
                def.showTrafficDecor = showTrafficDecor;
            }
            if (def.requiredAnchors == null || def.requiredAnchors.Count == 0)
            {
                def.requiredAnchors = new List<TrailerAnchorKey>(required);
            }
        }

        public void ResetCamerasToCanonicalDefaults()
        {
            if (takes == null) return;
            for (int i = 0; i < takes.Count; i++)
            {
                var def = takes[i];
                if (def == null) continue;
                def.cameraAStartOffset = Vector3.zero;
                def.cameraAEndOffset = Vector3.zero;
                def.cameraBOffset = Vector3.zero;
                def.cameraAFov = 0f;
                def.cameraBFov = 0f;
                def.activeActors = null;
                ApplyTakeDefaultsIfMissing(def);
            }
        }

        private static void SetCameraIfZero(TrailerTakeDefinition def, Vector3 startA, Vector3 endA, Vector3 offsetB)
        {
            if (def.cameraAStartOffset == Vector3.zero) def.cameraAStartOffset = startA;
            if (def.cameraAEndOffset == Vector3.zero) def.cameraAEndOffset = endA;
            if (def.hasCameraB && def.cameraBOffset == Vector3.zero) def.cameraBOffset = offsetB;
        }

        private static bool IsCueListMissingOrIncomplete(TrailerTakeDefinition def)
        {
            if (def.cues == null || def.cues.Count == 0) return true;
            // A auditoria de c9fccd25 deixou apenas 2 cues genéricos sem props em T03, T07 e T12.
            if (def.cues.Count <= 2)
            {
                bool hasVisualSwapOrMoto = false;
                for (int i = 0; i < def.cues.Count; i++)
                {
                    var act = def.cues[i].actionType;
                    if (act == TrailerActionType.SwapProp || act == TrailerActionType.PackPizza ||
                        act == TrailerActionType.DriveMoto || act == TrailerActionType.MountMoto)
                    {
                        hasVisualSwapOrMoto = true;
                        break;
                    }
                }
                if (!hasVisualSwapOrMoto) return true;
            }
            return false;
        }

        public static List<TrailerActionCue> BuildCanonicalCues(TrailerTakeId id)
        {
            var list = new List<TrailerActionCue>();
            switch (id)
            {
                case TrailerTakeId.T01_CityReveal:
                    list.Add(new TrailerActionCue { time = 4.6f, duration = 3.0f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.ENTRANCE, floatValue = 2.0f });
                    list.Add(new TrailerActionCue { time = 7.7f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.FACADE });
                    break;

                case TrailerTakeId.T02_KitchenTeam:
                    // A: massa
                    list.Add(new TrailerActionCue { time = 0.3f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 4.6f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SwapProp, anchorKey = TrailerAnchorKey.PREP_DOUGH, propKey = "dough_open" });
                    list.Add(new TrailerActionCue { time = 5.0f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 5.2f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PREP_DOUGH, propKey = "dough_open" });
                    list.Add(new TrailerActionCue { time = 5.6f, duration = 2.2f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.STAGING, floatValue = 2.0f });
                    list.Add(new TrailerActionCue { time = 8.0f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.STAGING });
                    // B: montagem
                    list.Add(new TrailerActionCue { time = 0.5f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 2.2f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_2_cheese" });
                    list.Add(new TrailerActionCue { time = 4.8f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_3_calabresa" });
                    list.Add(new TrailerActionCue { time = 7.2f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    // C: forno -> embalagem
                    list.Add(new TrailerActionCue { time = 0.6f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenDoor, anchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.0f, duration = 0.45f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenTransfer, anchorKey = TrailerAnchorKey.OVEN_SLOT, targetAnchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = false });
                    list.Add(new TrailerActionCue { time = 1.6f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenDoor, anchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = false });
                    list.Add(new TrailerActionCue { time = 2.0f, duration = 1.6f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.PACKAGING, floatValue = 2.0f });
                    list.Add(new TrailerActionCue { time = 3.7f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.PACKAGING });
                    list.Add(new TrailerActionCue { time = 4.1f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 6.2f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PackPizza, anchorKey = TrailerAnchorKey.PACKAGING });
                    list.Add(new TrailerActionCue { time = 6.8f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 7.2f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PACKAGING, propKey = "pizza_box" });
                    // D: comanda -> apoio
                    list.Add(new TrailerActionCue { time = 1.2f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PostTicket, anchorKey = TrailerAnchorKey.TICKETS, floatValue = 1f });
                    list.Add(new TrailerActionCue { time = 3.2f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PlayReaction, fxKey = "OrderInspected" });
                    list.Add(new TrailerActionCue { time = 5.4f, duration = 2.4f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.COUNTER, floatValue = 2.1f });
                    list.Add(new TrailerActionCue { time = 8.0f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.ASSEMBLY });
                    break;

                case TrailerTakeId.T03_Dough:
                    list.Add(new TrailerActionCue { time = 0.35f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PREP_DOUGH, propKey = "rolling_pin" });
                    list.Add(new TrailerActionCue { time = 0.80f, duration = 3.40f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.10f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlayFx, anchorKey = TrailerAnchorKey.PREP_DOUGH, fxKey = "dough_roll" });
                    list.Add(new TrailerActionCue { time = 4.20f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SwapProp, anchorKey = TrailerAnchorKey.PREP_DOUGH, propKey = "dough_open" });
                    list.Add(new TrailerActionCue { time = 4.35f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 4.75f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.PREP_DOUGH, propKey = "rolling_pin_side" });
                    break;

                case TrailerTakeId.T04_Ingredients:
                    list.Add(new TrailerActionCue { time = 0.45f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PREP_CUT, propKey = "knife" });
                    list.Add(new TrailerActionCue { time = 0.80f, duration = 3.10f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.00f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlayFx, anchorKey = TrailerAnchorKey.PREP_CUT, fxKey = "cut_calabresa" });
                    list.Add(new TrailerActionCue { time = 3.90f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SwapProp, anchorKey = TrailerAnchorKey.PREP_CUT, propKey = "calabresa_sliced" });
                    list.Add(new TrailerActionCue { time = 4.20f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 4.65f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.PREP_CUT, propKey = "knife_side" });
                    break;

                case TrailerTakeId.T05_Assembly:
                    list.Add(new TrailerActionCue { time = 1.0f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.2f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_1_sauce" });
                    list.Add(new TrailerActionCue { time = 2.8f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_2_cheese" });
                    list.Add(new TrailerActionCue { time = 4.4f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_3_calabresa" });
                    list.Add(new TrailerActionCue { time = 5.9f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_4_full" });
                    list.Add(new TrailerActionCue { time = 6.7f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    break;

                case TrailerTakeId.T06_Oven:
                    list.Add(new TrailerActionCue { time = 0.80f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenDoor, anchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.05f, duration = 0.45f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenTransfer, anchorKey = TrailerAnchorKey.OVEN_SLOT, targetAnchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.60f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenDoor, anchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = false });
                    list.Add(new TrailerActionCue { time = 1.80f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PlayFx, anchorKey = TrailerAnchorKey.OVEN_SLOT, fxKey = "oven_bake_start" });
                    list.Add(new TrailerActionCue { time = 3.50f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.PACKAGING });
                    break;

                case TrailerTakeId.T07_Packaging:
                    list.Add(new TrailerActionCue { time = 1.00f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 2.95f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PackPizza, anchorKey = TrailerAnchorKey.PACKAGING });
                    list.Add(new TrailerActionCue { time = 4.15f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 4.45f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PACKAGING, propKey = "pizza_box" });
                    list.Add(new TrailerActionCue { time = 5.20f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.DISPATCH });
                    break;

                case TrailerTakeId.T08_Tickets:
                    list.Add(new TrailerActionCue { time = 1.00f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PostTicket, anchorKey = TrailerAnchorKey.TICKETS, floatValue = 1f });
                    list.Add(new TrailerActionCue { time = 1.70f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PostTicket, anchorKey = TrailerAnchorKey.TICKETS, floatValue = 2f });
                    list.Add(new TrailerActionCue { time = 2.40f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PostTicket, anchorKey = TrailerAnchorKey.TICKETS, floatValue = 3f });
                    list.Add(new TrailerActionCue { time = 2.65f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.ASSEMBLY });
                    list.Add(new TrailerActionCue { time = 3.40f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PlayReaction, fxKey = "OrderDeadlineWarning" });
                    break;

                case TrailerTakeId.T09_KitchenChaos:
                    // A sai do storage para prep_cut com calabresa
                    list.Add(new TrailerActionCue { time = 0.2f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.STORAGE, propKey = "calabresa_whole" });
                    list.Add(new TrailerActionCue { time = 0.4f, duration = 2.6f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.PREP_CUT, floatValue = 2.8f });
                    list.Add(new TrailerActionCue { time = 3.1f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.PREP_CUT });
                    list.Add(new TrailerActionCue { time = 3.4f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    // D corre das comandas, para bruscamente, depois corre para staging
                    list.Add(new TrailerActionCue { time = 0.8f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PostTicket, anchorKey = TrailerAnchorKey.TICKETS, floatValue = 1f });
                    list.Add(new TrailerActionCue { time = 1.4f, duration = 2.7f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.ASSEMBLY, floatValue = 4.2f });
                    list.Add(new TrailerActionCue { time = 4.3f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PlayReaction, fxKey = "ActionRejected" });
                    list.Add(new TrailerActionCue { time = 5.4f, duration = 2.8f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.STAGING, floatValue = 4.0f });
                    list.Add(new TrailerActionCue { time = 8.4f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.OVEN_ENTRY });
                    // B monta rapido
                    list.Add(new TrailerActionCue { time = 0.5f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 2.4f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_2_cheese" });
                    list.Add(new TrailerActionCue { time = 5.0f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.SetPizzaStage, anchorKey = TrailerAnchorKey.ASSEMBLY, propKey = "pizza_stage_4_full" });
                    list.Add(new TrailerActionCue { time = 6.2f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.PlayReaction, fxKey = "ProcessCompleted" });
                    // C fecha e leva caixa ao apoio
                    list.Add(new TrailerActionCue { time = 1.0f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = true });
                    list.Add(new TrailerActionCue { time = 3.4f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PackPizza, anchorKey = TrailerAnchorKey.PACKAGING });
                    list.Add(new TrailerActionCue { time = 4.0f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.SetPreparing, boolValue = false });
                    list.Add(new TrailerActionCue { time = 4.3f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.PACKAGING, propKey = "pizza_box" });
                    list.Add(new TrailerActionCue { time = 4.8f, duration = 2.6f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.STAGING, floatValue = 3.2f });
                    list.Add(new TrailerActionCue { time = 7.6f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PlaceProp, anchorKey = TrailerAnchorKey.STAGING });
                    break;

                case TrailerTakeId.T10_BurntPizza:
                    list.Add(new TrailerActionCue { time = 0.90f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PlayFx, anchorKey = TrailerAnchorKey.OVEN_SLOT, fxKey = "oven_smoke_start" });
                    list.Add(new TrailerActionCue { time = 2.20f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.OVEN_ENTRY });
                    list.Add(new TrailerActionCue { time = 2.35f, duration = 0.60f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.OVEN_FRONT, floatValue = 2.4f });
                    list.Add(new TrailerActionCue { time = 3.00f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenDoor, anchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = true });
                    list.Add(new TrailerActionCue { time = 3.30f, duration = 0.45f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.OvenTransfer, anchorKey = TrailerAnchorKey.OVEN_SLOT, targetAnchorKey = TrailerAnchorKey.OVEN_ENTRY, boolValue = false });
                    list.Add(new TrailerActionCue { time = 3.85f, actor = TrailerActorId.ActorC, actionType = TrailerActionType.PlayReaction, fxKey = "OvenBurned" });
                    list.Add(new TrailerActionCue { time = 4.10f, actor = TrailerActorId.ActorB, actionType = TrailerActionType.FaceAnchor, anchorKey = TrailerAnchorKey.OVEN_ENTRY });
                    break;

                case TrailerTakeId.T11_Backpack:
                    list.Add(new TrailerActionCue { time = 0.30f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.BagEquip, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.20f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.AttachProp, anchorKey = TrailerAnchorKey.DISPATCH, propKey = "pizza_box" });
                    list.Add(new TrailerActionCue { time = 2.40f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.StoreBoxInBag, anchorKey = TrailerAnchorKey.DISPATCH });
                    list.Add(new TrailerActionCue { time = 3.70f, duration = 2.20f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.ENTRANCE, floatValue = 3.8f });
                    break;

                case TrailerTakeId.T12_MotorcycleStart:
                    list.Add(new TrailerActionCue { time = 0.00f, duration = 1.65f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.DISMOUNT_LEFT, floatValue = 2.2f });
                    list.Add(new TrailerActionCue { time = 1.80f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MountMoto, anchorKey = TrailerAnchorKey.PARKING });
                    list.Add(new TrailerActionCue { time = 3.30f, duration = 4.50f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DriveMoto, anchorKey = TrailerAnchorKey.PARKING, targetAnchorKey = TrailerAnchorKey.STREET_EXIT });
                    break;

                case TrailerTakeId.T13_CityRide:
                    list.Add(new TrailerActionCue { time = 0.00f, duration = 9.80f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DriveMoto, anchorKey = TrailerAnchorKey.ROUTE_START, targetAnchorKey = TrailerAnchorKey.ROUTE_END });
                    break;

                case TrailerTakeId.T14_Delivery:
                    list.Add(new TrailerActionCue { time = 0.00f, duration = 1.30f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DriveMoto, anchorKey = TrailerAnchorKey.DELIVERY, boolValue = true });
                    list.Add(new TrailerActionCue { time = 1.50f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DismountMoto, anchorKey = TrailerAnchorKey.DELIVERY });
                    list.Add(new TrailerActionCue { time = 2.15f, duration = 1.95f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.DELIVERY, floatValue = 2.2f });
                    list.Add(new TrailerActionCue { time = 4.30f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.TakeBoxFromBag, anchorKey = TrailerAnchorKey.DELIVERY });
                    list.Add(new TrailerActionCue { time = 5.35f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DeliverAtPoint, anchorKey = TrailerAnchorKey.DELIVERY });
                    list.Add(new TrailerActionCue { time = 5.95f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PlayReaction, fxKey = "DeliverySuccess" });
                    break;

                case TrailerTakeId.T15_FinalGag:
                    list.Add(new TrailerActionCue { time = 0.00f, duration = 1.15f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.MoveToAnchor, anchorKey = TrailerAnchorKey.ENTRANCE, floatValue = 1.9f });
                    list.Add(new TrailerActionCue { time = 1.20f, duration = 0.65f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.DropProp, anchorKey = TrailerAnchorKey.DISPATCH });
                    list.Add(new TrailerActionCue { time = 2.50f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.PlayReaction, fxKey = "ActionRejected" });
                    list.Add(new TrailerActionCue { time = 4.00f, actor = TrailerActorId.ActorD, actionType = TrailerActionType.LookAtTarget, anchorKey = TrailerAnchorKey.FACADE });
                    break;
            }
            return list;
        }
    }
}
