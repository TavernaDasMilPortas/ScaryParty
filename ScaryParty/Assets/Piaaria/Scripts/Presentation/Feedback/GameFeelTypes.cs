using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    public enum GameFeelEventId
    {
        None = 0,

        // Mãos e Bancadas
        FocusEnter = 100,
        FocusExit = 101,
        InteractPress = 102,
        ItemPickedUp = 103,
        ToolPickedUp = 104,
        ItemPlaced = 105,
        BoxPlaced = 106,
        ActiveHandChanged = 107,
        ActionRejected = 108,
        ItemDropped = 109,
        StorageDispensed = 110,
        DoorOpened = 111,
        DoorClosed = 112,

        // Processos de Cozinha
        ProcessStarted = 200,
        ProcessTick = 201,
        RollDoughStep = 202,
        SliceTomatoPepperoniStep = 203,
        SliceMushroomStep = 204,
        GrateCheeseStep = 205,
        SpreadSauce = 206,
        SprinkleCheese = 207,
        PlaceToppings = 208,
        RearrangeToppings = 209,
        ProcessCompleted = 210,
        ProcessInterrupted = 211,

        // Forno, Embalagem e Mochila
        OvenInserted = 300,
        OvenCookingLoop = 301,
        OvenReadyDing = 302,
        OvenBurningWarning = 303,
        OvenBurned = 304,
        OvenRemoved = 305,
        BoxOpened = 306,
        BoxPackaged = 307,
        BoxLabeled = 308,
        StackPlaced = 309,
        StackRemoved = 310,
        BackpackEquipped = 311,
        BackpackStored = 312,
        BackpackRemoved = 313,
        BackpackFullRejected = 314,

        // Telefone, Pedidos e Suprimentos
        PhoneRingPulse = 400,
        PhoneAnswered = 401,
        OrderPosted = 402,
        OrderInspected = 403,
        OrderCompleted = 404,
        OrderDeadlineWarning = 405,
        SupplyOrdered = 406,
        SupplyArrived = 407,

        // Personagens e Clientes NPC
        NpcCounterArrived = 500,
        NpcOrderRegistered = 501,
        NpcImpatient = 502,
        NpcOrderGood = 503,
        NpcOrderBad = 504,
        NpcSeatedSettle = 505,
        NpcEatingLoop = 506,
        NpcLeavingAngry = 507,
        PlayerWorkLoop = 508,
        PlayerLanded = 509,

        // Cidade e Entrega
        DeliveryAccepted = 600,
        DeliverySuccess = 601,
        DeliveryPartial = 602,
        DeliveryFailed = 603,

        // UI Toolkit
        UIHover = 700,
        UIClick = 701,
        UISuccess = 702,
        UIError = 703
    }

    public enum GameFeelMaterialFamily
    {
        Default = 0,
        Dough = 1,
        SmallBounceIngredient = 2,
        FluidGranular = 3,
        CardboardPaper = 4,
        RigidMetal = 5,
        FabricCargo = 6,
        CharacterBody = 7,
        UIElement = 8
    }

    public enum GameFeelPresetMode
    {
        Suave = 0,
        Cartoon = 1,
        Sloppy = 2,
        Reduzido = 3
    }

    [Flags]
    public enum GameFeelChannel
    {
        None = 0,
        Movement = 1 << 0,
        Particles = 1 << 1,
        Audio = 1 << 2,
        UI = 1 << 3,
        Camera = 1 << 4,
        All = Movement | Particles | Audio | UI | Camera
    }

    public struct GameFeelContext
    {
        public GameFeelEventId EventId;
        public ulong EntityId;
        public int StationId;
        public int SlotIndex;
        public ulong PlayerClientId;
        public uint Revision;
        public int ProcessId;
        public GameFeelMaterialFamily MaterialFamily;
        public Vector3 WorldPosition;
        public Transform TargetTransform;
        public float Intensity;
        public int VariantSeed;
        public bool IsLocalOwner;
        public double Timestamp;

        public static GameFeelContext Create(
            GameFeelEventId eventId,
            ulong entityId = 0,
            int stationId = 0,
            uint revision = 0,
            GameFeelMaterialFamily family = GameFeelMaterialFamily.Default)
        {
            return new GameFeelContext
            {
                EventId = eventId,
                EntityId = entityId,
                StationId = stationId,
                SlotIndex = -1,
                Revision = revision,
                MaterialFamily = family,
                Intensity = 1f,
                VariantSeed = unchecked((int)(entityId ^ (ulong)stationId ^ revision)),
                IsLocalOwner = true,
                Timestamp = -1d
            };
        }
    }

    public readonly struct GameFeelEventKey : IEquatable<GameFeelEventKey>
    {
        public readonly int SessionEpoch;
        public readonly ulong EntityId;
        public readonly int StationId;
        public readonly uint Revision;
        public readonly GameFeelEventId EventId;

        public GameFeelEventKey(int sessionEpoch, ulong entityId, int stationId, uint revision, GameFeelEventId eventId)
        {
            SessionEpoch = sessionEpoch;
            EntityId = entityId;
            StationId = stationId;
            Revision = revision;
            EventId = eventId;
        }

        public bool Equals(GameFeelEventKey other)
        {
            return SessionEpoch == other.SessionEpoch &&
                   EntityId == other.EntityId &&
                   StationId == other.StationId &&
                   Revision == other.Revision &&
                   EventId == other.EventId;
        }

        public override bool Equals(object obj) => obj is GameFeelEventKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = SessionEpoch;
                hash = (hash * 397) ^ EntityId.GetHashCode();
                hash = (hash * 397) ^ StationId;
                hash = (hash * 397) ^ (int)Revision;
                hash = (hash * 397) ^ (int)EventId;
                return hash;
            }
        }
    }

    /// <summary>
    /// Evita disparos duplicados no Host (predição local + evento replicado) e respeita janela temporal por revisão.
    /// </summary>
    public sealed class GameFeelDeduplicator
    {
        private readonly int _capacity;
        private readonly double _defaultWindowSeconds;
        private readonly Dictionary<GameFeelEventKey, double> _seenUntil;
        private readonly Queue<GameFeelEventKey> _fifoOrder;
        private int _sessionEpoch = 1;

        public int SessionEpoch => _sessionEpoch;
        public int Count => _seenUntil.Count;

        public GameFeelDeduplicator(int capacity = 256, double defaultWindowSeconds = 0.35d)
        {
            _capacity = Mathf.Clamp(capacity, 16, 2048);
            _defaultWindowSeconds = Math.Max(0.02d, defaultWindowSeconds);
            _seenUntil = new Dictionary<GameFeelEventKey, double>(_capacity);
            _fifoOrder = new Queue<GameFeelEventKey>(_capacity);
        }

        public void ResetEpoch()
        {
            _sessionEpoch++;
            _seenUntil.Clear();
            _fifoOrder.Clear();
        }

        public bool TryAccept(in GameFeelContext ctx, double nowSeconds, double windowSeconds = -1d)
        {
            double effectiveWindow = windowSeconds > 0d ? windowSeconds : _defaultWindowSeconds;
            var key = new GameFeelEventKey(_sessionEpoch, ctx.EntityId, ctx.StationId, ctx.Revision, ctx.EventId);

            if (_seenUntil.TryGetValue(key, out double expiresAt) && nowSeconds < expiresAt)
            {
                return false;
            }

            if (!_seenUntil.ContainsKey(key))
            {
                while (_seenUntil.Count >= _capacity && _fifoOrder.Count > 0)
                {
                    var oldest = _fifoOrder.Dequeue();
                    _seenUntil.Remove(oldest);
                }
                _fifoOrder.Enqueue(key);
            }

            _seenUntil[key] = nowSeconds + effectiveWindow;
            return true;
        }
    }
}
