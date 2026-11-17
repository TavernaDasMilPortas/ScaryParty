using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum RunDifficulty : byte
    {
        Normal = 0,
        Hard = 1
    }

    public enum RunModifierDuration : byte
    {
        OneNight = 0,
        Permanent = 1
    }

    public enum RunModifierKind : byte
    {
        Event = 0,
        Hindrance = 1,
        Opportunity = 2,
        PermanentUpgrade = 3
    }

    public sealed class RunModifierDefinition
    {
        public string Id;
        public string DisplayName;
        public RunModifierKind Kind;
        public RunModifierDuration Duration;
        public string Scope;
        public int MinNight = 1;
        public int MaxStacks = 1;
        public float Value;
        public string Description;
        public string[] IncompatibleIds = Array.Empty<string>();
    }

    public sealed class ActiveRunModifier
    {
        public RunModifierDefinition Definition;
        public int Stacks = 1;
    }

    public sealed class RunEffectiveStats
    {
        public float DemandMultiplier = 1f;
        public float VampireSpawnMultiplier = 1f;
        public float VampireAggressionMultiplier = 1f;
        public float SupplyEtaMultiplier = 1f;
        public float EmployeeErrorMultiplier = 1f;
        public int ExtraBackpackCapacity = 0;
        public bool OvenAutoShutoffEnabled = false;
    }

    public sealed class RunShopItem
    {
        public string Id;
        public string Name;
        public int Cost;
        public bool IsUnique;
    }

    /// <summary>
    /// Gerencia RUN de N noites (padrão 3, extensível até 7), escala congelada por noite,
    /// streams RNG separados, votação com desempate no servidor, modificadores e loja atômica.
    /// </summary>
    public sealed class RunProgressionService
    {
        public int MaxNights { get; set; } = 3;
        public RunDifficulty Difficulty { get; set; } = RunDifficulty.Normal;
        public int FrozenHumanCount { get; private set; } = 1;
        public bool IsRunCompleted { get; private set; }
        public bool IsRunVictory { get; private set; }

        public Random MapRng { get; private set; }
        public Random ChoiceRng { get; private set; }
        public Random EmployeeErrorRng { get; private set; }

        private readonly Dictionary<string, RunModifierDefinition> _catalog = new Dictionary<string, RunModifierDefinition>();
        private readonly Dictionary<string, ActiveRunModifier> _activeModifiers = new Dictionary<string, ActiveRunModifier>();
        private readonly List<RunModifierDefinition> _currentVoteOptions = new List<RunModifierDefinition>();
        private readonly Dictionary<ulong, string> _votes = new Dictionary<ulong, string>();
        private readonly HashSet<ulong> _readyPlayers = new HashSet<ulong>();
        private readonly HashSet<string> _purchasedUniqueItems = new HashSet<string>();

        public IReadOnlyList<RunModifierDefinition> CurrentVoteOptions => _currentVoteOptions;
        public IReadOnlyDictionary<string, ActiveRunModifier> ActiveModifiers => _activeModifiers;
        public IReadOnlyDictionary<ulong, string> Votes => _votes;
        public IReadOnlyCollection<ulong> ReadyPlayers => _readyPlayers;
        public bool IsVoteResolved { get; private set; }
        public string LastResolvedOptionId { get; private set; }

        public bool HasModifier(string id) => !string.IsNullOrEmpty(id) && _activeModifiers.ContainsKey(id);
        public RunModifierDefinition GetModifierDefinition(string id) => !string.IsNullOrEmpty(id) && _catalog.TryGetValue(id, out var d) ? d : null;

        public RunProgressionService(int seed = 12345, int maxNights = 3, RunDifficulty difficulty = RunDifficulty.Normal)
        {
            MaxNights = Math.Clamp(maxNights, 1, 7);
            Difficulty = difficulty;
            InitializeStreams(seed);
            RegisterDefaultCatalog();
        }

        public void InitializeStreams(int seed)
        {
            MapRng = new Random(seed);
            ChoiceRng = new Random(seed ^ 0x5A17C3);
            EmployeeErrorRng = new Random(seed ^ 0x3E91B7);
        }

        public void ResetRun(
            PizzeriaState state,
            int seed,
            int maxNights = 3,
            RunDifficulty difficulty = RunDifficulty.Normal,
            int startingBudget = 500,
            int initialReputation = 100)
        {
            MaxNights = Math.Clamp(maxNights, 1, 7);
            Difficulty = difficulty;
            IsRunCompleted = false;
            IsRunVictory = false;
            FrozenHumanCount = 1;
            _activeModifiers.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            _readyPlayers.Clear();
            _purchasedUniqueItems.Clear();
            IsVoteResolved = false;
            LastResolvedOptionId = null;
            InitializeStreams(seed);

            if (state != null)
            {
                state.Reset(state.GenerationId + 1, startingBudget, initialReputation);
                NightCycleService.ClearTransientNightEntities(state, repairBackpacks: true, preservePendingSupplies: false);
            }
        }

        public int ComputeTabulatedGoal(int nightNumber, int humanCount)
        {
            int n = Math.Max(1, nightNumber);
            int h = Math.Clamp(humanCount, 1, 4);
            int basePerNight = n switch
            {
                1 => 60,
                2 => 100,
                3 => 150,
                _ => 150 + (n - 3) * 45
            };
            float humanMult = h switch
            {
                1 => 1.0f,
                2 => 1.6f,
                3 => 2.15f,
                _ => 2.65f
            };
            float diffMult = Difficulty == RunDifficulty.Hard ? 1.2f : 1.0f;
            return (int)Math.Round(basePerNight * humanMult * diffMult);
        }

        public void FreezeNightStartParameters(PizzeriaState state, int connectedHumanCount)
        {
            FrozenHumanCount = Math.Clamp(connectedHumanCount, 1, 4);
            if (state != null)
            {
                state.NightRevenueGoal = ComputeTabulatedGoal(state.CurrentNightNumber, FrozenHumanCount);
            }
        }

        public IReadOnlyList<RunModifierDefinition> OpenChoiceVote(int nightNumber, int optionCount = 2)
        {
            _currentVoteOptions.Clear();
            _votes.Clear();
            IsVoteResolved = false;
            LastResolvedOptionId = null;

            var eligible = new List<RunModifierDefinition>();
            foreach (var def in _catalog.Values)
            {
                if (def.MinNight > nightNumber) continue;
                if (_activeModifiers.TryGetValue(def.Id, out var existing) && existing.Stacks >= def.MaxStacks) continue;
                if (HasIncompatibility(def)) continue;
                eligible.Add(def);
            }

            // Fallback garantido caso o pool se esgote
            if (eligible.Count < 2)
            {
                foreach (var def in _catalog.Values)
                {
                    if (!eligible.Contains(def) && def.Duration == RunModifierDuration.OneNight)
                        eligible.Add(def);
                }
            }

            int targetCount = Math.Min(Math.Max(2, optionCount), eligible.Count);
            for (int i = 0; i < targetCount; i++)
            {
                int pick = ChoiceRng.Next(0, eligible.Count);
                _currentVoteOptions.Add(eligible[pick]);
                eligible.RemoveAt(pick);
            }

            return _currentVoteOptions;
        }

        public bool CastVote(ulong clientId, string optionId)
        {
            if (IsVoteResolved || string.IsNullOrEmpty(optionId)) return false;
            for (int i = 0; i < _currentVoteOptions.Count; i++)
            {
                if (_currentVoteOptions[i].Id == optionId)
                {
                    _votes[clientId] = optionId;
                    return true;
                }
            }
            return false;
        }

        public void SetPlayerReady(ulong clientId, bool ready)
        {
            if (ready) _readyPlayers.Add(clientId);
            else _readyPlayers.Remove(clientId);
        }

        public void OnParticipantDisconnected(ulong clientId)
        {
            _votes.Remove(clientId);
            _readyPlayers.Remove(clientId);
        }

        public bool CanStartNight(IReadOnlyCollection<ulong> connectedHumans)
        {
            if (!IsVoteResolved || connectedHumans == null || connectedHumans.Count == 0) return false;
            foreach (var id in connectedHumans)
            {
                if (!_readyPlayers.Contains(id)) return false;
            }
            return true;
        }

        public RunModifierDefinition ResolveVoteServer()
        {
            if (IsVoteResolved && !string.IsNullOrEmpty(LastResolvedOptionId) && _catalog.TryGetValue(LastResolvedOptionId, out var cached))
                return cached;

            if (_currentVoteOptions.Count == 0) return null;

            var counts = new Dictionary<string, int>();
            foreach (var opt in _currentVoteOptions) counts[opt.Id] = 0;
            foreach (var v in _votes.Values)
            {
                if (counts.ContainsKey(v)) counts[v]++;
            }

            int bestScore = -1;
            var tied = new List<RunModifierDefinition>();
            foreach (var opt in _currentVoteOptions)
            {
                int score = counts[opt.Id];
                if (score > bestScore)
                {
                    bestScore = score;
                    tied.Clear();
                    tied.Add(opt);
                }
                else if (score == bestScore)
                {
                    tied.Add(opt);
                }
            }

            var winner = tied[ChoiceRng.Next(0, tied.Count)];
            IsVoteResolved = true;
            LastResolvedOptionId = winner.Id;
            ApplyModifier(winner);
            return winner;
        }

        public void ApplyModifier(RunModifierDefinition def)
        {
            if (def == null) return;
            if (_activeModifiers.TryGetValue(def.Id, out var existing))
            {
                existing.Stacks = Math.Min(def.MaxStacks, existing.Stacks + 1);
            }
            else
            {
                _activeModifiers[def.Id] = new ActiveRunModifier { Definition = def, Stacks = 1 };
            }
        }

        public void ExpireTemporaryModifiersAtNightEnd()
        {
            var toRemove = new List<string>();
            foreach (var kvp in _activeModifiers)
            {
                if (kvp.Value.Definition.Duration == RunModifierDuration.OneNight)
                    toRemove.Add(kvp.Key);
            }
            for (int i = 0; i < toRemove.Count; i++)
            {
                _activeModifiers.Remove(toRemove[i]);
            }
            _readyPlayers.Clear();
            IsVoteResolved = false;
        }

        public RunEffectiveStats RecalculateEffectiveStats()
        {
            var stats = new RunEffectiveStats();
            if (Difficulty == RunDifficulty.Hard)
            {
                stats.DemandMultiplier *= 1.15f;
                stats.VampireAggressionMultiplier *= 1.2f;
            }

            foreach (var mod in _activeModifiers.Values)
            {
                float v = mod.Definition.Value * mod.Stacks;
                switch (mod.Definition.Id)
                {
                    case "blood_moon": stats.VampireSpawnMultiplier += v; break;
                    case "vampire_attack": stats.VampireAggressionMultiplier += v; break;
                    case "oven_timer": stats.OvenAutoShutoffEnabled = true; break;
                    case "reinforced_bag": stats.ExtraBackpackCapacity += (int)Math.Round(v); break;
                    case "heavy_rush": stats.DemandMultiplier += v; break;
                    case "express_supply": stats.SupplyEtaMultiplier = Math.Max(0.3f, stats.SupplyEtaMultiplier - v); break;
                    case "staff_training": stats.EmployeeErrorMultiplier = Math.Max(0.2f, stats.EmployeeErrorMultiplier - v); break;
                }
            }
            return stats;
        }

        public bool TryPurchaseShopItem(PizzeriaState state, RunShopItem item)
        {
            if (state == null || item == null || item.Cost < 0) return false;
            if (item.IsUnique && _purchasedUniqueItems.Contains(item.Id)) return false;
            if (state.RestaurantBudget < item.Cost) return false;

            state.RestaurantBudget -= item.Cost;
            if (item.IsUnique) _purchasedUniqueItems.Add(item.Id);
            state.IncrementRevision();
            return true;
        }

        public bool TryCompleteNightAndAdvance(PizzeriaState state)
        {
            if (state == null || state.CurrentNightPhase != NightPhase.Results) return false;
            ExpireTemporaryModifiersAtNightEnd();

            if (state.CurrentNightNumber >= MaxNights)
            {
                IsRunCompleted = true;
                IsRunVictory = true;
                return false; // RUN encerrada com vitória na última noite
            }

            return NightCycleService.AdvanceToNextNight(state);
        }

        private bool HasIncompatibility(RunModifierDefinition def)
        {
            if (def.IncompatibleIds == null) return false;
            for (int i = 0; i < def.IncompatibleIds.Length; i++)
            {
                if (_activeModifiers.ContainsKey(def.IncompatibleIds[i])) return true;
            }
            return false;
        }

        private void RegisterDefaultCatalog()
        {
            void Add(RunModifierDefinition d) => _catalog[d.Id] = d;
            Add(new RunModifierDefinition { Id = "blood_moon", DisplayName = "Lua vermelha", Kind = RunModifierKind.Event, Duration = RunModifierDuration.OneNight, Scope = "Population", Value = 0.5f, Description = "Mais vampiros na população." });
            Add(new RunModifierDefinition { Id = "vampire_attack", DisplayName = "Ataque dos vampiros", Kind = RunModifierKind.Hindrance, Duration = RunModifierDuration.OneNight, Scope = "Combat", Value = 0.35f, Description = "Vampiros mais agressivos." });
            Add(new RunModifierDefinition { Id = "oven_timer", DisplayName = "Timer do forno", Kind = RunModifierKind.PermanentUpgrade, Duration = RunModifierDuration.Permanent, Scope = "Kitchen", Value = 1f, Description = "Desliga o forno ao atingir o ponto correto." });
            Add(new RunModifierDefinition { Id = "reinforced_bag", DisplayName = "Bolsa reforçada", Kind = RunModifierKind.PermanentUpgrade, Duration = RunModifierDuration.Permanent, Scope = "Delivery", MaxStacks = 2, Value = 1f, Description = "Aumenta capacidade/resistência da mochila." });
            Add(new RunModifierDefinition { Id = "heavy_rush", DisplayName = "Movimento intenso", Kind = RunModifierKind.Hindrance, Duration = RunModifierDuration.OneNight, Scope = "Orders", Value = 0.3f, Description = "Mais pedidos simultâneos." });
            Add(new RunModifierDefinition { Id = "express_supply", DisplayName = "Fornecimento expresso", Kind = RunModifierKind.Opportunity, Duration = RunModifierDuration.OneNight, Scope = "Supply", Value = 0.4f, Description = "Menor ETA de reposição." });
            Add(new RunModifierDefinition { Id = "staff_training", DisplayName = "Treinamento", Kind = RunModifierKind.PermanentUpgrade, Duration = RunModifierDuration.Permanent, Scope = "Employees", MaxStacks = 2, Value = 0.25f, Description = "Menor chance de erro do pizzaiolo." });
        }
    }
}
