using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.City.Modular;

namespace ScaryParty.City.Editor.Modular
{
    /// <summary>
    /// Janela de inspeção, preview e troca visual do kit de cidade modular.
    /// Organizada nas 4 abas especificadas no plano: Kit, Prédio, Cidade e Validação.
    /// </summary>
    public class ModularCityPreviewWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Scary Party/Wizards/Cidade/Cidade Modular (Painel de Controle)", false, 50)]
        public static void OpenWindow()
        {
            var win = GetWindow<ModularCityPreviewWindow>("Cidade Modular");
            win.minSize = new Vector2(650, 600);
            win.Show();
        }

        // ==========================================
        // ABA 1: KIT E TROCA VISUAL COM PREVIEW 3D
        // ==========================================
        [HideInInspector]
        public CityModuleCatalog catalog;

        [TabGroup("Tabs", "Kit Modular")]
        [HorizontalGroup("Tabs/Kit Modular/MainSplit", 0.45f)]
        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [LabelText("Módulo")]
        [OnValueChanged("OnModuleSelectionChanged")]
        public CityModuleId selectedModule = CityModuleId.A;

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Nome")]
        public string SelectedModuleName
        {
            get
            {
                if (catalog == null) return "Sem catálogo";
                var def = catalog.GetDefinition(selectedModule);
                return def != null ? def.displayName : "Não encontrado";
            }
        }

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [ShowInInspector]
        [ReadOnly]
        [LabelText("Dimensões")]
        public string SelectedModuleDimensions
        {
            get
            {
                if (catalog == null) return "";
                var def = catalog.GetDefinition(selectedModule);
                return def != null ? $"{def.width}m (larg) x {def.height}m (alt) x {def.depth}m (prof)" : "";
            }
        }

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [LabelText("Offset de Posição Visual")]
        [Tooltip("Compensa o pivot do modelo do Blender sem mexer nos colisores ou âncoras.")]
        [OnValueChanged("OnOffsetsChanged")]
        public Vector3 moduleVisualPositionOffset = Vector3.zero;

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [LabelText("Offset de Rotação Visual")]
        [Tooltip("Compensa a rotação do modelo do Blender sem mexer nos colisores ou âncoras.")]
        [OnValueChanged("OnOffsetsChanged")]
        public Vector3 moduleVisualRotationOffset = Vector3.zero;

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [ButtonGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo/OrientationActions")]
        [Button("Carregar Offsets", ButtonSizes.Small)]
        public void LoadModuleOffset()
        {
            if (catalog == null) LoadOrCreateCatalog();
            var def = catalog.GetDefinition(selectedModule);
            if (def != null)
            {
                moduleVisualPositionOffset = def.visualPositionOffset;
                moduleVisualRotationOffset = def.visualRotationOffset;
                replacementVisual = def.prefab;
                RebuildPreviewObject();
            }
        }

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [ButtonGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo/OrientationActions")]
        [Button("Salvar Offsets no Catálogo", ButtonSizes.Small)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void SaveModuleOffset()
        {
            if (catalog == null) LoadOrCreateCatalog();
            var def = catalog.GetDefinition(selectedModule);
            if (def != null)
            {
                def.visualPositionOffset = moduleVisualPositionOffset;
                def.visualRotationOffset = moduleVisualRotationOffset;
                catalog.RebuildLookup();
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();
                Debug.Log($"[ModularCityPreviewWindow] Offsets para {selectedModule} salvos no catálogo: Pos={moduleVisualPositionOffset}, Rot={moduleVisualRotationOffset}.");
            }
        }

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [LabelText("Novo Modelo Visual (FBX)")]
        [Tooltip("Arraste o FBX importado ou modelo visual final para substituir o prefab do módulo.")]
        public GameObject replacementVisual;

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Controls")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo")]
        [ButtonGroup("Tabs/Kit Modular/MainSplit/Controls/Controles do Módulo/ReplacementActions")]
        [Button("Atribuir Modelo no Módulo", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void ApplyVisualReplacement()
        {
            if (catalog == null) LoadOrCreateCatalog();
            if (replacementVisual == null)
            {
                EditorUtility.DisplayDialog("Aviso", "Arraste um FBX ou GameObject para o campo 'Novo Modelo Visual'.", "OK");
                return;
            }

            CityModuleDefinition def = catalog.GetDefinition(selectedModule);
            if (def == null)
            {
                EditorUtility.DisplayDialog("Erro", $"Módulo {selectedModule} não encontrado no catálogo.", "OK");
                return;
            }

            def.prefab = replacementVisual;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            RebuildPreviewObject();
            EditorUtility.DisplayDialog("Sucesso", $"Modelo de {selectedModule} atualizado para '{replacementVisual.name}'!", "OK");
        }

        // --- COLUNA DA DIREITA: VIEWPORT 3D INTERATIVO (MÓDULO OU PRÉDIO COMPLETO COM OS 12 MÓDULOS) ---
        [VerticalGroup("Tabs/Kit Modular/MainSplit/Preview3D")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Preview3D/Visualização 3D")]
        [EnumToggleButtons]
        [LabelText("Modo de Visão")]
        [OnValueChanged("OnPreviewModeChanged")]
        public PreviewDisplayMode previewMode = PreviewDisplayMode.BuildingWithAll12Modules;

        public enum PreviewDisplayMode
        {
            [LabelText("Prédio Completo (12 Módulos)")]
            BuildingWithAll12Modules,
            [LabelText("Peça Individual")]
            SingleModuleOnly
        }

        [VerticalGroup("Tabs/Kit Modular/MainSplit/Preview3D")]
        [BoxGroup("Tabs/Kit Modular/MainSplit/Preview3D/Visualização 3D")]
        [OnInspectorGUI]
        private void Draw3DModuleViewport()
        {
            Rect rect = GUILayoutUtility.GetRect(320, 380, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                RenderPreviewViewport(rect);
            }
            HandleViewportInput(rect);
        }

        [TabGroup("Tabs", "Kit Modular")]
        [ButtonGroup("KitActions")]
        [Button("Criar Ausentes", ButtonSizes.Medium)]
        public void CreateMissingAssets()
        {
            catalog = ModularCityKitBuilder.BuildCompleteKit(false);
        }

        [TabGroup("Tabs", "Kit Modular")]
        [ButtonGroup("KitActions")]
        [Button("Validar Kit", ButtonSizes.Medium)]
        [GUIColor(0.3f, 0.7f, 1f)]
        public void ValidateKitAction()
        {
            if (catalog == null) LoadOrCreateCatalog();
            var res = ModularCityValidator.ValidateKit(catalog);
            validationStatus = res.Summary;
            EditorUtility.DisplayDialog("Validação do Kit", res.Summary + "\n\n" + string.Join("\n", res.errors), "OK");
        }

        // ==========================================
        // ABA 2: PREVIEW DE PRÉDIO ISOLADO
        // ==========================================
        [TabGroup("Tabs", "Prédio Isolado")]
        [Title("Receita e Dimensões")]
        [LabelText("Receita do Edifício")]
        public ModularBuildingRecipe testRecipe = ModularBuildingRecipe.HouseWithGarage;

        [TabGroup("Tabs", "Prédio Isolado")]
        [ShowIf("testRecipe", ModularBuildingRecipe.Custom)]
        [LabelText("Largura (Módulos de 2m)")]
        [Range(2, 8)]
        public int customWidthModules = 4; // 8m

        [TabGroup("Tabs", "Prédio Isolado")]
        [ShowIf("testRecipe", ModularBuildingRecipe.Custom)]
        [LabelText("Profundidade (Módulos de 2m)")]
        [Range(2, 8)]
        public int customDepthModules = 3; // 6m

        [TabGroup("Tabs", "Prédio Isolado")]
        [ShowIf("testRecipe", ModularBuildingRecipe.Custom)]
        [LabelText("Pavimentos")]
        [Range(1, 6)]
        public int customFloorCount = 2;

        [TabGroup("Tabs", "Prédio Isolado")]
        [LabelText("Semente Visual")]
        public int previewSeed = 12345;

        [TabGroup("Tabs", "Prédio Isolado")]
        [Title("Orientação e Cores do Prédio")]
        [LabelText("Usar Cor Customizada")]
        public bool useCustomColor = false;

        [TabGroup("Tabs", "Prédio Isolado")]
        [ShowIf("useCustomColor")]
        [LabelText("Cor-Base de Teste")]
        public Color testBaseColor = new Color(0.2f, 0.45f, 0.8f);

        [TabGroup("Tabs", "Prédio Isolado")]
        [LabelText("Intensidade Escurecimento Acabamentos")]
        [Range(0.05f, 0.70f)]
        public float testDarkenIntensity = 0.30f;

        [TabGroup("Tabs", "Prédio Isolado")]
        [ButtonGroup("BuildingActions")]
        [Button("Gerar Preview do Prédio", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.8f, 0.3f)]
        public void GenerateBuildingPreview()
        {
            ClearPreview();
            if (catalog == null) LoadOrCreateCatalog();

            BuildingLayout layout;
            if (testRecipe != ModularBuildingRecipe.Custom)
            {
                layout = ModularBuildingAssembler.CreateRecipeLayout(testRecipe, Vector3.zero, Quaternion.identity, previewSeed);
            }
            else
            {
                var profile = ModularBuildingProfile.CreateDefault(ZoneType.Commercial);
                layout = ModularBuildingAssembler.CreateProceduralLayout(
                    customWidthModules, customDepthModules, customFloorCount, profile, Vector3.zero, Quaternion.identity, previewSeed);
            }

            if (useCustomColor)
            {
                layout.buildingColor = testBaseColor;
            }

            GameObject previewContainer = new GameObject("__Modular_Preview_Building__");
            GameObject bldg = ModularBuildingAssembler.AssembleBuilding(layout, catalog, previewContainer.transform);

            // Reaplica paleta com o fator de escurecimento personalizado da aba
            ModularBuildingAssembler.ApplyBuildingPalette(bldg, layout.buildingColor, catalog, testDarkenIntensity);

            Selection.activeGameObject = bldg;
            SceneView.lastActiveSceneView?.FrameSelected();
            Debug.Log($"[ModularCityPreviewWindow] Preview de prédio montado com sucesso! Receita: {testRecipe}, Tamanho: {layout.WidthMeters}x{layout.DepthMeters}m, Andares: {layout.floorCount}");
        }

        [TabGroup("Tabs", "Prédio Isolado")]
        [ButtonGroup("BuildingActions")]
        [Button("Nova Seed", ButtonSizes.Medium)]
        public void RandomizeSeedAndPreview()
        {
            previewSeed = UnityEngine.Random.Range(1, 999999);
            GenerateBuildingPreview();
        }

        [TabGroup("Tabs", "Prédio Isolado")]
        [ButtonGroup("BuildingActions")]
        [Button("Limpar Preview", ButtonSizes.Medium)]
        [GUIColor(1f, 0.4f, 0.4f)]
        public void ClearPreview()
        {
            GameObject old = GameObject.Find("__Modular_Preview_Building__");
            if (old != null) DestroyImmediate(old);
        }

        // ==========================================
        // ABA 3: CIDADE E GERAÇÃO
        // ==========================================
        [TabGroup("Tabs", "Cidade")]
        [Title("Geração da Cidade")]
        [InfoBox("Alterne entre o Modo Modular (12 peças) e o Modo Legado (cubos) mantendo compatibilidade.")]
        [LabelText("CityConfig Ativo")]
        public CityConfig activeConfig;

        [TabGroup("Tabs", "Cidade")]
        [ButtonGroup("CityActions")]
        [Button("Gerar Cidade (Nova Seed)", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.8f, 0.3f)]
        public void GenerateCityNewSeed()
        {
            CityGenerator gen = FindFirstObjectByType<CityGenerator>();
            if (gen == null)
            {
                EditorUtility.DisplayDialog("Aviso", "Nenhum CityGenerator encontrado na cena aberta.", "OK");
                return;
            }

            EnsureCityConfigAssigned(gen);
            gen.GenerateCity();
        }

        [TabGroup("Tabs", "Cidade")]
        [ButtonGroup("CityActions")]
        [Button("Regerar (Mesma Seed)", ButtonSizes.Medium)]
        public void RegenerateSameSeed()
        {
            CityGenerator gen = FindFirstObjectByType<CityGenerator>();
            if (gen == null) return;
            EnsureCityConfigAssigned(gen);

            int current = gen.config != null ? gen.config.seed : 12345;
            gen.ClearCity();
            if (gen.config != null) gen.config.seed = current;
            gen.GenerateCity();
        }

        [TabGroup("Tabs", "Cidade")]
        [ButtonGroup("CityActions")]
        [Button("Limpar Cidade", ButtonSizes.Medium)]
        [GUIColor(1f, 0.4f, 0.4f)]
        public void ClearCityAction()
        {
            CityGenerator gen = FindFirstObjectByType<CityGenerator>();
            if (gen != null) gen.ClearCity();
        }

        // ==========================================
        // ABA 4: VALIDAÇÃO E AUDITORIA
        // ==========================================
        [TabGroup("Tabs", "Validação")]
        [Title("Relatório Geral de Integridade")]
        [TextArea(8, 20)]
        [ReadOnly]
        public string validationStatus = "Kit não auditado. Clique abaixo para auditar.";

        [TabGroup("Tabs", "Validação")]
        [Button("Auditar Kit e Cena Atual", ButtonSizes.Large)]
        [GUIColor(0.3f, 0.7f, 1f)]
        public void RunFullAudit()
        {
            if (catalog == null) LoadOrCreateCatalog();

            var kitRes = ModularCityValidator.ValidateKit(catalog);
            string report = "=== AUDITORIA DO KIT MODULAR ===\n";
            report += kitRes.Summary + "\n\n";

            if (kitRes.errors.Count > 0)
            {
                report += "ERROS CRÍTICOS:\n";
                foreach (var err in kitRes.errors) report += "❌ " + err + "\n";
                report += "\n";
            }

            if (kitRes.warnings.Count > 0)
            {
                report += "AVISOS:\n";
                foreach (var w in kitRes.warnings) report += "⚠️ " + w + "\n";
                report += "\n";
            }

            report += "=== EDIFÍCIOS NA CENA ===\n";
            CityBuilding[] bldgs = FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            report += $"Total de prédios na cena: {bldgs.Length}\n";

            int modularCount = 0;
            int withEntrance = 0;
            foreach (var b in bldgs)
            {
                if (b.footprint != Vector2.zero) modularCount++;
                if (b.entrancePosition != Vector3.zero) withEntrance++;
            }

            report += $"Prédios modulares: {modularCount}\n";
            report += $"Prédios com entrada válida: {withEntrance}\n";

            validationStatus = report;
            Debug.Log($"[ModularCityPreviewWindow]\n{report}");
        }

        private PreviewRenderUtility _previewUtility;
        private GameObject _previewRoot;
        private GameObject _modulePreviewInstance;
        private float _camDistance = 4.2f;
        private float _camPitch = 25f;
        private float _camYaw = 45f;
        private Vector3 _camTarget = new Vector3(0f, 1.5f, 0f);
        private bool _isOrbiting;
        private bool _isPanning;

        private void OnPreviewModeChanged()
        {
            if (previewMode == PreviewDisplayMode.BuildingWithAll12Modules)
            {
                _camDistance = 16f;
                _camTarget = new Vector3(0f, 3.5f, -4f);
                _camPitch = 22f;
                _camYaw = 35f;
            }
            else
            {
                _camDistance = 4.2f;
                _camTarget = new Vector3(0f, 1.5f, 0f);
                _camPitch = 25f;
                _camYaw = 45f;
            }
            RebuildPreviewObject();
        }

        private void OnModuleSelectionChanged()
        {
            LoadModuleOffset();
            RebuildPreviewObject();
        }

        private void OnOffsetsChanged()
        {
            if (catalog == null) LoadOrCreateCatalog();
            var def = catalog != null ? catalog.GetDefinition(selectedModule) : null;
            if (def != null)
            {
                def.visualPositionOffset = moduleVisualPositionOffset;
                def.visualRotationOffset = moduleVisualRotationOffset;
                catalog.RebuildLookup();
            }

            RebuildPreviewObject();
        }

        private void InitPreviewUtility()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 30f;
                _previewUtility.camera.nearClipPlane = 0.05f;
                _previewUtility.camera.farClipPlane = 150f;
                _previewUtility.camera.clearFlags = CameraClearFlags.Color;
                _previewUtility.camera.backgroundColor = new Color(0.18f, 0.18f, 0.22f, 1f);
                _previewUtility.camera.cullingMask = ~0;
            }

            if (_previewRoot == null)
            {
                _previewRoot = new GameObject("ModulePreviewRoot");
                _previewRoot.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(_previewRoot);
            }
        }

        private void DestroyPreviewObjects()
        {
            if (_modulePreviewInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_modulePreviewInstance);
                _modulePreviewInstance = null;
            }
            if (_previewRoot != null)
            {
                UnityEngine.Object.DestroyImmediate(_previewRoot);
                _previewRoot = null;
            }
        }

        private void CleanupPreview()
        {
            DestroyPreviewObjects();
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private void RebuildPreviewObject()
        {
            if (_previewRoot == null) InitPreviewUtility();

            if (_modulePreviewInstance != null)
            {
                UnityEngine.Object.DestroyImmediate(_modulePreviewInstance);
                _modulePreviewInstance = null;
            }

            if (catalog == null) LoadOrCreateCatalog();

            if (previewMode == PreviewDisplayMode.BuildingWithAll12Modules)
            {
                // Constrói o prédio showcase que reúne todos os 12 módulos
                var layout = ModularBuildingAssembler.CreateRecipeLayout(
                    ModularBuildingRecipe.ShowcaseAll12Modules, Vector3.zero, Quaternion.identity, 12345);

                _modulePreviewInstance = ModularBuildingAssembler.AssembleBuilding(
                    layout, catalog, _previewRoot.transform);

                _modulePreviewInstance.transform.localPosition = Vector3.zero;
                _modulePreviewInstance.transform.localRotation = Quaternion.identity;
                _modulePreviewInstance.transform.localScale = Vector3.one;

                // Aplica a paleta padrão de duas tonalidades
                ModularBuildingAssembler.ApplyBuildingPalette(
                    _modulePreviewInstance, layout.buildingColor, catalog, 0.30f);
            }
            else
            {
                // Mostra apenas a peça individual selecionada
                var def = catalog != null ? catalog.GetDefinition(selectedModule) : null;
                if (def == null || def.prefab == null) return;

                _modulePreviewInstance = UnityEngine.Object.Instantiate(def.prefab, _previewRoot.transform);
                _modulePreviewInstance.transform.localPosition = Vector3.zero;
                _modulePreviewInstance.transform.localRotation = Quaternion.identity;
                _modulePreviewInstance.transform.localScale = Vector3.one;

                UpdatePreviewOffsets();
            }

            // Seta layer de preview recursivamente
            SetLayerRecursively(_previewRoot, _previewUtility.camera.gameObject.layer);
            _previewUtility.AddSingleGO(_previewRoot);
            Repaint();
        }

        private void UpdatePreviewOffsets()
        {
            if (_modulePreviewInstance == null) return;

            // Se estiver no modo prédio completo, recompõe o prédio para refletir os offsets atualizados
            if (previewMode == PreviewDisplayMode.BuildingWithAll12Modules)
            {
                // Atualiza a definição em memória temporariamente para o prédio renderizar
                var def = catalog != null ? catalog.GetDefinition(selectedModule) : null;
                if (def != null)
                {
                    def.visualPositionOffset = moduleVisualPositionOffset;
                    def.visualRotationOffset = moduleVisualRotationOffset;
                }
                RebuildPreviewObject();
                return;
            }

            Quaternion rotOffset = Quaternion.Euler(moduleVisualRotationOffset);
            Vector3 posOffset = moduleVisualPositionOffset;

            // Se possui nó canônico Visual
            Transform visual = _modulePreviewInstance.transform.Find("Visual");
            if (visual != null)
            {
                visual.localPosition = posOffset;
                visual.localRotation = rotOffset;
            }
            else if (_modulePreviewInstance.transform.childCount > 0)
            {
                // FBX direto: ajusta todos os nós visuais filhos
                for (int i = 0; i < _modulePreviewInstance.transform.childCount; i++)
                {
                    Transform c = _modulePreviewInstance.transform.GetChild(i);
                    if (c.name == "Collision" || c.name == "Anchors") continue;
                    c.localPosition = posOffset;
                    c.localRotation = rotOffset;
                }
            }
            else
            {
                // Instância direta sem filhos: move a própria raiz
                _modulePreviewInstance.transform.localPosition = posOffset;
                _modulePreviewInstance.transform.localRotation = rotOffset;
            }

            Repaint();
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            for (int i = 0; i < obj.transform.childCount; i++)
            {
                SetLayerRecursively(obj.transform.GetChild(i).gameObject, layer);
            }
        }

        private void RenderPreviewViewport(Rect rect)
        {
            if (_previewUtility == null || _previewRoot == null || _modulePreviewInstance == null)
            {
                InitPreviewUtility();
                RebuildPreviewObject();
            }

            if (_previewUtility == null) return;

            _previewUtility.BeginPreview(rect, GUIStyle.none);

            // Posiciona a câmera orbitando o ponto de mira
            Quaternion camRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
            Vector3 camPos = _camTarget - (camRot * Vector3.forward * _camDistance);

            _previewUtility.camera.transform.position = camPos;
            _previewUtility.camera.transform.rotation = camRot;

            // Luzes
            _previewUtility.lights[0].intensity = 1.3f;
            _previewUtility.lights[0].transform.rotation = Quaternion.Euler(50f, 40f, 0f);
            _previewUtility.lights[1].intensity = 0.6f;
            _previewUtility.lights[1].color = new Color(0.8f, 0.88f, 1f);
            _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-30f, -140f, 0f);

            _previewUtility.Render(true);
            Texture tex = _previewUtility.EndPreview();
            if (tex != null)
            {
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
            }

            // Overlay de instrução no viewport
            Rect infoRect = new Rect(rect.x + 8, rect.y + 8, 260, 20);
            EditorGUI.DrawRect(infoRect, new Color(0.08f, 0.08f, 0.1f, 0.75f));
            GUI.Label(infoRect, " 🔄 Arraste: Órbita | Shift+Arraste: Pan | Scroll: Zoom", EditorStyles.miniLabel);

            // Overlay de orientação cardinal (Frente voltada para +Z)
            Rect orientRect = new Rect(rect.x + 8, rect.yMax - 26, 260, 20);
            EditorGUI.DrawRect(orientRect, new Color(0.08f, 0.08f, 0.1f, 0.75f));
            GUI.Label(orientRect, " 🧭 Eixo de Fachada padrão: +Z (Para fora)", EditorStyles.miniLabel);
        }

        private void HandleViewportInput(Rect rect)
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
                            Quaternion camRot = Quaternion.Euler(_camPitch, _camYaw, 0f);
                            Vector3 camRight = camRot * Vector3.right;
                            Vector3 camUp = camRot * Vector3.up;
                            _camTarget -= (camRight * evt.delta.x - camUp * evt.delta.y) * (_camDistance * 0.0025f);
                        }
                        else if (_isOrbiting)
                        {
                            _camYaw += evt.delta.x * 0.6f;
                            _camPitch = Mathf.Clamp(_camPitch + evt.delta.y * 0.6f, -80f, 80f);
                        }
                        Repaint();
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        _isOrbiting = false;
                        _isPanning = false;
                        evt.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (rect.Contains(evt.mousePosition))
                    {
                        _camDistance = Mathf.Clamp(_camDistance + evt.delta.y * 0.35f, 0.5f, 20f);
                        Repaint();
                        evt.Use();
                    }
                    break;
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadOrCreateCatalog();
            LoadModuleOffset();
            InitPreviewUtility();
            RebuildPreviewObject();

            if (activeConfig == null)
            {
                CityGenerator gen = FindFirstObjectByType<CityGenerator>();
                if (gen != null) activeConfig = gen.config;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            CleanupPreview();
        }

        private void LoadOrCreateCatalog()
        {
            if (catalog == null)
            {
                catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(ModularCityKitBuilder.CatalogPath);
            }
        }

        private void EnsureCityConfigAssigned(CityGenerator gen)
        {
            if (gen.config == null && activeConfig != null)
            {
                gen.config = activeConfig;
            }

            if (catalog == null) LoadOrCreateCatalog();

            if (gen.config != null)
            {
                if (catalog != null)
                {
                    catalog.RebuildLookup();
                    gen.config.modularCatalog = catalog;
                    EditorUtility.SetDirty(gen.config);
                }
            }
        }
    }
}
