using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
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
        ROUTE_END = 18
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
        OvenDoor = 17
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
        public List<TrailerActionCue> cues = new List<TrailerActionCue>();

        public float TotalDuration => preRoll + actionDuration + postRoll;
    }
}
