using System;
using ScaryParty.VisualStyles;
using Unity.Netcode;
using UnityEngine;

namespace ScaryParty.Lighting
{
    [Serializable]
    public struct TimeOfDayNetState : INetworkSerializable, IEquatable<TimeOfDayNetState>
    {
        public float ReferenceClockHours;
        public double ReferenceServerTime;
        public float HoursPerRealMinute;
        public bool AutoAdvance;
        public int ActiveStyleHash;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ReferenceClockHours);
            serializer.SerializeValue(ref ReferenceServerTime);
            serializer.SerializeValue(ref HoursPerRealMinute);
            serializer.SerializeValue(ref AutoAdvance);
            serializer.SerializeValue(ref ActiveStyleHash);
        }

        public bool Equals(TimeOfDayNetState other)
        {
            return Mathf.Approximately(ReferenceClockHours, other.ReferenceClockHours) &&
                   Math.Abs(ReferenceServerTime - other.ReferenceServerTime) < 0.0001 &&
                   Mathf.Approximately(HoursPerRealMinute, other.HoursPerRealMinute) &&
                   AutoAdvance == other.AutoAdvance &&
                   ActiveStyleHash == other.ActiveStyleHash;
        }
    }

    /// <summary>
    /// Sincroniza o relogio global Dia/Noite em multiplayer via NetworkVariable sem RPCs por frame.
    /// Jogadores tardios (late-join) reconstroem a hora exata usando ServerTime.Time.
    /// </summary>
    public sealed class NetworkTimeOfDayState : NetworkBehaviour
    {
        public static NetworkTimeOfDayState Active { get; private set; }

        public readonly NetworkVariable<TimeOfDayNetState> State = new NetworkVariable<TimeOfDayNetState>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private TimeOfDayLightingDirector _subscribedDirector;

        public override void OnNetworkSpawn()
        {
            Active = this;
            State.OnValueChanged += HandleNetStateChanged;

            if (IsServer)
            {
                PublishCurrentDirectorStateToNetwork();
            }
            else
            {
                ApplyNetStateLocally(State.Value);
            }
        }

        public override void OnNetworkDespawn()
        {
            State.OnValueChanged -= HandleNetStateChanged;
            if (Active == this) Active = null;
        }

        private void Update()
        {
            if (!IsSpawned || IsServer) return;
            if (!State.Value.AutoAdvance || NetworkManager == null) return;

            var tod = TimeOfDayLightingDirector.Active;
            if (tod == null || tod.lockTimeForTests) return;

            // Calcula deterministicamente a hora atual do servidor sem trocar RPC por frame.
            float syncedHour = EvaluateSynchronizedClockHours(State.Value, NetworkManager.ServerTime.Time);
            if (Mathf.Abs(syncedHour - tod.currentClockHours) > 0.015f)
            {
                tod.autoAdvance = false; // Evita drift duplicado no cliente
                tod.SetClockHours(syncedHour, evaluateNow: true);
            }
        }

        public static float EvaluateSynchronizedClockHours(TimeOfDayNetState netState, double currentServerTime)
        {
            if (!netState.AutoAdvance || netState.HoursPerRealMinute <= 0.0001f)
            {
                return ((netState.ReferenceClockHours % 24f) + 24f) % 24f;
            }

            double elapsedMinutes = Math.Max(0.0, currentServerTime - netState.ReferenceServerTime) / 60.0;
            double totalHours = netState.ReferenceClockHours + elapsedMinutes * netState.HoursPerRealMinute;
            float mod = (float)(totalHours % 24.0);
            return mod < 0f ? mod + 24f : mod;
        }

        public void PublishCurrentDirectorStateToNetwork()
        {
            if (!IsServer || NetworkManager == null) return;

            TimeOfDayLightingDirector tod = TimeOfDayLightingDirector.Active;
            VisualStyleRuntimeConfig cfg = VisualStyleRuntimeConfig.LoadCached();

            float clockHours = tod != null ? tod.currentClockHours : (cfg != null ? cfg.initialTimeOfDayHours : 17f);
            float speed = tod != null ? tod.gameHoursPerRealMinute : 0.75f;
            bool autoAdv = tod != null ? (tod.autoAdvance && !tod.lockTimeForTests) : (cfg != null && cfg.enableDayNightCycle && !cfg.lockTimeOfDayForTests);
            int styleHash = VisualStyleManager.Active != null && VisualStyleManager.Active.activeProfile != null
                ? Animator.StringToHash(VisualStyleManager.Active.activeProfile.styleId)
                : 0;

            State.Value = new TimeOfDayNetState
            {
                ReferenceClockHours = clockHours,
                ReferenceServerTime = NetworkManager.ServerTime.Time,
                HoursPerRealMinute = speed,
                AutoAdvance = autoAdv,
                ActiveStyleHash = styleHash
            };
        }

        private void HandleNetStateChanged(TimeOfDayNetState previous, TimeOfDayNetState current)
        {
            if (IsServer) return;
            ApplyNetStateLocally(current);
        }

        private void ApplyNetStateLocally(TimeOfDayNetState current)
        {
            TimeOfDayLightingDirector tod = TimeOfDayLightingDirector.Active;
            if (tod == null) return;

            double serverTime = NetworkManager != null ? NetworkManager.ServerTime.Time : current.ReferenceServerTime;
            float hour = EvaluateSynchronizedClockHours(current, serverTime);
            tod.autoAdvance = false;
            tod.gameHoursPerRealMinute = current.HoursPerRealMinute;
            tod.SetClockHours(hour, evaluateNow: true);
        }
    }
}
