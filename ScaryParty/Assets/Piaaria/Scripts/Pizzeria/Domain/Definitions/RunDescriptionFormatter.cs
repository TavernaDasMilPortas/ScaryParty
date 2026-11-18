using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Domain.Definitions
{
    /// <summary>
    /// Formatador unico de descricoes e etiquetas de modificadores e ofertas (B1).
    /// Garante paridade estrita entre efeitos tipados configurados e texto apresentado no Wizard e nas UIs.
    /// </summary>
    public static class RunDescriptionFormatter
    {
        public static string FormatKindLabel(RunModifierKind kind)
        {
            return kind switch
            {
                RunModifierKind.Hindrance => "Entrave",
                RunModifierKind.Opportunity => "Beneficio",
                RunModifierKind.Event => "Evento",
                RunModifierKind.PermanentUpgrade => "Upgrade Permanente",
                _ => kind.ToString()
            };
        }

        public static string FormatDurationTag(RunModifierDuration duration)
        {
            return duration == RunModifierDuration.Permanent ? "Permanente (RUN)" : "1 Noite";
        }

        public static string FormatModifierDescription(RunModifierDefinition def, int stacks = 1)
        {
            if (def == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(def.DescriptionOverride))
            {
                return def.DescriptionOverride.Trim();
            }

            if (def.Effects != null && def.Effects.Count > 0)
            {
                var parts = new List<string>(def.Effects.Count);
                for (int i = 0; i < def.Effects.Count; i++)
                {
                    string formatted = FormatEffect(def.Effects[i], stacks);
                    if (!string.IsNullOrEmpty(formatted))
                    {
                        parts.Add(formatted);
                    }
                }

                if (parts.Count > 0)
                {
                    return string.Join("; ", parts) + ".";
                }
            }

            return !string.IsNullOrWhiteSpace(def.Description)
                ? def.Description.Trim()
                : def.DisplayName ?? def.Id ?? string.Empty;
        }

        public static string FormatEffect(RunModifierEffectData effect, int stacks = 1)
        {
            if (effect == null || effect.Target == RunEffectTarget.None) return string.Empty;
            int mult = Math.Max(1, stacks);

            if (effect.Operation == RunEffectOperation.EnableCapability)
            {
                return effect.Target switch
                {
                    RunEffectTarget.OvenAutoShutoff => "Forno desliga automaticamente ao finalizar o assado",
                    _ => $"Habilita {FormatTargetName(effect.Target)}"
                };
            }

            float rawValue = effect.ValuePerLevel * mult;
            string valueStr = FormatSignedEffectValue(effect, rawValue, mult);

            return effect.Target switch
            {
                RunEffectTarget.VampirePopulation => $"Populacao de vampiros {valueStr}",
                RunEffectTarget.VampireAcquisitionRange => $"Raio de agressividade dos vampiros {valueStr}",
                RunEffectTarget.OrderArrivalRate => $"Ritmo de chegada de pedidos {valueStr}",
                RunEffectTarget.SupplyDeliveryEta => $"Tempo de entrega de suprimentos {valueStr}",
                RunEffectTarget.BackpackCapacity => $"Capacidade da mochila {valueStr}",
                RunEffectTarget.BackpackResistance => $"Resistencia da mochila {valueStr}",
                RunEffectTarget.EmployeeErrorRate => $"Erro relativo dos funcionarios {valueStr}",
                RunEffectTarget.CustomerPatience => $"Paciencia dos clientes {valueStr}",
                RunEffectTarget.NightRevenueGoal => $"Meta de faturamento {valueStr}",
                _ => $"{FormatTargetName(effect.Target)} {valueStr}"
            };
        }

        public static string FormatShopOfferDescription(
            RunShopOfferData offer,
            RunBalancingSnapshot snapshot,
            Func<int, string> ingredientNameResolver = null,
            int currentModifierStacks = 0)
        {
            if (offer == null) return string.Empty;
            if (!string.IsNullOrWhiteSpace(offer.CustomDescription))
            {
                return offer.CustomDescription.Trim();
            }

            switch (offer.BenefitKind)
            {
                case ShopBenefitKind.GrantModifier:
                {
                    var mod = snapshot?.FindModifierById(offer.ModifierId);
                    if (mod == null)
                    {
                        return $"Concede beneficio '{offer.ModifierId}'.";
                    }

                    string durationLabel = FormatDurationTag(mod.Duration);
                    string body = FormatModifierDescription(mod, 1);
                    if (mod.MaxStacks > 1)
                    {
                        return $"[{durationLabel}] {body} (Max. {mod.MaxStacks} niveis; atual {currentModifierStacks}/{mod.MaxStacks})";
                    }
                    return $"[{durationLabel}] {body}";
                }
                case ShopBenefitKind.SupplyBundle:
                {
                    if (offer.SupplyBundleLines == null || offer.SupplyBundleLines.Count == 0)
                    {
                        return "Pacote de reposicao imediata no estoque.";
                    }

                    var sb = new StringBuilder("Adiciona ao estoque: ");
                    for (int i = 0; i < offer.SupplyBundleLines.Count; i++)
                    {
                        var line = offer.SupplyBundleLines[i];
                        string ingName = ingredientNameResolver != null
                            ? ingredientNameResolver(line.IngredientId)
                            : DefaultIngredientName(line.IngredientId);
                        if (i > 0) sb.Append(", ");
                        sb.Append($"+{line.Quantity} {ingName}");
                    }
                    sb.Append('.');
                    return sb.ToString();
                }
                case ShopBenefitKind.HireEmployee:
                {
                    var profile = snapshot?.FindEmployeeProfile(offer.EmployeeRole);
                    string roleName = profile != null ? profile.DisplayName : offer.EmployeeRole.ToString();
                    if (profile != null)
                    {
                        int errorPct = (int)Math.Round(profile.BaseErrorRate * 100f);
                        return $"Contrata {roleName} para o turno (erro base relativo de {errorPct}%).";
                    }
                    return $"Contrata {roleName} para o turno.";
                }
                default:
                    return offer.DisplayName ?? offer.OfferId;
            }
        }

        private static string FormatTargetName(RunEffectTarget target)
        {
            return target switch
            {
                RunEffectTarget.VampirePopulation => "Populacao de vampiros",
                RunEffectTarget.VampireAcquisitionRange => "Raio de ataque dos vampiros",
                RunEffectTarget.OrderArrivalRate => "Frequencia de pedidos",
                RunEffectTarget.SupplyDeliveryEta => "Entrega de suprimentos",
                RunEffectTarget.OvenAutoShutoff => "Desligamento automatico do forno",
                RunEffectTarget.BackpackCapacity => "Capacidade da mochila",
                RunEffectTarget.BackpackResistance => "Resistencia da mochila",
                RunEffectTarget.EmployeeErrorRate => "Erro dos funcionarios",
                RunEffectTarget.CustomerPatience => "Paciencia dos clientes",
                RunEffectTarget.NightRevenueGoal => "Meta da noite",
                _ => target.ToString()
            };
        }

        private static string FormatSignedEffectValue(RunModifierEffectData effect, float totalValue, int stacks)
        {
            if (effect.Operation == RunEffectOperation.Multiply)
            {
                float powVal = MathF.Pow(effect.ValuePerLevel, stacks);
                return $"x{powVal.ToString("0.##", CultureInfo.InvariantCulture)}";
            }

            if (effect.Unit == RunEffectUnit.RelativePercent)
            {
                int rounded = (int)Math.Round(totalValue);
                return rounded >= 0 ? $"+{rounded}%" : $"{rounded}%";
            }

            string sign = totalValue >= 0 ? "+" : string.Empty;
            string unitSuffix = effect.Unit switch
            {
                RunEffectUnit.Seconds => "s",
                RunEffectUnit.FlatUnits => effect.Target == RunEffectTarget.BackpackCapacity
                    ? (Math.Abs(totalValue) <= 1.01f ? " slot" : " slots")
                    : string.Empty,
                _ => string.Empty
            };

            return $"{sign}{totalValue.ToString("0.##", CultureInfo.InvariantCulture)}{unitSuffix}";
        }

        public static string DefaultIngredientName(int ingredientId)
        {
            return ingredientId switch
            {
                1 => "Massa",
                2 => "Massa Aberta",
                3 => "Molho",
                4 => "Queijo",
                5 => "Queijo Ralado",
                6 => "Calabresa",
                8 => "Cogumelo",
                10 => "Alho",
                _ => $"Ingrediente #{ingredientId}"
            };
        }
    }
}
