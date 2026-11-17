using System;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum NpcSpecies : byte
    {
        Human = 0,
        Vampire = 1,
        Werewolf = 2,
        Ghost = 3,
        Zombie = 4
    }

    public enum NpcBehaviorState : byte
    {
        Routine = 0,
        Customer = 1,
        Aggressive = 2,
        Attacking = 3,
        Recovering = 4,
        Disengaging = 5,
        Dead = 6
    }

    public sealed class SpeciesCapabilityProfile
    {
        public NpcSpecies Species;
        public int MaxHealth = 100;
        public int AttackDamage = 15;
        public float AttackCooldown = 1.2f;
        public float AcquisitionRange = 14f;
        public float DisengageDistance = 20f;
        public float DisengageSeconds = 5f;
        public bool CanDropFromRooftop;
        public bool HasCommittedLunge;
        public bool CanBlinkThroughObstacles;
        public bool CanConvertTargets;
        /// <summary>ID de ingrediente que ofende esta espécie (ex.: alho para vampiros), definido por dado do catálogo.</summary>
        public int OffenseIngredientId = -1;
        public int OffenseHostilityGain = 25;
    }

    public sealed class SpeciesHostilityEntry
    {
        public int Level;
        public float RemainingSeconds;
    }

    /// <summary>
    /// Gerencia hostilidade por espécie contra a equipe, teto, duração e política de reset entre noites.
    /// Proposta padrão configurável: reiniciar a cada noite (PersistAcrossNights = false).
    /// </summary>
    public sealed class SpeciesHostilityService
    {
        public int MaxHostilityLevel { get; set; } = 100;
        public bool PersistAcrossNights { get; set; } = false;

        private readonly Dictionary<NpcSpecies, SpeciesHostilityEntry> _entries = new Dictionary<NpcSpecies, SpeciesHostilityEntry>();
        private readonly Dictionary<NpcSpecies, SpeciesCapabilityProfile> _profiles = new Dictionary<NpcSpecies, SpeciesCapabilityProfile>();

        public SpeciesHostilityService()
        {
            RegisterDefaultProfiles();
        }

        public void RegisterProfile(SpeciesCapabilityProfile profile)
        {
            if (profile != null) _profiles[profile.Species] = profile;
        }

        public SpeciesCapabilityProfile GetProfile(NpcSpecies species)
        {
            return _profiles.TryGetValue(species, out var p) ? p : _profiles[NpcSpecies.Human];
        }

        public int GetHostility(NpcSpecies species)
        {
            return _entries.TryGetValue(species, out var e) ? e.Level : 0;
        }

        public void RegisterOffense(NpcSpecies species, int amount, float durationSeconds = 90f)
        {
            if (amount <= 0) return;
            if (!_entries.TryGetValue(species, out var entry))
            {
                entry = new SpeciesHostilityEntry();
                _entries[species] = entry;
            }
            entry.Level = Math.Min(MaxHostilityLevel, entry.Level + amount);
            entry.RemainingSeconds = Math.Max(entry.RemainingSeconds, durationSeconds);
        }

        public bool TryRegisterIngredientOffense(NpcSpecies species, int ingredientDefId, float durationSeconds = 90f)
        {
            var profile = GetProfile(species);
            if (profile.OffenseIngredientId > 0 && profile.OffenseIngredientId == ingredientDefId)
            {
                RegisterOffense(species, profile.OffenseHostilityGain, durationSeconds);
                return true;
            }
            return false;
        }

        public void Tick(float dt)
        {
            if (dt <= 0f || _entries.Count == 0) return;
            foreach (var kvp in _entries)
            {
                if (kvp.Value.Level <= 0) continue;
                kvp.Value.RemainingSeconds -= dt;
                if (kvp.Value.RemainingSeconds <= 0f)
                {
                    kvp.Value.Level = 0;
                    kvp.Value.RemainingSeconds = 0f;
                }
            }
        }

        public void OnNightEnded()
        {
            if (!PersistAcrossNights)
            {
                _entries.Clear();
            }
        }

        public void ResetForNewRun()
        {
            _entries.Clear();
        }

        private void RegisterDefaultProfiles()
        {
            _profiles[NpcSpecies.Human] = new SpeciesCapabilityProfile
            {
                Species = NpcSpecies.Human,
                MaxHealth = 80,
                AttackDamage = 12,
                AttackCooldown = 1.1f,
                AcquisitionRange = 12f,
                DisengageDistance = 18f,
                DisengageSeconds = 4f
            };
            _profiles[NpcSpecies.Vampire] = new SpeciesCapabilityProfile
            {
                Species = NpcSpecies.Vampire,
                MaxHealth = 130,
                AttackDamage = 22,
                AttackCooldown = 1.0f,
                AcquisitionRange = 16f,
                DisengageDistance = 22f,
                DisengageSeconds = 5f,
                CanDropFromRooftop = true,
                OffenseIngredientId = 10, // Alho (configurável por catálogo)
                OffenseHostilityGain = 35
            };
            _profiles[NpcSpecies.Werewolf] = new SpeciesCapabilityProfile
            {
                Species = NpcSpecies.Werewolf,
                MaxHealth = 180,
                AttackDamage = 28,
                AttackCooldown = 1.5f,
                AcquisitionRange = 15f,
                DisengageDistance = 24f,
                DisengageSeconds = 6f,
                HasCommittedLunge = true
            };
            _profiles[NpcSpecies.Ghost] = new SpeciesCapabilityProfile
            {
                Species = NpcSpecies.Ghost,
                MaxHealth = 100,
                AttackDamage = 18,
                AttackCooldown = 1.3f,
                AcquisitionRange = 15f,
                DisengageDistance = 20f,
                DisengageSeconds = 4.5f,
                CanBlinkThroughObstacles = true
            };
            _profiles[NpcSpecies.Zombie] = new SpeciesCapabilityProfile
            {
                Species = NpcSpecies.Zombie,
                MaxHealth = 110,
                AttackDamage = 16,
                AttackCooldown = 1.4f,
                AcquisitionRange = 12f,
                DisengageDistance = 20f,
                DisengageSeconds = 5f,
                CanConvertTargets = true
            };
        }
    }

    /// <summary>
    /// Protótipo de conversão de zumbis com teto de população, cooldown e idempotência por alvo.
    /// </summary>
    public sealed class ZombieConversionService
    {
        public int MaxInfectedCap { get; set; } = 6;
        public float ConversionChance { get; set; } = 0.5f;
        public double ConversionCooldownSeconds { get; set; } = 5.0;

        private readonly HashSet<ulong> _convertedIds = new HashSet<ulong>();
        private double _lastConversionTime = -100.0;

        public int ConvertedCount => _convertedIds.Count;

        public bool TryConvertTarget(ulong targetEntityId, int activeInfectedCount, double now, Random rng)
        {
            if (activeInfectedCount >= MaxInfectedCap) return false;
            if (_convertedIds.Contains(targetEntityId)) return false;
            if (now - _lastConversionTime < ConversionCooldownSeconds) return false;

            double roll = rng != null ? rng.NextDouble() : 0.0;
            if (roll > ConversionChance) return false;

            _convertedIds.Add(targetEntityId);
            _lastConversionTime = now;
            return true;
        }

        public void Reset()
        {
            _convertedIds.Clear();
            _lastConversionTime = -100.0;
        }
    }
}
