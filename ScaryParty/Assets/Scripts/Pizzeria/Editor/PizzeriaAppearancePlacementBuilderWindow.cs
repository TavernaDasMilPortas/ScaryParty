#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.City;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Janela de auditoria, calibração geométrica e aplicação de paleta Distant Lands para a Pizzaria Modular.
    /// Acesso via: Tools > Scary Party > Pizzeria Appearance & Placement Builder.
    /// </summary>
    public class PizzeriaAppearancePlacementBuilderWindow : EditorWindow
    {
        private const string DefaultManualPrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        private const string DefaultProfilePath = "Assets/ScriptableObjects/Pizzeria/PizzeriaAppearanceProfile_Default.asset";

        [SerializeField] private GameObject _targetPrefab;
        [SerializeField] private PizzeriaAppearanceProfile _profile;

        private Vector2 _scrollPos;
        private PizzeriaMaterialApplicator.AnalysisReport _analysisReport;
        private PlacementResult _lastPlacementResult;
        private bool _showAdvancedRules = false;
        private bool _showSlotsList = false;

        // Medições em tempo de análise
        private float _measuredFloorSurfaceY = -1.6f;
        private Rect _measuredSalaoRect = Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f);
        private Rect _measuredCozinhaRect = Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f);
        private Vector3 _entranceAnchorPosLocal;
        private Vector3 _entranceAnchorForwardLocal;
        private bool _hasAnchorConflict = false;
        private bool _hasAuthoringBindings = false;
        private bool _anchorsVerifiedByUser = false;

        [MenuItem("Tools/Scary Party/Pizzeria Appearance & Placement Builder")]
        public static void ShowWindow()
        {
            var window = GetWindow<PizzeriaAppearancePlacementBuilderWindow>("Pizzeria Builder");
            window.minSize = new Vector2(460, 650);
            window.Show();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;

            if (_targetPrefab == null)
            {
                _targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultManualPrefabPath);
            }

            if (_profile == null)
            {
                _profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(DefaultProfilePath);
                if (_profile == null)
                {
                    // Tenta achar qualquer perfil existente no projeto
                    string[] guids = AssetDatabase.FindAssets("t:PizzeriaAppearanceProfile");
                    if (guids.Length > 0)
                    {
                        string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(p);
                    }
                }
            }
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("🍕 Pizzaria Modular — Aparência e Implantação", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Ferramenta de autoria para auditar e calibrar metadados geométricos (planta em L, altura de piso, fachada) " +
                "e aplicar paleta de materiais Distant Lands de forma determinística e idempotente.",
                MessageType.Info);

            EditorGUILayout.Space(6);

            // ==========================================
            // BLOCO 1: PREFAB E DIAGNÓSTICO
            // ==========================================
            DrawPrefabAndDiagnosisSection();

            EditorGUILayout.Space(8);

            // ==========================================
            // BLOCO 2: PALETA E MATERIAIS
            // ==========================================
            DrawPaletteAndMaterialsSection();

            EditorGUILayout.Space(8);

            // ==========================================
            // BLOCO 3: IMPLANTAÇÃO E GEOMETRIA
            // ==========================================
            DrawPlacementAndGeometrySection();

            EditorGUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        private void DrawPrefabAndDiagnosisSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("1. Prefab e Diagnóstico", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _targetPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Alvo", _targetPrefab, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                _analysisReport = null;
            }

            if (_targetPrefab == null)
            {
                EditorGUILayout.HelpBox("Selecione o prefab manual da pizzaria (Assets/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab).", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔍 Analisar Prefab", GUILayout.Height(28)))
            {
                AnalyzePrefab();
            }
            EditorGUILayout.EndHorizontal();

            if (_analysisReport != null)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField($"Renderers: {_analysisReport.totalRenderers} | Slots Totais: {_analysisReport.totalSlots} | Slots Nulos: {_analysisReport.nullSlots}");
                EditorGUILayout.LabelField($"Slots Classificados: {_analysisReport.classifiedSlots} | Preservados: {_analysisReport.preservedSlots}");

                if (_hasAuthoringBindings)
                {
                    EditorGUILayout.LabelField($"Autoria Manual: Sim | Âncoras Verificadas pelo Usuário: {(_anchorsVerifiedByUser ? "Sim" : "Não (Provisórias)")}");
                }

                if (_hasAnchorConflict)
                {
                    EditorGUILayout.HelpBox(
                        $"⚠️ Conflito de Fachada Detectado!\n" +
                        $"entranceDirectionLocal do Footprint aponta para -X, enquanto EntranceAnchor aponta forward para {_entranceAnchorForwardLocal}.\n" +
                        $"Verifique o alinhamento antes de considerar as âncoras como definitivas.",
                        MessageType.Warning);
                }

                if (_analysisReport.nullSlots > 0)
                {
                    EditorGUILayout.HelpBox($"Foram detectados {_analysisReport.nullSlots} slot(s) com material nulo. O aplicador tratará os slots vazios sem quebrar o prefab.", MessageType.Info);
                }

                _showSlotsList = EditorGUILayout.Foldout(_showSlotsList, "Distribuição por Função:");
                if (_showSlotsList)
                {
                    EditorGUI.indentLevel++;
                    foreach (var kvp in _analysisReport.roleCounts)
                    {
                        EditorGUILayout.LabelField($"{kvp.Key}: {kvp.Value} slot(s)");
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPaletteAndMaterialsSection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("2. Paleta Distant Lands e Shading", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            _profile = (PizzeriaAppearanceProfile)EditorGUILayout.ObjectField("Perfil de Aparência", _profile, typeof(PizzeriaAppearanceProfile), false);

            if (_profile == null)
            {
                if (GUILayout.Button("Criar Novo Perfil de Aparência"))
                {
                    CreateNewProfile();
                }
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Paleta Arquitetural:", EditorStyles.miniBoldLabel);

            EditorGUI.BeginChangeCheck();
            _profile.paredeClaraColor = EditorGUILayout.ColorField("Parede Clara (#E8DCC8)", _profile.paredeClaraColor);
            _profile.fachadaDestaqueColor = EditorGUILayout.ColorField("Fachada e Destaque (#B84735)", _profile.fachadaDestaqueColor);
            _profile.madeiraColor = EditorGUILayout.ColorField("Madeira Geral (#855A3B)", _profile.madeiraColor);
            _profile.metalColor = EditorGUILayout.ColorField("Metal Geral (#89969B)", _profile.metalColor);
            _profile.pisoSalaoColor = EditorGUILayout.ColorField("Piso Salão (#B58C63)", _profile.pisoSalaoColor);
            _profile.pisoCozinhaColor = EditorGUILayout.ColorField("Piso Cozinha (#C5C4B4)", _profile.pisoCozinhaColor);
            _profile.escuroColor = EditorGUILayout.ColorField("Escuro / Detalhes (#30363A)", _profile.escuroColor);
            _profile.destaqueSecundarioColor = EditorGUILayout.ColorField("Destaque Secundário (#E2AD42)", _profile.destaqueSecundarioColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Fornos e Cozinha:", EditorStyles.miniBoldLabel);
            _profile.fornoLenhaColor = EditorGUILayout.ColorField("Forno a Lenha (#994828)", _profile.fornoLenhaColor);
            _profile.portaFornoColor = EditorGUILayout.ColorField("Porta do Forno (#25282A)", _profile.portaFornoColor);
            _profile.geladeiraColor = EditorGUILayout.ColorField("Geladeira (#DCE4E8)", _profile.geladeiraColor);
            _profile.armarioColor = EditorGUILayout.ColorField("Armários e Despensa (#654321)", _profile.armarioColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Mobiliário:", EditorStyles.miniBoldLabel);
            _profile.mesaColor = EditorGUILayout.ColorField("Mesas (#7A4B27)", _profile.mesaColor);
            _profile.cadeiraColor = EditorGUILayout.ColorField("Cadeiras (#8C5832)", _profile.cadeiraColor);
            _profile.balcaoColor = EditorGUILayout.ColorField("Balcões (#4E3E34)", _profile.balcaoColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Facas, Utensílios e Ferramentas:", EditorStyles.miniBoldLabel);
            _profile.facaColor = EditorGUILayout.ColorField("Lâminas de Facas (#CBD5D8)", _profile.facaColor);
            _profile.faqueiroColor = EditorGUILayout.ColorField("Faqueiro / Suporte (#54321A)", _profile.faqueiroColor);
            _profile.tabuaCorteColor = EditorGUILayout.ColorField("Tábua de Corte (#C29B68)", _profile.tabuaCorteColor);
            _profile.cortadorPizzaColor = EditorGUILayout.ColorField("Cortador de Pizza (#98A7AD)", _profile.cortadorPizzaColor);
            _profile.espatulaColor = EditorGUILayout.ColorField("Espátula (#B59E7E)", _profile.espatulaColor);
            _profile.formaPizzaColor = EditorGUILayout.ColorField("Forma de Pizza / Prato (#A0ABB0)", _profile.formaPizzaColor);
            _profile.panelaConchaColor = EditorGUILayout.ColorField("Panela e Concha (#7E8B91)", _profile.panelaConchaColor);
            _profile.recipienteMolhoColor = EditorGUILayout.ColorField("Cubas de Molho (#A92A22)", _profile.recipienteMolhoColor);
            _profile.pizzaProntaColor = EditorGUILayout.ColorField("Pizza Pronta (#D97925)", _profile.pizzaProntaColor);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Embalagens e Operação:", EditorStyles.miniBoldLabel);
            _profile.caixaPizzaColor = EditorGUILayout.ColorField("Caixas de Pizza (#C5A069)", _profile.caixaPizzaColor);
            _profile.deliveryBagColor = EditorGUILayout.ColorField("Mochila Delivery (#BA1C24)", _profile.deliveryBagColor);
            _profile.caixaRegistradoraColor = EditorGUILayout.ColorField("Caixa Registradora (#3B5054)", _profile.caixaRegistradoraColor);
            _profile.cardapioPranchetaColor = EditorGUILayout.ColorField("Cardápio / Prancheta (#8A603E)", _profile.cardapioPranchetaColor);
            _profile.lixeiraColor = EditorGUILayout.ColorField("Lixeiras (#3C4245)", _profile.lixeiraColor);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_profile);
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("👁️ Pré-visualizar", GUILayout.Height(30)))
            {
                PreviewPalette();
            }

            if (GUILayout.Button("🎨 Aplicar no Prefab", GUILayout.Height(30)))
            {
                ApplyMaterials();
            }

            if (GUILayout.Button("↩️ Restaurar", GUILayout.Height(30)))
            {
                RestoreMaterials();
            }
            EditorGUILayout.EndHorizontal();

            _showAdvancedRules = EditorGUILayout.Foldout(_showAdvancedRules, "Configurações Avançadas e Shaders");
            if (_showAdvancedRules)
            {
                EditorGUI.indentLevel++;
                _profile.opaqueShader = (Shader)EditorGUILayout.ObjectField("Opaque Shader", _profile.opaqueShader, typeof(Shader), false);
                _profile.simpleShader = (Shader)EditorGUILayout.ObjectField("Simple Shader", _profile.simpleShader, typeof(Shader), false);
                _profile.generatedMaterialsFolder = EditorGUILayout.TextField("Pasta Gerada", _profile.generatedMaterialsFolder);
                _profile.disableWaves = EditorGUILayout.Toggle("Desativar Ondas", _profile.disableWaves);
                _profile.disableHSVVariation = EditorGUILayout.Toggle("Desativar HSV Var", _profile.disableHSVVariation);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPlacementAndGeometrySection()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("3. Implantação e Planta em L", EditorStyles.boldLabel);

            PizzeriaFootprint footprint = _targetPrefab != null ? _targetPrefab.GetComponent<PizzeriaFootprint>() : null;

            EditorGUILayout.LabelField($"Nível do Piso Medido (local Y): {_measuredFloorSurfaceY:F2} m");
            EditorGUILayout.LabelField($"Salão: X [{_measuredSalaoRect.xMin:F1}, {_measuredSalaoRect.xMax:F1}], Z [{_measuredSalaoRect.yMin:F1}, {_measuredSalaoRect.yMax:F1}]");
            EditorGUILayout.LabelField($"Cozinha: X [{_measuredCozinhaRect.xMin:F1}, {_measuredCozinhaRect.xMax:F1}], Z [{_measuredCozinhaRect.yMin:F1}, {_measuredCozinhaRect.yMax:F1}]");

            float testSidewalk = 0.15f;
            float rootYCalc = testSidewalk - _measuredFloorSurfaceY;
            EditorGUILayout.HelpBox($"Cálculo de Elevação: Para calçada a {testSidewalk:F2}m, a raiz deve ficar em Y = {rootYCalc:F2}m (Piso = {testSidewalk:F2}m).", MessageType.None);

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📐 Calibrar Metadados no Prefab", GUILayout.Height(28)))
            {
                CalibrateMetadataOnPrefab();
            }

            if (GUILayout.Button("🏙️ Validar Implantação na Cidade", GUILayout.Height(28)))
            {
                ValidatePlacementInCity();
            }
            EditorGUILayout.EndHorizontal();

            if (_lastPlacementResult != null)
            {
                EditorGUILayout.Space(4);
                if (_lastPlacementResult.success)
                {
                    EditorGUILayout.HelpBox(
                        $"✅ Implantação Válida!\n" +
                        $"Quadra Selecionada: {_lastPlacementResult.blockIndex}\n" +
                        $"Posição Raiz: {_lastPlacementResult.position}\n" +
                        $"Rotação: {_lastPlacementResult.rotation.eulerAngles.y:F1}°\n" +
                        $"Bancada: {_lastPlacementResult.bancadaPosition}",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox($"❌ Falha na Implantação: {_lastPlacementResult.failureReason}", MessageType.Error);
                }
            }

            EditorGUILayout.EndVertical();
        }

        private void AnalyzePrefab()
        {
            if (_targetPrefab == null) return;
            if (_profile == null) EnsureDefaultProfile();

            _analysisReport = PizzeriaMaterialApplicator.AnalyzePrefab(_targetPrefab, _profile);

            // Mede pisos através das transformações completas
            Transform salaoFloor = FindChildRecursive(_targetPrefab.transform, "Floor_Salao");
            if (salaoFloor != null)
            {
                Matrix4x4 m = _targetPrefab.transform.worldToLocalMatrix * salaoFloor.localToWorldMatrix;
                Vector3 center = m.MultiplyPoint3x4(Vector3.zero);
                Vector3 lossyScale = salaoFloor.lossyScale;
                _measuredFloorSurfaceY = center.y + (lossyScale.y * 0.5f);
                _measuredSalaoRect = Rect.MinMaxRect(
                    center.x - lossyScale.x * 0.5f, center.z - lossyScale.z * 0.5f,
                    center.x + lossyScale.x * 0.5f, center.z + lossyScale.z * 0.5f);
            }

            Transform cozinhaFloor = FindChildRecursive(_targetPrefab.transform, "Floor_Cozinha");
            if (cozinhaFloor != null)
            {
                Matrix4x4 m = _targetPrefab.transform.worldToLocalMatrix * cozinhaFloor.localToWorldMatrix;
                Vector3 center = m.MultiplyPoint3x4(Vector3.zero);
                Vector3 lossyScale = cozinhaFloor.lossyScale;
                _measuredCozinhaRect = Rect.MinMaxRect(
                    center.x - lossyScale.x * 0.5f, center.z - lossyScale.z * 0.5f,
                    center.x + lossyScale.x * 0.5f, center.z + lossyScale.z * 0.5f);
            }

            // Verifica âncoras e conflitos
            Transform entAnchor = FindChildRecursive(_targetPrefab.transform, "EntranceAnchor");
            if (entAnchor != null)
            {
                _entranceAnchorPosLocal = _targetPrefab.transform.InverseTransformPoint(entAnchor.position);
                _entranceAnchorForwardLocal = _targetPrefab.transform.InverseTransformDirection(entAnchor.forward);

                PizzeriaFootprint fp = _targetPrefab.GetComponent<PizzeriaFootprint>();
                if (fp != null)
                {
                    Vector3 fpDir = new Vector3(fp.entranceDirectionLocal.x, 0, fp.entranceDirectionLocal.y).normalized;
                    _hasAnchorConflict = Vector3.Dot(fpDir, _entranceAnchorForwardLocal) < 0f;
                }
            }

            var bindings = _targetPrefab.GetComponent<PizzeriaAuthoringBindings>();
            if (bindings != null)
            {
                _hasAuthoringBindings = true;
                _anchorsVerifiedByUser = bindings.anchorsVerifiedByUser;
            }

            Repaint();
        }

        private void ApplyMaterials()
        {
            if (_targetPrefab == null || _profile == null) return;

            var report = PizzeriaMaterialApplicator.ApplyMaterialsToPrefab(_targetPrefab, _profile, true);
            EditorUtility.DisplayDialog("Aparência Aplicada",
                $"Materiais Distant Lands aplicados com sucesso!\n" +
                $"Renderers atualizados: {report.updatedRenderers}\n" +
                $"Slots atualizados: {report.updatedSlots}\n" +
                $"Slots preservados: {report.preservedSlots}", "OK");

            AnalyzePrefab();
        }

        private void RestoreMaterials()
        {
            if (_targetPrefab == null) return;

            int count = PizzeriaMaterialApplicator.RestorePreviousMaterials(_targetPrefab);
            EditorUtility.DisplayDialog("Materiais Restaurados",
                $"Restaurados {count} slot(s) de materiais para o estado original do backup.", "OK");

            AnalyzePrefab();
        }

        private void CalibrateMetadataOnPrefab()
        {
            if (_targetPrefab == null) return;

            Undo.RecordObject(_targetPrefab, "Calibrate Pizzeria Footprint");
            PizzeriaFootprint fp = _targetPrefab.GetComponent<PizzeriaFootprint>();
            if (fp == null)
            {
                fp = _targetPrefab.AddComponent<PizzeriaFootprint>();
            }

            fp.floorSurfaceLocalY = _measuredFloorSurfaceY;
            fp.diningHallRect = _measuredSalaoRect;
            fp.kitchenRect = _measuredCozinhaRect;
            fp.structuralRegions = new Rect[] { _measuredSalaoRect, _measuredCozinhaRect };

            // Envelope de saliências abrangendo os pisos com margem conservadora
            float minX = Mathf.Min(_measuredSalaoRect.xMin, _measuredCozinhaRect.xMin) - 0.85f;
            float maxX = Mathf.Max(_measuredSalaoRect.xMax, _measuredCozinhaRect.xMax) + 0.85f;
            float minZ = Mathf.Min(_measuredSalaoRect.yMin, _measuredCozinhaRect.yMin) - 0.85f;
            float maxZ = Mathf.Max(_measuredSalaoRect.yMax, _measuredCozinhaRect.yMax) + 0.85f;
            fp.overhangEnvelope = Rect.MinMaxRect(minX, minZ, maxX, maxZ);

            fp.footprintSize = new Vector2(maxX - minX, maxZ - minZ);
            fp.footprintCenter = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            fp.localBounds = new Bounds(
                new Vector3(fp.footprintCenter.x, _measuredFloorSurfaceY + 3.0f, fp.footprintCenter.y),
                new Vector3(fp.footprintSize.x, 6.0f, fp.footprintSize.y));

            Transform entAnchor = FindChildRecursive(_targetPrefab.transform, "EntranceAnchor");
            if (entAnchor != null) fp.entranceAnchor = entAnchor;

            Transform recAnchor = FindChildRecursive(_targetPrefab.transform, "ReceivingAnchor");
            if (recAnchor != null) fp.receivingAnchor = recAnchor;

            EditorUtility.SetDirty(fp);
            EditorUtility.SetDirty(_targetPrefab);
            PrefabUtility.SavePrefabAsset(_targetPrefab);

            EditorUtility.DisplayDialog("Calibração Concluída",
                $"Metadados de PizzeriaFootprint calibrados e persistidos no prefab com sucesso!\n" +
                $"Piso Y: {fp.floorSurfaceLocalY}m\n" +
                $"Bounds: {fp.footprintSize.x:F1}m x {fp.footprintSize.y:F1}m\n" +
                $"Regiões estruturais: Salão + Cozinha em L.", "OK");

            AnalyzePrefab();
        }

        private void ValidatePlacementInCity()
        {
            CityGenerator generator = FindObjectOfType<CityGenerator>();
            if (generator == null || generator.CityData == null || generator.CityData.blocks == null || generator.CityData.blocks.Count == 0)
            {
                EditorUtility.DisplayDialog("Validação na Cidade",
                    "Nenhuma cidade ativa com blocos gerados encontrada na cena atual.\n" +
                    "Abra uma cena com CityGenerator ou gere a cidade pelo CitySceneBuilder antes de testar.", "OK");
                return;
            }

            _lastPlacementResult = PizzeriaPlacementSolver.Solve(
                generator.CityData.blocks,
                _targetPrefab,
                generator.config,
                new System.Random(generator.CurrentSeed != 0 ? generator.CurrentSeed : 42));

            Repaint();
        }

        private void CreateNewProfile()
        {
            string folder = "Assets/ScriptableObjects/Pizzeria";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                    AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Pizzeria");
            }

            var profile = ScriptableObject.CreateInstance<PizzeriaAppearanceProfile>();
            profile.InitializeDefaultRules();
            AssetDatabase.CreateAsset(profile, DefaultProfilePath);
            AssetDatabase.SaveAssets();
            _profile = profile;
        }

        private void EnsureDefaultProfile()
        {
            if (_profile == null)
            {
                _profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(DefaultProfilePath);
                if (_profile == null)
                {
                    CreateNewProfile();
                }
            }
        }

        private void PreviewPalette()
        {
            if (_targetPrefab == null || _profile == null) return;
            PizzeriaMaterialApplicator.GetOrCreateGeneratedMaterials(_profile);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Pré-visualização da Paleta",
                "Materiais e cores Distant Lands atualizados com sucesso para visualização na cena!", "OK");
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (_lastPlacementResult == null || !_lastPlacementResult.success) return;

            // 1. Polígonos estruturais do L no mundo
            if (_lastPlacementResult.worldStructuralPolygons != null)
            {
                Handles.color = new Color(0.1f, 0.9f, 0.3f, 0.7f);
                foreach (var poly in _lastPlacementResult.worldStructuralPolygons)
                {
                    if (poly != null && poly.Length == 4)
                    {
                        Handles.DrawPolyLine(poly[0], poly[1], poly[2], poly[3], poly[0]);
                    }
                }
            }

            // 2. Seta de Fachada
            Handles.color = Color.yellow;
            Vector3 center = _lastPlacementResult.position;
            Vector3 frontDir = _lastPlacementResult.entranceDirectionWorld;
            Handles.ArrowHandleCap(0, center + Vector3.up * 1.5f, Quaternion.LookRotation(frontDir), 5.0f, EventType.Repaint);
            Handles.Label(center + frontDir * 5.5f + Vector3.up * 1.5f, "Fachada / Rua");

            // 3. Bancada de Entrega
            Handles.color = Color.cyan;
            Handles.DrawDottedLine(center, _lastPlacementResult.bancadaPosition, 4f);
            Handles.SphereHandleCap(0, _lastPlacementResult.bancadaPosition, Quaternion.identity, 0.8f, EventType.Repaint);
            Handles.Label(_lastPlacementResult.bancadaPosition + Vector3.up * 1.0f, "Bancada de Entrega");
        }

        private Transform FindChildRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindChildRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
#endif
