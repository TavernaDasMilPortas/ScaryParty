using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Network
{
    [Serializable]
    public struct SessionLaunchConfigData
    {
        public bool HasHostSelection;
        public string PresetId;
        public RunDifficulty Difficulty;
        public int MaxNights;
        public int SeedOverride;
        public bool IncludeNight0Tutorial;
        public uint Revision;

        public static SessionLaunchConfigData CreateDefault() => new SessionLaunchConfigData
        {
            HasHostSelection = false,
            PresetId = "Entrega_3Noites",
            Difficulty = RunDifficulty.Normal,
            MaxNights = 3,
            SeedOverride = 0,
            IncludeNight0Tutorial = false,
            Revision = 1
        };
    }

    /// <summary>
    /// Configuração de partida escolhida pelo Host na Lobby/ReadyScene (P4).
    /// Replicada via NetworkManager.CustomMessagingManager para clients sem exigir
    /// alteração de prefabs de rede nem do DefaultNetworkPrefabs.asset.
    /// </summary>
    public static class SessionLaunchConfig
    {
        public const string NetworkMessageName = "ScaryParty.SessionConfigV1";

        public static SessionLaunchConfigData Current { get; private set; } = SessionLaunchConfigData.CreateDefault();
        public static event Action<SessionLaunchConfigData> OnConfigChanged;

        private static bool _hooksRegistered;
        private static NetworkManager _hookedManager;
        private static CustomMessagingManager _hookedMessagingManager;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemLoad()
        {
            Current = SessionLaunchConfigData.CreateDefault();
            _hooksRegistered = false;
            _hookedManager = null;
            _hookedMessagingManager = null;
        }

        public static void ResetToDefault()
        {
            Current = SessionLaunchConfigData.CreateDefault();
            OnConfigChanged?.Invoke(Current);
        }

        public static void ApplyHostSelection(
            string presetId,
            RunDifficulty difficulty,
            int maxNights,
            int seedOverride,
            bool includeNight0Tutorial,
            bool broadcastToClients = true)
        {
            uint nextRev = Current.Revision + 1;
            Current = new SessionLaunchConfigData
            {
                HasHostSelection = true,
                PresetId = presetId ?? string.Empty,
                Difficulty = difficulty,
                MaxNights = Mathf.Clamp(maxNights, 1, 7),
                SeedOverride = seedOverride,
                IncludeNight0Tutorial = includeNight0Tutorial,
                Revision = nextRev
            };

            OnConfigChanged?.Invoke(Current);

            if (broadcastToClients)
            {
                TryBroadcastToAllClients();
            }
        }

        public static bool ShouldRebindMessaging(
            object hookedManager,
            object currentManager,
            object hookedMessaging,
            object currentMessaging,
            bool hooksRegistered)
        {
            return !(hooksRegistered &&
                     ReferenceEquals(hookedManager, currentManager) &&
                     currentMessaging != null &&
                     ReferenceEquals(hookedMessaging, currentMessaging));
        }

        public static void EnsureNetworkHooksRegistered()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsListening) return;

            if (_hookedManager != null && _hookedManager != nm)
            {
                UnhookManager(_hookedManager);
            }

            var currentMessaging = nm.CustomMessagingManager;
            if (!ShouldRebindMessaging(_hookedManager, nm, _hookedMessagingManager, currentMessaging, _hooksRegistered))
                return;

            if (_hookedMessagingManager != null && _hookedMessagingManager != currentMessaging)
            {
                try { _hookedMessagingManager.UnregisterNamedMessageHandler(NetworkMessageName); } catch { }
                _hookedMessagingManager = null;
            }

            if (_hookedManager != nm || !_hooksRegistered)
            {
                _hookedManager = nm;
                nm.OnClientConnectedCallback -= HandleClientConnected;
                nm.OnClientConnectedCallback += HandleClientConnected;
            }

            if (currentMessaging != null)
            {
                currentMessaging.UnregisterNamedMessageHandler(NetworkMessageName);
                currentMessaging.RegisterNamedMessageHandler(NetworkMessageName, HandleNamedMessageReceived);
                _hookedMessagingManager = currentMessaging;
                _hooksRegistered = true;
            }
            else
            {
                _hooksRegistered = false;
            }

            if (nm.IsServer && currentMessaging != null)
            {
                TryBroadcastToAllClients();
            }
        }

        private static void UnhookManager(NetworkManager nm)
        {
            if (nm == null) return;
            try
            {
                nm.OnClientConnectedCallback -= HandleClientConnected;
                if (_hookedMessagingManager != null)
                {
                    _hookedMessagingManager.UnregisterNamedMessageHandler(NetworkMessageName);
                }
                if (nm.CustomMessagingManager != null && nm.CustomMessagingManager != _hookedMessagingManager)
                {
                    nm.CustomMessagingManager.UnregisterNamedMessageHandler(NetworkMessageName);
                }
            }
            catch
            {
            }
            _hooksRegistered = false;
            _hookedManager = null;
            _hookedMessagingManager = null;
        }

        private static void HandleClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !nm.IsListening) return;
            if (clientId == NetworkManager.ServerClientId) return;
            TrySendToClient(clientId);
        }

        public static void TryBroadcastToAllClients()
        {
            EnsureNetworkHooksRegistered();
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !nm.IsListening || nm.CustomMessagingManager == null) return;
            if (nm.ConnectedClientsIds == null || nm.ConnectedClientsIds.Count <= 1) return;

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            WritePayload(writer, Current);
            nm.CustomMessagingManager.SendNamedMessageToAll(NetworkMessageName, writer);
        }

        private static void TrySendToClient(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer || !nm.IsListening || nm.CustomMessagingManager == null) return;

            using var writer = new FastBufferWriter(256, Allocator.Temp);
            WritePayload(writer, Current);
            nm.CustomMessagingManager.SendNamedMessage(NetworkMessageName, clientId, writer);
        }

        private static void HandleNamedMessageReceived(ulong senderClientId, FastBufferReader reader)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && senderClientId != NetworkManager.ServerClientId)
                return; // Somente o Host/Servidor tem autoridade sobre SessionLaunchConfig

            var data = ReadPayload(reader);
            Current = data;
            OnConfigChanged?.Invoke(Current);
        }

        public static void WritePayload(FastBufferWriter writer, in SessionLaunchConfigData data)
        {
            FixedString64Bytes fsPreset = string.IsNullOrEmpty(data.PresetId)
                ? default
                : new FixedString64Bytes(data.PresetId.Length > 60 ? data.PresetId.Substring(0, 60) : data.PresetId);

            writer.WriteValueSafe(data.HasHostSelection);
            writer.WriteValueSafe(in fsPreset);
            writer.WriteValueSafe((byte)data.Difficulty);
            writer.WriteValueSafe(data.MaxNights);
            writer.WriteValueSafe(data.SeedOverride);
            writer.WriteValueSafe(data.IncludeNight0Tutorial);
            writer.WriteValueSafe(data.Revision);
        }

        public static SessionLaunchConfigData ReadPayload(FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool hasSelection);
            reader.ReadValueSafe(out FixedString64Bytes fsPreset);
            reader.ReadValueSafe(out byte diffByte);
            reader.ReadValueSafe(out int maxNights);
            reader.ReadValueSafe(out int seedOverride);
            reader.ReadValueSafe(out bool includeNight0);
            reader.ReadValueSafe(out uint revision);

            return new SessionLaunchConfigData
            {
                HasHostSelection = hasSelection,
                PresetId = fsPreset.ToString(),
                Difficulty = Enum.IsDefined(typeof(RunDifficulty), diffByte) ? (RunDifficulty)diffByte : RunDifficulty.Normal,
                MaxNights = Mathf.Clamp(maxNights, 1, 7),
                SeedOverride = seedOverride,
                IncludeNight0Tutorial = includeNight0,
                Revision = revision
            };
        }
    }
}
