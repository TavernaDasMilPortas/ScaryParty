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
        // ABA 1: KIT E TROCA VISUAL
        // ==========================================
        [TabGroup("Tabs", "Kit Modular")]
        [Title("Catálogo de 12 Peças", "Inspeção e Substituição Segura de Modelos")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public CityModuleCatalog catalog;

        [TabGroup("Tabs", "Kit Modular")]
        [Title("Substituição Visual Individual")]
        [LabelText("Peça a Substituir")]
        public CityModuleId selectedModule = CityModuleId.A;

        [TabGroup("Tabs", "Kit Modular")]
        [LabelText("Novo Modelo Visual (FBX / Prefab)")]
        [Tooltip("Arraste o FBX importado ou modelo visual final para substituir apenas o filho 'Visual' do módulo.")]
        public GameObject replacementVisual;

        [TabGroup("Tabs", "Kit Modular")]
        [ButtonGroup("KitActions")]
        [Button("Aplicar Visual no Módulo", ButtonSizes.Medium)]
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
            if (def == null || def.prefab == null)
            {
                EditorUtility.DisplayDialog("Erro", $"Prefab do módulo {selectedModule} não encontrado.", "OK");
                return;
            }

            // Abre o prefab para edição e substitui apenas o nó Visual
            string prefabPath = AssetDatabase.GetAssetPath(def.prefab);
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);

            Transform oldVisual = prefabRoot.transform.Find("Visual");
            if (oldVisual != null)
            {
                DestroyImmediate(oldVisual.gameObject);
            }

            GameObject newVisualInstance = Instantiate(replacementVisual, prefabRoot.transform);
            newVisualInstance.name = "Visual";
            newVisualInstance.transform.localPosition = Vector3.zero;
            newVisualInstance.transform.localRotation = Quaternion.identity;
            newVisualInstance.transform.localScale = Vector3.one;

            // Remove colisores indevidos do nó visual importado para evitar duplicação com o nó Collision
            Collider[] visualCols = newVisualInstance.GetComponentsInChildren<Collider>();
            for (int i = 0; i < visualCols.Length; i++)
            {
                DestroyImmediate(visualCols[i]);
            }

            // Re-fiação do componente
            CityModuleInstance inst = prefabRoot.GetComponent<CityModuleInstance>();
            if (inst != null)
            {
                inst.visualRoot = newVisualInstance.transform;
                inst.AutoWireHierarchy();
            }

            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Sucesso", $"Visual do módulo {selectedModule} substituído com sucesso!", "OK");
            Debug.Log($"[ModularCityPreviewWindow] Visual do módulo {selectedModule} atualizado no prefab: {prefabPath}");
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

            GameObject previewContainer = new GameObject("__Modular_Preview_Building__");
            GameObject bldg = ModularBuildingAssembler.AssembleBuilding(layout, catalog, previewContainer.transform);

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

        protected override void OnEnable()
        {
            base.OnEnable();
            LoadOrCreateCatalog();
            if (activeConfig == null)
            {
                CityGenerator gen = FindFirstObjectByType<CityGenerator>();
                if (gen != null) activeConfig = gen.config;
            }
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

            if (gen.config != null)
            {
                if (gen.config.modularCatalog == null && catalog != null)
                {
                    gen.config.modularCatalog = catalog;
                    EditorUtility.SetDirty(gen.config);
                }
            }
        }
    }
}
