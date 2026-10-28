using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public class PizzeriaAuthoringWindow : EditorWindow
    {
        private const string DefaultSettingsPath = "Assets/ScriptableObjects/Pizzeria/PizzeriaAuthoringSettings_Modular.asset";

        private PizzeriaAuthoringSettings _settings;
        private int _selectedTabIndex = 0;
        private Vector2 _scrollPosition;

        // Estado de UI da Aba 1
        private PizzeriaHierarchySummaryReport _lastHierarchyReport;
        private PizzeriaRebuildPreviewInfo _rebuildPreview;
        private bool _showFloorSettingsFoldout = false;
        private bool _showRulesFoldout = false;
        private Transform _manualParentTarget;
        private PizzeriaHierarchyCategory _batchCategoryTarget = PizzeriaHierarchyCategory.Decoration;

        // Estado de UI da Aba 3
        private PizzeriaStationKind _selectedStationKind = PizzeriaStationKind.Oven;
        private PizzeriaStationConversionOptions _currentOptions;
        private PizzeriaVisualRole _selectedVisualRole = PizzeriaVisualRole.UtensilVisual;
        private int _selectedVisualDefinitionId = 1;

        // Estado de UI da Aba 4
        private PizzeriaGameplayValidationReport _lastValidationReport;
        private List<string> _lastIntegrationMessages = new List<string>();

        private string _statusBannerMessage = "";
        private MessageType _statusBannerType = MessageType.Info;

        private static readonly string[] TabLabels = new[]
        {
            "1. Modelo e Prefab",
            "2. Funcionamento Base",
            "3. Bancadas e Objetos",
            "4. Validação"
        };

        [MenuItem("Tools/Scary Party/Pizzaria Modular", priority = 10)]
        public static void ShowWindow()
        {
            var win = GetWindow<PizzeriaAuthoringWindow>("Pizzaria Modular");
            win.minSize = new Vector2(560f, 640f);
            win.EnsureSettingsLoaded();
            win.Show();
        }

        public static void OpenWithSettings(PizzeriaAuthoringSettings settings)
        {
            var win = GetWindow<PizzeriaAuthoringWindow>("Pizzaria Modular");
            win.minSize = new Vector2(560f, 640f);
            win._settings = settings;
            win.ResetOptionsForKind(win._selectedStationKind);
            win.Show();
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            SceneView.duringSceneGui += OnDuringSceneGui;
            EnsureSettingsLoaded();
            ResetOptionsForKind(_selectedStationKind);
        }

        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            SceneView.duringSceneGui -= OnDuringSceneGui;
        }

        private void OnSelectionChanged()
        {
            Repaint();
            SceneView.RepaintAll();
        }

        private void EnsureSettingsLoaded()
        {
            if (_settings != null) return;
            _settings = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>(DefaultSettingsPath);
            if (_settings == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:PizzeriaAuthoringSettings");
                if (guids != null && guids.Length > 0)
                {
                    _settings = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
        }

        private void CreateDefaultSettingsAsset()
        {
            PizzeriaPrefabAuthoringService.EnsureFolderExists("Assets/ScriptableObjects/Pizzeria");
            var asset = ScriptableObject.CreateInstance<PizzeriaAuthoringSettings>();
            asset.InitializeDefaultFbxPreset();

            // Vincular ao FBX padrão como exemplo se existir
            var defaultFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx");
            if (defaultFbx != null)
            {
                asset.sourceModel = defaultFbx;
            }

            asset.pizzeriaConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset")
                                ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Resources/Pizzeria/PizzeriaConfig.asset");
            asset.presentationCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");

            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultSettingsPath);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            _settings = asset;
            SetBanner($"Configuração criada em '{path}'.", MessageType.Info);
        }

        private void SetBanner(string msg, MessageType type = MessageType.Info)
        {
            _statusBannerMessage = msg;
            _statusBannerType = type;
            Repaint();
        }

        private void ResetOptionsForKind(PizzeriaStationKind kind)
        {
            _selectedStationKind = kind;
            PizzeriaConfig cfg = _settings != null ? _settings.pizzeriaConfig : null;
            _currentOptions = PizzeriaStationRecipeRegistry.CreateDefaultOptions(kind, cfg);
        }

        private void OnGUI()
        {
            DrawTopContextHeader();

            EditorGUILayout.Space(4);
            int newTab = GUILayout.Toolbar(_selectedTabIndex, TabLabels, GUILayout.Height(28));
            if (newTab != _selectedTabIndex)
            {
                _selectedTabIndex = newTab;
                if (_selectedTabIndex == 3 && _lastValidationReport == null && _settings != null && _settings.targetPrefab != null)
                {
                    RunReadOnlyValidation();
                }
            }

            if (!string.IsNullOrEmpty(_statusBannerMessage))
            {
                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(_statusBannerMessage, _statusBannerType);
            }

            EditorGUILayout.Space(4);
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            if (_settings == null)
            {
                DrawNoSettingsView();
            }
            else
            {
                switch (_selectedTabIndex)
                {
                    case 0:
                        DrawTab1ModelAndPrefab();
                        break;
                    case 1:
                        DrawTab2BaseFunctioning();
                        break;
                    case 2:
                        DrawTab3StationsAndObjects();
                        break;
                    case 3:
                        DrawTab4ValidationAndIntegration();
                        break;
                }
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Barra superior fixa que mostra sempre:
        /// - Configuração ativa do Builder
        /// - Prefab-alvo e status do Prefab Mode
        /// - Objetos selecionados atualmente e sua função atual
        /// </summary>
        private void DrawTopContextHeader()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Builder Manual — Pizzaria Modular", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (_settings != null && GUILayout.Button("Selecionar Asset de Config", EditorStyles.miniButton, GUILayout.Width(160)))
            {
                Selection.activeObject = _settings;
                EditorGUIUtility.PingObject(_settings);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var newSettings = (PizzeriaAuthoringSettings)EditorGUILayout.ObjectField("Configuração do Builder", _settings, typeof(PizzeriaAuthoringSettings), false);
            if (EditorGUI.EndChangeCheck())
            {
                _settings = newSettings;
                ResetOptionsForKind(_selectedStationKind);
            }

            if (_settings == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            // Status do Prefab-Alvo e Prefab Stage
            GameObject openRoot = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(_settings.targetPrefab);
            string targetLabel = _settings.targetPrefab != null
                ? $"{_settings.targetPrefab.name} ({AssetDatabase.GetAssetPath(_settings.targetPrefab)})"
                : "(Nenhum prefab-alvo criado/selecionado)";

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Prefab-Alvo:", targetLabel, EditorStyles.miniLabel);
            if (_settings.targetPrefab != null)
            {
                if (openRoot == null)
                {
                    GUI.backgroundColor = new Color(0.4f, 0.85f, 1f);
                    if (GUILayout.Button("Abrir Prefab para Editar", GUILayout.Width(170), GUILayout.Height(20)))
                    {
                        string path = AssetDatabase.GetAssetPath(_settings.targetPrefab);
                        PizzeriaPrefabAuthoringService.SanitizePrefabAssetOnDisk(path);
                        AssetDatabase.OpenAsset(_settings.targetPrefab);
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    GUI.backgroundColor = new Color(0.45f, 0.95f, 0.55f);
                    if (GUILayout.Button("Salvar Prefab Aberto", GUILayout.Width(150), GUILayout.Height(20)))
                    {
                        SaveOpenPrefabStage(openRoot);
                    }
                    GUI.backgroundColor = Color.white;
                }
            }
            EditorGUILayout.EndHorizontal();

            // Resumo da Seleção Atual e Função Atual
            GameObject[] sel = Selection.gameObjects;
            var val = PizzeriaSelectionConverter.ValidateSelection(sel, _settings);

            string selectionSummary = sel == null || sel.Length == 0
                ? "Nenhum objeto selecionado"
                : (sel.Length == 1 ? sel[0].name : $"{sel.Length} objetos ({ string.Join(", ", sel.Take(3).Select(g => g.name)) }{(sel.Length > 3 ? "..." : "")})");

            string currentFuncSummary = "Sem função de gameplay";
            if (val.existingStationOnPrimaryTarget != null)
            {
                var desc = PizzeriaStationRecipeRegistry.GetDescriptor(val.existingStationKind);
                currentFuncSummary = $"{desc.displayName} ({val.existingStationOnPrimaryTarget.GetType().Name} #{val.existingStationOnPrimaryTarget.stationId})";
            }
            else if (sel != null && sel.Length == 1 && sel[0] != null)
            {
                var nodeId = sel[0].GetComponent<PizzeriaNodeIdentity>();
                if (nodeId != null && nodeId.visualRole != PizzeriaVisualRole.None)
                {
                    currentFuncSummary = $"Papel Visual: {nodeId.visualRole}";
                }
            }

            EditorGUILayout.LabelField("Seleção Atual:", selectionSummary, EditorStyles.miniBoldLabel);
            EditorGUILayout.LabelField("Função Atual:", currentFuncSummary, EditorStyles.miniLabel);

            if (!val.isValid && sel != null && sel.Length > 0)
            {
                EditorGUILayout.HelpBox($"Ação na seleção desabilitada: {val.blockReason}", MessageType.Warning);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawNoSettingsView()
        {
            EditorGUILayout.HelpBox(
                "Selecione uma configuração PizzeriaAuthoringSettings acima ou crie uma nova configuração padrão para começar.",
                MessageType.Info);

            if (GUILayout.Button("Criar Configuração do Builder (Padrão)", GUILayout.Height(32)))
            {
                CreateDefaultSettingsAsset();
            }
        }

        // =====================================================================
        // ABA 1: MODELO E PREFAB + ORGANIZAÇÃO DA HIERARQUIA
        // =====================================================================
        private void DrawTab1ModelAndPrefab()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Etapa 1 — Modelo de Origem e Criação do Prefab Editável", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings.sourceModel = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Modelo de Origem (FBX/Prefab)", "Arraste o modelo 3D importado (ex: pizzariamodular.fbx) aqui."),
                _settings.sourceModel, typeof(GameObject), false);

            _settings.targetPrefab = (GameObject)EditorGUILayout.ObjectField(
                new GUIContent("Prefab-Alvo Editável", "Prefab regular onde a pizzaria é editada."),
                _settings.targetPrefab, typeof(GameObject), false);

            _settings.outputFolder = EditorGUILayout.TextField("Pasta de Saída", _settings.outputFolder);
            _settings.outputPrefabName = EditorGUILayout.TextField("Nome do Prefab de Saída", _settings.outputPrefabName);
            _settings.organizeHierarchyOnCreate = EditorGUILayout.ToggleLeft(
                "Organizar hierarquia automaticamente ao criar prefab (Recomendado)",
                _settings.organizeHierarchyOnCreate);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.Space(6);
            string resolvedPath = PizzeriaPrefabAuthoringService.ResolveTargetPrefabPath(_settings);
            EditorGUILayout.LabelField("Caminho de Saída Resolvido:", resolvedPath, EditorStyles.miniLabel);

            bool canCreateOrOpen = _settings.sourceModel != null || _settings.targetPrefab != null;
            using (new EditorGUI.DisabledScope(!canCreateOrOpen))
            {
                string btnLabel = _settings.targetPrefab != null
                    ? "Abrir / Continuar Edição do Prefab da Pizzaria"
                    : "Criar Prefab da Pizzaria";

                if (GUILayout.Button(btnLabel, GUILayout.Height(32)))
                {
                    var prefab = PizzeriaPrefabAuthoringService.CreateOrOpenEditablePrefab(
                        _settings, out _lastHierarchyReport, out bool createdNew, out string msg);
                    SetBanner(msg, prefab != null ? MessageType.Info : MessageType.Error);
                    if (prefab != null)
                    {
                        AssetDatabase.OpenAsset(prefab);
                    }
                }
            }
            if (!canCreateOrOpen)
            {
                EditorGUILayout.HelpBox("Desabilitado: selecione um Modelo de Origem (FBX) para criar o prefab.", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            // Seção de Organização Estrutural da Hierarquia
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Organização Estrutural Modular (Por Cômodos e Parentalidade em Bancadas/Mesas)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Divide os objetos por cômodo (as paredes divisórias ficam em apenas 1 cômodo), agrupa equipamentos com suas portas/tampas e " +
                "torna tudo que está apoiado em cima de uma bancada/mesa/prateleira filho dela para facilitar mover blocos inteiros.",
                MessageType.None);

            EditorGUI.BeginChangeCheck();
            _settings.groupByRooms = EditorGUILayout.ToggleLeft(
                "Dividir hierarquia por Cômodos (Paredes divisórias ficam em apenas 1 cômodo)",
                _settings.groupByRooms);
            _settings.parentItemsOnSurfaces = EditorGUILayout.ToggleLeft(
                "Parentalizar objetos em cima de bancadas/mesas/prateleiras (utensílios, molhos, tábuas, registradora, mochilas etc.)",
                _settings.parentItemsOnSurfaces);
            _settings.parentChairsToTables = EditorGUILayout.ToggleLeft(
                "Parentalizar cadeiras à mesa correspondente (mover conjunto de mesa inteiro)",
                _settings.parentChairsToTables);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
            }

            GameObject editableRoot = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(_settings.targetPrefab);
            bool hasOpenStage = editableRoot != null;

            using (new EditorGUI.DisabledScope(!hasOpenStage))
            {
                if (GUILayout.Button("Organizar / Reorganizar Hierarquia Agora (Preservando Travas Manuais)", GUILayout.Height(28)))
                {
                    _lastHierarchyReport = PizzeriaHierarchyOrganizer.OrganizeHierarchy(editableRoot, _settings, respectManualLocks: true);
                    SaveOpenPrefabStage(editableRoot);
                    SetBanner(
                        $"Hierarquia reorganizada por cômodos e superfícies: {_lastHierarchyReport.totalAnalyzedNodes} nós, " +
                        $"{_lastHierarchyReport.assembledUnits.Count} equipamentos montados, " +
                        $"{_lastHierarchyReport.surfaceParentedItems.Count} itens parentalizados em bancadas/mesas.",
                        MessageType.Info);
                }
            }
            if (!hasOpenStage)
            {
                EditorGUILayout.HelpBox("Abra o prefab-alvo em Prefab Mode para executar ou revisar a organização na hierarquia.", MessageType.None);
            }

            // Exibir relatório da última organização
            if (_lastHierarchyReport != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField(
                    $"Resumo: {_lastHierarchyReport.totalAnalyzedNodes} nós | Conjuntos: {_lastHierarchyReport.assembledUnits.Count} | " +
                    $"Em Bancadas/Mesas: {_lastHierarchyReport.surfaceParentedItems.Count} | Revisar: {_lastHierarchyReport.reviewItems.Count}",
                    EditorStyles.miniBoldLabel);

                if (_lastHierarchyReport.countsByRoom != null && _lastHierarchyReport.countsByRoom.Count > 0)
                {
                    EditorGUILayout.LabelField("Objetos por Cômodo:", EditorStyles.miniBoldLabel);
                    foreach (var kvp in _lastHierarchyReport.countsByRoom)
                    {
                        EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value} objeto(s)", EditorStyles.miniLabel);
                    }
                }

                EditorGUILayout.LabelField("Objetos por Categoria:", EditorStyles.miniBoldLabel);
                foreach (var kvp in _lastHierarchyReport.countsByCategory)
                {
                    EditorGUILayout.LabelField($"  • {kvp.Key}: {kvp.Value} objeto(s)", EditorStyles.miniLabel);
                }
                if (_lastHierarchyReport.assembledUnits.Count > 0)
                {
                    EditorGUILayout.LabelField("Conjuntos Pai/Filho Montados:", EditorStyles.miniBoldLabel);
                    foreach (var u in _lastHierarchyReport.assembledUnits)
                        EditorGUILayout.LabelField($"  ✓ {u}", EditorStyles.miniLabel);
                }
                if (_lastHierarchyReport.surfaceParentedItems.Count > 0)
                {
                    EditorGUILayout.LabelField($"Objetos Parentalizados em Bancadas/Mesas/Prateleiras ({_lastHierarchyReport.surfaceParentedItems.Count}):", EditorStyles.miniBoldLabel);
                    int maxShow = Mathf.Min(12, _lastHierarchyReport.surfaceParentedItems.Count);
                    for (int i = 0; i < maxShow; i++)
                        EditorGUILayout.LabelField($"  ↳ {_lastHierarchyReport.surfaceParentedItems[i]}", EditorStyles.miniLabel);
                    if (_lastHierarchyReport.surfaceParentedItems.Count > maxShow)
                        EditorGUILayout.LabelField($"  ... (+{_lastHierarchyReport.surfaceParentedItems.Count - maxShow} outros itens apoiados)", EditorStyles.miniLabel);
                }
            }

            // Assistente de Revisão / Fixação Manual de Pai e Categoria em Lote
            if (hasOpenStage)
            {
                DrawReviewAndManualAssociationPanel(editableRoot);
            }

            _showRulesFoldout = EditorGUILayout.Foldout(_showRulesFoldout, "Configurar Cômodos (Limites XZ) e Regras do Preset FBX");
            if (_showRulesFoldout)
            {
                EditorGUI.BeginChangeCheck();
                if (_settings.roomRegions != null)
                {
                    EditorGUILayout.LabelField("Regiões dos Cômodos (Paredes na fronteira ficam no Cômodo de menor Prioridade):", EditorStyles.miniBoldLabel);
                    for (int i = 0; i < _settings.roomRegions.Count; i++)
                    {
                        var r = _settings.roomRegions[i];
                        EditorGUILayout.BeginVertical("helpbox");
                        EditorGUILayout.BeginHorizontal();
                        r.roomName = EditorGUILayout.TextField($"Cômodo {i + 1}", r.roomName);
                        r.priority = EditorGUILayout.IntField("Prioridade", r.priority, GUILayout.Width(130));
                        if (GUILayout.Button("X", GUILayout.Width(24)))
                        {
                            _settings.roomRegions.RemoveAt(i);
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                        r.minXZ = EditorGUILayout.Vector2Field("Min XZ (m)", r.minXZ);
                        r.maxXZ = EditorGUILayout.Vector2Field("Max XZ (m)", r.maxXZ);
                        EditorGUILayout.EndVertical();
                    }
                    if (GUILayout.Button("+ Adicionar Cômodo"))
                    {
                        _settings.roomRegions.Add(new PizzeriaRoomRegionConfig
                        {
                            roomName = $"Comodo_{_settings.roomRegions.Count + 1:00}",
                            priority = _settings.roomRegions.Count + 1
                        });
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_settings);
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Restaurar Cômodos Padrão (3 Cômodos)"))
                {
                    Undo.RecordObject(_settings, "Restaurar Cômodos Padrão");
                    _settings.InitializeDefaultRooms();
                    EditorUtility.SetDirty(_settings);
                }
                if (GUILayout.Button("Restaurar Preset para pizzariamodular.fbx"))
                {
                    Undo.RecordObject(_settings, "Restaurar Preset FBX");
                    _settings.InitializeDefaultFbxPreset();
                    EditorUtility.SetDirty(_settings);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox($"Cômodos: {_settings.roomRegions?.Count ?? 0} | Regras de montagem: {_settings.assemblyRules.Count} | Regras de classificação: {_settings.classificationRules.Count}.", MessageType.None);
            }

            EditorGUILayout.EndVertical();

            // Seção de Piso Editável e Escala Explícita
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Piso Editável (Regiões / Formato em L) e Ajuste Explícito de Escala", EditorStyles.boldLabel);

            bool hasFloor = editableRoot != null && PizzeriaPrefabAuthoringService.HasFloorGeometry(editableRoot);
            if (editableRoot != null && !hasFloor)
            {
                EditorGUILayout.HelpBox("Nenhum piso detectado em Arquitetura/Pisos. Você pode criar o piso editável abaixo (suporta regiões retangulares / formato em L).", MessageType.Warning);
            }

            _showFloorSettingsFoldout = EditorGUILayout.Foldout(_showFloorSettingsFoldout, "Configurar Regiões de Piso e Escala", true);
            if (_showFloorSettingsFoldout)
            {
                EditorGUI.BeginChangeCheck();
                _settings.floorMaterial = (Material)EditorGUILayout.ObjectField("Material do Piso", _settings.floorMaterial, typeof(Material), false);
                for (int i = 0; i < _settings.floorRegions.Count; i++)
                {
                    var reg = _settings.floorRegions[i];
                    EditorGUILayout.BeginVertical("helpbox");
                    EditorGUILayout.BeginHorizontal();
                    reg.regionName = EditorGUILayout.TextField($"Região {i + 1}", reg.regionName);
                    if (GUILayout.Button("X", GUILayout.Width(24)))
                    {
                        _settings.floorRegions.RemoveAt(i);
                        break;
                    }
                    EditorGUILayout.EndHorizontal();
                    reg.minXZ = EditorGUILayout.Vector2Field("Min XZ (m)", reg.minXZ);
                    reg.maxXZ = EditorGUILayout.Vector2Field("Max XZ (m)", reg.maxXZ);
                    reg.floorY = EditorGUILayout.FloatField("Altura do Topo (Y)", reg.floorY);
                    reg.thickness = EditorGUILayout.FloatField("Espessura (m)", reg.thickness);
                    EditorGUILayout.EndVertical();
                }

                if (GUILayout.Button("+ Adicionar Região de Piso (ex: L)"))
                {
                    _settings.floorRegions.Add(new PizzeriaFloorRegionConfig { regionName = $"Regiao_{_settings.floorRegions.Count + 1}" });
                }

                EditorGUILayout.Space(4);
                _settings.explicitScaleFactor = EditorGUILayout.FloatField("Fator de Escala Explícito", _settings.explicitScaleFactor);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorUtility.SetDirty(_settings);
                }

                using (new EditorGUI.DisabledScope(!hasOpenStage))
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Criar / Atualizar Piso no Prefab", GUILayout.Height(26)))
                    {
                        int count = PizzeriaPrefabAuthoringService.CreateOrUpdateEditableFloor(editableRoot, _settings);
                        SaveOpenPrefabStage(editableRoot);
                        SetBanner($"Piso criado/atualizado com {count} região(ões) em Arquitetura/Pisos/PisosGerados.", MessageType.Info);
                    }
                    if (GUILayout.Button("Aplicar Escala Explícita", GUILayout.Height(26)))
                    {
                        PizzeriaPrefabAuthoringService.ApplyExplicitScaleToRoot(editableRoot, _settings.explicitScaleFactor);
                        SaveOpenPrefabStage(editableRoot);
                        SetBanner($"Escala explícita ({_settings.explicitScaleFactor:F2}x) aplicada aos nós do modelo.", MessageType.Info);
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndVertical();

            // Seção Separada de Reconstrução a partir da Fonte com Preview
            if (_settings.targetPrefab != null && _settings.sourceModel != null)
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Operação Separada: Reconstruir a partir do Modelo de Origem", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox("Reconstruir substitui a malha/hierarquia visual pela fonte, preservando Anchors e pisos gerados. Sempre visualize o impacto antes de confirmar.", MessageType.Warning);

                if (GUILayout.Button("Gerar Preview do que será afetado pela Reconstrução"))
                {
                    _rebuildPreview = PizzeriaPrefabAuthoringService.BuildRebuildPreview(_settings);
                }

                if (_rebuildPreview != null)
                {
                    EditorGUILayout.LabelField($"Prefab: {_rebuildPreview.targetPrefabPath}", EditorStyles.miniLabel);
                    EditorGUILayout.LabelField($"Estações já configuradas que seriam resetadas: {_rebuildPreview.existingConfiguredStationsCount}", EditorStyles.miniBoldLabel);
                    foreach (var stName in _rebuildPreview.existingConfiguredStationNames)
                    {
                        EditorGUILayout.LabelField($"  • {stName}", EditorStyles.miniLabel);
                    }
                    EditorGUILayout.LabelField($"Associações manuais fixadas: {_rebuildPreview.manualLocksCount}", EditorStyles.miniLabel);

                    GUI.backgroundColor = new Color(1f, 0.55f, 0.45f);
                    if (GUILayout.Button("Confirmar e Executar Reconstrução a partir da Fonte", GUILayout.Height(26)))
                    {
                        if (EditorUtility.DisplayDialog(
                            "Confirmar Reconstrução",
                            $"Deseja reconstruir '{_settings.targetPrefab.name}' a partir de '{_settings.sourceModel.name}'?\n" +
                            $"Isso removerá {_rebuildPreview.existingConfiguredStationsCount} estação(ões) configurada(s).",
                            "Sim, Reconstruir", "Cancelar"))
                        {
                            bool ok = PizzeriaPrefabAuthoringService.RebuildFromSourceModel(_settings, out _lastHierarchyReport, out string msg);
                            _rebuildPreview = null;
                            SetBanner(msg, ok ? MessageType.Info : MessageType.Error);
                        }
                    }
                    GUI.backgroundColor = Color.white;
                }

                EditorGUILayout.EndVertical();
            }
        }

        private void DrawReviewAndManualAssociationPanel(GameObject editableRoot)
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField("Correção Manual / Em Lote e Itens em 'Revisar'", EditorStyles.miniBoldLabel);

            var pendingNodes = editableRoot.GetComponentsInChildren<PizzeriaNodeIdentity>(true)
                .Where(n => n != null && n.pendingReview).ToList();

            EditorGUILayout.LabelField($"Itens atualmente pendentes em Revisar: {pendingNodes.Count}", EditorStyles.miniLabel);
            for (int i = 0; i < Mathf.Min(8, pendingNodes.Count); i++)
            {
                var p = pendingNodes[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"• {p.name}: {p.reviewReason}", EditorStyles.miniLabel);
                if (GUILayout.Button("Selecionar", EditorStyles.miniButton, GUILayout.Width(75)))
                {
                    Selection.activeGameObject = p.gameObject;
                    EditorGUIUtility.PingObject(p.gameObject);
                }
                EditorGUILayout.EndHorizontal();
            }

            // Alteração em lote para os objetos selecionados
            if (Selection.gameObjects != null && Selection.gameObjects.Length > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                _batchCategoryTarget = (PizzeriaHierarchyCategory)EditorGUILayout.EnumPopup("Categoria para Seleção", _batchCategoryTarget);
                if (GUILayout.Button("Mover e Fixar Categoria em Lote", GUILayout.Width(200)))
                {
                    int moved = PizzeriaHierarchyOrganizer.BatchAssignCategory(editableRoot.transform, Selection.gameObjects, _batchCategoryTarget, lockManualAssociation: true);
                    SaveOpenPrefabStage(editableRoot);
                    SetBanner($"{moved} objeto(s) movido(s) e fixado(s) na categoria {_batchCategoryTarget}.", MessageType.Info);
                }
                EditorGUILayout.EndHorizontal();

                if (Selection.gameObjects.Length == 1 && Selection.activeTransform != null && Selection.activeTransform != editableRoot.transform)
                {
                    EditorGUILayout.BeginHorizontal();
                    _manualParentTarget = (Transform)EditorGUILayout.ObjectField("Pai Manual", _manualParentTarget, typeof(Transform), true);
                    using (new EditorGUI.DisabledScope(_manualParentTarget == null || !_manualParentTarget.IsChildOf(editableRoot.transform)))
                    {
                        if (GUILayout.Button("Fixar Associação Manual ao Pai", GUILayout.Width(200)))
                        {
                            if (PizzeriaHierarchyOrganizer.ApplyManualParentAndLock(Selection.activeTransform, _manualParentTarget, _batchCategoryTarget, out string msg))
                            {
                                SaveOpenPrefabStage(editableRoot);
                                SetBanner(msg, MessageType.Info);
                            }
                            else
                            {
                                SetBanner(msg, MessageType.Warning);
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // ABA 2: FUNCIONAMENTO BASE
        // =====================================================================
        private void DrawTab2BaseFunctioning()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Etapa 2 — Injetar Funcionamento Base na Raiz do Prefab", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Adiciona de forma idempotente na raiz: NetworkObject, PizzeriaRoot, PizzeriaFootprint, PizzeriaNetworkState, " +
                "PizzeriaCommandHandler, Kitchen/Order/LogisticsCommands, PizzeriaNotifier, PlayerLifecycleBridge, " +
                "PizzeriaHudBuilder, PizzeriaItemVisualizer, TicketModalPresenter, Night1Bootstrapper e PizzeriaAuthoringBindings.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _settings.cloneConfigForNewPrefab = EditorGUILayout.ToggleLeft(
                "Criar configuração própria (clonar PizzeriaConfig) a partir da atual",
                _settings.cloneConfigForNewPrefab);
            _settings.pizzeriaConfig = (PizzeriaConfig)EditorGUILayout.ObjectField("PizzeriaConfig", _settings.pizzeriaConfig, typeof(PizzeriaConfig), false);
            _settings.presentationCatalog = (ItemPresentationCatalog)EditorGUILayout.ObjectField("Catálogo de Apresentação", _settings.presentationCatalog, typeof(ItemPresentationCatalog), false);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Footprint e Alinhamento com a Calçada", EditorStyles.miniBoldLabel);
            _settings.defaultFootprintSize = EditorGUILayout.Vector2Field("Tamanho XZ (m)", _settings.defaultFootprintSize);
            _settings.defaultFootprintCenterOffset = EditorGUILayout.Vector3Field("Center Offset Local", _settings.defaultFootprintCenterOffset);
            _settings.defaultEntranceDirectionXZ = EditorGUILayout.Vector2Field("Direção Local da Fachada XZ", _settings.defaultEntranceDirectionXZ);
            _settings.defaultEntranceOffsetAlongFacade = EditorGUILayout.FloatField("Offset da Porta na Fachada", _settings.defaultEntranceOffsetAlongFacade);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.Space(6);
            GameObject editableRoot = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(_settings.targetPrefab);
            bool canInject = editableRoot != null || _settings.targetPrefab != null;

            using (new EditorGUI.DisabledScope(!canInject))
            {
                if (GUILayout.Button("Injetar / Atualizar Funcionamento Base", GUILayout.Height(34)))
                {
                    if (editableRoot != null)
                    {
                        bool ok = PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(editableRoot, _settings, out string msg);
                        SaveOpenPrefabStage(editableRoot);
                        SetBanner(msg, ok ? MessageType.Info : MessageType.Error);
                    }
                    else if (_settings.targetPrefab != null)
                    {
                        string path = AssetDatabase.GetAssetPath(_settings.targetPrefab);
                        var contents = PrefabUtility.LoadPrefabContents(path);
                        try
                        {
                            bool ok = PizzeriaPrefabAuthoringService.InjectOrUpdateBaseFunctioning(contents, _settings, out string msg);
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                            SetBanner(msg, ok ? MessageType.Info : MessageType.Error);
                        }
                        finally
                        {
                            PrefabUtility.UnloadPrefabContents(contents);
                        }
                    }
                }
            }
            if (!canInject)
            {
                EditorGUILayout.HelpBox("Desabilitado: crie ou selecione um Prefab-Alvo na Etapa 1 primeiro.", MessageType.None);
            }

            // Mostrar status atual dos componentes raiz e âncoras
            GameObject inspectRoot = editableRoot != null ? editableRoot : _settings.targetPrefab;
            if (inspectRoot != null)
            {
                EditorGUILayout.Space(6);
                var pRoot = inspectRoot.GetComponent<PizzeriaRoot>();
                var bindings = inspectRoot.GetComponent<PizzeriaAuthoringBindings>();
                var footprint = inspectRoot.GetComponent<PizzeriaFootprint>();

                EditorGUILayout.LabelField("Status na Raiz do Prefab:", EditorStyles.miniBoldLabel);
                EditorGUILayout.LabelField($"• PizzeriaRoot: {(pRoot != null ? "Presente" : "Ausente")} | Config: {(pRoot != null && pRoot.Config != null ? pRoot.Config.name : "Nulo")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"• EntranceAnchor: {(pRoot != null && pRoot.entranceAnchor != null ? pRoot.entranceAnchor.name : "Ausente")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"• ReceivingAnchor: {(pRoot != null && pRoot.receivingAnchor != null ? pRoot.receivingAnchor.name : "Ausente")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"• RecoveryAnchor: {(pRoot != null && pRoot.recoveryAnchor != null ? pRoot.recoveryAnchor.name : "Ausente")}", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"• PlayerSpawns: {(pRoot != null && pRoot.playerSpawnAnchors != null ? pRoot.playerSpawnAnchors.Count(t => t != null) : 0)}/4", EditorStyles.miniLabel);

                if (bindings != null && editableRoot != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Selecionar Grupo Anchors na Hierarchy"))
                    {
                        var anchors = editableRoot.transform.Find("Anchors");
                        if (anchors != null) Selection.activeGameObject = anchors.gameObject;
                    }
                    if (!bindings.anchorsVerifiedByUser)
                    {
                        if (GUILayout.Button("Marcar Âncoras como Verificadas"))
                        {
                            Undo.RecordObject(bindings, "Marcar Âncoras como Verificadas");
                            bindings.anchorsVerifiedByUser = true;
                            bindings.entranceAnchorProvisional = false;
                            bindings.receivingAnchorProvisional = false;
                            bindings.recoveryAnchorProvisional = false;
                            bindings.playerSpawnsProvisional = false;
                            SaveOpenPrefabStage(editableRoot);
                            SetBanner("Âncoras marcadas como verificadas pelo autor.", MessageType.Info);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.EndVertical();
        }

        // =====================================================================
        // ABA 3: BANCADAS, OBJETOS E APRESENTAÇÃO VISUAL
        // =====================================================================
        private void DrawTab3StationsAndObjects()
        {
            var val = PizzeriaSelectionConverter.ValidateSelection(Selection.gameObjects, _settings);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Etapa 3 — Converter Seleção em Bancada / Equipamento", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings.multiSelectionMode = (PizzeriaMultiSelectionMode)EditorGUILayout.EnumPopup(
                new GUIContent("Modo de Seleção Múltipla", "Um equipamento com a seleção (ex: corpo + porta do forno) ou um equipamento independente por objeto selecionado."),
                _settings.multiSelectionMode);
            _settings.showSceneGizmos = EditorGUILayout.ToggleLeft("Mostrar Gizmos e Handles de Slots / Frente / Alcance na Scene View", _settings.showSceneGizmos);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
                SceneView.RepaintAll();
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. Escolha o Tipo de Estação / Equipamento:", EditorStyles.miniBoldLabel);

            // Grade de botões rápidos para os 15 tipos de estação
            var recipes = PizzeriaStationRecipeRegistry.AllRecipes;
            int cols = 3;
            for (int i = 0; i < recipes.Count; i += cols)
            {
                EditorGUILayout.BeginHorizontal();
                for (int c = 0; c < cols && (i + c) < recipes.Count; c++)
                {
                    var rec = recipes[i + c];
                    bool isSelected = _selectedStationKind == rec.kind;
                    GUI.backgroundColor = isSelected ? rec.gizmoColor : Color.white;
                    if (GUILayout.Button(rec.displayName, GUILayout.Height(26)))
                    {
                        ResetOptionsForKind(rec.kind);
                    }
                    GUI.backgroundColor = Color.white;
                }
                EditorGUILayout.EndHorizontal();
            }

            var activeDesc = PizzeriaStationRecipeRegistry.GetDescriptor(_selectedStationKind);
            if (_currentOptions == null || _currentOptions.kind != _selectedStationKind)
            {
                ResetOptionsForKind(_selectedStationKind);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("helpbox");
            EditorGUILayout.LabelField($"Parâmetros: {activeDesc.displayName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(activeDesc.shortDescription, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            _currentOptions.customDisplayName = EditorGUILayout.TextField("Nome na UI", _currentOptions.customDisplayName);
            if (_selectedStationKind != PizzeriaStationKind.TicketBoard)
            {
                _currentOptions.slotCount = Mathf.Clamp(EditorGUILayout.IntField("Quantidade de Slots", _currentOptions.slotCount), 1, 12);
            }
            _currentOptions.operationalForwardLocal = EditorGUILayout.Vector3Field("Frente Operacional (Local)", _currentOptions.operationalForwardLocal);
            _currentOptions.maxInteractionDistance = EditorGUILayout.FloatField("Alcance Máx. Interação (m)", _currentOptions.maxInteractionDistance);

            DrawSpecificOptionsForKind(_selectedStationKind);

            EditorGUILayout.Space(6);

            // Verificar se o objeto já tem outra função diferente
            bool hasDifferentExistingKind = val.existingStationOnPrimaryTarget != null &&
                                            val.existingStationKind != PizzeriaStationKind.None &&
                                            val.existingStationKind != _selectedStationKind;

            using (new EditorGUI.DisabledScope(!val.isValid))
            {
                if (!hasDifferentExistingKind)
                {
                    GUI.backgroundColor = new Color(0.45f, 0.95f, 0.55f);
                    if (GUILayout.Button(activeDesc.actionButtonLabel, GUILayout.Height(34)))
                    {
                        var stations = PizzeriaSelectionConverter.ConvertSelectionToStation(
                            val.editablePrefabRoot,
                            val.validTargets,
                            _currentOptions,
                            _settings,
                            allowReplaceDifferentKind: false,
                            out string msg);
                        SaveOpenPrefabStage(val.editablePrefabRoot);
                        SetBanner(msg, stations.Count > 0 ? MessageType.Info : MessageType.Warning);
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = Color.white;
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"O objeto selecionado já está configurado como '{val.existingStationKind}' (#{val.existingStationOnPrimaryTarget.stationId}). " +
                        $"Clique em 'Trocar função para {activeDesc.displayName}' para substituir os componentes anteriores sem destruir o modelo.",
                        MessageType.Warning);

                    GUI.backgroundColor = new Color(1f, 0.75f, 0.35f);
                    if (GUILayout.Button($"Trocar Função para {activeDesc.displayName}", GUILayout.Height(34)))
                    {
                        var stations = PizzeriaSelectionConverter.ConvertSelectionToStation(
                            val.editablePrefabRoot,
                            val.validTargets,
                            _currentOptions,
                            _settings,
                            allowReplaceDifferentKind: true,
                            out string msg);
                        SaveOpenPrefabStage(val.editablePrefabRoot);
                        SetBanner(msg, stations.Count > 0 ? MessageType.Info : MessageType.Warning);
                        SceneView.RepaintAll();
                    }
                    GUI.backgroundColor = Color.white;
                }
            }

            // Ações extras para estação já existente (Adicionar posto de trabalho / Remover função)
            if (val.isValid && val.existingStationOnPrimaryTarget != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("+ Adicionar Posto de Trabalho (Slot + Sub-Alvo)", GUILayout.Height(26)))
                {
                    PizzeriaSelectionConverter.AddWorkpostToStation(val.existingStationOnPrimaryTarget, out string msg);
                    SaveOpenPrefabStage(val.editablePrefabRoot);
                    SetBanner(msg, MessageType.Info);
                    SceneView.RepaintAll();
                }
                if (GUILayout.Button("Remover Função de Estação", GUILayout.Height(26), GUILayout.Width(180)))
                {
                    PizzeriaSelectionConverter.RemoveStationFunctionFromObject(
                        val.existingStationOnPrimaryTarget.gameObject,
                        val.editablePrefabRoot,
                        _settings);
                    SaveOpenPrefabStage(val.editablePrefabRoot);
                    SetBanner("Função de estação removida do objeto.", MessageType.Info);
                    SceneView.RepaintAll();
                }
                EditorGUILayout.EndHorizontal();
            }

            // Slots vinculados a objetos físicos (pratos etc.)
            {
                var bindStation = PizzeriaSelectionConverter.ResolveStationForSlotBinding(Selection.gameObjects, out var bindObjs);
                if (bindStation != null)
                {
                    EditorGUILayout.Space(4);
                    EditorGUILayout.HelpBox(
                        $"Slots por objeto físico: '{bindStation.name}' + {bindObjs.Count} objeto(s) selecionado(s).\n" +
                        "Cada objeto (ex: prato) vira um slot: o item fica em cima dele, e o alcance de interação e o outline partem dele.",
                        MessageType.None);
                    using (new EditorGUI.DisabledScope(bindObjs.Count == 0))
                    {
                        EditorGUILayout.BeginHorizontal();
                        if (GUILayout.Button("Vincular Slots aos Objetos (substituir)", GUILayout.Height(26)))
                        {
                            PizzeriaSelectionConverter.BindSlotsToPhysicalObjects(bindStation, bindObjs, true, out string msg);
                            SaveOpenPrefabStage(bindStation.transform.root.gameObject);
                            SetBanner(msg, MessageType.Info);
                            SceneView.RepaintAll();
                        }
                        if (GUILayout.Button("Adicionar como Slots Extras", GUILayout.Height(26), GUILayout.Width(180)))
                        {
                            PizzeriaSelectionConverter.BindSlotsToPhysicalObjects(bindStation, bindObjs, false, out string msg);
                            SaveOpenPrefabStage(bindStation.transform.root.gameObject);
                            SetBanner(msg, MessageType.Info);
                            SceneView.RepaintAll();
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            // Seção de Itens e Apresentação Visual (Faca, Tábua, Forma, Caixa, Mochila, Decoração)
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Itens Soltos e Apresentação Visual (Utensílios, Formas, Caixas, Mochilas)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Para objetos como faca, tábua, cortador, forma, caixa ou deliverybag selecionados no prefab: " +
                "marque como somente decorativo ou extraia um prefab visual vinculado ao ItemPresentationCatalog / BackpackVisualizer.",
                MessageType.None);

            _selectedVisualRole = (PizzeriaVisualRole)EditorGUILayout.EnumPopup("Papel Visual", _selectedVisualRole);
            if (_selectedVisualRole == PizzeriaVisualRole.UtensilVisual || _selectedVisualRole == PizzeriaVisualRole.IngredientVisual)
            {
                _selectedVisualDefinitionId = EditorGUILayout.IntField("Definition ID (ex: 1=Ralador, 2=Faca)", _selectedVisualDefinitionId);
            }

            bool canBindVisual = val.isValid && val.validTargets.Count == 1;
            using (new EditorGUI.DisabledScope(!canBindVisual))
            {
                string visBtn = _selectedVisualRole == PizzeriaVisualRole.DecorationOnly
                    ? "Marcar Objeto Selecionado como Somente Decorativo"
                    : $"Extrair Prefab Visual e Vincular ({_selectedVisualRole})";

                if (GUILayout.Button(visBtn, GUILayout.Height(28)))
                {
                    PizzeriaSelectionConverter.ExtractVisualAndBindRole(
                        val.editablePrefabRoot,
                        val.validTargets[0],
                        _selectedVisualRole,
                        _selectedVisualDefinitionId,
                        _settings,
                        out string msg);
                    SaveOpenPrefabStage(val.editablePrefabRoot);
                    SetBanner(msg, MessageType.Info);
                }
            }
            if (!canBindVisual)
            {
                EditorGUILayout.HelpBox("Selecione exatamente 1 objeto interno do prefab aberto para configurar seu papel visual.", MessageType.None);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawSpecificOptionsForKind(PizzeriaStationKind kind)
        {
            switch (kind)
            {
                case PizzeriaStationKind.Prep:
                    _currentOptions.prepPreset = (PizzeriaPrepPreset)EditorGUILayout.EnumPopup("Preset de Processos", _currentOptions.prepPreset);
                    _currentOptions.defaultProcessId = EditorGUILayout.IntField("Processo Padrão (ID)", _currentOptions.defaultProcessId);
                    if (_currentOptions.prepPreset == PizzeriaPrepPreset.Custom)
                    {
                        string csv = string.Join(",", _currentOptions.customAcceptedProcessIds);
                        string newCsv = EditorGUILayout.TextField("IDs de Processos (ex: 1,2,3)", csv);
                        if (newCsv != csv)
                        {
                            _currentOptions.customAcceptedProcessIds = ParseIntCsv(newCsv);
                        }
                    }
                    break;

                case PizzeriaStationKind.Pantry:
                case PizzeriaStationKind.Fridge:
                    _currentOptions.storagePreset = (PizzeriaStoragePreset)EditorGUILayout.EnumPopup("Preset de Estoque", _currentOptions.storagePreset);
                    _currentOptions.initialStockPerIngredient = EditorGUILayout.IntField("Estoque Inicial por Item", _currentOptions.initialStockPerIngredient);
                    _currentOptions.capacityPerIngredient = EditorGUILayout.IntField("Capacidade Máx. por Item", _currentOptions.capacityPerIngredient);

                    string ingCsv = string.Join(",", _currentOptions.storageIngredientIds);
                    string newIngCsv = EditorGUILayout.TextField("IDs de Ingredientes (1=Massa,3=Molho,4=Queijo,6=Calabresa,8=Cogumelo)", ingCsv);
                    if (newIngCsv != ingCsv)
                    {
                        _currentOptions.storageIngredientIds = ParseIntCsv(newIngCsv);
                    }
                    break;

                case PizzeriaStationKind.Oven:
                    _currentOptions.defaultBakeDuration = EditorGUILayout.FloatField("Tempo de Assar (s)", _currentOptions.defaultBakeDuration);
                    _currentOptions.burnGraceDuration = EditorGUILayout.FloatField("Tolerância antes de Queimar (s)", _currentOptions.burnGraceDuration);
                    break;

                case PizzeriaStationKind.TicketBoard:
                    _currentOptions.ticketSlotCount = Mathf.Clamp(EditorGUILayout.IntField("Slots de Comanda", _currentOptions.ticketSlotCount), 1, 12);
                    break;

                case PizzeriaStationKind.BoxStack:
                    _currentOptions.stackId = (ulong)Mathf.Max(1, EditorGUILayout.IntField("Stack ID Único", (int)_currentOptions.stackId));
                    _currentOptions.stackMaxCapacity = Mathf.Max(1, EditorGUILayout.IntField("Capacidade Máx. da Pilha", _currentOptions.stackMaxCapacity));
                    break;
            }
        }

        private static List<int> ParseIntCsv(string csv)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(csv)) return list;
            foreach (var part in csv.Split(','))
            {
                if (int.TryParse(part.Trim(), out int val) && val > 0)
                    list.Add(val);
            }
            return list;
        }

        // =====================================================================
        // ABA 4: VALIDAÇÃO E INTEGRAÇÃO COM O JOGO
        // =====================================================================
        private void DrawTab4ValidationAndIntegration()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Etapa 4 — Validador de Cobertura e Ciclo de Gameplay (Somente Leitura)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _settings.validationProfile = (PizzeriaValidationProfileMode)EditorGUILayout.EnumPopup("Perfil de Validação", _settings.validationProfile);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_settings);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("Validar Ciclo de Gameplay Agora", GUILayout.Height(34)))
            {
                RunReadOnlyValidation();
            }

            if (_lastValidationReport != null)
            {
                EditorGUILayout.Space(6);
                MessageType summaryType = _lastValidationReport.isPrefabReadyForGameplay ? MessageType.Info : MessageType.Error;
                string readyText = _lastValidationReport.isPrefabReadyForGameplay
                    ? "APROVADO: O prefab possui cobertura completa e ciclo de gameplay válido!"
                    : $"PENDENTE: {_lastValidationReport.blockingErrorsCount} erro(s) impeditivo(s), {_lastValidationReport.warningsCount} aviso(s).";
                EditorGUILayout.HelpBox(readyText, summaryType);

                // A. Tabela de Cobertura Mínima por Tipo
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("A. Cobertura Mínima por Tipo (15 Tipos):", EditorStyles.boldLabel);
                foreach (var row in _lastValidationReport.coverageTable)
                {
                    EditorGUILayout.BeginHorizontal("helpbox");
                    string icon = row.status == PizzeriaValidationSeverity.Ok ? "✓" : (row.status == PizzeriaValidationSeverity.BlockingError ? "✗" : "⚠");
                    EditorGUILayout.LabelField($"{icon} {row.displayName}", GUILayout.Width(210));
                    EditorGUILayout.LabelField($"Total: {row.totalFound} | Válidos: {row.validCount} | Mín: {row.minimumRequired}", GUILayout.Width(190));
                    EditorGUILayout.LabelField(row.statusDetail, EditorStyles.miniLabel);

                    if (row.instances.Count > 0)
                    {
                        if (GUILayout.Button("Selecionar", EditorStyles.miniButton, GUILayout.Width(75)))
                        {
                            Selection.activeGameObject = row.instances[0].gameObject;
                            EditorGUIUtility.PingObject(row.instances[0].gameObject);
                        }
                    }
                    else if (row.status == PizzeriaValidationSeverity.BlockingError)
                    {
                        if (GUILayout.Button("Ir para Tipo", EditorStyles.miniButton, GUILayout.Width(80)))
                        {
                            ResetOptionsForKind(row.kind);
                            _selectedTabIndex = 2;
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }

                // B & C. Problemas do Prefab e do Ciclo
                DrawIssuesSection("B & C. Diagnóstico do Prefab e Ciclo Produtivo:", _lastValidationReport.prefabIssues.Concat(_lastValidationReport.cycleIssues).ToList());

                // Dependências externas de cena
                DrawIssuesSection("Dependências de Cena / Rede (Separadas do Prefab):", _lastValidationReport.sceneDependencyIssues);
            }

            EditorGUILayout.EndVertical();

            // Seção Explícita: Usar esta pizzaria no jogo
            EditorGUILayout.Space(8);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Integração com o Jogo e Cidade", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Atualiza explicitamente CityGenerator.pizzeriaPrefab, PizzeriaSpawner e registra o prefab em DefaultNetworkPrefabs.asset, " +
                "mesmo quando as referências anteriores já estavam preenchidas.",
                MessageType.Info);

            bool canIntegrate = _settings.targetPrefab != null;
            using (new EditorGUI.DisabledScope(!canIntegrate))
            {
                GUI.backgroundColor = new Color(0.35f, 0.85f, 1f);
                if (GUILayout.Button("Usar Esta Pizzaria no Jogo", GUILayout.Height(34)))
                {
                    GameObject openRoot = PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(_settings.targetPrefab);
                    if (openRoot != null)
                    {
                        SaveOpenPrefabStage(openRoot);
                    }
                    _lastIntegrationMessages = PizzeriaGameplayValidator.ApplyPrefabToGameIntegration(_settings.targetPrefab, updateGameSceneIfClosed: true);
                    SetBanner($"Pizzaria '{_settings.targetPrefab.name}' integrada ao jogo ({_lastIntegrationMessages.Count} atualização(ões)).", MessageType.Info);
                }
                GUI.backgroundColor = Color.white;
            }

            if (_lastIntegrationMessages != null && _lastIntegrationMessages.Count > 0)
            {
                EditorGUILayout.Space(4);
                foreach (var m in _lastIntegrationMessages)
                {
                    EditorGUILayout.LabelField($"• {m}", EditorStyles.wordWrappedMiniLabel);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawIssuesSection(string header, List<PizzeriaValidationIssue> issues)
        {
            if (issues == null || issues.Count == 0) return;
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(header, EditorStyles.boldLabel);

            foreach (var issue in issues)
            {
                EditorGUILayout.BeginVertical("helpbox");
                EditorGUILayout.BeginHorizontal();
                string prefix = issue.severity == PizzeriaValidationSeverity.BlockingError ? "[ERRO IMPEDITIVO]" :
                               (issue.severity == PizzeriaValidationSeverity.Warning ? "[AVISO]" :
                               (issue.severity == PizzeriaValidationSeverity.NotVerifiedInEditMode ? "[NÃO VERIFICADO EM EDIÇÃO]" : "[OK]"));

                EditorGUILayout.LabelField($"{prefix} [{issue.category}] {issue.title}", EditorStyles.miniBoldLabel);

                if (issue.targetObject != null)
                {
                    if (GUILayout.Button("Selecionar Objeto", EditorStyles.miniButton, GUILayout.Width(115)))
                    {
                        Selection.activeObject = issue.targetObject;
                        EditorGUIUtility.PingObject(issue.targetObject);
                    }
                }
                if (issue.relatedStationKind != PizzeriaStationKind.None && issue.severity == PizzeriaValidationSeverity.BlockingError)
                {
                    if (GUILayout.Button("Ir para Tipo", EditorStyles.miniButton, GUILayout.Width(85)))
                    {
                        ResetOptionsForKind(issue.relatedStationKind);
                        _selectedTabIndex = 2;
                    }
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField(issue.description, EditorStyles.wordWrappedMiniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        private void RunReadOnlyValidation()
        {
            GameObject root = _settings != null
                ? (PizzeriaPrefabAuthoringService.GetOpenPrefabStageRoot(_settings.targetPrefab) ?? _settings.targetPrefab)
                : null;
            _lastValidationReport = PizzeriaGameplayValidator.ValidatePrefabAndCycle(root, _settings, alsoValidateActiveSceneDependencies: true);
        }

        private void SaveOpenPrefabStage(GameObject editableRoot)
        {
            if (editableRoot == null) return;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot == editableRoot)
            {
                int removed = PizzeriaPrefabAuthoringService.RemoveMissingScriptsRecursively(editableRoot, ensureNodeIdentities: true);
                if (removed > 0)
                {
                    Debug.Log($"[PizzeriaBuilder] {removed} componente(s) com Missing Script removido(s) automaticamente antes de salvar '{stage.assetPath}'.");
                }
                EditorSceneManager.MarkSceneDirty(stage.scene);
                PrefabUtility.SaveAsPrefabAsset(editableRoot, stage.assetPath);
            }
        }

        // =====================================================================
        // GIZMOS E HANDLES NA SCENE VIEW (SLOTS, FRENTE OPERACIONAL, ALCANCE, ÂNCORAS)
        // =====================================================================
        private void OnDuringSceneGui(SceneView sceneView)
        {
            if (_settings == null || !_settings.showSceneGizmos) return;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null) return;

            GameObject root = stage.prefabContentsRoot;
            var bindings = root.GetComponent<PizzeriaAuthoringBindings>();
            var pRoot = root.GetComponent<PizzeriaRoot>();
            var footprint = root.GetComponent<PizzeriaFootprint>();

            // 1. Desenhar Footprint e Âncoras quando estiver na Aba 2
            if (_selectedTabIndex == 1 && footprint != null)
            {
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Vector3 center = root.transform.TransformPoint(new Vector3(footprint.footprintCenter.x, 0f, footprint.footprintCenter.y));
                Handles.DrawWireCube(center + Vector3.up * 0.1f, new Vector3(footprint.footprintSize.x, 0.2f, footprint.footprintSize.y));
            }

            if (pRoot != null && (_selectedTabIndex == 1 || _selectedTabIndex == 3))
            {
                DrawAnchorHandle(pRoot.entranceAnchor, "EntranceAnchor", Color.cyan);
                DrawAnchorHandle(pRoot.receivingAnchor, "ReceivingAnchor", Color.yellow);
                DrawAnchorHandle(pRoot.recoveryAnchor, "RecoveryAnchor", Color.magenta);
                if (pRoot.playerSpawnAnchors != null)
                {
                    for (int i = 0; i < pRoot.playerSpawnAnchors.Length; i++)
                    {
                        DrawAnchorHandle(pRoot.playerSpawnAnchors[i], $"Spawn_{i}", Color.green);
                    }
                }
            }

            // 2. Desenhar Gizmos e PositionHandles dos Slots das Estações
            var stations = root.GetComponentsInChildren<StationView>(true);
            for (int i = 0; i < stations.Length; i++)
            {
                var st = stations[i];
                if (st == null || !st.gameObject.activeInHierarchy) continue;

                bool isSelectedStation = Selection.activeGameObject != null &&
                    (Selection.activeGameObject == st.gameObject || Selection.activeGameObject.transform.IsChildOf(st.transform));

                PizzeriaStationKind kind = bindings != null ? bindings.GetStationKind(st) : PizzeriaStationKind.Counter;
                var desc = PizzeriaStationRecipeRegistry.GetDescriptor(kind);

                Handles.color = desc.gizmoColor;
                Bounds b = PizzeriaHierarchyOrganizer.ComputeWorldBounds(st.transform, out bool hasMesh);
                Vector3 labelPos = (hasMesh ? b.center : st.transform.position) + Vector3.up * (hasMesh ? b.extents.y + 0.35f : 1.2f);
                Handles.Label(labelPos, $"[{st.stationId}] {st.stationName} ({kind})", EditorStyles.whiteBoldLabel);

                // Seta de frente operacional
                var rec = bindings != null ? bindings.FindRecordByStation(st) : null;
                Vector3 fwdLocal = rec != null && rec.operationalForwardLocal.sqrMagnitude > 0.001f ? rec.operationalForwardLocal.normalized : Vector3.forward;
                Vector3 fwdWorld = st.transform.TransformDirection(fwdLocal);
                Vector3 origin = hasMesh ? new Vector3(b.center.x, b.min.y + 0.9f, b.center.z) : st.transform.position + Vector3.up * 0.9f;
                Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(fwdWorld, Vector3.up), 0.65f, EventType.Repaint);

                if (st.slotAnchors != null)
                {
                    for (int s = 0; s < st.slotAnchors.Length; s++)
                    {
                        Transform slotT = st.slotAnchors[s];
                        if (slotT == null) continue;

                        Handles.color = isSelectedStation ? Color.yellow : desc.gizmoColor;
                        Handles.SphereHandleCap(0, slotT.position, slotT.rotation, 0.12f, EventType.Repaint);
                        Handles.Label(slotT.position + Vector3.up * 0.15f, $"Slot_{s}", EditorStyles.miniBoldLabel);

                        if (isSelectedStation && _settings.showSlotHandles)
                        {
                            EditorGUI.BeginChangeCheck();
                            Vector3 newPos = Handles.PositionHandle(slotT.position, slotT.rotation);
                            if (EditorGUI.EndChangeCheck())
                            {
                                Undo.RecordObject(slotT, $"Mover Slot_{s}");
                                slotT.position = newPos;
                                EditorUtility.SetDirty(slotT);
                            }
                        }
                    }
                }

                if (isSelectedStation && _settings.showInteractionReachGizmos)
                {
                    float reach = rec != null && rec.maxInteractionDistance > 0f ? rec.maxInteractionDistance : 3.0f;
                    Handles.color = new Color(0.3f, 0.9f, 1f, 0.25f);
                    Handles.DrawWireDisc(origin, Vector3.up, reach);
                }
            }
        }

        private static void DrawAnchorHandle(Transform anchor, string label, Color color)
        {
            if (anchor == null) return;
            Handles.color = color;
            Handles.SphereHandleCap(0, anchor.position, anchor.rotation, 0.2f, EventType.Repaint);
            Handles.ArrowHandleCap(0, anchor.position, anchor.rotation, 0.55f, EventType.Repaint);
            Handles.Label(anchor.position + Vector3.up * 0.3f, label, EditorStyles.whiteBoldLabel);

            if (Selection.activeTransform == anchor)
            {
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(anchor.position, anchor.rotation);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(anchor, $"Mover {label}");
                    anchor.position = newPos;
                    EditorUtility.SetDirty(anchor);
                }
            }
        }
    }
}
