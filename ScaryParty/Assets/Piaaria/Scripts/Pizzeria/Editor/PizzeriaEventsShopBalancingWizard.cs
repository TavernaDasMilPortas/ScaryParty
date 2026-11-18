using System;
using System.Collections.Generic;
using System.Text;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Definitions;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Editor.Migration;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Wizard centralizado de Eventos, Entraves, Beneficios, Loja, Funcionarios e Balanceamento (B2).
    /// Opera diretamente sobre RunBalancingCatalog como fonte unica de verdade, com Undo, Validacao e Simulacao isolada.
    /// </summary>
    public sealed class PizzeriaEventsShopBalancingWizard : OdinMenuEditorWindow
    {
        private const string MenuPath = "Tools/Scary Party/Wizards/Pizzaria/Eventos, Loja e Balanceamento";

        private RunBalancingCatalog _catalog;
        private PizzeriaConfig _activeConfig;
        private PlaytestReferenceManifest _manifest;
        private readonly List<PizzeriaRunPreset> _presets = new List<PizzeriaRunPreset>();

        private OverviewPage _overviewPage;
        private ModifiersCategoryPage _hindranceEventPage;
        private ModifiersCategoryPage _benefitOpportunityPage;
        private ShopOffersPage _shopOffersPage;
        private EmployeesPage _employeesPage;
        private EconomyReplenishmentPage _economyPage;
        private NightPreparationPage _nightPreparationPage;
        private SimulationValidationPage _simulationPage;

        [MenuItem(MenuPath, false, 18)]
        public static void OpenWindow()
        {
            var window = GetWindow<PizzeriaEventsShopBalancingWizard>();
            window.titleContent = new GUIContent("Eventos, Loja e Balanceamento");
            window.minSize = new Vector2(980f, 660f);
            window.Show();
        }

        protected override void Initialize()
        {
            base.Initialize();
            ReloadAssetReferences();
        }

        private void ReloadAssetReferences()
        {
            _manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(RunBalancingMigrationRunner.ManifestAssetPath);
            _activeConfig = _manifest != null && _manifest.activePizzeriaConfig != null
                ? _manifest.activePizzeriaConfig
                : AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(RunBalancingMigrationRunner.ActivePizzeriaConfigPath);

            _catalog = _activeConfig != null && _activeConfig.balancingCatalog != null
                ? _activeConfig.balancingCatalog
                : AssetDatabase.LoadAssetAtPath<RunBalancingCatalog>(RunBalancingMigrationRunner.CatalogAssetPath);

            _presets.Clear();
            if (_manifest != null && _manifest.runPresets != null)
            {
                for (int i = 0; i < _manifest.runPresets.Count; i++)
                {
                    if (_manifest.runPresets[i] is PizzeriaRunPreset p && !_presets.Contains(p))
                        _presets.Add(p);
                }
            }
            if (_activeConfig != null && _activeConfig.availablePresets != null)
            {
                for (int i = 0; i < _activeConfig.availablePresets.Count; i++)
                {
                    if (_activeConfig.availablePresets[i] != null && !_presets.Contains(_activeConfig.availablePresets[i]))
                        _presets.Add(_activeConfig.availablePresets[i]);
                }
            }

            _overviewPage = new OverviewPage(this);
            _hindranceEventPage = new ModifiersCategoryPage(this, "Eventos e Entraves (Negativos / Variacao)", RunModifierKind.Hindrance, RunModifierKind.Event);
            _benefitOpportunityPage = new ModifiersCategoryPage(this, "Beneficios e Upgrades Permanentes", RunModifierKind.Opportunity, RunModifierKind.PermanentUpgrade);
            _shopOffersPage = new ShopOffersPage(this);
            _employeesPage = new EmployeesPage(this);
            _economyPage = new EconomyReplenishmentPage(this);
            _nightPreparationPage = new NightPreparationPage(this);
            _simulationPage = new SimulationValidationPage(this);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            if (_overviewPage == null) ReloadAssetReferences();

            var tree = new OdinMenuTree(supportsMultiSelect: false)
            {
                { "1. Visao Geral e Fonte Ativa", _overviewPage },
                { "2. Eventos e Entraves", _hindranceEventPage },
                { "3. Beneficios e Oportunidades", _benefitOpportunityPage },
                { "4. Loja da Pizzaria", _shopOffersPage },
                { "5. Funcionarios", _employeesPage },
                { "6. Economia e Reposicao", _economyPage },
                { "7. Preparacao por Noite", _nightPreparationPage },
                { "8. Simulacao e Validacao", _simulationPage }
            };
            tree.Config.DrawSearchToolbar = true;
            return tree;
        }

        protected override void OnBeginDrawEditors()
        {
            base.OnBeginDrawEditors();

            if (_catalog != null && EditorUtility.IsDirty(_catalog))
            {
                Sirenix.Utilities.Editor.SirenixEditorGUI.WarningMessageBox(
                    "Existem alteracoes nao salvas no RunBalancingCatalog. Use 'Salvar Alteracoes' para persistir no asset.");
            }
        }

        internal RunBalancingCatalog Catalog => _catalog;
        internal PizzeriaConfig ActiveConfig => _activeConfig;
        internal PlaytestReferenceManifest Manifest => _manifest;
        internal IReadOnlyList<PizzeriaRunPreset> Presets => _presets;

        internal void RecordCatalogChange(string undoLabel)
        {
            if (_catalog == null) return;
            Undo.RecordObject(_catalog, undoLabel);
            EditorUtility.SetDirty(_catalog);
        }

        internal void SaveAllDirtyAssets()
        {
            if (_catalog != null) EditorUtility.SetDirty(_catalog);
            if (_activeConfig != null) EditorUtility.SetDirty(_activeConfig);
            if (_manifest != null) EditorUtility.SetDirty(_manifest);
            for (int i = 0; i < _presets.Count; i++)
            {
                if (_presets[i] != null) EditorUtility.SetDirty(_presets[i]);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _simulationPage?.RunValidation();
        }

        internal string ResolveIngredientName(int ingredientId)
        {
            if (_activeConfig != null && _activeConfig.ingredients != null)
            {
                for (int i = 0; i < _activeConfig.ingredients.Count; i++)
                {
                    var ing = _activeConfig.ingredients[i];
                    if (ing != null && ing.id == ingredientId && !string.IsNullOrEmpty(ing.ingredientName))
                        return ing.ingredientName;
                }
            }
            return RunDescriptionFormatter.DefaultIngredientName(ingredientId);
        }

        // =========================================================================
        // ABA 1: VISAO GERAL E FONTE ATIVA
        // =========================================================================
        private sealed class OverviewPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            public OverviewPage(PizzeriaEventsShopBalancingWizard owner) => _owner = owner;

            [Title("Fonte Unica de Autoridade — Eventos, Loja e Balanceamento", "Rastreamento do caminho efetivo usado em gameplay")]
            [ShowInInspector, ReadOnly, LabelText("RunBalancingCatalog Ativo")]
            public RunBalancingCatalog ActiveCatalog => _owner.Catalog;

            [ShowInInspector, ReadOnly, LabelText("PizzeriaConfig Ativo")]
            public PizzeriaConfig ActiveConfig => _owner.ActiveConfig;

            [ShowInInspector, ReadOnly, LabelText("Manifesto Playtest")]
            public PlaytestReferenceManifest Manifest => _owner.Manifest;

            [ShowInInspector, ReadOnly, LabelText("Status de Validacao")]
            public string ValidationStatus
            {
                get
                {
                    if (_owner.Catalog == null) return "ERRO: RunBalancingCatalog ausente.";
                    var rep = RunBalancingValidator.Validate(_owner.Catalog.BuildDomainSnapshot());
                    return rep.HasErrors
                        ? $"BLOQUEADO ({rep.ErrorCount} erros, {rep.WarningCount} avisos)"
                        : $"OK ({rep.ErrorCount} erros, {rep.WarningCount} avisos)";
                }
            }

            [ShowInInspector, ReadOnly, TextArea(6, 12), LabelText("Resumo do Catalogo Efetivo")]
            public string CatalogSummary
            {
                get
                {
                    if (_owner.Catalog == null) return "Nenhum catalogo vinculado.";
                    var snap = _owner.Catalog.BuildDomainSnapshot();
                    int hindrances = 0, events = 0, opportunities = 0, upgrades = 0;
                    for (int i = 0; i < snap.Modifiers.Count; i++)
                    {
                        switch (snap.Modifiers[i].Kind)
                        {
                            case RunModifierKind.Hindrance: hindrances++; break;
                            case RunModifierKind.Event: events++; break;
                            case RunModifierKind.Opportunity: opportunities++; break;
                            case RunModifierKind.PermanentUpgrade: upgrades++; break;
                        }
                    }
                    return $"Caminho Efetivo: GameScene / GameScene_CelShaded -> PizzeriaModular_Manual -> PizzeriaConfig_PizzeriaModular_Manual -> RunBalancingCatalog\n" +
                           $"Modificadores: {snap.Modifiers.Count} (Entraves={hindrances}, Eventos={events}, Beneficios={opportunities}, Upgrades={upgrades})\n" +
                           $"Ofertas de Loja: {snap.ShopOffers.Count}\n" +
                           $"Perfis de Funcionarios: {snap.EmployeeProfiles.Count}\n" +
                           $"Agendas de Noite: {snap.PreparationSchedules.Count}\n" +
                           $"Presets Carregados: {_owner.Presets.Count}";
                }
            }

            [HorizontalGroup("Actions")]
            [Button("Salvar Alteracoes nos Assets", ButtonSizes.Large), GUIColor(0.35f, 0.85f, 0.45f)]
            private void SaveChanges()
            {
                _owner.SaveAllDirtyAssets();
            }

            [HorizontalGroup("Actions")]
            [Button("Revalidar Tudo", ButtonSizes.Large)]
            private void Revalidate()
            {
                _owner._simulationPage?.RunValidation();
            }

            [HorizontalGroup("Migration")]
            [Button("Preview Migracao Idempotente (B0)", ButtonSizes.Medium)]
            private void PreviewMigration()
            {
                var rep = RunBalancingMigrationRunner.PreviewMigration();
                EditorUtility.DisplayDialog("Preview Migracao B0", rep.Summary, "OK");
            }

            [HorizontalGroup("Migration")]
            [Button("Executar Migracao Idempotente (B0)", ButtonSizes.Medium)]
            private void RunMigration()
            {
                var rep = RunBalancingMigrationRunner.RunIdempotentMigration();
                _owner.ReloadAssetReferences();
                EditorUtility.DisplayDialog("Migracao B0 Concluida", $"Alteracoes: {rep.TotalChanges}\n\n{rep.Summary}", "OK");
            }
        }

        // =========================================================================
        // ABAS 2 & 3: MODIFICADORES FILTRADOS POR CATEGORIA
        // =========================================================================
        private sealed class ModifiersCategoryPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            private readonly string _title;
            private readonly RunModifierKind _primaryKind;
            private readonly RunModifierKind _secondaryKind;

            [Title("Novo Modificador nesta Categoria")]
            [HorizontalGroup("AddBar"), LabelText("Novo ID Estavel")]
            public string newId = "novo_modificador";

            [HorizontalGroup("AddBar"), LabelText("Nome")]
            public string newDisplayName = "Novo Modificador";

            public ModifiersCategoryPage(
                PizzeriaEventsShopBalancingWizard owner,
                string title,
                RunModifierKind primaryKind,
                RunModifierKind secondaryKind)
            {
                _owner = owner;
                _title = title;
                _primaryKind = primaryKind;
                _secondaryKind = secondaryKind;
            }

            [HorizontalGroup("AddButtons")]
            [Button("Adicionar na Categoria Primaria")]
            private void AddPrimary() => AddEntry(_primaryKind);

            [HorizontalGroup("AddButtons")]
            [Button("Adicionar na Categoria Secundaria")]
            private void AddSecondary() => AddEntry(_secondaryKind);

            private void AddEntry(RunModifierKind kind)
            {
                if (_owner.Catalog == null || string.IsNullOrWhiteSpace(newId)) return;
                if (_owner.Catalog.FindModifierById(newId.Trim()) != null)
                {
                    EditorUtility.DisplayDialog("ID Duplicado", $"Ja existe modificador com ID '{newId}'.", "OK");
                    return;
                }

                _owner.RecordCatalogChange("Adicionar Modificador no Catalogo");
                _owner.Catalog.modifiers.Add(new RunModifierAuthoringEntry
                {
                    id = newId.Trim(),
                    displayName = string.IsNullOrWhiteSpace(newDisplayName) ? newId.Trim() : newDisplayName.Trim(),
                    kind = kind,
                    enabled = true,
                    allowedSource = kind == RunModifierKind.PermanentUpgrade ? RunModifierSource.VoteAndShop : RunModifierSource.VoteOnly,
                    duration = kind == RunModifierKind.PermanentUpgrade ? RunModifierDuration.Permanent : RunModifierDuration.OneNight,
                    maxStacks = 1,
                    minNight = 1,
                    weight = 1f,
                    effects = new List<RunModifierEffectEntry>
                    {
                        new RunModifierEffectEntry
                        {
                            target = RunEffectTarget.OrderArrivalRate,
                            operation = RunEffectOperation.PercentDelta,
                            unit = RunEffectUnit.RelativePercent,
                            valuePerLevel = 15f,
                            useLimits = true,
                            minLimit = 0.4f,
                            maxLimit = 2.5f
                        }
                    }
                });
            }

            [ShowInInspector, LabelText("Preview das Descricoes Geradas pelo Dominio"), TextArea(5, 10), ReadOnly]
            public string FormattedPreview
            {
                get
                {
                    if (_owner.Catalog == null) return string.Empty;
                    var snap = _owner.Catalog.BuildDomainSnapshot();
                    var sb = new StringBuilder();
                    for (int i = 0; i < snap.Modifiers.Count; i++)
                    {
                        var m = snap.Modifiers[i];
                        if (m.Kind != _primaryKind && m.Kind != _secondaryKind) continue;
                        string dur = RunDescriptionFormatter.FormatDurationTag(m.Duration);
                        string desc = RunDescriptionFormatter.FormatModifierDescription(m);
                        sb.AppendLine($"• {m.DisplayName} [{m.Id}] ({RunDescriptionFormatter.FormatKindLabel(m.Kind)} | {dur} | Max {m.MaxStacks}): {desc}");
                    }
                    return sb.ToString();
                }
            }

            [ShowInInspector, LabelText("Modificadores Filtrados (Edicao Direta no Catalogo)")]
            [OnValueChanged(nameof(OnEdited), includeChildren: true)]
            [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true, DefaultExpandedState = false)]
            public List<RunModifierAuthoringEntry> Items
            {
                get
                {
                    var filtered = new List<RunModifierAuthoringEntry>();
                    if (_owner.Catalog == null || _owner.Catalog.modifiers == null) return filtered;
                    for (int i = 0; i < _owner.Catalog.modifiers.Count; i++)
                    {
                        var m = _owner.Catalog.modifiers[i];
                        if (m != null && (m.kind == _primaryKind || m.kind == _secondaryKind))
                            filtered.Add(m);
                    }
                    return filtered;
                }
            }

            [Button("Salvar Alteracoes no Catalogo"), GUIColor(0.35f, 0.85f, 0.45f)]
            private void Save() => _owner.SaveAllDirtyAssets();

            private void OnEdited() => _owner.RecordCatalogChange($"Editar {_title}");
        }

        // =========================================================================
        // ABA 4: LOJA DA PIZZARIA
        // =========================================================================
        private sealed class ShopOffersPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            public ShopOffersPage(PizzeriaEventsShopBalancingWizard owner) => _owner = owner;

            [Title("Ofertas da Loja Coletiva (Votacao + Turno)", "Unica fonte de precos, pacotes de suprimentos base e limites compartilhados")]
            [ShowInInspector, ReadOnly, TextArea(5, 9), LabelText("Preview em Tempo Real na Loja (UI)")]
            public string ShopPreview
            {
                get
                {
                    if (_owner.Catalog == null) return string.Empty;
                    var snap = _owner.Catalog.BuildDomainSnapshot();
                    var sb = new StringBuilder();
                    for (int i = 0; i < snap.ShopOffers.Count; i++)
                    {
                        var o = snap.ShopOffers[i];
                        string desc = RunDescriptionFormatter.FormatShopOfferDescription(o, snap, _owner.ResolveIngredientName, 0);
                        sb.AppendLine($"• [{o.LegacyByteId}] {o.DisplayName} ({o.OfferId}) — R$ {o.BaseCost} | {(o.Enabled ? "Ativa" : "Inativa")}: {desc}");
                    }
                    return sb.ToString();
                }
            }

            [ShowInInspector, LabelText("Ofertas Configuradas")]
            [OnValueChanged(nameof(OnEdited), includeChildren: true)]
            public List<RunShopOfferAuthoringEntry> Offers
            {
                get => _owner.Catalog != null ? _owner.Catalog.shopOffers : null;
                set
                {
                    if (_owner.Catalog == null) return;
                    _owner.RecordCatalogChange("Editar Ofertas da Loja");
                    _owner.Catalog.shopOffers = value;
                }
            }

            [Button("Salvar Ofertas da Loja"), GUIColor(0.35f, 0.85f, 0.45f)]
            private void Save() => _owner.SaveAllDirtyAssets();

            private void OnEdited() => _owner.RecordCatalogChange("Editar Oferta da Loja");
        }

        // =========================================================================
        // ABA 5: FUNCIONARIOS
        // =========================================================================
        private sealed class EmployeesPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            public EmployeesPage(PizzeriaEventsShopBalancingWizard owner) => _owner = owner;

            [Title("Perfis de Funcionarios (Contratacao, Promocao e Curva de Erro)", "Sincronizado com EmployeeTaskService e Loja")]
            [ShowInInspector, ReadOnly, TextArea(4, 8), LabelText("Curva Calculada (Base -> Niveis + Treinamento)")]
            public string EmployeeCurvePreview
            {
                get
                {
                    if (_owner.Catalog == null) return string.Empty;
                    var snap = _owner.Catalog.BuildDomainSnapshot();
                    var sb = new StringBuilder();
                    for (int i = 0; i < snap.EmployeeProfiles.Count; i++)
                    {
                        var p = snap.EmployeeProfiles[i];
                        float lv1Base = p.BaseErrorRate * 100f;
                        float lv1With1Train = Math.Clamp(p.BaseErrorRate * 0.75f, p.MinErrorRate, 0.9f) * 100f;
                        int lv2Cost = p.ComputeCostForLevel(2);
                        sb.AppendLine($"• {p.DisplayName} ({p.Role}): Contrata R$ {p.HireCost} | Promocao NV2 R$ {lv2Cost} | Erro NV1 {lv1Base:0.#}% -> c/ Treinamento (-25%): {lv1With1Train:0.#}% | Acao {p.BaseActionSeconds:0.##}s");
                    }
                    return sb.ToString();
                }
            }

            [ShowInInspector, LabelText("Perfis por Funcao")]
            [OnValueChanged(nameof(OnEdited), includeChildren: true)]
            public List<EmployeeRoleProfileAuthoringEntry> Profiles
            {
                get => _owner.Catalog != null ? _owner.Catalog.employeeProfiles : null;
                set
                {
                    if (_owner.Catalog == null) return;
                    _owner.RecordCatalogChange("Editar Perfis de Funcionarios");
                    _owner.Catalog.employeeProfiles = value;
                }
            }

            [Button("Salvar Perfis e Custos"), GUIColor(0.35f, 0.85f, 0.45f)]
            private void Save() => _owner.SaveAllDirtyAssets();

            private void OnEdited() => _owner.RecordCatalogChange("Editar Perfil Funcionario");
        }

        // =========================================================================
        // ABA 6: ECONOMIA E REPOSICAO
        // =========================================================================
        private sealed class EconomyReplenishmentPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            public EconomyReplenishmentPage(PizzeriaEventsShopBalancingWizard owner) => _owner = owner;

            [Title("Economia Base, Ingredientes, Receitas e Presets de RUN")]
            [ShowInInspector, InlineEditor(InlineEditorObjectFieldModes.Boxed), LabelText("PizzeriaConfig Ativo")]
            public PizzeriaConfig Config => _owner.ActiveConfig;

            [ShowInInspector, LabelText("Presets de RUN Registrados")]
            [ListDrawerSettings(HideAddButton = true, HideRemoveButton = true)]
            public List<PizzeriaRunPreset> Presets => new List<PizzeriaRunPreset>(_owner.Presets);

            [ShowInInspector, ReadOnly, TextArea(5, 10), LabelText("Resumo de Suprimentos e Receitas Ativas")]
            public string SupplyAndRecipeSummary
            {
                get
                {
                    var cfg = _owner.ActiveConfig;
                    if (cfg == null) return "PizzeriaConfig nao encontrado.";
                    var sb = new StringBuilder();
                    sb.AppendLine($"Meta base: R$ {cfg.baseQuotaMoney} | Duracao base: {cfg.baseNightDurationSeconds}s | Rep inicial: {cfg.initialReputation} | Piso minimo ingredientes: {cfg.minimumIngredientFloor}");
                    sb.AppendLine("--- Pacotes de Reposicao ---");
                    if (cfg.supplyItems != null)
                    {
                        for (int i = 0; i < cfg.supplyItems.Count; i++)
                        {
                            var s = cfg.supplyItems[i];
                            if (s == null) continue;
                            string ingName = _owner.ResolveIngredientName(s.ingredientId);
                            sb.AppendLine($"  • [{s.id}] {s.itemName}: +{s.unitsPerPack} {ingName} — R$ {s.packPrice} | ETA {s.baseETA:0.#}s");
                        }
                    }
                    sb.AppendLine("--- Receitas ---");
                    if (cfg.recipes != null)
                    {
                        for (int i = 0; i < cfg.recipes.Count; i++)
                        {
                            var r = cfg.recipes[i];
                            if (r == null) continue;
                            sb.AppendLine($"  • [{r.id}] {r.recipeName}: R$ {r.basePrice} | Forno {r.baseBakeDuration:0.#}s");
                        }
                    }
                    return sb.ToString();
                }
            }

            [Button("Salvar Configs e Presets"), GUIColor(0.35f, 0.85f, 0.45f)]
            private void Save() => _owner.SaveAllDirtyAssets();
        }

        // =========================================================================
        // ABA 7: PREPARACAO POR NOITE
        // =========================================================================
        private sealed class NightPreparationPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;
            public NightPreparationPage(PizzeriaEventsShopBalancingWizard owner) => _owner = owner;

            [Title("Agenda de Votacao por Noite (Entraves, Beneficios e Eventos)", "Permite configurar exatamente quantas etapas e alternativas aparecem antes de cada noite (ex.: 3 eventos na Noite 4)")]
            [ShowInInspector, ReadOnly, TextArea(4, 8), LabelText("Suficiencia de Pools por Noite")]
            public string PoolSufficiencyPreview
            {
                get
                {
                    if (_owner.Catalog == null) return string.Empty;
                    var snap = _owner.Catalog.BuildDomainSnapshot();
                    var sb = new StringBuilder();
                    for (int i = 0; i < snap.PreparationSchedules.Count; i++)
                    {
                        var s = snap.PreparationSchedules[i];
                        sb.AppendLine($"Noite {s.NightNumber}: {s.MandatoryHindranceStages} Entrave(s) [{s.HindranceAlternatives} alt] | " +
                                      $"{s.OpportunityStages} Beneficio(s) [{s.OpportunityAlternatives} alt] | " +
                                      $"{s.AdditionalEventStages} Evento(s) [{s.EventAlternatives} alt]");
                    }
                    return sb.ToString();
                }
            }

            [ShowInInspector, LabelText("Tabela por Noite")]
            [OnValueChanged(nameof(OnEdited), includeChildren: true)]
            public List<NightPreparationScheduleAuthoringEntry> Schedules
            {
                get => _owner.Catalog != null ? _owner.Catalog.preparationSchedules : null;
                set
                {
                    if (_owner.Catalog == null) return;
                    _owner.RecordCatalogChange("Editar Agenda de Preparacao por Noite");
                    _owner.Catalog.preparationSchedules = value;
                }
            }

            [Button("Salvar Agenda de Preparacao"), GUIColor(0.35f, 0.85f, 0.45f)]
            private void Save() => _owner.SaveAllDirtyAssets();

            private void OnEdited() => _owner.RecordCatalogChange("Editar Agenda Noturna");
        }

        // =========================================================================
        // ABA 8: SIMULACAO E VALIDACAO
        // =========================================================================
        private sealed class SimulationValidationPage
        {
            private readonly PizzeriaEventsShopBalancingWizard _owner;

            [Title("Validacao Estrita de Dominio")]
            [ShowInInspector, ReadOnly, TextArea(6, 14), LabelText("Relatorio de Validacao")]
            public string ValidationReportText = "Clique em 'Validar Catalogo' para inspecionar.";

            [Title("Simulador Isolado de Sorteios (RNG Proprio — Sem Mutar Jogo)")]
            [HorizontalGroup("SimRow1"), LabelText("Seed")]
            public int seed = 2026;

            [HorizontalGroup("SimRow1"), LabelText("Noite")]
            [MinValue(1), MaxValue(7)]
            public int nightNumber = 1;

            [HorizontalGroup("SimRow2"), LabelText("Categoria")]
            public RunModifierKind category = RunModifierKind.Hindrance;

            [HorizontalGroup("SimRow2"), LabelText("Dificuldade")]
            public RunDifficulty difficulty = RunDifficulty.Normal;

            [HorizontalGroup("SimRow3"), LabelText("Alternativas")]
            [MinValue(2), MaxValue(5)]
            public int alternatives = 2;

            [HorizontalGroup("SimRow3"), LabelText("Iteracoes")]
            [ValueDropdown(nameof(IterationChoices))]
            public int iterations = 1000;

            private static int[] IterationChoices => new[] { 100, 1000 };

            [ShowInInspector, ReadOnly, TextArea(6, 14), LabelText("Resultado da Simulacao")]
            public string SimulationOutputText = "Execute uma simulacao (100 ou 1000 sorteios) para ver a distribuicao empirica.";

            [Title("Matriz de Cobertura: Efeito -> Consumidor Real -> Teste")]
            [ShowInInspector, ReadOnly, TextArea(8, 12), LabelText("Auditoria de Paridade")]
            public string CoverageMatrixText =>
                "1. VampirePopulation -> MonsterPopulationDirector.ComputeVampireSpawnCount -> T07 / OvernightExperience\n" +
                "2. VampireAcquisitionRange -> MonsterNpcController.EffectiveAcquisitionRange -> T08 / B3\n" +
                "3. OrderArrivalRate -> DemandDirector.ComputeEffectiveSpawnInterval -> T07 / T08 / B3\n" +
                "4. SupplyDeliveryEta -> NightCycleService.TryPlaceSupplyOrder + CommandHandler -> Overnight / B3\n" +
                "5. OvenAutoShutoff -> ProcessingService.TickOven -> Overnight / T09\n" +
                "6. BackpackCapacity -> PizzeriaCommandHandler.ApplyRunBackpackCapacityBonusServer -> T09\n" +
                "7. EmployeeErrorRate -> EmployeeTaskService.ComputeEffectiveErrorProbability -> T04 / T09\n" +
                "8. CustomerPatience -> DemandDirector patience min/max -> T08 / B3\n" +
                "9. NightRevenueGoal -> NightParameterResolver / RunEffectiveStats -> T29\n" +
                "10. Suprimentos Base (1,3,4) -> PizzeriaCommandHandler + TryPurchaseCatalogOffer -> T10 / T11";

            public SimulationValidationPage(PizzeriaEventsShopBalancingWizard owner)
            {
                _owner = owner;
                RunValidation();
            }

            [HorizontalGroup("SimButtons")]
            [Button("Validar Catalogo", ButtonSizes.Large)]
            public void RunValidation()
            {
                if (_owner.Catalog == null)
                {
                    ValidationReportText = "[ERRO] RunBalancingCatalog nao vinculado.";
                    return;
                }

                var report = RunBalancingValidator.Validate(_owner.Catalog.BuildDomainSnapshot());
                ValidationReportText = report.BuildSummaryText();
            }

            [HorizontalGroup("SimButtons")]
            [Button("Simular Sorteio (100 / 1000x)", ButtonSizes.Large), GUIColor(0.4f, 0.75f, 1f)]
            public void RunSimulation()
            {
                if (_owner.Catalog == null)
                {
                    SimulationOutputText = "[ERRO] RunBalancingCatalog nao vinculado.";
                    return;
                }

                var snap = _owner.Catalog.BuildDomainSnapshot();
                var res = RunBalancingValidator.SimulateCategoryDraw(
                    snap, category, nightNumber, difficulty, alternatives, iterations, seed);

                var sb = new StringBuilder();
                sb.AppendLine($"Simulacao: Categoria={res.Kind}, Noite={res.NightNumber}, Dificuldade={res.Difficulty}, Iteracoes={res.Iterations}, Seed={seed}");
                sb.AppendLine($"Pool Elegivel={res.EligiblePoolCount}, Alternativas/Sorteio={res.OptionsPerDraw}, Peso Total={res.TotalWeight:0.##}, Usou Reserva={res.UsedReserveFallback}");
                foreach (var kvp in res.SelectionCounts)
                {
                    double pct = res.Iterations > 0 ? (kvp.Value / (double)(res.Iterations * Math.Max(1, res.OptionsPerDraw))) * 100d : 0d;
                    double expectedPct = res.ExpectedProbabilities.TryGetValue(kvp.Key, out var e) ? e * 100d : 0d;
                    sb.AppendLine($"  • {kvp.Key}: sorteado {kvp.Value}x ({pct:0.0}% das vagas | peso relativo ~{expectedPct:0.0}%)");
                }
                SimulationOutputText = sb.ToString();
            }
        }
    }
}
