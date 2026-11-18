using System;
using System.Collections.Generic;
using System.Text;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public struct RunBalancingMigrationReport
    {
        public int CreatedAssets;
        public int AddedModifiers;
        public int AddedShopOffers;
        public int AddedEmployeeProfiles;
        public int AddedSchedules;
        public int UpdatedReferences;
        public string Summary;

        public int TotalChanges => CreatedAssets + AddedModifiers + AddedShopOffers + AddedEmployeeProfiles + AddedSchedules + UpdatedReferences;
    }

    /// <summary>
    /// Migracao idempotente de B0 para o catalogo unico de eventos, loja, funcionarios e preparacao por noite.
    /// Nunca sobrescreve entradas ja existentes editadas pelo usuario.
    /// </summary>
    public static class RunBalancingMigrationRunner
    {
        public const string BalancingFolder = "Assets/Piaaria/ScriptableObjects/Pizzeria/Balancing";
        public const string CatalogAssetPath = BalancingFolder + "/RunBalancingCatalog.asset";
        public const string ActivePizzeriaConfigPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset";
        public const string LegacyPizzeriaConfigPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig.asset";
        public const string ManifestAssetPath = "Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset";

        private const string IngredientDoughPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/Ingredient_1_Massa.asset";
        private const string IngredientSaucePath = "Assets/Piaaria/ScriptableObjects/Pizzeria/Ingredient_3_Molho.asset";
        private const string IngredientCheesePath = "Assets/Piaaria/ScriptableObjects/Pizzeria/Ingredient_4_QueijoInteiro.asset";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Pizzaria/Migrar Catálogo de Eventos, Loja e Balanceamento", false, 252)]
        public static void RunMigrationMenu()
        {
            var report = RunIdempotentMigration();
            Debug.Log(report.Summary);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(
                    "Migracao de Eventos, Loja e Balanceamento",
                    $"Alteracoes aplicadas: {report.TotalChanges}\n\n{report.Summary}",
                    "OK");
            }
        }

        public static RunBalancingMigrationReport PreviewMigration()
        {
            return ExecuteInternal(applyChanges: false);
        }

        public static RunBalancingMigrationReport RunIdempotentMigration()
        {
            return ExecuteInternal(applyChanges: true);
        }

        private static RunBalancingMigrationReport ExecuteInternal(bool applyChanges)
        {
            var sb = new StringBuilder();
            var report = new RunBalancingMigrationReport();

            var existingCatalog = AssetDatabase.LoadAssetAtPath<RunBalancingCatalog>(CatalogAssetPath);
            RunBalancingCatalog workingCatalog = existingCatalog;
            bool catalogCreated = false;

            if (workingCatalog == null)
            {
                report.CreatedAssets++;
                sb.AppendLine($"[CREATE] {CatalogAssetPath}");
                if (applyChanges)
                {
                    EnsureFolder("Assets/Piaaria/ScriptableObjects/Pizzeria", "Balancing");
                    workingCatalog = ScriptableObject.CreateInstance<RunBalancingCatalog>();
                    AssetDatabase.CreateAsset(workingCatalog, CatalogAssetPath);
                    catalogCreated = true;
                }
                else
                {
                    workingCatalog = ScriptableObject.CreateInstance<RunBalancingCatalog>();
                }
            }

            if (workingCatalog.modifiers == null) workingCatalog.modifiers = new List<RunModifierAuthoringEntry>();
            if (workingCatalog.shopOffers == null) workingCatalog.shopOffers = new List<RunShopOfferAuthoringEntry>();
            if (workingCatalog.employeeProfiles == null) workingCatalog.employeeProfiles = new List<EmployeeRoleProfileAuthoringEntry>();
            if (workingCatalog.preparationSchedules == null) workingCatalog.preparationSchedules = new List<NightPreparationScheduleAuthoringEntry>();

            var defaultModifiers = BuildDefaultModifiers();
            for (int i = 0; i < defaultModifiers.Count; i++)
            {
                var def = defaultModifiers[i];
                if (workingCatalog.FindModifierById(def.id) == null)
                {
                    report.AddedModifiers++;
                    sb.AppendLine($"[+Modifier] {def.id} ({def.displayName}) [{def.kind}]");
                    if (applyChanges) workingCatalog.modifiers.Add(def);
                }
            }

            var defaultOffers = BuildDefaultShopOffers();
            for (int i = 0; i < defaultOffers.Count; i++)
            {
                var offer = defaultOffers[i];
                if (workingCatalog.FindShopOfferById(offer.offerId) == null)
                {
                    report.AddedShopOffers++;
                    sb.AppendLine($"[+ShopOffer] {offer.offerId} ({offer.displayName}) — R$ {offer.baseCost}");
                    if (applyChanges) workingCatalog.shopOffers.Add(offer);
                }
            }

            var defaultProfiles = BuildDefaultEmployeeProfiles();
            for (int i = 0; i < defaultProfiles.Count; i++)
            {
                var prof = defaultProfiles[i];
                if (workingCatalog.FindEmployeeProfile(prof.role) == null)
                {
                    report.AddedEmployeeProfiles++;
                    sb.AppendLine($"[+EmployeeProfile] {prof.role} — R$ {prof.hireCost}, erro {prof.baseErrorRatePercent}%");
                    if (applyChanges) workingCatalog.employeeProfiles.Add(prof);
                }
            }

            var defaultSchedules = BuildDefaultSchedules();
            for (int i = 0; i < defaultSchedules.Count; i++)
            {
                var sched = defaultSchedules[i];
                if (workingCatalog.FindScheduleForNight(sched.nightNumber) == null)
                {
                    report.AddedSchedules++;
                    sb.AppendLine($"[+Schedule] Noite {sched.nightNumber}: Entraves={sched.mandatoryHindranceStages}, Beneficios={sched.opportunityStages}, Eventos={sched.additionalEventStages}");
                    if (applyChanges) workingCatalog.preparationSchedules.Add(sched);
                }
            }

            bool catalogDirty = catalogCreated ||
                                (applyChanges && (report.AddedModifiers > 0 || report.AddedShopOffers > 0 || report.AddedEmployeeProfiles > 0 || report.AddedSchedules > 0));

            if (applyChanges && catalogDirty)
            {
                EditorUtility.SetDirty(workingCatalog);
            }

            // Vincular catalogo no PizzeriaConfig ativo e legado (somente se ausente)
            LinkConfigReference(ActivePizzeriaConfigPath, workingCatalog, applyChanges, ref report, sb);
            LinkConfigReference(LegacyPizzeriaConfigPath, workingCatalog, applyChanges, ref report, sb);

            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(ManifestAssetPath);
            if (manifest != null && manifest.activeBalancingCatalog == null)
            {
                report.UpdatedReferences++;
                sb.AppendLine($"[Link] {ManifestAssetPath}.activeBalancingCatalog -> {CatalogAssetPath}");
                if (applyChanges)
                {
                    manifest.activeBalancingCatalog = workingCatalog;
                    EditorUtility.SetDirty(manifest);
                }
            }

            if (applyChanges)
            {
                if (report.TotalChanges > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }
            else if (existingCatalog == null && workingCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(workingCatalog);
            }

            if (report.TotalChanges == 0)
            {
                sb.AppendLine("[OK] Nenhuma alteracao pendente; catalogo e referencias ja estao migrados (idempotente).");
            }

            report.Summary = sb.ToString();
            return report;
        }

        private static void LinkConfigReference(
            string configPath,
            RunBalancingCatalog catalog,
            bool applyChanges,
            ref RunBalancingMigrationReport report,
            StringBuilder sb)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(configPath);
            if (cfg != null && cfg.balancingCatalog == null)
            {
                report.UpdatedReferences++;
                sb.AppendLine($"[Link] {configPath}.balancingCatalog -> {CatalogAssetPath}");
                if (applyChanges && catalog != null)
                {
                    cfg.balancingCatalog = catalog;
                    EditorUtility.SetDirty(cfg);
                }
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        public static List<RunModifierAuthoringEntry> BuildDefaultModifiers()
        {
            return new List<RunModifierAuthoringEntry>
            {
                // --- 7 MODIFICADORES ORIGINAIS ---
                new RunModifierAuthoringEntry
                {
                    id = "blood_moon",
                    displayName = "Lua vermelha",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    tags = new List<string> { "population", "vampire" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.VampirePopulation,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 50f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 3.0f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "vampire_attack",
                    displayName = "Ataque dos vampiros",
                    kind = RunModifierKind.Hindrance,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    tags = new List<string> { "combat", "vampire" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.VampireAcquisitionRange,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 35f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "heavy_rush",
                    displayName = "Movimento intenso",
                    kind = RunModifierKind.Hindrance,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "festival_demand" },
                    tags = new List<string> { "orders", "demand" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.OrderArrivalRate,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 30f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "express_supply",
                    displayName = "Fornecimento expresso",
                    kind = RunModifierKind.Opportunity,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "slow_supply", "storm_logistics" },
                    tags = new List<string> { "supply", "eta" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.SupplyDeliveryEta,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = -40f,
                            useLimits = true,
                            minLimit = 0.3f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "oven_timer",
                    displayName = "Timer do forno",
                    kind = RunModifierKind.PermanentUpgrade,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteAndShop,
                    duration = RunModifierDuration.Permanent,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    tags = new List<string> { "kitchen", "oven" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.OvenAutoShutoff,
                            operation = RunEffectOperation.EnableCapability,
                            unit = RunEffectUnit.BooleanFlag,
                            valuePerLevel = 1f,
                            useLimits = false
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "reinforced_bag",
                    displayName = "Bolsa reforcada",
                    kind = RunModifierKind.PermanentUpgrade,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteAndShop,
                    duration = RunModifierDuration.Permanent,
                    maxStacks = 2,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    tags = new List<string> { "delivery", "backpack" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.BackpackCapacity,
                            operation = RunEffectOperation.AddFlat,
                            unit = RunEffectUnit.FlatUnits,
                            valuePerLevel = 1f,
                            useLimits = true,
                            minLimit = 0f,
                            maxLimit = 2f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "staff_training",
                    displayName = "Treinamento",
                    kind = RunModifierKind.PermanentUpgrade,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteAndShop,
                    duration = RunModifierDuration.Permanent,
                    maxStacks = 2,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    tags = new List<string> { "employees", "training" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.EmployeeErrorRate,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = -25f,
                            useLimits = true,
                            minLimit = 0.2f,
                            maxLimit = 1.5f
                        }
                    }
                },

                // --- NOVOS MODIFICADORES NECESSARIOS PARA POOLS DISTINTAS (B3) ---
                new RunModifierAuthoringEntry
                {
                    id = "impatient_customers",
                    displayName = "Clientes impacientes",
                    kind = RunModifierKind.Hindrance,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "calm_customers" },
                    tags = new List<string> { "customers", "patience" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.CustomerPatience,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = -25f,
                            useLimits = true,
                            minLimit = 0.4f,
                            maxLimit = 2.0f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "slow_supply",
                    displayName = "Entrega demorada",
                    kind = RunModifierKind.Hindrance,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    isFallbackReserve = true,
                    incompatibleIds = new List<string> { "express_supply" },
                    tags = new List<string> { "supply", "eta" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.SupplyDeliveryEta,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 35f,
                            useLimits = true,
                            minLimit = 0.3f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "calm_customers",
                    displayName = "Clientes pacientes",
                    kind = RunModifierKind.Opportunity,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "impatient_customers" },
                    tags = new List<string> { "customers", "patience" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.CustomerPatience,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 25f,
                            useLimits = true,
                            minLimit = 0.4f,
                            maxLimit = 2.0f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "quiet_night",
                    displayName = "Noite tranquila",
                    kind = RunModifierKind.Opportunity,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 0.8f,
                    isFallbackReserve = true,
                    incompatibleIds = new List<string> { "blood_moon" },
                    tags = new List<string> { "population", "vampire" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.VampirePopulation,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = -25f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 3.0f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "festival_demand",
                    displayName = "Festival na cidade",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "heavy_rush" },
                    tags = new List<string> { "orders", "event" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.OrderArrivalRate,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 25f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "vampire_prowl",
                    displayName = "Ronda dos vampiros",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 1f,
                    incompatibleIds = new List<string> { "vampire_attack" },
                    tags = new List<string> { "combat", "vampire", "event" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.VampireAcquisitionRange,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 25f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "storm_logistics",
                    displayName = "Tempestade nas vias",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 0.9f,
                    incompatibleIds = new List<string> { "express_supply", "slow_supply" },
                    tags = new List<string> { "supply", "event" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.SupplyDeliveryEta,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 25f,
                            useLimits = true,
                            minLimit = 0.3f,
                            maxLimit = 2.5f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "midnight_surge",
                    displayName = "Madrugada agitada",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 0.8f,
                    isFallbackReserve = true,
                    incompatibleIds = new List<string> { "blood_moon", "quiet_night" },
                    tags = new List<string> { "population", "vampire", "event" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.VampirePopulation,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 35f,
                            useLimits = true,
                            minLimit = 0.5f,
                            maxLimit = 3.0f
                        }
                    }
                },
                new RunModifierAuthoringEntry
                {
                    id = "restless_patrons",
                    displayName = "Freguesia inquieta",
                    kind = RunModifierKind.Event,
                    enabled = true,
                    allowedSource = RunModifierSource.VoteOnly,
                    duration = RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    maxNight = 0,
                    allowedDifficulties = RunDifficultyFlags.All,
                    weight = 0.8f,
                    isFallbackReserve = true,
                    incompatibleIds = new List<string> { "impatient_customers", "calm_customers" },
                    tags = new List<string> { "customers", "event" },
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.CustomerPatience,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = -15f,
                            useLimits = true,
                            minLimit = 0.4f,
                            maxLimit = 2.0f
                        }
                    }
                }
            };
        }

        public static List<RunShopOfferAuthoringEntry> BuildDefaultShopOffers()
        {
            var dough = AssetDatabase.LoadAssetAtPath<IngredientDef>(IngredientDoughPath);
            var sauce = AssetDatabase.LoadAssetAtPath<IngredientDef>(IngredientSaucePath);
            var cheese = AssetDatabase.LoadAssetAtPath<IngredientDef>(IngredientCheesePath);

            return new List<RunShopOfferAuthoringEntry>
            {
                new RunShopOfferAuthoringEntry
                {
                    offerId = "oven_timer_offer",
                    displayName = "Timer do Forno",
                    enabled = true,
                    benefitKind = ShopBenefitKind.GrantModifier,
                    modifierId = "oven_timer",
                    baseCost = 90,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 1,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 1
                },
                new RunShopOfferAuthoringEntry
                {
                    offerId = "reinforced_bag_offer",
                    displayName = "Bolsa Reforcada",
                    enabled = true,
                    benefitKind = ShopBenefitKind.GrantModifier,
                    modifierId = "reinforced_bag",
                    baseCost = 75,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 2,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 2
                },
                new RunShopOfferAuthoringEntry
                {
                    offerId = "staff_training_offer",
                    displayName = "Treinamento de Equipe",
                    enabled = true,
                    benefitKind = ShopBenefitKind.GrantModifier,
                    modifierId = "staff_training",
                    baseCost = 85,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 2,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 3
                },
                new RunShopOfferAuthoringEntry
                {
                    offerId = "base_supplies_offer",
                    displayName = "Suprimentos Base +5",
                    enabled = true,
                    benefitKind = ShopBenefitKind.SupplyBundle,
                    supplyBundleLines = new List<SupplyBundleLineEntry>
                    {
                        new SupplyBundleLineEntry { ingredient = dough, ingredientIdFallback = 1, quantity = 5 },
                        new SupplyBundleLineEntry { ingredient = sauce, ingredientIdFallback = 3, quantity = 5 },
                        new SupplyBundleLineEntry { ingredient = cheese, ingredientIdFallback = 4, quantity = 5 }
                    },
                    baseCost = 45,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 0,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 4
                },
                new RunShopOfferAuthoringEntry
                {
                    offerId = "hire_cashier_offer",
                    displayName = "Contratar Balconista",
                    enabled = true,
                    benefitKind = ShopBenefitKind.HireEmployee,
                    employeeRole = EmployeeRole.Cashier,
                    baseCost = 180,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 0,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 5
                },
                new RunShopOfferAuthoringEntry
                {
                    offerId = "hire_pizzaiolo_offer",
                    displayName = "Contratar Pizzaiolo",
                    enabled = true,
                    benefitKind = ShopBenefitKind.HireEmployee,
                    employeeRole = EmployeeRole.Pizzaiolo,
                    baseCost = 120,
                    costIncreasePerLevel = 0,
                    maxPurchasesPerRun = 0,
                    maxPurchasesPerNight = 0,
                    phaseAvailability = ShopPhaseAvailability.PreparationAndActive,
                    legacyByteId = 6
                }
            };
        }

        public static List<EmployeeRoleProfileAuthoringEntry> BuildDefaultEmployeeProfiles()
        {
            return new List<EmployeeRoleProfileAuthoringEntry>
            {
                new EmployeeRoleProfileAuthoringEntry
                {
                    role = EmployeeRole.Cashier,
                    displayName = "Balconista",
                    hireCost = 180,
                    maxLevel = 5,
                    promotionCostMultiplier = 1.0f,
                    promotionFlatStepCost = 0,
                    baseErrorRatePercent = 5f,
                    levelErrorReductionPercent = 15f,
                    minErrorRatePercent = 2f,
                    baseActionSeconds = 2.2f
                },
                new EmployeeRoleProfileAuthoringEntry
                {
                    role = EmployeeRole.Pizzaiolo,
                    displayName = "Pizzaiolo",
                    hireCost = 120,
                    maxLevel = 5,
                    promotionCostMultiplier = 1.0f,
                    promotionFlatStepCost = 0,
                    baseErrorRatePercent = 20f,
                    levelErrorReductionPercent = 15f,
                    minErrorRatePercent = 2f,
                    baseActionSeconds = 1.8f
                }
            };
        }

        public static List<NightPreparationScheduleAuthoringEntry> BuildDefaultSchedules()
        {
            return new List<NightPreparationScheduleAuthoringEntry>
            {
                new NightPreparationScheduleAuthoringEntry { nightNumber = 1, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 0, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 2, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 3, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 4, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 5, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 6, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 },
                new NightPreparationScheduleAuthoringEntry { nightNumber = 7, mandatoryHindranceStages = 1, hindranceAlternatives = 2, opportunityStages = 1, opportunityAlternatives = 2, additionalEventStages = 1, eventAlternatives = 2 }
            };
        }
    }
}
