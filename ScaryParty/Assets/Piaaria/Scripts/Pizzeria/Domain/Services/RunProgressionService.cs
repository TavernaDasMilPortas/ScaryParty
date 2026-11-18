using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Definitions;
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
        public string Scope = "Global";
        public int MinNight = 1;
        public int MaxNight = 0;
        public int MaxStacks = 1;
        public float Value;
        public string Description;
        public string DescriptionOverride;
        public bool Enabled = true;
        public RunModifierSource AllowedSource = RunModifierSource.VoteOnly;
        public RunDifficultyFlags AllowedDifficulties = RunDifficultyFlags.All;
        public float Weight = 1f;
        public bool IsFallbackReserve;
        public string[] PrerequisiteIds = Array.Empty<string>();
        public string[] IncompatibleIds = Array.Empty<string>();
        public string[] Tags = Array.Empty<string>();
        public IReadOnlyList<RunModifierEffectData> Effects = Array.Empty<RunModifierEffectData>();
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
        public float BackpackResistanceMultiplier = 1f;
        public float CustomerPatienceMultiplier = 1f;
        public float NightGoalMultiplier = 1f;
    }

    public sealed class RunShopItem
    {
        public string Id;
        public string Name;
        public int Cost;
        public bool IsUnique;
    }

    public sealed class RunVoteStageState
    {
        public int StageIndex;
        public RunModifierKind Category;
        public bool IsMandatory;
        public List<RunModifierDefinition> Options = new List<RunModifierDefinition>();
        public Dictionary<ulong, string> Votes = new Dictionary<ulong, string>();
        public string ResolvedOptionId = string.Empty;
        public bool IsResolved => !string.IsNullOrEmpty(ResolvedOptionId);
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
        public RunBalancingSnapshot BalancingSnapshot { get; private set; }

        public Random MapRng { get; private set; }
        public Random ChoiceRng { get; private set; }
        public Random EmployeeErrorRng { get; private set; }

        private readonly Dictionary<string, RunModifierDefinition> _catalog = new Dictionary<string, RunModifierDefinition>(StringComparer.Ordinal);
        private readonly Dictionary<string, ActiveRunModifier> _activeModifiers = new Dictionary<string, ActiveRunModifier>(StringComparer.Ordinal);
        private readonly List<RunVoteStageState> _voteStages = new List<RunVoteStageState>();
        private readonly List<RunModifierDefinition> _currentVoteOptions = new List<RunModifierDefinition>();
        private readonly Dictionary<ulong, string> _votes = new Dictionary<ulong, string>();
        private readonly HashSet<ulong> _readyPlayers = new HashSet<ulong>();
        private readonly HashSet<string> _purchasedUniqueItems = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _shopRunPurchases = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _shopNightPurchases = new Dictionary<string, int>(StringComparer.Ordinal);

        public IReadOnlyList<RunVoteStageState> CurrentVoteStages => _voteStages;
        public int PreparationTargetNight { get; private set; } = 1;
        public IReadOnlyList<RunModifierDefinition> CurrentVoteOptions => _currentVoteOptions;
        public IReadOnlyDictionary<string, ActiveRunModifier> ActiveModifiers => _activeModifiers;
        public IReadOnlyDictionary<ulong, string> Votes => _votes;
        public IReadOnlyCollection<ulong> ReadyPlayers => _readyPlayers;
        public bool IsVoteResolved { get; private set; }
        public string LastResolvedOptionId { get; private set; }

        // Intermissão 2.1 (urnas duplas sincronizadas por servidor)
        public IntermissionStage IntermissionStage { get; private set; } = IntermissionStage.None;
        public bool IsIntermissionLocked => IntermissionStage >= IntermissionStage.Locked;
        public double RevealStartedAtServerTime { get; private set; }
        public double CountdownStartsAtServerTime { get; private set; }
        public double StartNightAtServerTime { get; private set; }
        public string LastWinningHindranceId { get; private set; }
        public string LastWinningOpportunityId { get; private set; }

        public bool HasModifier(string id) => !string.IsNullOrEmpty(id) && _activeModifiers.ContainsKey(id);
        public bool RemoveModifierForDebug(string id) => !string.IsNullOrEmpty(id) && _activeModifiers.Remove(id);
        public int GetModifierStacks(string id) => !string.IsNullOrEmpty(id) && _activeModifiers.TryGetValue(id, out var m) ? m.Stacks : 0;
        public RunModifierDefinition GetModifierDefinition(string id) => !string.IsNullOrEmpty(id) && _catalog.TryGetValue(id, out var d) ? d : null;

        public void RestoreFromCheckpoint(
            int maxNights,
            RunDifficulty difficulty,
            IReadOnlyList<(string Id, int Stacks)> activeModifiers,
            IReadOnlyDictionary<string, int> shopRunPurchases)
        {
            MaxNights = Math.Clamp(maxNights, 1, 7);
            Difficulty = difficulty;
            IsRunCompleted = false;
            IsRunVictory = false;
            _activeModifiers.Clear();
            _voteStages.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            _readyPlayers.Clear();
            _shopRunPurchases.Clear();
            _shopNightPurchases.Clear();
            _purchasedUniqueItems.Clear();
            IsVoteResolved = false;
            LastResolvedOptionId = null;

            if (activeModifiers != null)
            {
                for (int i = 0; i < activeModifiers.Count; i++)
                {
                    var entry = activeModifiers[i];
                    var def = GetModifierDefinition(entry.Id);
                    if (def != null)
                    {
                        _activeModifiers[entry.Id] = new ActiveRunModifier
                        {
                            Definition = def,
                            Stacks = Math.Clamp(entry.Stacks, 1, def.MaxStacks)
                        };
                    }
                }
            }

            if (shopRunPurchases != null)
            {
                foreach (var kvp in shopRunPurchases)
                {
                    _shopRunPurchases[kvp.Key] = kvp.Value;
                }
            }
        }

        public RunProgressionService(
            int seed = 12345,
            int maxNights = 3,
            RunDifficulty difficulty = RunDifficulty.Normal,
            RunBalancingSnapshot snapshot = null)
        {
            MaxNights = Math.Clamp(maxNights, 1, 7);
            Difficulty = difficulty;
            InitializeStreams(seed);
            ApplyBalancingSnapshot(snapshot ?? RunBalancingSnapshot.CreateForDomainUnitTests());
        }

        public void ApplyBalancingSnapshot(RunBalancingSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            BalancingSnapshot = snapshot;
            _catalog.Clear();
            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                var mod = snapshot.Modifiers[i];
                if (mod != null && !string.IsNullOrWhiteSpace(mod.Id))
                {
                    _catalog[mod.Id] = mod;
                }
            }
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
            _voteStages.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            _readyPlayers.Clear();
            _purchasedUniqueItems.Clear();
            _shopRunPurchases.Clear();
            _shopNightPurchases.Clear();
            PreparationTargetNight = 1;
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

        /// <summary>Compatibilidade legado para testes T52; o runtime usa FreezeResolvedNightParameters via NightParameterResolver.</summary>
        public void FreezeNightStartParameters(PizzeriaState state, int connectedHumanCount)
        {
            FrozenHumanCount = Math.Clamp(connectedHumanCount, 1, 4);
            if (state != null)
            {
                state.FrozenHumanCount = FrozenHumanCount;
                state.NightRevenueGoal = ComputeTabulatedGoal(state.CurrentNightNumber, FrozenHumanCount);
            }
        }

        public void FreezeResolvedNightParameters(PizzeriaState state, ResolvedNightParameters resolved)
        {
            FrozenHumanCount = resolved.FrozenHumanCount;
            if (state != null)
            {
                state.FrozenHumanCount = resolved.FrozenHumanCount;
                state.ResolvedBaseGoal = resolved.BaseGoal;
                state.ResolvedHumanScaleFactor = resolved.HumanScaleFactor;
                state.ResolvedModifierMultiplier = resolved.ModifierGoalMultiplier;
                state.ResolvedGoalSource = (byte)resolved.GoalSource;
                state.ActivePresetId = resolved.PresetId;
                state.NightRevenueGoal = resolved.FinalGoal;
                state.NightReputationMinGoal = resolved.ReputationMinGoal;
                state.NightPizzasMinGoal = resolved.PizzasMinGoal;
                state.IsNonLethalTutorialNight = resolved.NonLethalDefeat;
                state.IncrementRevision();
            }
        }

        public bool CanApplyModifierStack(string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId) || !_catalog.TryGetValue(modifierId, out var def))
                return false;
            if (_activeModifiers.TryGetValue(modifierId, out var existing))
                return existing.Stacks < def.MaxStacks;
            return true;
        }

        public IReadOnlyList<RunModifierDefinition> OpenChoiceVote(int nightNumber, int optionCount = 2)
        {
            _voteStages.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            IsVoteResolved = false;
            LastResolvedOptionId = null;
            PreparationTargetNight = Math.Max(1, nightNumber);

            var eligible = CollectVotePoolForKind(nightNumber, categoryFilter: null, excludeIds: null, includeReserveFallback: false);
            if (eligible.Count < Math.Max(2, optionCount))
            {
                eligible = CollectVotePoolForKind(nightNumber, categoryFilter: null, excludeIds: null, includeReserveFallback: true);
            }

            // Fallback garantido caso o pool se esgote em testes ad-hoc
            if (eligible.Count < 2)
            {
                foreach (var def in _catalog.Values)
                {
                    if (!eligible.Contains(def) && def.Enabled && def.AllowedSource != RunModifierSource.ShopOnly && def.Duration == RunModifierDuration.OneNight)
                        eligible.Add(def);
                }
            }

            DrawWeightedOptionsWithoutReplacement(eligible, Math.Max(2, optionCount), _currentVoteOptions);
            return _currentVoteOptions;
        }

        public IReadOnlyList<RunVoteStageState> OpenPreparationStages(int targetNight, NightPreparationScheduleData scheduleOverride = null)
        {
            _voteStages.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            _readyPlayers.Clear();
            IsVoteResolved = false;
            LastResolvedOptionId = null;
            PreparationTargetNight = Math.Max(1, targetNight);

            var schedule = scheduleOverride ?? (BalancingSnapshot != null
                ? BalancingSnapshot.GetScheduleForNight(PreparationTargetNight)
                : new NightPreparationScheduleData(PreparationTargetNight, 1, 2, 1, 2, PreparationTargetNight >= 2 ? 1 : 0, 2));

            var usedInSession = new HashSet<string>(StringComparer.Ordinal);
            int stageIndex = 0;

            AppendStagesForCategory(RunModifierKind.Hindrance, isMandatory: true, schedule.MandatoryHindranceStages, schedule.HindranceAlternatives, usedInSession, ref stageIndex);
            AppendStagesForCategory(RunModifierKind.Opportunity, isMandatory: false, schedule.OpportunityStages, schedule.OpportunityAlternatives, usedInSession, ref stageIndex);
            AppendStagesForCategory(RunModifierKind.Event, isMandatory: false, schedule.AdditionalEventStages, schedule.EventAlternatives, usedInSession, ref stageIndex);

            if (_voteStages.Count > 0 && _voteStages[0].Options.Count > 0)
            {
                _currentVoteOptions.AddRange(_voteStages[0].Options);
            }

            return _voteStages;
        }

        private void AppendStagesForCategory(
            RunModifierKind kind,
            bool isMandatory,
            int stageCount,
            int alternativesCount,
            HashSet<string> usedInSession,
            ref int stageIndex)
        {
            for (int s = 0; s < stageCount; s++)
            {
                int targetAlts = Math.Max(2, alternativesCount);
                var pool = CollectVotePoolForKind(PreparationTargetNight, kind, usedInSession, includeReserveFallback: false);
                if (pool.Count < targetAlts)
                {
                    pool = CollectVotePoolForKind(PreparationTargetNight, kind, usedInSession, includeReserveFallback: true);
                }
                if (pool.Count < targetAlts)
                {
                    // Permite reutilizar itens nao exauridos caso multiplas etapas na mesma noite superem opcoes ineditas
                    pool = CollectVotePoolForKind(PreparationTargetNight, kind, excludeIds: null, includeReserveFallback: true);
                }

                if (pool.Count == 0) continue;

                var stage = new RunVoteStageState
                {
                    StageIndex = stageIndex++,
                    Category = kind,
                    IsMandatory = isMandatory
                };

                DrawWeightedOptionsWithoutReplacement(pool, targetAlts, stage.Options);
                for (int i = 0; i < stage.Options.Count; i++)
                {
                    usedInSession.Add(stage.Options[i].Id);
                }
                _voteStages.Add(stage);
            }
        }

        private List<RunModifierDefinition> CollectVotePoolForKind(
            int nightNumber,
            RunModifierKind? categoryFilter,
            HashSet<string> excludeIds,
            bool includeReserveFallback)
        {
            var eligible = new List<RunModifierDefinition>();
            foreach (var def in _catalog.Values)
            {
                if (def == null || !def.Enabled) continue;
                if (def.AllowedSource == RunModifierSource.ShopOnly) continue;
                if (categoryFilter.HasValue && def.Kind != categoryFilter.Value) continue;
                if (!def.AllowedDifficulties.Includes(Difficulty)) continue;
                if (def.MinNight > nightNumber || (def.MaxNight > 0 && def.MaxNight < nightNumber)) continue;
                if (!CanApplyModifierStack(def.Id)) continue;
                if (HasIncompatibility(def)) continue;
                if (!HasAllPrerequisites(def)) continue;
                if (excludeIds != null && excludeIds.Contains(def.Id)) continue;
                if (def.IsFallbackReserve && !includeReserveFallback) continue;
                if (!def.IsFallbackReserve && def.Weight <= 0f) continue;

                eligible.Add(def);
            }
            return eligible;
        }

        private void DrawWeightedOptionsWithoutReplacement(List<RunModifierDefinition> pool, int count, List<RunModifierDefinition> destination)
        {
            int targetCount = Math.Min(count, pool.Count);
            for (int i = 0; i < targetCount; i++)
            {
                float totalWeight = 0f;
                for (int k = 0; k < pool.Count; k++)
                {
                    totalWeight += Math.Max(0.0001f, pool[k].Weight);
                }

                double roll = ChoiceRng.NextDouble() * totalWeight;
                double acc = 0d;
                int pickedIdx = pool.Count - 1;
                for (int k = 0; k < pool.Count; k++)
                {
                    acc += Math.Max(0.0001f, pool[k].Weight);
                    if (roll <= acc)
                    {
                        pickedIdx = k;
                        break;
                    }
                }

                destination.Add(pool[pickedIdx]);
                pool.RemoveAt(pickedIdx);
            }
        }

        private bool HasAllPrerequisites(RunModifierDefinition def)
        {
            if (def.PrerequisiteIds == null || def.PrerequisiteIds.Length == 0) return true;
            for (int i = 0; i < def.PrerequisiteIds.Length; i++)
            {
                string pre = def.PrerequisiteIds[i];
                if (!string.IsNullOrWhiteSpace(pre) && !HasModifier(pre)) return false;
            }
            return true;
        }

        public bool CastVote(ulong clientId, string optionId)
        {
            if (IsVoteResolved || string.IsNullOrEmpty(optionId)) return false;
            if (_voteStages.Count > 0)
            {
                for (int s = 0; s < _voteStages.Count; s++)
                {
                    if (CastStageVote(clientId, _voteStages[s].StageIndex, optionId))
                    {
                        return true;
                    }
                }
                return false;
            }

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

        public bool CastStageVote(ulong clientId, int stageIndex, string optionId)
        {
            if (IsVoteResolved || string.IsNullOrEmpty(optionId)) return false;
            for (int s = 0; s < _voteStages.Count; s++)
            {
                var stage = _voteStages[s];
                if (stage.StageIndex != stageIndex) continue;

                for (int o = 0; o < stage.Options.Count; o++)
                {
                    if (stage.Options[o].Id == optionId)
                    {
                        stage.Votes[clientId] = optionId;
                        if (stage.StageIndex == _voteStages[0].StageIndex)
                        {
                            _votes[clientId] = optionId;
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        public bool AreAllMandatoryStagesVoted()
        {
            if (_voteStages.Count == 0) return true;
            for (int s = 0; s < _voteStages.Count; s++)
            {
                var stage = _voteStages[s];
                if (stage.IsMandatory && stage.Votes.Count == 0)
                {
                    return false;
                }
            }
            return true;
        }

        public void SetPlayerReady(ulong clientId, bool ready)
        {
            if (ready) _readyPlayers.Add(clientId);
            else _readyPlayers.Remove(clientId);
        }

        public void OnParticipantDisconnected(ulong clientId)
        {
            if (IsIntermissionLocked)
            {
                return;
            }

            _votes.Remove(clientId);
            _readyPlayers.Remove(clientId);
            for (int s = 0; s < _voteStages.Count; s++)
            {
                _voteStages[s].Votes.Remove(clientId);
            }
        }

        public void OpenTypedBallots(int targetNight, int hindranceOptionsCount = 3, int opportunityOptionsCount = 3)
        {
            var schedule = new NightPreparationScheduleData(
                targetNight,
                mandatoryHindranceStages: 1,
                hindranceAlternatives: hindranceOptionsCount < 1 ? 3 : hindranceOptionsCount,
                opportunityStages: 1,
                opportunityAlternatives: opportunityOptionsCount < 1 ? 3 : opportunityOptionsCount,
                additionalEventStages: 0,
                eventAlternatives: 0);

            OpenPreparationStages(targetNight, schedule);
            IntermissionStage = IntermissionStage.Open;
            RevealStartedAtServerTime = 0d;
            CountdownStartsAtServerTime = 0d;
            StartNightAtServerTime = 0d;
            LastWinningHindranceId = null;
            LastWinningOpportunityId = null;
        }

        public bool CastCategoryVote(ulong clientId, RunModifierKind category, string optionId)
        {
            if (IsIntermissionLocked) return false;
            for (int s = 0; s < _voteStages.Count; s++)
            {
                if (_voteStages[s].Category == category)
                {
                    return CastStageVote(clientId, s, optionId);
                }
            }
            return false;
        }

        public bool TryGetPlayerVote(ulong clientId, RunModifierKind category, out string optionId)
        {
            for (int s = 0; s < _voteStages.Count; s++)
            {
                if (_voteStages[s].Category == category)
                {
                    return _voteStages[s].Votes.TryGetValue(clientId, out optionId);
                }
            }
            optionId = null;
            return false;
        }

        public bool HasPlayerVotedAllMandatoryStages(ulong clientId)
        {
            for (int s = 0; s < _voteStages.Count; s++)
            {
                var stage = _voteStages[s];
                if (stage.IsMandatory && !stage.Votes.ContainsKey(clientId))
                {
                    return false;
                }
            }
            return true;
        }

        public bool TrySetPlayerReady(ulong clientId, bool ready, out string failureReason)
        {
            if (IsIntermissionLocked)
            {
                failureReason = "Votação já encerrada.";
                return false;
            }

            if (ready && !HasPlayerVotedAllMandatoryStages(clientId))
            {
                failureReason = "Vote em Imprevistos e Oportunidades antes de ficar pronto.";
                return false;
            }

            SetPlayerReady(clientId, ready);
            failureReason = null;
            return true;
        }

        public bool TryLockAndResolveBallotsServer(
            double serverNow,
            Func<int, int> nextIntExclusive = null,
            float revealDuration = 4.5f,
            float countdownDuration = 3.0f)
        {
            if (IsIntermissionLocked || _voteStages.Count == 0)
            {
                return false;
            }

            Func<int, int> rng = nextIntExclusive ?? (Func<int, int>)(max => ChoiceRng.Next(max));
            IntermissionStage = IntermissionStage.Locked;

            var candidates = new List<BallotCandidate>(4);
            for (int s = 0; s < _voteStages.Count; s++)
            {
                var stage = _voteStages[s];
                candidates.Clear();

                for (int o = 0; o < stage.Options.Count; o++)
                {
                    string optId = stage.Options[o].Id;
                    int votes = 0;
                    foreach (var kv in stage.Votes)
                    {
                        if (string.Equals(kv.Value, optId, StringComparison.Ordinal))
                        {
                            votes++;
                        }
                    }
                    candidates.Add(new BallotCandidate(optId, votes));
                }

                var result = RunBallotResolver.ResolveWeighted(candidates, rng);
                stage.ResolvedOptionId = result.WinningOptionId;

                if (!string.IsNullOrEmpty(result.WinningOptionId) &&
                    _catalog.TryGetValue(result.WinningOptionId, out var winningDef))
                {
                    ApplyModifier(winningDef);
                    LastResolvedOptionId = result.WinningOptionId;
                    if (stage.Category == RunModifierKind.Hindrance)
                    {
                        LastWinningHindranceId = result.WinningOptionId;
                    }
                    else if (stage.Category == RunModifierKind.Opportunity)
                    {
                        LastWinningOpportunityId = result.WinningOptionId;
                    }
                }
            }

            IsVoteResolved = true;
            RevealStartedAtServerTime = serverNow;
            CountdownStartsAtServerTime = serverNow + Math.Max(0.5f, revealDuration);
            StartNightAtServerTime = CountdownStartsAtServerTime + Math.Max(0.5f, countdownDuration);
            IntermissionStage = IntermissionStage.Revealing;
            return true;
        }

        public void TickIntermissionServer(double serverNow, out bool shouldStartNight)
        {
            shouldStartNight = false;
            if (IntermissionStage == IntermissionStage.Revealing && serverNow >= CountdownStartsAtServerTime)
            {
                IntermissionStage = IntermissionStage.Countdown;
            }

            if (IntermissionStage == IntermissionStage.Countdown && serverNow >= StartNightAtServerTime)
            {
                IntermissionStage = IntermissionStage.Starting;
                shouldStartNight = true;
            }
        }

        public void ResetIntermissionState()
        {
            IntermissionStage = IntermissionStage.None;
            RevealStartedAtServerTime = 0d;
            CountdownStartsAtServerTime = 0d;
            StartNightAtServerTime = 0d;
            LastWinningHindranceId = null;
            LastWinningOpportunityId = null;
        }

        public bool CanStartNight(IReadOnlyCollection<ulong> connectedHumans)
        {
            if (!IsVoteResolved || connectedHumans == null || connectedHumans.Count == 0) return false;
            if (_voteStages.Count > 0 && !AreAllMandatoryStagesVoted()) return false;
            foreach (var id in connectedHumans)
            {
                if (!_readyPlayers.Contains(id)) return false;
            }
            return true;
        }

        public IReadOnlyList<RunModifierDefinition> ResolveAllStagesServer()
        {
            var winners = new List<RunModifierDefinition>();
            if (_voteStages.Count == 0)
            {
                var single = ResolveVoteServer();
                if (single != null) winners.Add(single);
                return winners;
            }

            if (IsVoteResolved)
            {
                for (int s = 0; s < _voteStages.Count; s++)
                {
                    if (!string.IsNullOrEmpty(_voteStages[s].ResolvedOptionId))
                    {
                        var def = GetModifierDefinition(_voteStages[s].ResolvedOptionId);
                        if (def != null) winners.Add(def);
                    }
                }
                return winners;
            }

            for (int s = 0; s < _voteStages.Count; s++)
            {
                var stage = _voteStages[s];
                if (stage.Options.Count == 0) continue;

                // Beneficio livre (Opportunity) sem votos nao e aplicado automaticamente;
                // Entraves obrigatorios e Eventos da noite resolvem vencedor (ou empate por ChoiceRng).
                if (!stage.IsMandatory && stage.Category == RunModifierKind.Opportunity && stage.Votes.Count == 0)
                {
                    continue;
                }

                var eligibleStageOpts = new List<RunModifierDefinition>();
                for (int o = 0; o < stage.Options.Count; o++)
                {
                    var opt = stage.Options[o];
                    if (CanApplyModifierStack(opt.Id) && !HasIncompatibility(opt))
                    {
                        eligibleStageOpts.Add(opt);
                    }
                }
                if (eligibleStageOpts.Count == 0) eligibleStageOpts.AddRange(stage.Options);

                var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var opt in eligibleStageOpts) counts[opt.Id] = 0;
                foreach (var v in stage.Votes.Values)
                {
                    if (counts.ContainsKey(v)) counts[v]++;
                }

                int bestScore = -1;
                var tied = new List<RunModifierDefinition>();
                foreach (var opt in eligibleStageOpts)
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
                stage.ResolvedOptionId = winner.Id;
                ApplyModifier(winner);
                winners.Add(winner);
                if (string.IsNullOrEmpty(LastResolvedOptionId))
                {
                    LastResolvedOptionId = winner.Id;
                }
            }

            IsVoteResolved = true;
            return winners;
        }

        public RunModifierDefinition ResolveVoteServer()
        {
            if (_voteStages.Count > 0)
            {
                var list = ResolveAllStagesServer();
                return list.Count > 0 ? list[0] : null;
            }

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
            _shopNightPurchases.Clear();
            _voteStages.Clear();
            _currentVoteOptions.Clear();
            _votes.Clear();
            _readyPlayers.Clear();
            IsVoteResolved = false;
        }

        public RunEffectiveStats RecalculateEffectiveStats()
        {
            var stats = new RunEffectiveStats();

            var accumulators = new Dictionary<RunEffectTarget, TargetEffectAccumulator>();
            if (Difficulty == RunDifficulty.Hard)
            {
                AccumulateEffect(accumulators, new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 15f), 1);
                AccumulateEffect(accumulators, new RunModifierEffectData(RunEffectTarget.VampireAcquisitionRange, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 20f), 1);
            }

            foreach (var mod in _activeModifiers.Values)
            {
                if (mod == null || mod.Definition == null) continue;
                int stacks = Math.Max(1, mod.Stacks);
                var effects = ResolveEffectiveEffects(mod.Definition);
                for (int i = 0; i < effects.Count; i++)
                {
                    AccumulateEffect(accumulators, effects[i], stacks);
                }
            }

            stats.DemandMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.OrderArrivalRate, 0.3f, 3.0f);
            stats.VampireSpawnMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.VampirePopulation, 0.3f, 4.0f);
            stats.VampireAggressionMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.VampireAcquisitionRange, 0.3f, 3.0f);
            stats.SupplyEtaMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.SupplyDeliveryEta, 0.25f, 3.0f);
            stats.EmployeeErrorMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.EmployeeErrorRate, 0.1f, 2.0f);
            stats.BackpackResistanceMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.BackpackResistance, 0.2f, 2.0f);
            stats.CustomerPatienceMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.CustomerPatience, 0.3f, 2.5f);
            stats.NightGoalMultiplier = ResolveMultiplierTarget(accumulators, RunEffectTarget.NightRevenueGoal, 0.4f, 3.0f);
            stats.ExtraBackpackCapacity = Math.Max(0, (int)Math.Round(ResolveFlatTarget(accumulators, RunEffectTarget.BackpackCapacity, 0f, 10f)));
            stats.OvenAutoShutoffEnabled = ResolveCapabilityTarget(accumulators, RunEffectTarget.OvenAutoShutoff);

            return stats;
        }

        private struct TargetEffectAccumulator
        {
            public float FlatAdd;
            public float PercentDeltaSum;
            public float MultiplierProduct;
            public bool InitializedMultiplier;
            public bool CapabilityEnabled;
            public bool HasCustomLimits;
            public float MinLimit;
            public float MaxLimit;
        }

        private static void AccumulateEffect(
            Dictionary<RunEffectTarget, TargetEffectAccumulator> map,
            RunModifierEffectData effect,
            int stacks)
        {
            if (effect == null || effect.Target == RunEffectTarget.None) return;

            if (!map.TryGetValue(effect.Target, out var acc))
            {
                acc = new TargetEffectAccumulator { MultiplierProduct = 1f, InitializedMultiplier = true };
            }
            if (!acc.InitializedMultiplier)
            {
                acc.MultiplierProduct = 1f;
                acc.InitializedMultiplier = true;
            }

            float domainPerLevel = effect.ToDomainValuePerLevel();
            switch (effect.Operation)
            {
                case RunEffectOperation.AddFlat:
                    acc.FlatAdd += domainPerLevel * stacks;
                    break;
                case RunEffectOperation.PercentDelta:
                    acc.PercentDeltaSum += domainPerLevel * stacks;
                    break;
                case RunEffectOperation.Multiply:
                    acc.MultiplierProduct *= MathF.Pow(Math.Max(0.01f, domainPerLevel), stacks);
                    break;
                case RunEffectOperation.EnableCapability:
                    if (domainPerLevel * stacks > 0f) acc.CapabilityEnabled = true;
                    break;
            }

            if (effect.HasLimits)
            {
                if (!acc.HasCustomLimits)
                {
                    acc.HasCustomLimits = true;
                    acc.MinLimit = effect.MinLimit;
                    acc.MaxLimit = effect.MaxLimit;
                }
                else
                {
                    acc.MinLimit = Math.Max(acc.MinLimit, effect.MinLimit);
                    acc.MaxLimit = Math.Min(acc.MaxLimit, effect.MaxLimit);
                }
            }

            map[effect.Target] = acc;
        }

        private static float ResolveMultiplierTarget(
            Dictionary<RunEffectTarget, TargetEffectAccumulator> map,
            RunEffectTarget target,
            float defaultMin,
            float defaultMax)
        {
            if (!map.TryGetValue(target, out var acc)) return 1f;

            // Ordem unica deterministica: base (1.0) -> adicoes planas -> deltas percentuais relativos -> multiplicadores -> clamp
            float value = (1f + acc.FlatAdd) * Math.Max(0.05f, 1f + acc.PercentDeltaSum) * acc.MultiplierProduct;
            float min = acc.HasCustomLimits ? acc.MinLimit : defaultMin;
            float max = acc.HasCustomLimits ? Math.Max(min, acc.MaxLimit) : defaultMax;
            return Math.Clamp(value, min, max);
        }

        private static float ResolveFlatTarget(
            Dictionary<RunEffectTarget, TargetEffectAccumulator> map,
            RunEffectTarget target,
            float defaultMin,
            float defaultMax)
        {
            if (!map.TryGetValue(target, out var acc)) return 0f;
            float value = (0f + acc.FlatAdd) * Math.Max(0f, 1f + acc.PercentDeltaSum) * acc.MultiplierProduct;
            float min = acc.HasCustomLimits ? acc.MinLimit : defaultMin;
            float max = acc.HasCustomLimits ? Math.Max(min, acc.MaxLimit) : defaultMax;
            return Math.Clamp(value, min, max);
        }

        private static bool ResolveCapabilityTarget(Dictionary<RunEffectTarget, TargetEffectAccumulator> map, RunEffectTarget target)
        {
            return map.TryGetValue(target, out var acc) && acc.CapabilityEnabled;
        }

        private static IReadOnlyList<RunModifierEffectData> ResolveEffectiveEffects(RunModifierDefinition def)
        {
            if (def.Effects != null && def.Effects.Count > 0)
            {
                return def.Effects;
            }

            // Compatibilidade explicita para instancias ad-hoc em testes unitarios legados que preenchem apenas Id + Value
            float v = def.Value;
            return def.Id switch
            {
                "blood_moon" => new[] { new RunModifierEffectData(RunEffectTarget.VampirePopulation, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, v * 100f) },
                "blood_surge" => new[] { new RunModifierEffectData(RunEffectTarget.VampirePopulation, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, v * 100f) },
                "vampire_attack" => new[] { new RunModifierEffectData(RunEffectTarget.VampireAcquisitionRange, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, v * 100f) },
                "heavy_rush" => new[] { new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, v * 100f) },
                "calm_shift" => new[] { new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -Math.Abs(v) * 100f) },
                "focused_staff" => new[] { new RunModifierEffectData(RunEffectTarget.EmployeeErrorRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -Math.Abs(v) * 100f) },
                "express_supply" => new[] { new RunModifierEffectData(RunEffectTarget.SupplyDeliveryEta, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -Math.Abs(v) * 100f) },
                "oven_timer" => new[] { new RunModifierEffectData(RunEffectTarget.OvenAutoShutoff, RunEffectOperation.EnableCapability, RunEffectUnit.BooleanFlag, 1f) },
                "reinforced_bag" => new[] { new RunModifierEffectData(RunEffectTarget.BackpackCapacity, RunEffectOperation.AddFlat, RunEffectUnit.FlatUnits, v) },
                "staff_training" => new[] { new RunModifierEffectData(RunEffectTarget.EmployeeErrorRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -Math.Abs(v) * 100f) },
                _ => Array.Empty<RunModifierEffectData>()
            };
        }

        public bool TryEvaluateShopOffer(
            PizzeriaState state,
            RunShopOfferData offer,
            EmployeeTaskService employees,
            out int effectiveCost,
            out string lockReason)
        {
            effectiveCost = 0;
            lockReason = string.Empty;
            if (offer == null || !offer.Enabled)
            {
                lockReason = "Oferta indisponivel no catalogo.";
                return false;
            }

            if (state == null)
            {
                lockReason = "Estado da pizzaria indisponivel.";
                return false;
            }

            if (state.CurrentNightPhase == NightPhase.Results || state.CurrentNightPhase == NightPhase.Defeat)
            {
                lockReason = "Loja fechada fora da preparacao e do turno.";
                return false;
            }

            bool inPrep = state.CurrentNightPhase == NightPhase.PreparingNextNight || state.CurrentNightPhase == NightPhase.Preparing;
            if (offer.PhaseAvailability == ShopPhaseAvailability.PreparationOnly && !inPrep)
            {
                lockReason = "Disponivel apenas na preparacao.";
                return false;
            }

            if (offer.PhaseAvailability == ShopPhaseAvailability.ActiveShiftOnly && inPrep)
            {
                lockReason = "Disponivel apenas durante a noite ativa.";
                return false;
            }

            int runPurchases = _shopRunPurchases.TryGetValue(offer.OfferId, out var rp) ? rp : 0;
            int nightPurchases = _shopNightPurchases.TryGetValue(offer.OfferId, out var np) ? np : 0;

            if (offer.MaxPurchasesPerRun > 0 && runPurchases >= offer.MaxPurchasesPerRun)
            {
                lockReason = $"Limite por RUN atingido ({runPurchases}/{offer.MaxPurchasesPerRun}).";
                return false;
            }

            if (offer.MaxPurchasesPerNight > 0 && nightPurchases >= offer.MaxPurchasesPerNight)
            {
                lockReason = $"Limite por noite atingido ({nightPurchases}/{offer.MaxPurchasesPerNight}).";
                return false;
            }

            switch (offer.BenefitKind)
            {
                case ShopBenefitKind.GrantModifier:
                {
                    var modDef = GetModifierDefinition(offer.ModifierId);
                    if (modDef == null)
                    {
                        lockReason = "Modificador associado ausente no catalogo.";
                        return false;
                    }

                    int stacks = GetModifierStacks(offer.ModifierId);
                    if (stacks >= modDef.MaxStacks)
                    {
                        lockReason = $"Nivel maximo atingido ({stacks}/{modDef.MaxStacks}).";
                        return false;
                    }

                    effectiveCost = offer.ComputeEffectiveCost(stacks);
                    break;
                }
                case ShopBenefitKind.HireEmployee:
                {
                    int hiredCount = 0;
                    if (employees != null)
                    {
                        foreach (var emp in employees.Employees.Values)
                        {
                            if (emp.Role == offer.EmployeeRole) hiredCount++;
                        }
                    }

                    var profile = employees?.GetRoleProfile(offer.EmployeeRole) ?? BalancingSnapshot?.FindEmployeeProfile(offer.EmployeeRole);
                    int baseRoleCost = profile != null ? profile.HireCost : offer.BaseCost;
                    effectiveCost = offer.CostIncreasePerLevel > 0
                        ? baseRoleCost + offer.CostIncreasePerLevel * hiredCount
                        : baseRoleCost;
                    break;
                }
                default:
                    effectiveCost = offer.ComputeEffectiveCost(runPurchases);
                    break;
            }

            if (state.RestaurantBudget < effectiveCost)
            {
                lockReason = $"Caixa insuficiente (precisa R$ {effectiveCost}).";
                return false;
            }

            return true;
        }

        public bool TryPurchaseCatalogOffer(
            PizzeriaState state,
            string offerId,
            EmployeeTaskService employees,
            Func<IReadOnlyList<SupplyBundleLineData>, int> applySupplyBundle,
            out string failureReason,
            out int appliedCost,
            out HiredEmployeeState hiredEmployee)
        {
            failureReason = string.Empty;
            appliedCost = 0;
            hiredEmployee = null;

            var offer = BalancingSnapshot?.FindShopOfferById(offerId);
            if (!TryEvaluateShopOffer(state, offer, employees, out appliedCost, out failureReason))
            {
                return false;
            }

            switch (offer.BenefitKind)
            {
                case ShopBenefitKind.GrantModifier:
                {
                    var modDef = GetModifierDefinition(offer.ModifierId);
                    if (modDef == null || !CanApplyModifierStack(offer.ModifierId))
                    {
                        failureReason = "Nao foi possivel aplicar o modificador.";
                        return false;
                    }

                    state.RestaurantBudget -= appliedCost;
                    state.NightShopSpent += appliedCost;
                    ApplyModifier(modDef);
                    break;
                }
                case ShopBenefitKind.SupplyBundle:
                {
                    if (applySupplyBundle == null)
                    {
                        failureReason = "Aplicador de suprimentos ausente.";
                        return false;
                    }

                    int added = applySupplyBundle(offer.SupplyBundleLines);
                    if (added <= 0)
                    {
                        failureReason = "Armazenamento cheio para todos os itens deste pacote.";
                        return false;
                    }

                    state.RestaurantBudget -= appliedCost;
                    state.NightShopSpent += appliedCost;
                    break;
                }
                case ShopBenefitKind.HireEmployee:
                {
                    if (employees == null)
                    {
                        failureReason = "Servico de funcionarios indisponivel.";
                        return false;
                    }

                    hiredEmployee = employees.TryHireEmployeeWithExplicitCost(state, offer.EmployeeRole, appliedCost);
                    if (hiredEmployee == null)
                    {
                        failureReason = "Falha ao contratar funcionario.";
                        return false;
                    }
                    break;
                }
                default:
                    failureReason = "Tipo de oferta desconhecido.";
                    return false;
            }

            _shopRunPurchases[offer.OfferId] = (_shopRunPurchases.TryGetValue(offer.OfferId, out var rp) ? rp : 0) + 1;
            _shopNightPurchases[offer.OfferId] = (_shopNightPurchases.TryGetValue(offer.OfferId, out var np) ? np : 0) + 1;
            if (offer.MaxPurchasesPerRun == 1)
            {
                _purchasedUniqueItems.Add(offer.OfferId);
            }

            state.IncrementRevision();
            return true;
        }

        public bool TryPurchaseShopItem(PizzeriaState state, RunShopItem item)
        {
            if (state == null || item == null || item.Cost < 0) return false;
            if (item.IsUnique && _purchasedUniqueItems.Contains(item.Id)) return false;
            if (state.RestaurantBudget < item.Cost) return false;

            state.RestaurantBudget -= item.Cost;
            state.NightShopSpent += item.Cost;
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

        public int GetShopOfferRunPurchases(string offerId) => !string.IsNullOrEmpty(offerId) && _shopRunPurchases.TryGetValue(offerId, out var c) ? c : 0;
        public int GetShopOfferNightPurchases(string offerId) => !string.IsNullOrEmpty(offerId) && _shopNightPurchases.TryGetValue(offerId, out var c) ? c : 0;

        private bool HasIncompatibility(RunModifierDefinition def)
        {
            if (def.IncompatibleIds == null) return false;
            for (int i = 0; i < def.IncompatibleIds.Length; i++)
            {
                if (_activeModifiers.ContainsKey(def.IncompatibleIds[i])) return true;
            }
            return false;
        }
    }
}
