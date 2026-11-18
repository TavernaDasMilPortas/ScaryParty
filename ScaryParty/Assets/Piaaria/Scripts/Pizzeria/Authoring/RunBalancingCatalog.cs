using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class RunModifierEffectEntry
    {
        [LabelText("Alvo")]
        public RunEffectTarget target = RunEffectTarget.None;

        [LabelText("Operacao")]
        public RunEffectOperation operation = RunEffectOperation.PercentDelta;

        [LabelText("Unidade")]
        public RunEffectUnit unit = RunEffectUnit.RelativePercent;

        [LabelText("Valor por Nivel")]
        [Tooltip("Para unidade RelativePercent, informe em porcentagem legivel (ex.: 50 para +50%, -25 para -25%).")]
        public float valuePerLevel = 0f;

        [LabelText("Aplicar Limites")]
        public bool useLimits = false;

        [ShowIf(nameof(useLimits))]
        [LabelText("Limite Minimo")]
        public float minLimit = 0.2f;

        [ShowIf(nameof(useLimits))]
        [LabelText("Limite Maximo")]
        public float maxLimit = 3.0f;

        public RunModifierEffectEntry Clone()
        {
            return new RunModifierEffectEntry
            {
                target = target,
                operation = operation,
                unit = unit,
                valuePerLevel = valuePerLevel,
                useLimits = useLimits,
                minLimit = minLimit,
                maxLimit = maxLimit
            };
        }

        public RunModifierEffectData ToDomainData()
        {
            return new RunModifierEffectData(target, operation, unit, valuePerLevel, useLimits, minLimit, maxLimit);
        }
    }

    [Serializable]
    public class RunModifierAuthoringEntry
    {
        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("ID Estavel")]
        public string id = string.Empty;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Nome Exibido")]
        public string displayName = string.Empty;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Descricao Customizada (Opcional)")]
        [Tooltip("Deixe vazio para usar o formatador automatico baseado nos efeitos tipados.")]
        [TextArea(1, 3)]
        public string descriptionOverride = string.Empty;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Icone (Opcional)")]
        public Sprite icon;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Categoria")]
        public RunModifierKind kind = RunModifierKind.Event;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Ativo")]
        public bool enabled = true;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Origem Permitida")]
        public RunModifierSource allowedSource = RunModifierSource.VoteOnly;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Duracao")]
        public RunModifierDuration duration = RunModifierDuration.OneNight;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [MinValue(1)]
        [LabelText("Niveis Maximos")]
        public int maxStacks = 1;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [MinValue(1)]
        [LabelText("Noite Minima")]
        public int minNight = 1;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [MinValue(0)]
        [Tooltip("0 = sem limite maximo de noite.")]
        [LabelText("Noite Maxima (0 = Sem Limite)")]
        public int maxNight = 0;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Dificuldades Permitidas")]
        public RunDifficultyFlags allowedDifficulties = RunDifficultyFlags.All;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [MinValue(0f)]
        [Tooltip("Peso relativo no sorteio sem reposicao (nao e porcentagem garantida).")]
        [LabelText("Peso de Sorteio")]
        public float weight = 1f;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Alternativa de Reserva")]
        public bool isFallbackReserve = false;

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Pre-requisitos (IDs)")]
        public List<string> prerequisiteIds = new List<string>();

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Incompativeis (IDs)")]
        public List<string> incompatibleIds = new List<string>();

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Tags")]
        public List<string> tags = new List<string>();

        [FoldoutGroup("$GetSummaryTitle", false)]
        [LabelText("Efeitos Tipados")]
        [ListDrawerSettings(ShowFoldout = true)]
        public List<RunModifierEffectEntry> effects = new List<RunModifierEffectEntry>();

        private string GetSummaryTitle()
        {
            string title = string.IsNullOrEmpty(displayName) ? id : displayName;
            string status = enabled ? "" : " [DESATIVADO]";
            return $"{title} ({id}) [{kind}/{duration}]{status}";
        }
    }

    [Serializable]
    public class SupplyBundleLineEntry
    {
        [LabelText("Ingrediente")]
        public IngredientDef ingredient;

        [LabelText("ID Fallback")]
        public int ingredientIdFallback;

        [MinValue(1)]
        [LabelText("Quantidade")]
        public int quantity = 5;

        public int EffectiveIngredientId => ingredient != null ? ingredient.id : ingredientIdFallback;
    }

    [Serializable]
    public class RunShopOfferAuthoringEntry
    {
        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("ID da Oferta")]
        public string offerId = string.Empty;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Nome na Loja")]
        public string displayName = string.Empty;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Descricao Customizada (Opcional)")]
        [TextArea(1, 2)]
        public string customDescription = string.Empty;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Icone (Opcional)")]
        public Sprite icon;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Ativo")]
        public bool enabled = true;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Tipo de Beneficio")]
        public ShopBenefitKind benefitKind = ShopBenefitKind.GrantModifier;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [ShowIf("@benefitKind == ShopBenefitKind.GrantModifier")]
        [LabelText("ID do Modificador Alvo")]
        public string modifierId = string.Empty;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [ShowIf("@benefitKind == ShopBenefitKind.HireEmployee")]
        [LabelText("Funcao do Funcionario")]
        public EmployeeRole employeeRole = EmployeeRole.Cashier;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [ShowIf("@benefitKind == ShopBenefitKind.SupplyBundle")]
        [LabelText("Itens do Pacote de Suprimentos")]
        public List<SupplyBundleLineEntry> supplyBundleLines = new List<SupplyBundleLineEntry>();

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [MinValue(0)]
        [LabelText("Preco Base (R$)")]
        public int baseCost = 50;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [MinValue(0)]
        [LabelText("Aumento por Nivel (R$)")]
        public int costIncreasePerLevel = 0;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [MinValue(0)]
        [Tooltip("0 = sem limite proprio (usa MaxStacks do modificador se houver).")]
        [LabelText("Limite por RUN (0 = Sem Limite)")]
        public int maxPurchasesPerRun = 0;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [MinValue(0)]
        [LabelText("Limite por Noite (0 = Sem Limite)")]
        public int maxPurchasesPerNight = 0;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [LabelText("Fases Permitidas")]
        public ShopPhaseAvailability phaseAvailability = ShopPhaseAvailability.PreparationAndActive;

        [FoldoutGroup("$GetOfferSummaryTitle", false)]
        [Tooltip("ID legado (1..6) mantido para compatibilidade de RPCs existentes.")]
        [LabelText("ID Legado (1..6)")]
        public byte legacyByteId = 0;

        private string GetOfferSummaryTitle()
        {
            string title = string.IsNullOrEmpty(displayName) ? offerId : displayName;
            string status = enabled ? "" : " [DESATIVADO]";
            return $"{title} ({offerId}) — R$ {baseCost}{status}";
        }
    }

    [Serializable]
    public class EmployeeRoleProfileAuthoringEntry
    {
        [LabelText("Funcao")]
        public EmployeeRole role = EmployeeRole.Cashier;

        [LabelText("Nome Exibido")]
        public string displayName = "Balconista";

        [MinValue(0)]
        [LabelText("Custo Contratacao (R$)")]
        public int hireCost = 180;

        [MinValue(1)]
        [LabelText("Nivel Maximo")]
        public int maxLevel = 5;

        [MinValue(0.1f)]
        [Tooltip("Custo de promocao para nivel L = hireCost * L * multiplicador + passo * (L - 1)")]
        [LabelText("Multiplicador Promocao")]
        public float promotionCostMultiplier = 1.0f;

        [MinValue(0)]
        [LabelText("Passo Fixo Promocao (R$)")]
        public int promotionFlatStepCost = 0;

        [Range(0f, 95f)]
        [LabelText("Erro Base (%)")]
        public float baseErrorRatePercent = 5f;

        [Range(0f, 90f)]
        [LabelText("Reducao de Erro por Nivel (%)")]
        public float levelErrorReductionPercent = 15f;

        [Range(1f, 90f)]
        [LabelText("Erro Minimo (%)")]
        public float minErrorRatePercent = 2f;

        [MinValue(0.2f)]
        [LabelText("Duracao Base Acao (s)")]
        public float baseActionSeconds = 2.2f;
    }

    [Serializable]
    public class NightPreparationScheduleAuthoringEntry
    {
        [MinValue(1)]
        [LabelText("Noite")]
        public int nightNumber = 1;

        [MinValue(0)]
        [LabelText("Etapas Entraves Obrigatorios")]
        public int mandatoryHindranceStages = 1;

        [MinValue(2)]
        [LabelText("Alternativas por Entrave")]
        public int hindranceAlternatives = 2;

        [MinValue(0)]
        [LabelText("Etapas Beneficio/Oportunidade")]
        public int opportunityStages = 1;

        [MinValue(2)]
        [LabelText("Alternativas por Beneficio")]
        public int opportunityAlternatives = 2;

        [MinValue(0)]
        [LabelText("Etapas Eventos Adicionais")]
        public int additionalEventStages = 0;

        [MinValue(2)]
        [LabelText("Alternativas por Evento")]
        public int eventAlternatives = 2;
    }

    /// <summary>
    /// Fonte unica de autoridade para eventos, entraves, beneficios, loja, funcionarios e agenda de preparacao por noite (B1).
    /// </summary>
    [CreateAssetMenu(fileName = "RunBalancingCatalog", menuName = "Scary Party/Pizzeria/Run Balancing Catalog")]
    public class RunBalancingCatalog : ScriptableObject
    {
        [Header("Versao e Rastreabilidade")]
        public int catalogSchemaVersion = 1;

        [Header("Modificadores (Eventos, Entraves, Oportunidades e Upgrades)")]
        public List<RunModifierAuthoringEntry> modifiers = new List<RunModifierAuthoringEntry>();

        [Header("Ofertas da Loja Coletiva")]
        public List<RunShopOfferAuthoringEntry> shopOffers = new List<RunShopOfferAuthoringEntry>();

        [Header("Perfis de Funcionarios")]
        public List<EmployeeRoleProfileAuthoringEntry> employeeProfiles = new List<EmployeeRoleProfileAuthoringEntry>();

        [Header("Agenda de Preparacao por Noite (Noite 1..N)")]
        public List<NightPreparationScheduleAuthoringEntry> preparationSchedules = new List<NightPreparationScheduleAuthoringEntry>();

        public RunModifierAuthoringEntry FindModifierById(string id)
        {
            if (string.IsNullOrEmpty(id) || modifiers == null) return null;
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i] != null && string.Equals(modifiers[i].id, id, StringComparison.Ordinal))
                    return modifiers[i];
            }
            return null;
        }

        public RunShopOfferAuthoringEntry FindShopOfferById(string offerId)
        {
            if (string.IsNullOrEmpty(offerId) || shopOffers == null) return null;
            for (int i = 0; i < shopOffers.Count; i++)
            {
                if (shopOffers[i] != null && string.Equals(shopOffers[i].offerId, offerId, StringComparison.Ordinal))
                    return shopOffers[i];
            }
            return null;
        }

        public EmployeeRoleProfileAuthoringEntry FindEmployeeProfile(EmployeeRole role)
        {
            if (employeeProfiles == null) return null;
            for (int i = 0; i < employeeProfiles.Count; i++)
            {
                if (employeeProfiles[i] != null && employeeProfiles[i].role == role)
                    return employeeProfiles[i];
            }
            return null;
        }

        public NightPreparationScheduleAuthoringEntry FindScheduleForNight(int nightNumber)
        {
            if (preparationSchedules == null) return null;
            for (int i = 0; i < preparationSchedules.Count; i++)
            {
                if (preparationSchedules[i] != null && preparationSchedules[i].nightNumber == nightNumber)
                    return preparationSchedules[i];
            }
            return null;
        }
    }
}
