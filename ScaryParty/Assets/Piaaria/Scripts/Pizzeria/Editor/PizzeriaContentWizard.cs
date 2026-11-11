using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaContentWizard : OdinMenuEditorWindow
    {
        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Conteúdo da Pizzaria (Odin Wizard)", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaContentWizard>("Configurar Conteúdo da Pizzaria");
            window.minSize = new Vector2(950, 680);
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();
            tree.Config.DrawSearchToolbar = true;

            // 1. Estúdio e Visão Geral da Pizzaria Modular
            var modularConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset");
            if (modularConfig != null)
            {
                tree.Add("Pizzaria Modular/Estúdio 3D Independente", new StudioLauncherTab());
                tree.Add("Pizzaria Modular/Pizza Studio 2.0 (Prévia 3D)", new ModularPizzaPreviewTab());
                tree.Add("Pizzaria Modular/Auditoria e Reparo", new ModularAuditAndRepairTab());
                tree.Add("Pizzaria Modular/Estações e Slots (Diagnóstico)", new ModularStationsAndSlotsTab());
                tree.Add("Pizzaria Modular/Visão Geral (Config)", modularConfig);
            }

            // 2. Bancadas e Estações (Catálogo, Presets e Ocorrências no Prefab)
            tree.Add("Bancadas e Estações/➕ Criar Bancada ou Preset", new StationCreationActions());

            // Presets de Layout Reutilizáveis
            string[] layoutGuids = AssetDatabase.FindAssets("t:StationSlotLayoutAsset");
            var layouts = new List<StationSlotLayoutAsset>();
            for (int i = 0; i < layoutGuids.Length; i++)
            {
                var l = AssetDatabase.LoadAssetAtPath<StationSlotLayoutAsset>(AssetDatabase.GUIDToAssetPath(layoutGuids[i]));
                if (l != null) layouts.Add(l);
            }
            layouts = layouts.OrderBy(l => l.name).ToList();
            foreach (var l in layouts)
            {
                tree.Add($"Bancadas e Estações/Presets de Layout/{l.name}", new StationUnifiedEditor(l));
            }

            // Definições de Estações no Catálogo
            string[] stationGuids = AssetDatabase.FindAssets("t:StationDef");
            var stationDefs = new List<StationDef>();
            for (int i = 0; i < stationGuids.Length; i++)
            {
                var def = AssetDatabase.LoadAssetAtPath<StationDef>(AssetDatabase.GUIDToAssetPath(stationGuids[i]));
                if (def != null) stationDefs.Add(def);
            }
            stationDefs = stationDefs.OrderBy(s => s.id).ToList();
            foreach (var stDef in stationDefs)
            {
                string dispName = !string.IsNullOrEmpty(stDef.stationName) ? stDef.stationName : stDef.name;
                tree.Add($"Bancadas e Estações/Catálogo de Definições/{stDef.id:D2} - {dispName}", new StationUnifiedEditor(stDef));
            }

            // Ocorrências Físicas no Prefab Modular
            var manualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPhase1Repair.ManualPrefabPath);
            if (manualPrefab != null)
            {
                var physStations = manualPrefab.GetComponentsInChildren<StationView>(true).OrderBy(s => s.stationId).ToList();
                foreach (var ps in physStations)
                {
                    if (ps == null) continue;
                    tree.Add($"Bancadas e Estações/Ocorrências no Prefab/{ps.stationId:D2} - {ps.name}", new StationUnifiedEditor(ps));
                }
            }

            // 3. Ingredientes Centralizados com Sub-Abas
            tree.Add("Ingredientes/➕ Criar Novo Ingrediente", new IngredientCreationActions());
            string[] ingGuids = AssetDatabase.FindAssets("t:IngredientDef");
            var ingredients = new List<IngredientDef>();
            for (int i = 0; i < ingGuids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(ingGuids[i]);
                var def = AssetDatabase.LoadAssetAtPath<IngredientDef>(p);
                if (def != null) ingredients.Add(def);
            }
            ingredients = ingredients.OrderBy(ing => ing.id).ToList();
            foreach (var ing in ingredients)
            {
                string displayName = !string.IsNullOrEmpty(ing.ingredientName) ? ing.ingredientName : ing.name;
                string badge = (ing.placeholderPrefab == null || (ing.requiresPrep && ing.preparedIngredient == null)) ? "⚠️ " : "";
                string menuPath = $"Ingredientes/{badge}{ing.id:D2} - {displayName}";
                tree.Add(menuPath, new IngredientUnifiedEditor(ing));
            }

            // 4. Utensílios
            tree.Add("Utensílios/➕ Criar Novo Utensílio", new ToolCreationActions());
            string[] toolGuids = AssetDatabase.FindAssets("t:ToolDef");
            var tools = new List<ToolDef>();
            for (int i = 0; i < toolGuids.Length; i++)
            {
                var t = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(toolGuids[i]));
                if (t != null) tools.Add(t);
            }
            tools = tools.OrderBy(t => t.id).ToList();
            foreach (var tool in tools)
            {
                string disp = !string.IsNullOrEmpty(tool.toolName) ? tool.toolName : tool.name;
                tree.Add($"Utensílios/{tool.id:D2} - {disp}", new ToolUnifiedEditor(tool));
            }

            // 5. Processos
            tree.Add("Processos/➕ Criar Novo Processo", new ProcessCreationActions());
            string[] procGuids = AssetDatabase.FindAssets("t:ProcessDef");
            var processes = new List<ProcessDef>();
            for (int i = 0; i < procGuids.Length; i++)
            {
                var p = AssetDatabase.LoadAssetAtPath<ProcessDef>(AssetDatabase.GUIDToAssetPath(procGuids[i]));
                if (p != null) processes.Add(p);
            }
            processes = processes.OrderBy(p => p.id).ToList();
            foreach (var proc in processes)
            {
                string disp = !string.IsNullOrEmpty(proc.processName) ? proc.processName : proc.name;
                tree.Add($"Processos/{proc.id:D2} - {disp}", new ProcessUnifiedEditor(proc));
            }

            // 6. Receitas
            tree.Add("Receitas/➕ Criar Nova Receita", new RecipeCreationActions());
            string[] recGuids = AssetDatabase.FindAssets("t:RecipeDef");
            var recipes = new List<RecipeDef>();
            for (int i = 0; i < recGuids.Length; i++)
            {
                var r = AssetDatabase.LoadAssetAtPath<RecipeDef>(AssetDatabase.GUIDToAssetPath(recGuids[i]));
                if (r != null) recipes.Add(r);
            }
            recipes = recipes.OrderBy(r => r.id).ToList();
            foreach (var rec in recipes)
            {
                string disp = !string.IsNullOrEmpty(rec.recipeName) ? rec.recipeName : rec.name;
                tree.Add($"Receitas/{rec.id:D2} - {disp}", new RecipeUnifiedEditor(rec));
            }

            // 7. Configurações Globais & Cenário
            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
            if (config != null)
            {
                tree.Add("Outros/Config Procedural", config);
            }
            tree.Add("Cenário e Validação", new ValidationActions());

            return tree;
        }

        #region Ações de Criação com Geração de Próximo ID Único
        public class StationCreationActions
        {
            [Title("Criar Nova Bancada ou Layout de Slots", "Geração com ID único sem colisões no catálogo", TitleAlignments.Centered)]

            [BoxGroup("Nova Bancada (StationDef)")]
            public string NomeNovaBancada = "Bancada Extra";

            [BoxGroup("Nova Bancada (StationDef)")]
            public PizzeriaStationKind TipoBancada = PizzeriaStationKind.Prep;

            [BoxGroup("Nova Bancada (StationDef)")]
            [Button("➕ Criar StationDef no Catálogo", ButtonSizes.Large)]
            [GUIColor(0.25f, 0.85f, 0.45f)]
            public void CriarBancada()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Stations";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                // Descobrir maior ID existente
                int nextId = 1;
                string[] guids = AssetDatabase.FindAssets("t:StationDef");
                foreach (var g in guids)
                {
                    var st = AssetDatabase.LoadAssetAtPath<StationDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (st != null && st.id >= nextId) nextId = st.id + 1;
                }

                string safeName = NomeNovaBancada.Replace(" ", "");
                string assetPath = $"{dir}/StationDef_{nextId}_{safeName}.asset";

                var def = ScriptableObject.CreateInstance<StationDef>();
                def.id = nextId;
                def.stationName = NomeNovaBancada;
                def.defaultStationKind = TipoBancada;
                def.slotCount = 2;

                AssetDatabase.CreateAsset(def, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Bancada Criada", $"StationDef '{NomeNovaBancada}' criado com sucesso com ID #{nextId} em:\n{assetPath}", "OK");
            }

            [BoxGroup("Novo Preset de Layout (StationSlotLayoutAsset)")]
            public string NomeLayout = "Layout_GradePersonalizada";

            [BoxGroup("Novo Preset de Layout (StationSlotLayoutAsset)")]
            public StationPresetType Preset = StationPresetType.Grid;

            [BoxGroup("Novo Preset de Layout (StationSlotLayoutAsset)")]
            [Button("➕ Criar Asset de Layout de Slots", ButtonSizes.Medium)]
            [GUIColor(0.2f, 0.7f, 0.95f)]
            public void CriarLayout()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Layouts";
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string assetPath = $"{dir}/{NomeLayout}.asset";
                var layout = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();
                layout.layoutName = NomeLayout;
                layout.presetType = Preset;
                layout.rows = 2;
                layout.columns = 2;
                layout.spacingX = 0.35f;
                layout.spacingZ = 0.35f;

                layout.RecalculateFromPreset(out _, out _, out _, out _);

                AssetDatabase.CreateAsset(layout, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Layout Criado", $"Layout '{NomeLayout}' criado com sucesso!", "OK");
            }
        }

        public class IngredientCreationActions
        {
            [Title("Criar Novo Ingrediente", "ID estável automático", TitleAlignments.Centered)]
            public string NomeIngrediente = "Novo Ingrediente";
            public ItemCategory Categoria = ItemCategory.Topping;
            public bool RequerPreparo = true;

            [Button("➕ Criar IngredientDef", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.85f, 0.35f)]
            public void Criar()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria";
                int nextId = 1;
                string[] guids = AssetDatabase.FindAssets("t:IngredientDef");
                foreach (var g in guids)
                {
                    var ing = AssetDatabase.LoadAssetAtPath<IngredientDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (ing != null && ing.id >= nextId) nextId = ing.id + 1;
                }

                string safeName = NomeIngrediente.Replace(" ", "");
                string assetPath = $"{dir}/Ingredient_{nextId}_{safeName}.asset";

                var def = ScriptableObject.CreateInstance<IngredientDef>();
                def.id = nextId;
                def.ingredientName = NomeIngrediente;
                def.category = Categoria;
                def.requiresPrep = RequerPreparo;

                AssetDatabase.CreateAsset(def, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Ingrediente Criado", $"Ingrediente '{NomeIngrediente}' criado com sucesso com ID #{nextId}!", "OK");
            }
        }

        public class ToolCreationActions
        {
            [Title("Criar Novo Utensílio", "ID estável automático", TitleAlignments.Centered)]
            public string NomeUtensilio = "Nova Ferramenta";
            public ToolCapability Capacidade = ToolCapability.Slice;

            [Button("➕ Criar ToolDef", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.85f, 0.35f)]
            public void Criar()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria";
                int nextId = 1;
                string[] guids = AssetDatabase.FindAssets("t:ToolDef");
                foreach (var g in guids)
                {
                    var t = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null && t.id >= nextId) nextId = t.id + 1;
                }

                string safeName = NomeUtensilio.Replace(" ", "");
                string assetPath = $"{dir}/Tool_{nextId}_{safeName}.asset";

                var def = ScriptableObject.CreateInstance<ToolDef>();
                def.id = nextId;
                def.toolName = NomeUtensilio;
                def.capabilities = Capacidade;
                def.initialSharedCount = 1;

                AssetDatabase.CreateAsset(def, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Utensílio Criado", $"Utensílio '{NomeUtensilio}' criado com sucesso com ID #{nextId}!", "OK");
            }
        }

        public class ProcessCreationActions
        {
            [Title("Criar Novo Processo", "ID estável automático", TitleAlignments.Centered)]
            public string NomeProcesso = "Novo Processo";
            public IngredientDef Entrada;
            public IngredientDef Saida;
            public ToolCapability Ferramenta = ToolCapability.Slice;
            public float DuracaoSegundos = 3.0f;

            [Button("➕ Criar ProcessDef", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.85f, 0.35f)]
            public void Criar()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria";
                int nextId = 1;
                string[] guids = AssetDatabase.FindAssets("t:ProcessDef");
                foreach (var g in guids)
                {
                    var p = AssetDatabase.LoadAssetAtPath<ProcessDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (p != null && p.id >= nextId) nextId = p.id + 1;
                }

                string safeName = NomeProcesso.Replace(" ", "");
                string assetPath = $"{dir}/Process_{nextId}_{safeName}.asset";

                var def = ScriptableObject.CreateInstance<ProcessDef>();
                def.id = nextId;
                def.processName = NomeProcesso;
                def.inputIngredient = Entrada;
                def.outputIngredient = Saida;
                def.requiredToolCapability = Ferramenta;
                def.baseDuration = DuracaoSegundos;

                AssetDatabase.CreateAsset(def, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Processo Criado", $"Processo '{NomeProcesso}' criado com sucesso com ID #{nextId}!", "OK");
            }
        }

        public class RecipeCreationActions
        {
            [Title("Criar Nova Receita", "ID estável automático", TitleAlignments.Centered)]
            public string NomeReceita = "Nova Pizza";
            public int PrecoBase = 25;
            public float DuracaoForno = 15f;

            [Button("➕ Criar RecipeDef", ButtonSizes.Large)]
            [GUIColor(0.2f, 0.85f, 0.35f)]
            public void Criar()
            {
                string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria";
                int nextId = 1;
                string[] guids = AssetDatabase.FindAssets("t:RecipeDef");
                foreach (var g in guids)
                {
                    var r = AssetDatabase.LoadAssetAtPath<RecipeDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (r != null && r.id >= nextId) nextId = r.id + 1;
                }

                string safeName = NomeReceita.Replace(" ", "");
                string assetPath = $"{dir}/Recipe_{nextId}_{safeName}.asset";

                var def = ScriptableObject.CreateInstance<RecipeDef>();
                def.id = nextId;
                def.recipeName = NomeReceita;
                def.basePrice = PrecoBase;
                def.baseBakeDuration = DuracaoForno;

                AssetDatabase.CreateAsset(def, assetPath);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayDialog("Receita Criada", $"Receita '{NomeReceita}' criada com sucesso com ID #{nextId}!", "OK");
            }
        }
        #endregion

        public class StudioLauncherTab
        {
            [Title("Estúdio Visual de Conteúdo 3D", "Ambiente de visualização isolado e edição contínua fora do Play Mode", TitleAlignments.Centered)]
            [InfoBox("Abra a janela independente para manter uma bancada fixada enquanto edita qualquer outro item na ficha Odin.")]

            [Button("🖥️ Abrir Janela do Estúdio Visual 3D Independente", ButtonSizes.Large)]
            [GUIColor(0.25f, 0.75f, 0.95f)]
            public void OpenStudio()
            {
                PizzeriaContentStudioWindow.ShowWindow();
            }
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

        public class ModularPizzaPreviewTab
        {
            [Title("Composição e Prévia da Pizza Modular (Fase 3)", "Visualizador 3D em tempo real com suporte a todos os 7 modos de posicionamento", TitleAlignments.Centered)]
            [InfoBox("Abra a janela independente de prévia 3D para editar ingredientes, posicionamentos e assamento ao vivo com suporte a Undo/Redo sem Play Mode.")]

            [Button(ButtonSizes.Large, Name = "Abrir Janela de Prévia 3D da Pizza")]
            public void OpenPreviewWindow()
            {
                ScaryParty.Pizzeria.Editor.PizzaVisual.PizzaVisualPreviewWindow.ShowWindow();
            }
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
    }
}
