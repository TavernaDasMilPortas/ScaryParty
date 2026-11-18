using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    /// <summary>
    /// Snapshot imutavel de balanceamento para uma RUN (B1).
    /// Produzido pelo catalogo de authoring ou montado explicitamente em testes puros de dominio.
    /// </summary>
    public sealed class RunBalancingSnapshot
    {
        private readonly List<RunModifierDefinition> _modifiers;
        private readonly List<RunShopOfferData> _shopOffers;
        private readonly List<EmployeeRoleProfileData> _employeeProfiles;
        private readonly List<NightPreparationScheduleData> _preparationSchedules;

        private readonly Dictionary<string, RunModifierDefinition> _modifiersById;
        private readonly Dictionary<string, RunShopOfferData> _offersById;
        private readonly Dictionary<byte, RunShopOfferData> _offersByLegacyId;
        private readonly Dictionary<EmployeeRole, EmployeeRoleProfileData> _profilesByRole;
        private readonly Dictionary<int, NightPreparationScheduleData> _schedulesByNight;

        public IReadOnlyList<RunModifierDefinition> Modifiers => _modifiers;
        public IReadOnlyList<RunShopOfferData> ShopOffers => _shopOffers;
        public IReadOnlyList<EmployeeRoleProfileData> EmployeeProfiles => _employeeProfiles;
        public IReadOnlyList<NightPreparationScheduleData> PreparationSchedules => _preparationSchedules;

        public RunBalancingSnapshot(
            IEnumerable<RunModifierDefinition> modifiers,
            IEnumerable<RunShopOfferData> shopOffers,
            IEnumerable<EmployeeRoleProfileData> employeeProfiles,
            IEnumerable<NightPreparationScheduleData> preparationSchedules)
        {
            _modifiers = new List<RunModifierDefinition>();
            _shopOffers = new List<RunShopOfferData>();
            _employeeProfiles = new List<EmployeeRoleProfileData>();
            _preparationSchedules = new List<NightPreparationScheduleData>();

            _modifiersById = new Dictionary<string, RunModifierDefinition>(StringComparer.Ordinal);
            _offersById = new Dictionary<string, RunShopOfferData>(StringComparer.Ordinal);
            _offersByLegacyId = new Dictionary<byte, RunShopOfferData>();
            _profilesByRole = new Dictionary<EmployeeRole, EmployeeRoleProfileData>();
            _schedulesByNight = new Dictionary<int, NightPreparationScheduleData>();

            if (modifiers != null)
            {
                foreach (var mod in modifiers)
                {
                    if (mod == null || string.IsNullOrWhiteSpace(mod.Id)) continue;
                    _modifiers.Add(mod);
                    if (!_modifiersById.ContainsKey(mod.Id))
                    {
                        _modifiersById[mod.Id] = mod;
                    }
                }
            }

            if (shopOffers != null)
            {
                foreach (var offer in shopOffers)
                {
                    if (offer == null || string.IsNullOrWhiteSpace(offer.OfferId)) continue;
                    _shopOffers.Add(offer);
                    if (!_offersById.ContainsKey(offer.OfferId))
                    {
                        _offersById[offer.OfferId] = offer;
                    }
                    if (offer.LegacyByteId != 0 && !_offersByLegacyId.ContainsKey(offer.LegacyByteId))
                    {
                        _offersByLegacyId[offer.LegacyByteId] = offer;
                    }
                }
            }

            if (employeeProfiles != null)
            {
                foreach (var profile in employeeProfiles)
                {
                    if (profile == null) continue;
                    _employeeProfiles.Add(profile);
                    if (!_profilesByRole.ContainsKey(profile.Role))
                    {
                        _profilesByRole[profile.Role] = profile;
                    }
                }
            }

            if (preparationSchedules != null)
            {
                foreach (var sched in preparationSchedules)
                {
                    if (sched == null) continue;
                    _preparationSchedules.Add(sched);
                    if (!_schedulesByNight.ContainsKey(sched.NightNumber))
                    {
                        _schedulesByNight[sched.NightNumber] = sched;
                    }
                }
            }
        }

        public RunModifierDefinition FindModifierById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _modifiersById.TryGetValue(id, out var def) ? def : null;
        }

        public RunShopOfferData FindShopOfferById(string offerId)
        {
            if (string.IsNullOrWhiteSpace(offerId)) return null;
            return _offersById.TryGetValue(offerId, out var offer) ? offer : null;
        }

        public RunShopOfferData FindShopOfferByLegacyByteId(byte legacyByteId)
        {
            if (legacyByteId == 0) return null;
            return _offersByLegacyId.TryGetValue(legacyByteId, out var offer) ? offer : null;
        }

        public EmployeeRoleProfileData FindEmployeeProfile(EmployeeRole role)
        {
            return _profilesByRole.TryGetValue(role, out var profile) ? profile : null;
        }

        public NightPreparationScheduleData GetScheduleForNight(int nightNumber)
        {
            int targetNight = Math.Max(1, nightNumber);
            if (_schedulesByNight.TryGetValue(targetNight, out var exact))
            {
                return exact;
            }

            NightPreparationScheduleData highestBelowOrEqual = null;
            for (int i = 0; i < _preparationSchedules.Count; i++)
            {
                var candidate = _preparationSchedules[i];
                if (candidate == null) continue;
                if (candidate.NightNumber <= targetNight &&
                    (highestBelowOrEqual == null || candidate.NightNumber > highestBelowOrEqual.NightNumber))
                {
                    highestBelowOrEqual = candidate;
                }
            }

            if (highestBelowOrEqual != null)
            {
                return highestBelowOrEqual;
            }

            return new NightPreparationScheduleData(
                targetNight,
                mandatoryHindranceStages: 1,
                hindranceAlternatives: 2,
                opportunityStages: 1,
                opportunityAlternatives: 2,
                additionalEventStages: targetNight >= 2 ? 1 : 0,
                eventAlternatives: 2);
        }

        /// <summary>
        /// Construtor explicito para testes unitarios puros de dominio (sem UnityEngine/ScriptableObjects).
        /// Nao deve ser usado como fallback silencioso quando PizzeriaConfig estiver sem catalogo em cena.
        /// </summary>
        public static RunBalancingSnapshot CreateForDomainUnitTests()
        {
            var modifiers = new List<RunModifierDefinition>
            {
                new RunModifierDefinition
                {
                    Id = "blood_moon",
                    DisplayName = "Lua vermelha",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.5f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.VampirePopulation, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 50f, true, 0.5f, 3f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "vampire_attack",
                    DisplayName = "Ataque dos vampiros",
                    Kind = RunModifierKind.Hindrance,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.35f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.VampireAcquisitionRange, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 35f, true, 0.5f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "heavy_rush",
                    DisplayName = "Movimento intenso",
                    Kind = RunModifierKind.Hindrance,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.3f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "festival_demand" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 30f, true, 0.5f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "express_supply",
                    DisplayName = "Fornecimento expresso",
                    Kind = RunModifierKind.Opportunity,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = -0.4f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "slow_supply", "storm_logistics" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.SupplyDeliveryEta, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -40f, true, 0.3f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "oven_timer",
                    DisplayName = "Timer do forno",
                    Kind = RunModifierKind.PermanentUpgrade,
                    Duration = RunModifierDuration.Permanent,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 1f,
                    AllowedSource = RunModifierSource.VoteAndShop,
                    Weight = 1f,
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.OvenAutoShutoff, RunEffectOperation.EnableCapability, RunEffectUnit.BooleanFlag, 1f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "reinforced_bag",
                    DisplayName = "Bolsa reforcada",
                    Kind = RunModifierKind.PermanentUpgrade,
                    Duration = RunModifierDuration.Permanent,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 2,
                    Value = 1f,
                    AllowedSource = RunModifierSource.VoteAndShop,
                    Weight = 1f,
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.BackpackCapacity, RunEffectOperation.AddFlat, RunEffectUnit.FlatUnits, 1f, true, 0f, 2f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "staff_training",
                    DisplayName = "Treinamento",
                    Kind = RunModifierKind.PermanentUpgrade,
                    Duration = RunModifierDuration.Permanent,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 2,
                    Value = -0.25f,
                    AllowedSource = RunModifierSource.VoteAndShop,
                    Weight = 1f,
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.EmployeeErrorRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -25f, true, 0.2f, 1.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "impatient_customers",
                    DisplayName = "Clientes impacientes",
                    Kind = RunModifierKind.Hindrance,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = -0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "calm_customers" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.CustomerPatience, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -25f, true, 0.4f, 2f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "slow_supply",
                    DisplayName = "Entrega demorada",
                    Kind = RunModifierKind.Hindrance,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.35f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IsFallbackReserve = true,
                    IncompatibleIds = new[] { "express_supply" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.SupplyDeliveryEta, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 35f, true, 0.3f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "calm_customers",
                    DisplayName = "Clientes pacientes",
                    Kind = RunModifierKind.Opportunity,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "impatient_customers" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.CustomerPatience, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 25f, true, 0.4f, 2f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "quiet_night",
                    DisplayName = "Noite tranquila",
                    Kind = RunModifierKind.Opportunity,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = -0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 0.8f,
                    IsFallbackReserve = true,
                    IncompatibleIds = new[] { "blood_moon" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.VampirePopulation, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -25f, true, 0.5f, 3f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "festival_demand",
                    DisplayName = "Festival na cidade",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "heavy_rush" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.OrderArrivalRate, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 25f, true, 0.5f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "vampire_prowl",
                    DisplayName = "Ronda dos vampiros",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 1f,
                    IncompatibleIds = new[] { "vampire_attack" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.VampireAcquisitionRange, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 25f, true, 0.5f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "storm_logistics",
                    DisplayName = "Tempestade nas vias",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.25f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 0.9f,
                    IncompatibleIds = new[] { "express_supply", "slow_supply" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.SupplyDeliveryEta, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 25f, true, 0.3f, 2.5f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "midnight_surge",
                    DisplayName = "Madrugada agitada",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = 0.35f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 0.8f,
                    IsFallbackReserve = true,
                    IncompatibleIds = new[] { "blood_moon", "quiet_night" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.VampirePopulation, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, 35f, true, 0.5f, 3f)
                    }
                },
                new RunModifierDefinition
                {
                    Id = "restless_patrons",
                    DisplayName = "Freguesia inquieta",
                    Kind = RunModifierKind.Event,
                    Duration = RunModifierDuration.OneNight,
                    Scope = "Global",
                    MinNight = 1,
                    MaxStacks = 1,
                    Value = -0.15f,
                    AllowedSource = RunModifierSource.VoteOnly,
                    Weight = 0.8f,
                    IsFallbackReserve = true,
                    IncompatibleIds = new[] { "impatient_customers", "calm_customers" },
                    Effects = new[]
                    {
                        new RunModifierEffectData(RunEffectTarget.CustomerPatience, RunEffectOperation.PercentDelta, RunEffectUnit.RelativePercent, -15f, true, 0.4f, 2f)
                    }
                }
            };

            var shopOffers = new List<RunShopOfferData>
            {
                new RunShopOfferData("oven_timer_offer", "Timer do Forno", string.Empty, true, ShopBenefitKind.GrantModifier, "oven_timer", EmployeeRole.Cashier, null, 90, 0, 1, 0, ShopPhaseAvailability.PreparationAndActive, 1),
                new RunShopOfferData("reinforced_bag_offer", "Bolsa Reforcada", string.Empty, true, ShopBenefitKind.GrantModifier, "reinforced_bag", EmployeeRole.Cashier, null, 75, 0, 2, 0, ShopPhaseAvailability.PreparationAndActive, 2),
                new RunShopOfferData("staff_training_offer", "Treinamento de Equipe", string.Empty, true, ShopBenefitKind.GrantModifier, "staff_training", EmployeeRole.Cashier, null, 85, 0, 2, 0, ShopPhaseAvailability.PreparationAndActive, 3),
                new RunShopOfferData("base_supplies_offer", "Suprimentos Base +5", string.Empty, true, ShopBenefitKind.SupplyBundle, string.Empty, EmployeeRole.Cashier, new[]
                {
                    new SupplyBundleLineData(1, 5),
                    new SupplyBundleLineData(3, 5),
                    new SupplyBundleLineData(4, 5)
                }, 45, 0, 0, 0, ShopPhaseAvailability.PreparationAndActive, 4),
                new RunShopOfferData("hire_cashier_offer", "Contratar Balconista", string.Empty, true, ShopBenefitKind.HireEmployee, string.Empty, EmployeeRole.Cashier, null, 180, 0, 0, 0, ShopPhaseAvailability.PreparationAndActive, 5),
                new RunShopOfferData("hire_pizzaiolo_offer", "Contratar Pizzaiolo", string.Empty, true, ShopBenefitKind.HireEmployee, string.Empty, EmployeeRole.Pizzaiolo, null, 120, 0, 0, 0, ShopPhaseAvailability.PreparationAndActive, 6)
            };

            var employeeProfiles = new List<EmployeeRoleProfileData>
            {
                new EmployeeRoleProfileData(EmployeeRole.Cashier, "Balconista", 180, 5, 1.0f, 0, 0.05f, 0.15f, 0.02f, 2.2f),
                new EmployeeRoleProfileData(EmployeeRole.Pizzaiolo, "Pizzaiolo", 120, 5, 1.0f, 0, 0.20f, 0.15f, 0.02f, 1.8f)
            };

            var schedules = new List<NightPreparationScheduleData>
            {
                new NightPreparationScheduleData(1, 1, 2, 1, 2, 0, 2),
                new NightPreparationScheduleData(2, 1, 2, 1, 2, 1, 2),
                new NightPreparationScheduleData(3, 1, 2, 1, 2, 1, 2),
                new NightPreparationScheduleData(4, 1, 2, 1, 2, 1, 2)
            };

            return new RunBalancingSnapshot(modifiers, shopOffers, employeeProfiles, schedules);
        }
    }
}
