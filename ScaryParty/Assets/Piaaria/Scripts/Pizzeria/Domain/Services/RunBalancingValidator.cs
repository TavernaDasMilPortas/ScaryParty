using System;
using System.Collections.Generic;
using System.Text;
using ScaryParty.Pizzeria.Domain.Definitions;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public enum RunValidationSeverity : byte
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    public sealed class RunValidationIssue
    {
        public RunValidationSeverity Severity { get; }
        public string Category { get; }
        public string TargetId { get; }
        public string Message { get; }

        public RunValidationIssue(RunValidationSeverity severity, string category, string targetId, string message)
        {
            Severity = severity;
            Category = category ?? string.Empty;
            TargetId = targetId ?? string.Empty;
            Message = message ?? string.Empty;
        }
    }

    public sealed class RunValidationReport
    {
        public List<RunValidationIssue> Issues { get; } = new List<RunValidationIssue>();
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;

        public void Add(RunValidationSeverity severity, string category, string targetId, string message)
        {
            Issues.Add(new RunValidationIssue(severity, category, targetId, message));
            if (severity == RunValidationSeverity.Error) ErrorCount++;
            else if (severity == RunValidationSeverity.Warning) WarningCount++;
        }

        public string BuildSummaryText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Erros: {ErrorCount} | Avisos: {WarningCount} | Total de apontamentos: {Issues.Count}");
            for (int i = 0; i < Issues.Count; i++)
            {
                var issue = Issues[i];
                sb.AppendLine($"[{issue.Severity}] ({issue.Category} :: {issue.TargetId}) {issue.Message}");
            }
            return sb.ToString();
        }
    }

    public sealed class RunDrawSimulationResult
    {
        public int Iterations { get; }
        public int NightNumber { get; }
        public RunDifficulty Difficulty { get; }
        public RunModifierKind Kind { get; }
        public int EligiblePoolCount { get; }
        public int OptionsPerDraw { get; }
        public float TotalWeight { get; }
        public bool UsedReserveFallback { get; }
        public Dictionary<string, int> SelectionCounts { get; } = new Dictionary<string, int>(StringComparer.Ordinal);
        public Dictionary<string, double> ExpectedProbabilities { get; } = new Dictionary<string, double>(StringComparer.Ordinal);

        public RunDrawSimulationResult(
            int iterations,
            int nightNumber,
            RunDifficulty difficulty,
            RunModifierKind kind,
            int eligiblePoolCount,
            int optionsPerDraw,
            float totalWeight,
            bool usedReserveFallback)
        {
            Iterations = iterations;
            NightNumber = nightNumber;
            Difficulty = difficulty;
            Kind = kind;
            EligiblePoolCount = eligiblePoolCount;
            OptionsPerDraw = optionsPerDraw;
            TotalWeight = totalWeight;
            UsedReserveFallback = usedReserveFallback;
        }
    }

    /// <summary>
    /// Validador puro de dominio e simulador isolado de sorteios para RunBalancingSnapshot (B2).
    /// </summary>
    public static class RunBalancingValidator
    {
        public static RunValidationReport Validate(RunBalancingSnapshot snapshot)
        {
            var report = new RunValidationReport();
            if (snapshot == null)
            {
                report.Add(RunValidationSeverity.Error, "Catalogo", "-", "RunBalancingSnapshot nulo.");
                return report;
            }

            if (snapshot.Modifiers.Count == 0)
            {
                report.Add(RunValidationSeverity.Error, "Modificadores", "-", "Nenhum modificador configurado no catalogo.");
            }

            ValidateModifiers(snapshot, report);
            ValidateShopOffers(snapshot, report);
            ValidateEmployeeProfiles(snapshot, report);
            ValidateSchedulesAndPools(snapshot, report);

            return report;
        }

        private static void ValidateModifiers(RunBalancingSnapshot snapshot, RunValidationReport report)
        {
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            var validIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                var m = snapshot.Modifiers[i];
                if (m != null && !string.IsNullOrWhiteSpace(m.Id))
                {
                    validIds.Add(m.Id);
                }
            }

            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                var m = snapshot.Modifiers[i];
                if (m == null) continue;

                if (string.IsNullOrWhiteSpace(m.Id))
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", $"index:{i}", "ID vazio ou invalido.");
                    continue;
                }

                if (!seenIds.Add(m.Id))
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "ID duplicado no catalogo.");
                }

                if (m.MaxStacks < 1)
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "MaxStacks deve ser >= 1.");
                }

                if (m.MinNight < 1)
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "MinNight deve ser >= 1.");
                }

                if (m.MaxNight > 0 && m.MaxNight < m.MinNight)
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, $"MaxNight ({m.MaxNight}) menor que MinNight ({m.MinNight}).");
                }

                if (m.Weight < 0f)
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "Weight nao pode ser negativo.");
                }

                if (m.Enabled && m.AllowedSource != RunModifierSource.ShopOnly && m.Weight <= 0f)
                {
                    report.Add(RunValidationSeverity.Warning, "Modificador", m.Id, "Modificador elegivel para sorteio tem Weight = 0 e nunca sera sorteado.");
                }

                if (m.Effects == null || m.Effects.Count == 0)
                {
                    report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "Modificador sem efeitos tipados configurados.");
                }
                else
                {
                    for (int e = 0; e < m.Effects.Count; e++)
                    {
                        var eff = m.Effects[e];
                        if (eff == null || eff.Target == RunEffectTarget.None)
                        {
                            report.Add(RunValidationSeverity.Error, "Efeito", m.Id, $"Efeito [{e}] possui Target = None.");
                            continue;
                        }

                        if (eff.HasLimits && eff.MaxLimit < eff.MinLimit)
                        {
                            report.Add(RunValidationSeverity.Error, "Efeito", m.Id, $"Efeito [{e}] ({eff.Target}) tem MaxLimit ({eff.MaxLimit}) < MinLimit ({eff.MinLimit}).");
                        }
                    }
                }

                if (m.IncompatibleIds != null)
                {
                    for (int j = 0; j < m.IncompatibleIds.Length; j++)
                    {
                        string inc = m.IncompatibleIds[j];
                        if (string.IsNullOrWhiteSpace(inc)) continue;
                        if (string.Equals(inc, m.Id, StringComparison.Ordinal))
                        {
                            report.Add(RunValidationSeverity.Error, "Modificador", m.Id, "Modificador marcado como incompativel consigo mesmo.");
                        }
                        else if (!validIds.Contains(inc))
                        {
                            report.Add(RunValidationSeverity.Warning, "Modificador", m.Id, $"IncompatibleId '{inc}' nao encontrado no catalogo.");
                        }
                    }
                }

                if (m.PrerequisiteIds != null)
                {
                    for (int j = 0; j < m.PrerequisiteIds.Length; j++)
                    {
                        string pre = m.PrerequisiteIds[j];
                        if (!string.IsNullOrWhiteSpace(pre) && !validIds.Contains(pre))
                        {
                            report.Add(RunValidationSeverity.Error, "Modificador", m.Id, $"PrerequisiteId '{pre}' nao existe no catalogo.");
                        }
                    }
                }
            }
        }

        private static void ValidateShopOffers(RunBalancingSnapshot snapshot, RunValidationReport report)
        {
            var seenOffers = new HashSet<string>(StringComparer.Ordinal);
            var seenLegacyBytes = new HashSet<byte>();

            for (int i = 0; i < snapshot.ShopOffers.Count; i++)
            {
                var o = snapshot.ShopOffers[i];
                if (o == null) continue;

                if (string.IsNullOrWhiteSpace(o.OfferId))
                {
                    report.Add(RunValidationSeverity.Error, "Loja", $"index:{i}", "OfferId vazio.");
                    continue;
                }

                if (!seenOffers.Add(o.OfferId))
                {
                    report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, "OfferId duplicado.");
                }

                if (o.LegacyByteId != 0 && !seenLegacyBytes.Add(o.LegacyByteId))
                {
                    report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, $"LegacyByteId {o.LegacyByteId} duplicado em outra oferta.");
                }

                if (o.BaseCost < 0 || o.CostIncreasePerLevel < 0)
                {
                    report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, "Custos negativos nao sao permitidos.");
                }

                if (o.BenefitKind == ShopBenefitKind.GrantModifier)
                {
                    var mod = snapshot.FindModifierById(o.ModifierId);
                    if (mod == null)
                    {
                        report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, $"ModifierId '{o.ModifierId}' nao encontrado.");
                    }
                    else
                    {
                        if (mod.AllowedSource == RunModifierSource.VoteOnly)
                        {
                            report.Add(RunValidationSeverity.Warning, "Loja", o.OfferId, $"Modificador '{mod.Id}' esta como VoteOnly mas referenciado por oferta da loja.");
                        }
                        if (o.MaxPurchasesPerRun > 0 && o.MaxPurchasesPerRun > mod.MaxStacks)
                        {
                            report.Add(RunValidationSeverity.Warning, "Loja", o.OfferId, $"MaxPurchasesPerRun ({o.MaxPurchasesPerRun}) > MaxStacks ({mod.MaxStacks}); o limite efetivo sera {mod.MaxStacks}.");
                        }
                    }
                }
                else if (o.BenefitKind == ShopBenefitKind.SupplyBundle)
                {
                    if (o.SupplyBundleLines == null || o.SupplyBundleLines.Count == 0)
                    {
                        report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, "Pacote de suprimentos sem ingredientes configurados.");
                    }
                    else
                    {
                        for (int l = 0; l < o.SupplyBundleLines.Count; l++)
                        {
                            var line = o.SupplyBundleLines[l];
                            if (line.IngredientId <= 0 || line.Quantity <= 0)
                            {
                                report.Add(RunValidationSeverity.Error, "Loja", o.OfferId, $"Linha [{l}] de pacote de suprimentos invalida (IngredientId={line.IngredientId}, Qty={line.Quantity}).");
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateEmployeeProfiles(RunBalancingSnapshot snapshot, RunValidationReport report)
        {
            var seenRoles = new HashSet<EmployeeRole>();
            for (int i = 0; i < snapshot.EmployeeProfiles.Count; i++)
            {
                var p = snapshot.EmployeeProfiles[i];
                if (p == null) continue;

                if (!seenRoles.Add(p.Role))
                {
                    report.Add(RunValidationSeverity.Error, "Funcionarios", p.Role.ToString(), "Perfil duplicado para a funcao.");
                }

                if (p.MinErrorRate > p.BaseErrorRate)
                {
                    report.Add(RunValidationSeverity.Error, "Funcionarios", p.Role.ToString(), $"MinErrorRate ({p.MinErrorRate:P0}) maior que BaseErrorRate ({p.BaseErrorRate:P0}).");
                }

                if (p.MaxLevel < 1)
                {
                    report.Add(RunValidationSeverity.Error, "Funcionarios", p.Role.ToString(), "MaxLevel deve ser >= 1.");
                }
            }
        }

        private static void ValidateSchedulesAndPools(RunBalancingSnapshot snapshot, RunValidationReport report)
        {
            var seenNights = new HashSet<int>();
            for (int i = 0; i < snapshot.PreparationSchedules.Count; i++)
            {
                var s = snapshot.PreparationSchedules[i];
                if (s == null) continue;
                if (!seenNights.Add(s.NightNumber))
                {
                    report.Add(RunValidationSeverity.Error, "Agenda", $"Noite {s.NightNumber}", "Agenda duplicada para o mesmo numero de noite.");
                }

                CheckCategoryPool(snapshot, s.NightNumber, RunModifierKind.Hindrance, s.MandatoryHindranceStages, s.HindranceAlternatives, report);
                CheckCategoryPool(snapshot, s.NightNumber, RunModifierKind.Opportunity, s.OpportunityStages, s.OpportunityAlternatives, report);
                CheckCategoryPool(snapshot, s.NightNumber, RunModifierKind.Event, s.AdditionalEventStages, s.EventAlternatives, report);
            }
        }

        private static void CheckCategoryPool(
            RunBalancingSnapshot snapshot,
            int nightNumber,
            RunModifierKind kind,
            int stages,
            int alternatives,
            RunValidationReport report)
        {
            if (stages <= 0) return;

            int eligibleCount = 0;
            int reserveCount = 0;
            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                var m = snapshot.Modifiers[i];
                if (m == null || !m.Enabled || m.Kind != kind) continue;
                if (m.AllowedSource == RunModifierSource.ShopOnly) continue;
                if (m.MinNight > nightNumber || (m.MaxNight > 0 && m.MaxNight < nightNumber)) continue;

                if (m.IsFallbackReserve) reserveCount++;
                else if (m.Weight > 0f) eligibleCount++;
            }

            if (eligibleCount + reserveCount < alternatives)
            {
                report.Add(
                    RunValidationSeverity.Error,
                    "PoolVotacao",
                    $"Noite {nightNumber}/{kind}",
                    $"Pool insuficiente: {eligibleCount + reserveCount} disponivel(is) para {alternatives} alternativas na categoria {kind}.");
            }
            else if (eligibleCount < alternatives && reserveCount > 0)
            {
                report.Add(
                    RunValidationSeverity.Warning,
                    "PoolVotacao",
                    $"Noite {nightNumber}/{kind}",
                    $"Pool principal tem apenas {eligibleCount} item(ns) e dependera de alternativa de reserva.");
            }
        }

        /// <summary>
        /// Simula N sorteios ponderados sem reposicao em RNG isolado (sem alterar estado de jogo).
        /// </summary>
        public static RunDrawSimulationResult SimulateCategoryDraw(
            RunBalancingSnapshot snapshot,
            RunModifierKind kind,
            int nightNumber,
            RunDifficulty difficulty,
            int optionsPerDraw,
            int iterations,
            int seed)
        {
            var pool = CollectEligiblePool(snapshot, kind, nightNumber, difficulty, includeReserveFallback: false, out bool usedReserve);
            int targetOptions = Math.Max(2, optionsPerDraw);
            if (pool.Count < targetOptions)
            {
                pool = CollectEligiblePool(snapshot, kind, nightNumber, difficulty, includeReserveFallback: true, out _);
                usedReserve = true;
            }

            float totalWeight = 0f;
            for (int i = 0; i < pool.Count; i++)
            {
                totalWeight += Math.Max(0.0001f, pool[i].Weight);
            }

            var result = new RunDrawSimulationResult(iterations, nightNumber, difficulty, kind, pool.Count, Math.Min(targetOptions, pool.Count), totalWeight, usedReserve);
            for (int i = 0; i < pool.Count; i++)
            {
                result.SelectionCounts[pool[i].Id] = 0;
                result.ExpectedProbabilities[pool[i].Id] = totalWeight > 0f ? Math.Max(0.0001f, pool[i].Weight) / totalWeight : 0d;
            }

            if (pool.Count == 0 || iterations <= 0) return result;

            var rng = new Random(seed);
            var tempPool = new List<RunModifierDefinition>(pool.Count);
            int drawCount = Math.Min(targetOptions, pool.Count);

            for (int iter = 0; iter < iterations; iter++)
            {
                tempPool.Clear();
                tempPool.AddRange(pool);

                for (int d = 0; d < drawCount; d++)
                {
                    int pickedIdx = PickWeightedIndex(tempPool, rng);
                    var picked = tempPool[pickedIdx];
                    result.SelectionCounts[picked.Id] = result.SelectionCounts[picked.Id] + 1;
                    tempPool.RemoveAt(pickedIdx);
                }
            }

            return result;
        }

        private static List<RunModifierDefinition> CollectEligiblePool(
            RunBalancingSnapshot snapshot,
            RunModifierKind kind,
            int nightNumber,
            RunDifficulty difficulty,
            bool includeReserveFallback,
            out bool anyReserve)
        {
            anyReserve = false;
            var list = new List<RunModifierDefinition>();
            if (snapshot == null) return list;

            for (int i = 0; i < snapshot.Modifiers.Count; i++)
            {
                var m = snapshot.Modifiers[i];
                if (m == null || !m.Enabled || m.Kind != kind) continue;
                if (m.AllowedSource == RunModifierSource.ShopOnly) continue;
                if (!m.AllowedDifficulties.Includes(difficulty)) continue;
                if (m.MinNight > nightNumber || (m.MaxNight > 0 && m.MaxNight < nightNumber)) continue;
                if (m.IsFallbackReserve)
                {
                    if (!includeReserveFallback) continue;
                    anyReserve = true;
                }
                else if (m.Weight <= 0f)
                {
                    continue;
                }

                list.Add(m);
            }
            return list;
        }

        private static int PickWeightedIndex(List<RunModifierDefinition> items, Random rng)
        {
            float sum = 0f;
            for (int i = 0; i < items.Count; i++)
            {
                sum += Math.Max(0.0001f, items[i].Weight);
            }

            double roll = rng.NextDouble() * sum;
            double acc = 0d;
            for (int i = 0; i < items.Count; i++)
            {
                acc += Math.Max(0.0001f, items[i].Weight);
                if (roll <= acc) return i;
            }
            return items.Count - 1;
        }
    }
}
