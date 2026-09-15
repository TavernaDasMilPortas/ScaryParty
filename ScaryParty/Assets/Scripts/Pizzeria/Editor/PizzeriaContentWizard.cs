using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor
{
    public class PizzeriaContentWizard : EditorWindow
    {
        private enum WizardTab
        {
            Ingredientes,
            Receitas,
            Utensilios,
            Processos,
            Bancadas,
            Upgrades
        }

        private WizardTab _currentTab = WizardTab.Ingredientes;
        private PizzeriaConfig _config;
        private Vector2 _scrollPos;

        // Draft state for new Ingredient
        private int _newIngId = 10;
        private string _newIngName = "Novo Ingrediente";
        private ItemCategory _newIngCat = ItemCategory.Topping;
        private bool _newIngReqPrep = false;
        private int _newIngPrepId = 0;

        // Draft state for new Process
        private int _newProcId = 10;
        private string _newProcName = "Novo Processo";
        private int _newProcInputId = 4;
        private ProcessingStage _newProcInputStage = ProcessingStage.Raw;
        private int _newProcOutputId = 5;
        private ProcessingStage _newProcOutputStage = ProcessingStage.Prepared;
        private ToolCapability _newProcTool = ToolCapability.Grate;
        private float _newProcDuration = 3.0f;

        // Draft state for new Tool
        private int _newToolId = 10;
        private string _newToolName = "Novo Utensílio";
        private ToolCapability _newToolCap = ToolCapability.Grate;
        private int _newToolCount = 1;

        // Draft state for new Recipe
        private int _newRecId = 10;
        private string _newRecName = "Nova Pizza";
        private int _newRecBaseId = 2;
        private int _newRecPrice = 25;
        private float _newRecBakeDur = 15f;

        [MenuItem("Tools/Scary Party/Pizzeria/Configurar conteúdo", false, 12)]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaContentWizard>("Configurar Conteúdo");
            window.minSize = new Vector2(650, 500);
        }

        private void OnEnable()
        {
            LoadConfig();
        }

        private void LoadConfig()
        {
            _config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset");
        }

        private void OnGUI()
        {
            if (_config == null)
            {
                EditorGUILayout.HelpBox("PizzeriaConfig.asset não encontrado em Assets/ScriptableObjects/Pizzeria/! Use o menu 'Build or Update' primeiro.", MessageType.Warning);
                if (GUILayout.Button("Criar PizzeriaConfig"))
                {
                    PizzeriaBuilder.BuildOrUpdate();
                    LoadConfig();
                }
                return;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("🍕 Wizard de Autoria da Pizzaria", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Crie e edite ingredientes, receitas, utensílios, processos e bancadas sem alterar código C#.", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(5);
            _currentTab = (WizardTab)GUILayout.Toolbar((int)_currentTab, new[] { "Ingredientes", "Receitas", "Utensílios", "Processos", "Bancadas", "Upgrades" });
            EditorGUILayout.Space(5);

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            switch (_currentTab)
            {
                case WizardTab.Ingredientes:
                    DrawIngredientsTab();
                    break;
                case WizardTab.Receitas:
                    DrawRecipesTab();
                    break;
                case WizardTab.Utensilios:
                    DrawToolsTab();
                    break;
                case WizardTab.Processos:
                    DrawProcessesTab();
                    break;
                case WizardTab.Bancadas:
                    DrawStationsTab();
                    break;
                case WizardTab.Upgrades:
                    DrawUpgradesTab();
                    break;
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawIngredientsTab()
        {
            EditorGUILayout.LabelField("Ingredientes Configurados", EditorStyles.boldLabel);
            for (int i = 0; i < _config.ingredients.Count; i++)
            {
                var ing = _config.ingredients[i];
                if (ing == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{ing.id} {ing.ingredientName} ({ing.category})", GUILayout.Width(250));
                EditorGUILayout.LabelField(ing.requiresPrep ? $"Preparo -> #{ing.preparedIngredientId}" : "Pronto", GUILayout.Width(150));

                if (GUILayout.Button("Editar Asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = ing;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Adicionar Novo Ingrediente (Rascunho)", EditorStyles.boldLabel);
            _newIngId = EditorGUILayout.IntField("ID:", _newIngId);
            _newIngName = EditorGUILayout.TextField("Nome:", _newIngName);
            _newIngCat = (ItemCategory)EditorGUILayout.EnumPopup("Categoria:", _newIngCat);
            _newIngReqPrep = EditorGUILayout.Toggle("Requer Preparo?", _newIngReqPrep);
            if (_newIngReqPrep)
            {
                _newIngPrepId = EditorGUILayout.IntField("ID Resultante:", _newIngPrepId);
            }

            if (GUILayout.Button("Salvar Ingrediente no Catálogo", GUILayout.Height(30)))
            {
                var def = ScriptableObject.CreateInstance<IngredientDef>();
                def.id = _newIngId;
                def.ingredientName = _newIngName;
                def.category = _newIngCat;
                def.requiresPrep = _newIngReqPrep;
                def.preparedIngredientId = _newIngPrepId;

                string path = $"Assets/ScriptableObjects/Pizzeria/Ingredient_{_newIngId}_{_newIngName.Replace(" ", "")}.asset";
                AssetDatabase.CreateAsset(def, path);
                _config.ingredients.Add(def);
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();

                _newIngId++;
                EditorUtility.DisplayDialog("Sucesso", $"Ingrediente '{def.ingredientName}' adicionado com sucesso!", "OK");
            }
        }

        private void DrawRecipesTab()
        {
            EditorGUILayout.LabelField("Receitas Configuradas", EditorStyles.boldLabel);
            for (int i = 0; i < _config.recipes.Count; i++)
            {
                var rec = _config.recipes[i];
                if (rec == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{rec.id} {rec.recipeName} - R${rec.basePrice} ({rec.baseBakeDuration}s)", GUILayout.Width(250));
                EditorGUILayout.LabelField($"{rec.ingredients.Count} ingredientes", GUILayout.Width(150));

                if (GUILayout.Button("Editar Asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = rec;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Adicionar Nova Receita", EditorStyles.boldLabel);
            _newRecId = EditorGUILayout.IntField("ID:", _newRecId);
            _newRecName = EditorGUILayout.TextField("Nome da Pizza:", _newRecName);
            _newRecBaseId = EditorGUILayout.IntField("ID Base (Massa):", _newRecBaseId);
            _newRecPrice = EditorGUILayout.IntField("Preço (R$):", _newRecPrice);
            _newRecBakeDur = EditorGUILayout.FloatField("Tempo Forno (s):", _newRecBakeDur);

            if (GUILayout.Button("Salvar Nova Receita", GUILayout.Height(30)))
            {
                var def = ScriptableObject.CreateInstance<RecipeDef>();
                def.id = _newRecId;
                def.recipeName = _newRecName;
                def.baseIngredientId = _newRecBaseId;
                def.basePrice = _newRecPrice;
                def.baseBakeDuration = _newRecBakeDur;

                string path = $"Assets/ScriptableObjects/Pizzeria/Recipe_{_newRecId}_{_newRecName.Replace(" ", "")}.asset";
                AssetDatabase.CreateAsset(def, path);
                _config.recipes.Add(def);
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();

                _newRecId++;
                EditorUtility.DisplayDialog("Sucesso", $"Receita '{def.recipeName}' criada!", "OK");
            }
        }

        private void DrawToolsTab()
        {
            EditorGUILayout.LabelField("Utensílios Compartilhados", EditorStyles.boldLabel);
            for (int i = 0; i < _config.tools.Count; i++)
            {
                var tool = _config.tools[i];
                if (tool == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{tool.id} {tool.toolName} ({tool.capabilities})", GUILayout.Width(250));
                EditorGUILayout.LabelField($"Qtd Inicial: {tool.initialSharedCount}", GUILayout.Width(150));

                if (GUILayout.Button("Editar Asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = tool;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Adicionar Novo Utensílio", EditorStyles.boldLabel);
            _newToolId = EditorGUILayout.IntField("ID:", _newToolId);
            _newToolName = EditorGUILayout.TextField("Nome:", _newToolName);
            _newToolCap = (ToolCapability)EditorGUILayout.EnumFlagsField("Capacidades:", _newToolCap);
            _newToolCount = EditorGUILayout.IntField("Qtd Inicial Compartilhada:", _newToolCount);

            if (GUILayout.Button("Salvar Utensílio", GUILayout.Height(30)))
            {
                var def = ScriptableObject.CreateInstance<ToolDef>();
                def.id = _newToolId;
                def.toolName = _newToolName;
                def.capabilities = _newToolCap;
                def.initialSharedCount = _newToolCount;

                string path = $"Assets/ScriptableObjects/Pizzeria/Tool_{_newToolId}_{_newToolName.Replace(" ", "")}.asset";
                AssetDatabase.CreateAsset(def, path);
                _config.tools.Add(def);
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();

                _newToolId++;
                EditorUtility.DisplayDialog("Sucesso", $"Utensílio '{def.toolName}' criado!", "OK");
            }
        }

        private void DrawProcessesTab()
        {
            EditorGUILayout.LabelField("Processos de Preparo", EditorStyles.boldLabel);
            for (int i = 0; i < _config.processes.Count; i++)
            {
                var proc = _config.processes[i];
                if (proc == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{proc.id} {proc.processName} ({proc.baseDuration}s)", GUILayout.Width(250));
                EditorGUILayout.LabelField($"Ferramenta: {proc.requiredToolCapability}", GUILayout.Width(150));

                if (GUILayout.Button("Editar Asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = proc;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Adicionar Novo Processo", EditorStyles.boldLabel);
            _newProcId = EditorGUILayout.IntField("ID:", _newProcId);
            _newProcName = EditorGUILayout.TextField("Nome:", _newProcName);
            _newProcInputId = EditorGUILayout.IntField("ID Insumo:", _newProcInputId);
            _newProcInputStage = (ProcessingStage)EditorGUILayout.EnumPopup("Estágio Entrada:", _newProcInputStage);
            _newProcOutputId = EditorGUILayout.IntField("ID Saída:", _newProcOutputId);
            _newProcOutputStage = (ProcessingStage)EditorGUILayout.EnumPopup("Estágio Saída:", _newProcOutputStage);
            _newProcTool = (ToolCapability)EditorGUILayout.EnumPopup("Ferramenta Exigida:", _newProcTool);
            _newProcDuration = EditorGUILayout.FloatField("Duração Base (s):", _newProcDuration);

            if (GUILayout.Button("Salvar Processo", GUILayout.Height(30)))
            {
                var def = ScriptableObject.CreateInstance<ProcessDef>();
                def.id = _newProcId;
                def.processName = _newProcName;
                def.inputIngredientId = _newProcInputId;
                def.inputStage = _newProcInputStage;
                def.outputIngredientId = _newProcOutputId;
                def.outputStage = _newProcOutputStage;
                def.requiredToolCapability = _newProcTool;
                def.baseDuration = _newProcDuration;

                string path = $"Assets/ScriptableObjects/Pizzeria/Process_{_newProcId}_{_newProcName.Replace(" ", "")}.asset";
                AssetDatabase.CreateAsset(def, path);
                _config.processes.Add(def);
                EditorUtility.SetDirty(_config);
                AssetDatabase.SaveAssets();

                _newProcId++;
                EditorUtility.DisplayDialog("Sucesso", $"Processo '{def.processName}' adicionado!", "OK");
            }
        }

        private void DrawStationsTab()
        {
            EditorGUILayout.LabelField("Bancadas e Estações", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Exemplo obrigatório configurável: Bancada Preparo A dura 3s para Ralar Queijo; Bancada Preparo B aceita o mesmo processo mas tem override de duração para 2s.", MessageType.Info);

            for (int i = 0; i < _config.stations.Count; i++)
            {
                var st = _config.stations[i];
                if (st == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{st.id} {st.stationName} (Slots: {st.slotCount})", GUILayout.Width(250));
                EditorGUILayout.LabelField($"Overrides: {st.durationOverrides.Count}", GUILayout.Width(150));

                if (GUILayout.Button("Editar Asset", GUILayout.Width(100)))
                {
                    Selection.activeObject = st;
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawUpgradesTab()
        {
            EditorGUILayout.LabelField("Upgrades Funcionais", EditorStyles.boldLabel);
            for (int i = 0; i < _config.upgrades.Count; i++)
            {
                var up = _config.upgrades[i];
                if (up == null) continue;

                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"#{up.id} {up.upgradeName} ({up.scope}:{up.statName})", GUILayout.Width(250));
                EditorGUILayout.LabelField($"{up.operation} {up.valuePerLevel} (Max Lvl: {up.maxLevel})", GUILayout.Width(200));

                if (GUILayout.Button("Editar", GUILayout.Width(80)))
                {
                    Selection.activeObject = up;
                }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
