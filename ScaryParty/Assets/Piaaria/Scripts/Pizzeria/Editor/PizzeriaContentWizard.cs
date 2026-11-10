using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaContentWizard : OdinMenuEditorWindow
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Configurar conteúdo", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaContentWizard>("Configurar Conteúdo da Pizzaria");
            window.minSize = new Vector2(850, 620);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();

            // Configuração da Pizzaria Modular Manual (Fase 1)
            var modularConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset");
            if (modularConfig != null)
            {
                tree.Add("Pizzaria Modular/Visão Geral", modularConfig);
                tree.Add("Pizzaria Modular/Auditoria e Reparo (Fase 1)", new ModularAuditAndRepairTab());
                tree.Add("Pizzaria Modular/Estações e Slots (Fase 1)", new ModularStationsAndSlotsTab());
            }

            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
            if (config != null)
            {
                tree.Add("Pizzaria Procedural/Visão Geral", config);
                tree.AddAllAssetsAtPath("Ingredientes", "Assets/Piaaria/ScriptableObjects/Pizzeria", typeof(IngredientDef), true);
                tree.AddAllAssetsAtPath("Utensílios", "Assets/Piaaria/ScriptableObjects/Pizzeria", typeof(ToolDef), true);
                tree.AddAllAssetsAtPath("Processos", "Assets/Piaaria/ScriptableObjects/Pizzeria", typeof(ProcessDef), true);
                tree.AddAllAssetsAtPath("Receitas", "Assets/Piaaria/ScriptableObjects/Pizzeria", typeof(RecipeDef), true);
                tree.Add("Cenário e Validação", new ValidationActions());
            }
            else if (modularConfig == null)
            {
                tree.Add("Aviso", new WarningObject("PizzeriaConfig não encontrado! Execute 'Build or Update' primeiro."));
            }

            return tree;
        }

        public class ModularAuditAndRepairTab
        {
            [Title("Auditoria da Pizzaria Modular", "Diagnóstico de IDs duplicados, slots nulos, ticket boards e ciclo completo", TitleAlignments.Centered)]
            [InfoBox("Executa auditoria estrita em PizzeriaModular_Manual.prefab e PizzeriaConfig_PizzeriaModular_Manual.asset.")]

            [ShowInInspector, ReadOnly]
            public string StatusAuditoria = "Clique em 'Auditar Integridade' para inspecionar.";

            [ShowInInspector, ReadOnly]
            public int ErrosBloqueantes = 0;

            [ShowInInspector, ReadOnly]
            public int Avisos = 0;

            [ShowInInspector, ReadOnly]
            public int ItensValidos = 0;

            [TableList(IsReadOnly = true, ShowPaging = true)]
            public List<AuditIssueRow> ProblemasDetectados = new List<AuditIssueRow>();

            [Button("🔍 Auditar Integridade (PizzeriaGameplayValidator)", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.7f, 0.9f)]
            public void AuditarIntegridade()
            {
                var settings = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaAuthoringSettings_Modular.asset");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPhase1Repair.ManualPrefabPath);

                var report = PizzeriaGameplayValidator.ValidatePrefabAndCycle(prefab, settings, false);
                ErrosBloqueantes = report.blockingErrorsCount;
                Avisos = report.warningsCount;
                ItensValidos = report.okCount;

                ProblemasDetectados.Clear();
                foreach (var issue in report.prefabIssues.Concat(report.cycleIssues))
                {
                    ProblemasDetectados.Add(new AuditIssueRow
                    {
                        Severidade = issue.severity.ToString(),
                        Categoria = issue.category,
                        Titulo = issue.title,
                        Descricao = issue.description,
                        ObjetoAlvo = issue.targetObject != null ? issue.targetObject.name : "-"
                    });
                }

                StatusAuditoria = ErrosBloqueantes == 0
                    ? "✅ PREFAB E CONFIGURAÇÕES 100% ÍNTEGROS! Zero erros bloqueantes."
                    : $"⚠️ Encontrados {ErrosBloqueantes} erro(s) bloqueante(s) e {Avisos} aviso(s).";
            }

            [Button("🛠️ Reparar Todas as Inconsistências (IDs, Slots, Comandas)", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.85f, 0.35f)]
            public void RepararTodasAsInconsistencias()
            {
                PizzeriaPhase1Repair.Repair();
                AuditarIntegridade();
            }
        }

        [Serializable]
        public class AuditIssueRow
        {
            public string Severidade;
            public string Categoria;
            public string Titulo;
            public string Descricao;
            public string ObjetoAlvo;
        }

        public class ModularStationsAndSlotsTab
        {
            [Title("Estações e Slots da Pizzaria Modular", "Visualização detalhada dos equipamentos configurados", TitleAlignments.Centered)]

            [TableList(IsReadOnly = true, ShowPaging = true)]
            public List<StationInfoRow> EstacoesConfiguradas = new List<StationInfoRow>();

            [Button("🔄 Atualizar Lista de Estações do Prefab", ButtonSizes.Medium)]
            [GUIColor(0.3f, 0.7f, 0.9f)]
            public void AtualizarLista()
            {
                EstacoesConfiguradas.Clear();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPhase1Repair.ManualPrefabPath);
                if (prefab == null) return;

                var bindings = prefab.GetComponent<PizzeriaAuthoringBindings>();
                var stations = prefab.GetComponentsInChildren<StationView>(true);

                foreach (var st in stations)
                {
                    if (st == null) continue;
                    int slotCount = st.slotAnchors != null ? st.slotAnchors.Length : 0;
                    if (st is TicketBoardStation tb && tb.ticketSlots != null)
                        slotCount = tb.ticketSlots.Length;

                    var kind = bindings != null ? bindings.GetStationKind(st) : PizzeriaStationKind.None;

                    EstacoesConfiguradas.Add(new StationInfoRow
                    {
                        ID = st.stationId,
                        Tipo = kind.ToString(),
                        NomeComponente = st.GetType().Name,
                        GameObject = st.gameObject.name,
                        SlotsValidos = slotCount,
                        Status = st.stationId > 0 && slotCount >= 0 ? "Válido" : "Inválido"
                    });
                }

                EstacoesConfiguradas = EstacoesConfiguradas.OrderBy(e => e.ID).ToList();
            }

            public ModularStationsAndSlotsTab()
            {
                AtualizarLista();
            }
        }

        [Serializable]
        public class StationInfoRow
        {
            public int ID;
            public string Tipo;
            public string NomeComponente;
            public string GameObject;
            public int SlotsValidos;
            public string Status;
        }

        private class ValidationActions
        {
            [Title("Ações de Manutenção e Validação", "Execute verificações automáticas e builders idempotentes", TitleAlignments.Centered)]
            [InfoBox("O sistema de melhorias (upgrades) foi dispensado do fluxo de execução conforme especificação.")]

            [Button("🔍 Validar Setup Completo da Pizzaria", ButtonSizes.Large)]
            [GUIColor(0.3f, 0.8f, 0.4f)]
            public void ValidateSetup()
            {
                PizzeriaValidator.ValidateSetup();
            }

            [Button("🏗️ Reconstruir PizzeriaPrefab (Idempotente)", ButtonSizes.Medium)]
            [GUIColor(0.2f, 0.6f, 0.9f)]
            public void RebuildPrefab()
            {
                PizzeriaBuilder.BuildOrUpdate();
            }

            [Button("🏡 Configurar Pontos de Entrega na Cidade", ButtonSizes.Medium)]
            [GUIColor(0.9f, 0.6f, 0.2f)]
            public void ConfigureCityDeliveries()
            {
                DeliveryAddressBuilder.ConfigureCityDeliveryPoints();
            }
        }

        private class WarningObject
        {
            [DisplayAsString(false)]
            [HideLabel]
            public string message;

            public WarningObject(string msg) => message = msg;
        }
    }
}
