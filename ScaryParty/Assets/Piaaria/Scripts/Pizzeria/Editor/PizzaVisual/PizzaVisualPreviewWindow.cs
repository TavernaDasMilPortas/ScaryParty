using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor.PizzaVisual
{
    /// <summary>
    /// Pizza Studio 2.0 — Ferramenta de Autoria Visual & Live Preview 3D de Pizza Modular.
    /// Construída com UI Toolkit no Unity Editor, preservando o PreviewRenderUtility para alta performance 3D.
    /// Suporta 4 abas completas (Montagem, Modelo Original, Modelo Processado, Comparação),
    /// controle orbital/pan/zoom da câmera, enquadramento automático, diagnóstico em tempo real,
    /// ajuste progressivo de cozimento/queima e persistência com Undo/Redo.
    /// </summary>
    public class PizzaVisualPreviewWindow : EditorWindow
    {
        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Pizza Studio 2.0 (Prévia 3D Modular)", false, 12)]
        public static void ShowWindow()
        {
            var win = GetWindow<PizzaVisualPreviewWindow>("Pizza Studio 2.0");
            win.minSize = new Vector2(1000, 720);
            win.Show();
        }

        public static void NotifyProfileChanged()
        {
            if (HasOpenInstances<PizzaVisualPreviewWindow>())
            {
                var win = GetWindow<PizzaVisualPreviewWindow>(null, false);
                if (win != null)
                {
                    win.RebuildPizzaPreview();
                    win._viewport3DContainer?.MarkDirtyRepaint();
                    win.Repaint();
                }
            }
        }

        public enum StudioTab
        {
            Montagem,
            ModeloOriginal,
            ModeloProcessado,
            Comparacao
        }

        #region Preview Utility State
        private PreviewRenderUtility _previewUtility;
        private GameObject _previewPizzaRoot;
        private GameObject _previewItemA;
        private GameObject _previewItemB;

        private float _cameraDistance = 0.72f;
        private float _cameraPitch = 42f;
        private float _cameraYaw = 28f;
        private Vector3 _cameraTarget = new Vector3(0f, 0.015f, 0f);
        private bool _isPanning;
        private bool _isOrbiting;
        #endregion

        #region Studio State
        [SerializeField] private StudioTab _activeTab = StudioTab.Montagem;
        [SerializeField] [Range(0f, 1f)] public float CookProgress = 0f;
        [SerializeField] [Range(0f, 1f)] public float BurnProgress = 0f;
        [SerializeField] public int VisualSeed = 42;
        [SerializeField] public string IngredientFilter = "";

        [Serializable]
        public class PreviewIngredientEntry
        {
            public int IngredientDefId = 5;
            public string Name = "Ingrediente";
            public ProcessingStage Stage = ProcessingStage.Prepared;
            public int Count = 1;
        }

        [SerializeField]
        public List<PreviewIngredientEntry> ActiveIngredients = new List<PreviewIngredientEntry>
        {
            new PreviewIngredientEntry { IngredientDefId = 3, Stage = ProcessingStage.Prepared, Count = 1, Name = "Molho de Tomate" },
            new PreviewIngredientEntry { IngredientDefId = 5, Stage = ProcessingStage.Prepared, Count = 1, Name = "Queijo Ralado" },
            new PreviewIngredientEntry { IngredientDefId = 7, Stage = ProcessingStage.Prepared, Count = 1, Name = "Calabresa Fatiada" },
            new PreviewIngredientEntry { IngredientDefId = 9, Stage = ProcessingStage.Prepared, Count = 1, Name = "Cogumelo Fatiado" }
        };

        [SerializeField] public PizzaIngredientVisualProfile ActiveProfileForTuning;
        [SerializeField] public IngredientDef OriginalIngredient;
        [SerializeField] public IngredientDef ProcessedIngredient;

        // Diagnóstico
        private int _diagPieceCount;
        private int _diagVertexCount;
        private int _diagTriangleCount;
        private string _diagWarnings = "Tudo OK.";
        private bool _diagHasErrors;
        #endregion

        #region UI Toolkit Elements
        private VisualElement _sidebarContainer;
        private IMGUIContainer _viewport3DContainer;
        private ToolbarButton _tabBtnMontagem;
        private ToolbarButton _tabBtnOriginal;
        private ToolbarButton _tabBtnProcessado;
        private ToolbarButton _tabBtnComparacao;

        private Slider _cookSlider;
        private Slider _burnSlider;
        private IntegerField _seedField;
        private TextField _searchField;
        private VisualElement _ingredientListElement;
        private VisualElement _tuningProfileElement;
        private Label _metricsLabel;
        #endregion

        private void OnEnable()
        {
            Undo.undoRedoPerformed += OnUndoRedo;
            InitPreview();
            LoadDefaultProfiles();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnUndoRedo()
        {
            RebuildPizzaPreview();
            RefreshSidebarUI();
            _viewport3DContainer?.MarkDirtyRepaint();
            Repaint();
        }

        public void CreateGUI()
        {
            VisualElement root = rootVisualElement;
            root.style.flexDirection = FlexDirection.Column;
            root.style.backgroundColor = new Color(0.14f, 0.14f, 0.15f);

            // 1. Top Bar / Tabs
            var topBar = BuildTopToolbar();
            root.Add(topBar);

            // 2. Main Resizable Split View (Controls + 3D Viewport)
            var splitView = new TwoPaneSplitView(0, 420, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            _sidebarContainer = new VisualElement();
            _sidebarContainer.style.flexGrow = 1;
            _sidebarContainer.style.backgroundColor = new Color(0.18f, 0.18f, 0.19f);
            _sidebarContainer.style.borderRightWidth = 1;
            _sidebarContainer.style.borderRightColor = new Color(0.11f, 0.11f, 0.12f);

            var rightContainer = BuildViewportContainer();

            splitView.Add(_sidebarContainer);
            splitView.Add(rightContainer);
            root.Add(splitView);

            SwitchTab(_activeTab);
            RebuildPizzaPreview();
        }

        #region Top Toolbar & Tabs
        private VisualElement BuildTopToolbar()
        {
            var bar = new Toolbar();
            bar.style.height = 36;
            bar.style.backgroundColor = new Color(0.12f, 0.12f, 0.13f);
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = new Color(0.22f, 0.22f, 0.24f);
            bar.style.paddingLeft = 8;
            bar.style.paddingRight = 8;
            bar.style.alignItems = Align.Center;

            var titleLabel = new Label("🍕 PIZZA STUDIO 2.0");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.fontSize = 13;
            titleLabel.style.color = new Color(0.95f, 0.65f, 0.25f);
            titleLabel.style.marginRight = 16;
            bar.Add(titleLabel);

            _tabBtnMontagem = new ToolbarButton(() => SwitchTab(StudioTab.Montagem)) { text = "  Montagem  " };
            _tabBtnOriginal = new ToolbarButton(() => SwitchTab(StudioTab.ModeloOriginal)) { text = "  Modelo Original  " };
            _tabBtnProcessado = new ToolbarButton(() => SwitchTab(StudioTab.ModeloProcessado)) { text = "  Modelo Processado  " };
            _tabBtnComparacao = new ToolbarButton(() => SwitchTab(StudioTab.Comparacao)) { text = "  Comparação  " };

            StyleTabButton(_tabBtnMontagem);
            StyleTabButton(_tabBtnOriginal);
            StyleTabButton(_tabBtnProcessado);
            StyleTabButton(_tabBtnComparacao);

            bar.Add(_tabBtnMontagem);
            bar.Add(_tabBtnOriginal);
            bar.Add(_tabBtnProcessado);
            bar.Add(_tabBtnComparacao);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1;
            bar.Add(spacer);

            var reseedBtn = new ToolbarButton(() =>
            {
                Undo.RecordObject(this, "Randomize Seed");
                VisualSeed = UnityEngine.Random.Range(100, 99999);
                if (_seedField != null) _seedField.value = VisualSeed;
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            })
            { text = "🎲 Randomizar Seed" };
            bar.Add(reseedBtn);

            var frameBtn = new ToolbarButton(FramePizza) { text = "🎯 Enquadrar (F)" };
            bar.Add(frameBtn);

            return bar;
        }

        private static void StyleTabButton(ToolbarButton btn)
        {
            btn.style.height = 26;
            btn.style.fontSize = 12;
            btn.style.marginLeft = 2;
            btn.style.marginRight = 2;
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
        }

        private void SwitchTab(StudioTab tab)
        {
            _activeTab = tab;
            HighlightActiveTab();
            RefreshSidebarUI();
            RebuildActiveTabModels();
            _viewport3DContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void HighlightActiveTab()
        {
            Color activeColor = new Color(0.28f, 0.52f, 0.85f);
            Color defaultColor = Color.clear;

            if (_tabBtnMontagem != null) _tabBtnMontagem.style.backgroundColor = (_activeTab == StudioTab.Montagem) ? activeColor : defaultColor;
            if (_tabBtnOriginal != null) _tabBtnOriginal.style.backgroundColor = (_activeTab == StudioTab.ModeloOriginal) ? activeColor : defaultColor;
            if (_tabBtnProcessado != null) _tabBtnProcessado.style.backgroundColor = (_activeTab == StudioTab.ModeloProcessado) ? activeColor : defaultColor;
            if (_tabBtnComparacao != null) _tabBtnComparacao.style.backgroundColor = (_activeTab == StudioTab.Comparacao) ? activeColor : defaultColor;
        }
        #endregion

        #region Right Viewport & Camera Toolbar
        private VisualElement BuildViewportContainer()
        {
            var container = new VisualElement();
            container.style.flexGrow = 1;
            container.style.flexDirection = FlexDirection.Column;

            // Viewport Header Controls
            var viewHeader = new Toolbar();
            viewHeader.style.height = 28;
            viewHeader.style.backgroundColor = new Color(0.13f, 0.13f, 0.14f);

            viewHeader.Add(new Label(" Vista: ") { style = { alignSelf = Align.Center, color = Color.gray } });
            viewHeader.Add(new ToolbarButton(() => SetCameraPreset(89.5f, 0f, 0.65f)) { text = "Top (Cima)" });
            viewHeader.Add(new ToolbarButton(() => SetCameraPreset(5f, 0f, 0.65f)) { text = "Side (Lado)" });
            viewHeader.Add(new ToolbarButton(() => SetCameraPreset(45f, 45f, 0.72f)) { text = "Iso 45°" });
            viewHeader.Add(new ToolbarButton(() => SetCameraPreset(20f, 0f, 0.68f)) { text = "Front 20°" });

            var sp = new ToolbarSpacer();
            sp.style.flexGrow = 1;
            viewHeader.Add(sp);

            viewHeader.Add(new ToolbarButton(() =>
            {
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
                ShowNotification(new GUIContent("Layout Regenerado!"));
            })
            { text = "↺ Regenerar" });

            container.Add(viewHeader);

            // Viewport IMGUI
            _viewport3DContainer = new IMGUIContainer(OnDrawViewport)
            {
                style = { flexGrow = 1 }
            };
            container.Add(_viewport3DContainer);

            return container;
        }

        private void SetCameraPreset(float pitch, float yaw, float dist)
        {
            _cameraPitch = pitch;
            _cameraYaw = yaw;
            _cameraDistance = dist;
            _cameraTarget = new Vector3(0f, 0.015f, 0f);
            _viewport3DContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void FramePizza()
        {
            _cameraTarget = new Vector3(0f, 0.015f, 0f);
            _cameraDistance = (_activeTab == StudioTab.Montagem) ? 0.72f : 0.40f;
            _cameraPitch = 42f;
            _cameraYaw = 28f;
            _viewport3DContainer?.MarkDirtyRepaint();
            Repaint();
        }
        #endregion

        #region Sidebar UI Construction
        private void RefreshSidebarUI()
        {
            if (_sidebarContainer == null) return;
            _sidebarContainer.Clear();

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            scroll.style.paddingLeft = 12;
            scroll.style.paddingRight = 12;
            scroll.style.paddingTop = 10;
            scroll.style.paddingBottom = 20;

            switch (_activeTab)
            {
                case StudioTab.Montagem:
                    BuildMontagemSidebar(scroll);
                    break;
                case StudioTab.ModeloOriginal:
                    BuildOriginalSidebar(scroll);
                    break;
                case StudioTab.ModeloProcessado:
                    BuildProcessadoSidebar(scroll);
                    break;
                case StudioTab.Comparacao:
                    BuildComparacaoSidebar(scroll);
                    break;
            }

            _sidebarContainer.Add(scroll);
        }

        private void BuildMontagemSidebar(ScrollView scroll)
        {
            // Card 1: Cozimento & Semente
            var cardBake = CreateCard("🔥 Cozimento & Semente");

            _cookSlider = new Slider("Assado (Cook)", 0f, 1f) { value = CookProgress };
            _cookSlider.RegisterValueChangedCallback(evt =>
            {
                CookProgress = evt.newValue;
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            cardBake.Add(_cookSlider);

            _burnSlider = new Slider("Queimado (Burn)", 0f, 1f) { value = BurnProgress };
            _burnSlider.RegisterValueChangedCallback(evt =>
            {
                BurnProgress = evt.newValue;
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            cardBake.Add(_burnSlider);

            _seedField = new IntegerField("Semente Visual") { value = VisualSeed };
            _seedField.RegisterValueChangedCallback(evt =>
            {
                VisualSeed = evt.newValue;
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            cardBake.Add(_seedField);

            scroll.Add(cardBake);

            // Card 2: Ingredientes Ativos
            var cardIngs = CreateCard("🍕 Ingredientes Ativos");

            _searchField = new TextField("Filtrar") { value = IngredientFilter };
            _searchField.RegisterValueChangedCallback(evt =>
            {
                IngredientFilter = evt.newValue.Trim().ToLowerInvariant();
                RefreshIngredientCards();
            });
            cardIngs.Add(_searchField);

            _ingredientListElement = new VisualElement();
            cardIngs.Add(_ingredientListElement);
            RefreshIngredientCards();

            var addBtn = new Button(AddNewIngredient) { text = "➕ Adicionar Ingrediente à Pizza" };
            addBtn.style.marginTop = 8;
            addBtn.style.height = 28;
            cardIngs.Add(addBtn);

            scroll.Add(cardIngs);

            // Card 3: Perfil Visual em Edição (Tuning)
            var cardProfile = CreateCard("🎨 Perfil Visual (Tuning em Tempo Real)");
            _tuningProfileElement = new VisualElement();
            cardProfile.Add(_tuningProfileElement);
            RefreshProfileTuningUI();
            scroll.Add(cardProfile);

            // Card 4: Diagnóstico & Métricas
            var cardDiag = CreateCard("📊 Diagnóstico do Layout & Performance");
            _metricsLabel = new Label();
            _metricsLabel.style.whiteSpace = WhiteSpace.Normal;
            _metricsLabel.style.fontSize = 11;
            _metricsLabel.style.color = new Color(0.85f, 0.85f, 0.85f);
            cardDiag.Add(_metricsLabel);
            UpdateMetricsLabel();
            scroll.Add(cardDiag);
        }

        private struct AvailableIngredientOption
        {
            public int Id;
            public string Name;
            public ProcessingStage Stage;
            public string Label => $"{Name} (#{Id})";
        }

        private static readonly List<AvailableIngredientOption> s_standardIngredients = new List<AvailableIngredientOption>
        {
            new AvailableIngredientOption { Id = 3, Name = "Molho de Tomate", Stage = ProcessingStage.Prepared },
            new AvailableIngredientOption { Id = 5, Name = "Queijo Ralado", Stage = ProcessingStage.Prepared },
            new AvailableIngredientOption { Id = 7, Name = "Calabresa Fatiada", Stage = ProcessingStage.Prepared },
            new AvailableIngredientOption { Id = 9, Name = "Cogumelo Fatiado", Stage = ProcessingStage.Prepared },
            new AvailableIngredientOption { Id = 11, Name = "Manjericão Fresco", Stage = ProcessingStage.Prepared },
            new AvailableIngredientOption { Id = 4, Name = "Queijo Inteiro", Stage = ProcessingStage.Raw },
            new AvailableIngredientOption { Id = 6, Name = "Calabresa Inteira", Stage = ProcessingStage.Raw },
            new AvailableIngredientOption { Id = 8, Name = "Cogumelo Inteiro", Stage = ProcessingStage.Raw }
        };

        private List<AvailableIngredientOption> GetAvailableIngredientOptions()
        {
            var list = new List<AvailableIngredientOption>(s_standardIngredients);
            try
            {
                // 1. Escanear todos os IngredientDefs do projeto
                var guids = AssetDatabase.FindAssets("t:IngredientDef");
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    var def = AssetDatabase.LoadAssetAtPath<IngredientDef>(path);
                    if (def != null && def.id > 0 && !list.Exists(x => x.Id == def.id))
                    {
                        list.Add(new AvailableIngredientOption
                        {
                            Id = def.id,
                            Name = !string.IsNullOrEmpty(def.ingredientName) ? def.ingredientName : def.name,
                            Stage = def.requiresPrep ? ProcessingStage.Raw : ProcessingStage.Prepared
                        });
                    }
                }

                // 2. Escanear todos os perfis visuais de pizza
                var profGuids = AssetDatabase.FindAssets("t:PizzaIngredientVisualProfile");
                for (int i = 0; i < profGuids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(profGuids[i]);
                    var prof = AssetDatabase.LoadAssetAtPath<PizzaIngredientVisualProfile>(path);
                    if (prof != null && prof.ingredientId > 0 && !list.Exists(x => x.Id == prof.ingredientId))
                    {
                        list.Add(new AvailableIngredientOption
                        {
                            Id = prof.ingredientId,
                            Name = !string.IsNullOrEmpty(prof.displayName) ? prof.displayName : prof.name,
                            Stage = prof.stage
                        });
                    }
                }

                // Ordenação determinística por ID
                list.Sort((a, b) => a.Id.CompareTo(b.Id));
            }
            catch {}
            return list;
        }

        private void RefreshIngredientCards()
        {
            if (_ingredientListElement == null) return;
            _ingredientListElement.Clear();

            var options = GetAvailableIngredientOptions();
            var choiceLabels = new List<string>();
            for (int o = 0; o < options.Count; o++) choiceLabels.Add(options[o].Label);

            for (int i = 0; i < ActiveIngredients.Count; i++)
            {
                int idx = i;
                var ing = ActiveIngredients[i];

                if (!string.IsNullOrEmpty(IngredientFilter))
                {
                    if (!ing.Name.ToLowerInvariant().Contains(IngredientFilter) && !ing.IngredientDefId.ToString().Contains(IngredientFilter))
                        continue;
                }

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.backgroundColor = new Color(0.22f, 0.22f, 0.24f);
                row.style.marginTop = 4;
                row.style.paddingLeft = 6;
                row.style.paddingRight = 6;
                row.style.paddingTop = 4;
                row.style.paddingBottom = 4;
                row.style.borderLeftWidth = 3;
                row.style.borderLeftColor = new Color(0.85f, 0.55f, 0.22f);
                row.style.borderTopRightRadius = 4;
                row.style.borderBottomRightRadius = 4;

                var idBadge = new Label($"#{ing.IngredientDefId}");
                idBadge.style.fontSize = 10;
                idBadge.style.color = new Color(0.6f, 0.8f, 1f);
                idBadge.style.width = 30;
                row.Add(idBadge);

                // Dropdown para seleção direta do ingrediente
                int selectedIndex = options.FindIndex(x => x.Id == ing.IngredientDefId);
                if (selectedIndex < 0)
                {
                    var customOpt = new AvailableIngredientOption { Id = ing.IngredientDefId, Name = ing.Name, Stage = ing.Stage };
                    options.Add(customOpt);
                    choiceLabels.Add(customOpt.Label);
                    selectedIndex = options.Count - 1;
                }

                string currentLabel = choiceLabels[selectedIndex];
                var dropdown = new DropdownField(choiceLabels, currentLabel);
                dropdown.style.flexGrow = 1;
                dropdown.style.marginLeft = 4;
                dropdown.style.marginRight = 4;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int chosenIdx = choiceLabels.IndexOf(evt.newValue);
                    if (chosenIdx >= 0 && chosenIdx < options.Count)
                    {
                        var chosen = options[chosenIdx];
                        ing.IngredientDefId = chosen.Id;
                        ing.Name = chosen.Name;
                        ing.Stage = chosen.Stage;
                        idBadge.text = $"#{chosen.Id}";
                        RebuildPizzaPreview();
                        _viewport3DContainer?.MarkDirtyRepaint();
                    }
                });
                row.Add(dropdown);

                var minusBtn = new Button(() =>
                {
                    if (ing.Count > 1)
                    {
                        ing.Count--;
                        RebuildPizzaPreview();
                        RefreshIngredientCards();
                        _viewport3DContainer?.MarkDirtyRepaint();
                    }
                })
                { text = "-" };
                minusBtn.style.width = 22;
                row.Add(minusBtn);

                var countLabel = new Label(ing.Count.ToString()) { style = { width = 20, unityTextAlign = TextAnchor.MiddleCenter } };
                row.Add(countLabel);

                var plusBtn = new Button(() =>
                {
                    ing.Count++;
                    RebuildPizzaPreview();
                    RefreshIngredientCards();
                    _viewport3DContainer?.MarkDirtyRepaint();
                })
                { text = "+" };
                plusBtn.style.width = 22;
                row.Add(plusBtn);

                var tuneBtn = new Button(() =>
                {
                    SelectProfileForTuning(ing.IngredientDefId, ing.Stage);
                })
                { text = "⚙️" };
                tuneBtn.tooltip = "Ajustar perfil visual deste ingrediente";
                tuneBtn.style.width = 26;
                row.Add(tuneBtn);

                var delBtn = new Button(() =>
                {
                    ActiveIngredients.RemoveAt(idx);
                    RebuildPizzaPreview();
                    RefreshIngredientCards();
                    _viewport3DContainer?.MarkDirtyRepaint();
                })
                { text = "✕" };
                delBtn.style.width = 22;
                delBtn.style.color = new Color(1f, 0.4f, 0.4f);
                row.Add(delBtn);

                _ingredientListElement.Add(row);
            }
        }

        private void AddNewIngredient()
        {
            Undo.RecordObject(this, "Add Ingredient to Pizza");
            ActiveIngredients.Add(new PreviewIngredientEntry
            {
                IngredientDefId = 7,
                Name = "Calabresa Adicional",
                Stage = ProcessingStage.Prepared,
                Count = 1
            });
            RebuildPizzaPreview();
            RefreshIngredientCards();
            _viewport3DContainer?.MarkDirtyRepaint();
        }

        private void SelectProfileForTuning(int id, ProcessingStage stage)
        {
            var catalog = PizzaVisualCatalog.Instance;
            if (catalog != null)
            {
                ActiveProfileForTuning = catalog.GetProfile(id, stage);
                RefreshProfileTuningUI();
            }
        }

        private void RefreshProfileTuningUI()
        {
            if (_tuningProfileElement == null) return;
            _tuningProfileElement.Clear();

            if (ActiveProfileForTuning == null)
            {
                var lbl = new Label("Nenhum perfil selecionado. Clique no botão ⚙️ ao lado de um ingrediente.");
                lbl.style.color = Color.gray;
                lbl.style.whiteSpace = WhiteSpace.Normal;
                _tuningProfileElement.Add(lbl);
                return;
            }

            var prof = ActiveProfileForTuning;

            var title = new Label($"Editando: {prof.displayName} (#{prof.ingredientId})");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.9f, 0.7f, 0.3f);
            title.style.marginBottom = 6;
            _tuningProfileElement.Add(title);

            var modeField = new EnumField("Modo de Posicionamento", prof.placementMode);
            modeField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(prof, "Change Placement Mode");
                prof.placementMode = (PizzaPlacementMode)evt.newValue;
                EditorUtility.SetDirty(prof);
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            _tuningProfileElement.Add(modeField);

            var rawColorField = new ColorField("Cor Crua (Raw)") { value = prof.tintColor };
            rawColorField.RegisterValueChangedCallback(evt =>
            {
                prof.tintColor = evt.newValue;
                EditorUtility.SetDirty(prof);
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            _tuningProfileElement.Add(rawColorField);

            var bakedColorField = new ColorField("Cor Assada (Baked)") { value = prof.bakedTintColor };
            bakedColorField.RegisterValueChangedCallback(evt =>
            {
                prof.bakedTintColor = evt.newValue;
                EditorUtility.SetDirty(prof);
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            _tuningProfileElement.Add(bakedColorField);

            var burnedColorField = new ColorField("Cor Queimada (Burned)") { value = prof.burnedTintColor };
            burnedColorField.RegisterValueChangedCallback(evt =>
            {
                prof.burnedTintColor = evt.newValue;
                EditorUtility.SetDirty(prof);
                RebuildPizzaPreview();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            _tuningProfileElement.Add(burnedColorField);

            if (prof.placementMode == PizzaPlacementMode.Sliced)
            {
                var piecesSlider = new SliderInt("Peças por Porção", 1, 12) { value = prof.piecesPerPortion };
                piecesSlider.RegisterValueChangedCallback(evt =>
                {
                    prof.piecesPerPortion = evt.newValue;
                    EditorUtility.SetDirty(prof);
                    RebuildPizzaPreview();
                    _viewport3DContainer?.MarkDirtyRepaint();
                });
                _tuningProfileElement.Add(piecesSlider);

                var occSlider = new Slider("Raio de Ocupação", 0.01f, 0.15f) { value = prof.occupancyRadius };
                occSlider.RegisterValueChangedCallback(evt =>
                {
                    prof.occupancyRadius = evt.newValue;
                    EditorUtility.SetDirty(prof);
                    RebuildPizzaPreview();
                    _viewport3DContainer?.MarkDirtyRepaint();
                });
                _tuningProfileElement.Add(occSlider);

                var ySlider = new Slider("Altura Y", 0.005f, 0.05f) { value = prof.yOffset };
                ySlider.RegisterValueChangedCallback(evt =>
                {
                    prof.yOffset = evt.newValue;
                    EditorUtility.SetDirty(prof);
                    RebuildPizzaPreview();
                    _viewport3DContainer?.MarkDirtyRepaint();
                });
                _tuningProfileElement.Add(ySlider);
            }
            else if (prof.placementMode == PizzaPlacementMode.Total)
            {
                var hSlider = new Slider("Espessura Camada", 0.002f, 0.03f) { value = prof.layerHeight };
                hSlider.RegisterValueChangedCallback(evt =>
                {
                    prof.layerHeight = evt.newValue;
                    EditorUtility.SetDirty(prof);
                    RebuildPizzaPreview();
                    _viewport3DContainer?.MarkDirtyRepaint();
                });
                _tuningProfileElement.Add(hSlider);

                var scaleSlider = new Slider("Escala Cobertura", 0.20f, 0.45f) { value = prof.coatingScale };
                scaleSlider.RegisterValueChangedCallback(evt =>
                {
                    prof.coatingScale = evt.newValue;
                    EditorUtility.SetDirty(prof);
                    RebuildPizzaPreview();
                    _viewport3DContainer?.MarkDirtyRepaint();
                });
                _tuningProfileElement.Add(scaleSlider);
            }

            var saveBtn = new Button(() =>
            {
                EditorUtility.SetDirty(prof);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent($"Perfil '{prof.displayName}' salvo!"));
            })
            { text = "💾 Salvar Perfil no Disco" };
            saveBtn.style.marginTop = 8;
            saveBtn.style.height = 26;
            _tuningProfileElement.Add(saveBtn);
        }

        private void UpdateMetricsLabel()
        {
            if (_metricsLabel == null) return;
            string statusIcon = _diagHasErrors ? "⚠️" : "✅";
            _metricsLabel.text = $"{statusIcon} Peças Geradas: {_diagPieceCount}\n" +
                                 $"🔺 Vértices: {_diagVertexCount:N0} | Triângulos: {_diagTriangleCount:N0}\n" +
                                 $"📏 Diâmetro Base: 0.38 m (Cornicione Artesanal)\n" +
                                 $"🛡️ Status de Colisão & Bordas:\n{_diagWarnings}";
        }

        private void BuildOriginalSidebar(ScrollView scroll)
        {
            var card = CreateCard("📦 Inspeção de Ingrediente Bruto");

            var objField = new ObjectField("Ingrediente Bruto")
            {
                objectType = typeof(IngredientDef),
                value = OriginalIngredient
            };
            objField.RegisterValueChangedCallback(evt =>
            {
                OriginalIngredient = evt.newValue as IngredientDef;
                RebuildActiveTabModels();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            card.Add(objField);

            var info = new Label("Visualização anatômica da matéria-prima antes do processamento (ex: Salame Inteiro, Peça de Queijo, Tomate Fresco).");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.color = Color.gray;
            info.style.marginTop = 6;
            card.Add(info);

            scroll.Add(card);
        }

        private void BuildProcessadoSidebar(ScrollView scroll)
        {
            var card = CreateCard("✂️ Inspeção de Ingrediente Processado");

            var objField = new ObjectField("Ingrediente Processado")
            {
                objectType = typeof(IngredientDef),
                value = ProcessedIngredient
            };
            objField.RegisterValueChangedCallback(evt =>
            {
                ProcessedIngredient = evt.newValue as IngredientDef;
                RebuildActiveTabModels();
                _viewport3DContainer?.MarkDirtyRepaint();
            });
            card.Add(objField);

            var info = new Label("Visualização da unidade processada (Fatia de Calabresa, Fatias de Cogumelo, Fiapos de Queijo Ralado).");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.color = Color.gray;
            info.style.marginTop = 6;
            card.Add(info);

            scroll.Add(card);
        }

        private void BuildComparacaoSidebar(ScrollView scroll)
        {
            var card = CreateCard("⚖️ Comparação: Bruto vs Processado");

            var info = new Label("No viewport ao lado, o modelo bruto (à esquerda) é exibido lado a lado com a unidade processada (à direita).\n\n" +
                                 "Permite verificar a consistência de escala métrica, fidelidade visual de texturas toon/URP e impacto de polígonos.");
            info.style.whiteSpace = WhiteSpace.Normal;
            info.style.fontSize = 11;
            info.style.color = new Color(0.85f, 0.85f, 0.85f);
            card.Add(info);

            scroll.Add(card);
        }

        private static VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new Color(0.20f, 0.20f, 0.22f);
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = new Color(0.26f, 0.26f, 0.28f);
            card.style.borderBottomColor = new Color(0.26f, 0.26f, 0.28f);
            card.style.borderLeftColor = new Color(0.26f, 0.26f, 0.28f);
            card.style.borderRightColor = new Color(0.26f, 0.26f, 0.28f);
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.paddingLeft = 10;
            card.style.paddingRight = 10;
            card.style.paddingTop = 8;
            card.style.paddingBottom = 10;
            card.style.marginBottom = 10;

            var titleLbl = new Label(title);
            titleLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLbl.style.fontSize = 12;
            titleLbl.style.color = new Color(0.95f, 0.95f, 0.95f);
            titleLbl.style.marginBottom = 8;
            card.Add(titleLbl);

            return card;
        }
        #endregion

        #region Preview Utility Management & Rendering
        private void InitPreview()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 36f;
                _previewUtility.camera.nearClipPlane = 0.01f;
                _previewUtility.camera.farClipPlane = 50f;
                _previewUtility.camera.clearFlags = CameraClearFlags.Color;
                _previewUtility.camera.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
                _previewUtility.camera.cullingMask = ~0;
            }

            if (_previewPizzaRoot == null)
            {
                _previewPizzaRoot = new GameObject("PizzaPreviewRoot");
                _previewPizzaRoot.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(_previewPizzaRoot);
            }
        }

        private void CleanupPreview()
        {
            if (_previewPizzaRoot != null) { DestroyImmediate(_previewPizzaRoot); _previewPizzaRoot = null; }
            if (_previewItemA != null) { DestroyImmediate(_previewItemA); _previewItemA = null; }
            if (_previewItemB != null) { DestroyImmediate(_previewItemB); _previewItemB = null; }
            if (_previewUtility != null) { _previewUtility.Cleanup(); _previewUtility = null; }
        }

        private void LoadDefaultProfiles()
        {
            if (ActiveProfileForTuning == null)
            {
                var guids = AssetDatabase.FindAssets("t:PizzaIngredientVisualProfile");
                if (guids.Length > 0)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                    ActiveProfileForTuning = AssetDatabase.LoadAssetAtPath<PizzaIngredientVisualProfile>(p);
                }
                else
                {
                    ActiveProfileForTuning = PizzaVisualCatalog.Instance?.GetProfile(7, ProcessingStage.Prepared);
                }
            }

            if (OriginalIngredient == null)
            {
                var guids = AssetDatabase.FindAssets("t:IngredientDef");
                if (guids.Length > 0)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                    OriginalIngredient = AssetDatabase.LoadAssetAtPath<IngredientDef>(p);
                }
            }
        }

        public void RebuildPizzaPreview()
        {
            InitPreview();

            // Garantir base de massa com geometria anatômica (Cornicione)
            PizzaVisualComposer.EnsureDoughBase(_previewPizzaRoot, CookProgress, BurnProgress);

            var netIngredients = new List<NetIngredientDto>();
            for (int i = 0; i < ActiveIngredients.Count; i++)
            {
                netIngredients.Add(new NetIngredientDto
                {
                    DefinitionId = ActiveIngredients[i].IngredientDefId,
                    Stage = (byte)ActiveIngredients[i].Stage,
                    Count = (byte)Mathf.Clamp(ActiveIngredients[i].Count, 1, 255)
                });
            }

            var cookingStage = BurnProgress > 0.5f ? CookingStage.Burned : (CookProgress >= 0.99f ? CookingStage.Baked : CookingStage.Uncooked);

            // Análise dinâmica da geometria da massa real (placeholder ou malha final)
            var surfaceMetrics = PizzaSurfaceAnalyzer.Analyze(_previewPizzaRoot);

            var comp = new PizzaVisualComposition(
                (ulong)VisualSeed,
                2,
                cookingStage,
                CookProgress,
                BurnProgress,
                netIngredients,
                surfaceMetrics
            );

            var layout = PizzaLayoutGenerator.Generate(comp, PizzaVisualCatalog.Instance);
            PizzaVisualComposer.ApplyLayout(_previewPizzaRoot, layout, default);

            // Registrar todos os GameObjects no Preview Utility
            SetLayerRecursively(_previewPizzaRoot, _previewUtility.camera.gameObject.layer);
            _previewUtility.AddSingleGO(_previewPizzaRoot);

            // Coleta de métricas e diagnóstico
            _diagPieceCount = layout.Placements.Count;
            _diagHasErrors = layout.HasFailures;

            int vCount = 0;
            int tCount = 0;
            var filters = _previewPizzaRoot.GetComponentsInChildren<MeshFilter>(true);
            for (int f = 0; f < filters.Length; f++)
            {
                if (filters[f] != null && filters[f].sharedMesh != null)
                {
                    vCount += filters[f].sharedMesh.vertexCount;
                    tCount += filters[f].sharedMesh.triangles.Length / 3;
                }
            }
            _diagVertexCount = vCount;
            _diagTriangleCount = tCount;

            if (layout.Warnings.Count > 0 || layout.Errors.Count > 0)
            {
                _diagWarnings = string.Join("\n", layout.Warnings) + "\n" + string.Join("\n", layout.Errors);
            }
            else
            {
                _diagWarnings = "Geometria perfeitamente contida no reservatório da pizza. Sem colisões de borda.";
            }

            UpdateMetricsLabel();
            _viewport3DContainer?.MarkDirtyRepaint();
            Repaint();
        }

        private void RebuildActiveTabModels()
        {
            InitPreview();

            if (_previewItemA != null) { DestroyImmediate(_previewItemA); _previewItemA = null; }
            if (_previewItemB != null) { DestroyImmediate(_previewItemB); _previewItemB = null; }

            if (_activeTab == StudioTab.Montagem)
            {
                if (_previewPizzaRoot != null) _previewPizzaRoot.SetActive(true);
                return;
            }

            if (_previewPizzaRoot != null) _previewPizzaRoot.SetActive(false);

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") 
                         ?? Shader.Find("Distant Lands/Toon Lit") 
                         ?? Shader.Find("Standard");

            if (_activeTab == StudioTab.ModeloOriginal || _activeTab == StudioTab.Comparacao)
            {
                _previewItemA = new GameObject("OriginalItemModel");
                _previewItemA.hideFlags = HideFlags.HideAndDontSave;

                if (OriginalIngredient != null && OriginalIngredient.placeholderPrefab != null)
                {
                    var inst = Instantiate(OriginalIngredient.placeholderPrefab, _previewItemA.transform);
                    inst.transform.localPosition = Vector3.zero;
                }
                else
                {
                    // Modelo representativo bruto: salame cilíndrico alongado
                    var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cyl.transform.SetParent(_previewItemA.transform, false);
                    cyl.transform.localScale = new Vector3(0.06f, 0.12f, 0.06f);
                    cyl.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var col = cyl.GetComponent<Collider>();
                    if (col != null) DestroyImmediate(col);

                    var r = cyl.GetComponent<Renderer>();
                    if (r != null)
                    {
                        r.material = new Material(shader) { color = new Color(0.65f, 0.15f, 0.15f) };
                    }
                }

                SetLayerRecursively(_previewItemA, _previewUtility.camera.gameObject.layer);
                _previewUtility.AddSingleGO(_previewItemA);

                if (_activeTab == StudioTab.Comparacao)
                {
                    _previewItemA.transform.position = new Vector3(-0.12f, 0.02f, 0f);
                }
                else
                {
                    _previewItemA.transform.position = new Vector3(0f, 0.02f, 0f);
                }
            }

            if (_activeTab == StudioTab.ModeloProcessado || _activeTab == StudioTab.Comparacao)
            {
                _previewItemB = new GameObject("ProcessedItemModel");
                _previewItemB.hideFlags = HideFlags.HideAndDontSave;

                if (ProcessedIngredient != null && ProcessedIngredient.placeholderPrefab != null)
                {
                    var inst = Instantiate(ProcessedIngredient.placeholderPrefab, _previewItemB.transform);
                    inst.transform.localPosition = Vector3.zero;
                }
                else
                {
                    // Unidade fatiada gastronômica com efeito cupping
                    var mf = _previewItemB.AddComponent<MeshFilter>();
                    var mr = _previewItemB.AddComponent<MeshRenderer>();
                    mf.sharedMesh = PizzaModularGeometryFactory.GetPepperoniSliceMesh();

                    var mat = new Material(shader);
                    Color pepCol = new Color(0.72f, 0.18f, 0.14f);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", pepCol);
                    mat.color = pepCol;
                    mr.material = mat;
                    _previewItemB.transform.localScale = Vector3.one * 1.5f;
                }

                SetLayerRecursively(_previewItemB, _previewUtility.camera.gameObject.layer);
                _previewUtility.AddSingleGO(_previewItemB);

                if (_activeTab == StudioTab.Comparacao)
                {
                    _previewItemB.transform.position = new Vector3(0.12f, 0.02f, 0f);
                }
                else
                {
                    _previewItemB.transform.position = new Vector3(0f, 0.02f, 0f);
                }
            }

            _viewport3DContainer?.MarkDirtyRepaint();
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }

        private void OnDrawViewport()
        {
            if (_viewport3DContainer == null) return;
            Rect rect = new Rect(0, 0, _viewport3DContainer.contentRect.width, _viewport3DContainer.contentRect.height);
            if (rect.width <= 10 || rect.height <= 10) return;

            GUILayoutUtility.GetRect(rect.width, rect.height);
            Draw3DPreview(rect);
        }

        private void Draw3DPreview(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && _previewUtility != null)
            {
                _previewUtility.BeginPreview(rect, GUIStyle.none);

                Quaternion camRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                Vector3 camPos = _cameraTarget + camRot * new Vector3(0f, 0f, -_cameraDistance);

                _previewUtility.camera.transform.position = camPos;
                _previewUtility.camera.transform.rotation = camRot;
                _previewUtility.camera.clearFlags = CameraClearFlags.Color;
                _previewUtility.camera.backgroundColor = new Color(0.15f, 0.15f, 0.17f, 1f);
                _previewUtility.camera.cullingMask = ~0;

                // Iluminação de estúdio gastronômico de alta qualidade
                _previewUtility.lights[0].intensity = 1.4f;
                _previewUtility.lights[0].color = new Color(1f, 0.96f, 0.90f);
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(42f, 40f, 0f);

                _previewUtility.lights[1].intensity = 0.85f;
                _previewUtility.lights[1].color = new Color(0.85f, 0.90f, 1f);
                _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-25f, -145f, 0f);

                _previewUtility.Render(true);
                Texture resultTex = _previewUtility.EndPreview();
                GUI.DrawTexture(rect, resultTex, ScaleMode.StretchToFill, false);
            }

            HandleInputEvents(rect);
        }

        private void HandleInputEvents(Rect rect)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        _isPanning = (evt.button == 2) || (evt.button == 0 && evt.shift) || (evt.button == 1 && evt.shift);
                        _isOrbiting = !_isPanning && (evt.button == 0 || evt.button == 1);
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        if (_isPanning)
                        {
                            Quaternion camRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                            Vector3 camRight = camRot * Vector3.right;
                            Vector3 camUp = camRot * Vector3.up;
                            _cameraTarget -= (camRight * evt.delta.x - camUp * evt.delta.y) * (_cameraDistance * 0.0016f);
                        }
                        else if (_isOrbiting)
                        {
                            _cameraYaw += evt.delta.x * 0.65f;
                            _cameraPitch = Mathf.Clamp(_cameraPitch + evt.delta.y * 0.65f, 2f, 88f);
                        }
                        evt.Use();
                        _viewport3DContainer?.MarkDirtyRepaint();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        _isPanning = false;
                        _isOrbiting = false;
                        evt.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (rect.Contains(evt.mousePosition))
                    {
                        _cameraDistance = Mathf.Clamp(_cameraDistance + evt.delta.y * 0.04f, 0.15f, 3.5f);
                        evt.Use();
                        _viewport3DContainer?.MarkDirtyRepaint();
                        Repaint();
                    }
                    break;

                case EventType.KeyDown:
                    if (evt.keyCode == KeyCode.F)
                    {
                        FramePizza();
                        evt.Use();
                    }
                    break;
            }
        }
        #endregion
    }
}
