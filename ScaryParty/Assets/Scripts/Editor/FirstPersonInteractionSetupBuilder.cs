#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ScaryParty.Interaction;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using StarterAssets;

namespace ScaryParty.EditorScripts
{
    public class FirstPersonInteractionSetupBuilder : EditorWindow
    {
        private Vector2 _scrollPos;
        private string _report = "Clique em 'Analyze / Preview Changes' para iniciar o diagnóstico.";
        private MessageType _reportType = MessageType.Info;

        // Pose preview
        private ItemPresentationCatalog _previewCatalog;
        private int _selectedProfileIndex = 0;
        private int _selectedSocketIndex = 0; // 0=FP Left, 1=FP Right, 2=Remote Left, 3=Remote Right, 4=Station
        private readonly string[] _socketNames = new[] { "FP Left Hand", "FP Right Hand", "Remote Left Hand", "Remote Right Hand", "Station Slot" };

        [MenuItem("Tools/Scary Party/First Person & Interaction Setup", false, 10)]
        public static void ShowWindow()
        {
            var win = GetWindow<FirstPersonInteractionSetupBuilder>("FP & Interaction Setup");
            win.minSize = new Vector2(550, 650);
            win.Show();
        }

        public static void ApplyFromCommandLine()
        {
            try
            {
                ApplyAll(msg => Debug.Log($"[CLI] {msg}"));
                bool ok = ValidateAll(out string report);
                Debug.Log(report);
                EditorApplication.Exit(ok ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CLI-ERROR] {ex}");
                EditorApplication.Exit(2);
            }
        }

        private void OnEnable()
        {
            LoadCatalogRef();
        }

        private void LoadCatalogRef()
        {
            if (_previewCatalog == null)
            {
                _previewCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("Scary Party — First Person & Interaction Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Configura e valida a Câmera FP estável (baseada no Penumbra), Outline URP (QuickOutline), " +
                "Perfis de Poses dos itens e os Prefabs/Cenas do projeto.",
                MessageType.None
            );

            EditorGUILayout.Space(6);
            DrawActionButtons();

            EditorGUILayout.Space(10);
            DrawPosePreviewSection();

            EditorGUILayout.Space(10);
            DrawReportSection();
        }

        private void DrawActionButtons()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Operações Principais", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("1. Analyze / Preview Changes", GUILayout.Height(30)))
            {
                RunAnalyze();
            }

            if (GUILayout.Button("2. Apply Setup", GUILayout.Height(30)))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog("Aviso", "Não é permitido aplicar o setup durante o Play Mode.", "OK");
                    return;
                }
                RunApply();
            }

            if (GUILayout.Button("3. Validate Setup", GUILayout.Height(30)))
            {
                RunValidate();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawPosePreviewSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("4. Preview & Ajuste de Pose de Item", EditorStyles.boldLabel);

            _previewCatalog = (ItemPresentationCatalog)EditorGUILayout.ObjectField("Catálogo", _previewCatalog, typeof(ItemPresentationCatalog), false);
            if (_previewCatalog == null || _previewCatalog.profiles == null || _previewCatalog.profiles.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum catálogo carregado. Execute 'Apply Setup' para gerar os perfis padrão.", MessageType.Warning);
                EditorGUILayout.EndVertical();
                return;
            }

            List<string> profileNames = new List<string>();
            for (int i = 0; i < _previewCatalog.profiles.Count; i++)
            {
                var p = _previewCatalog.profiles[i];
                profileNames.Add(p != null ? (string.IsNullOrEmpty(p.presentationKey) ? p.name : p.presentationKey) : $"[Nulo #{i}]");
            }

            _selectedProfileIndex = Mathf.Clamp(_selectedProfileIndex, 0, profileNames.Count - 1);
            _selectedProfileIndex = EditorGUILayout.Popup("Perfil do Item", _selectedProfileIndex, profileNames.ToArray());

            var selectedProfile = _previewCatalog.profiles[_selectedProfileIndex];
            if (selectedProfile == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            _selectedSocketIndex = EditorGUILayout.Popup("Socket Alvo", _selectedSocketIndex, _socketNames);

            ItemPoseOffset currentPose;
            switch (_selectedSocketIndex)
            {
                case 0: currentPose = selectedProfile.fpLeftHandPose; break;
                case 1: currentPose = selectedProfile.fpRightHandPose; break;
                case 2: currentPose = selectedProfile.remoteLeftHandPose; break;
                case 3: currentPose = selectedProfile.remoteRightHandPose; break;
                default: currentPose = selectedProfile.stationSlotPose; break;
            }

            EditorGUI.BeginChangeCheck();
            Vector3 newPos = EditorGUILayout.Vector3Field("Posição Local", currentPose.position);
            Vector3 newRot = EditorGUILayout.Vector3Field("Rotação Euler", currentPose.rotationEuler);
            Vector3 newScale = EditorGUILayout.Vector3Field("Escala", currentPose.scale == Vector3.zero ? Vector3.one : currentPose.scale);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(selectedProfile, "Ajuste de Pose de Item");
                var updatedPose = new ItemPoseOffset { position = newPos, rotationEuler = newRot, scale = newScale };
                switch (_selectedSocketIndex)
                {
                    case 0: selectedProfile.fpLeftHandPose = updatedPose; break;
                    case 1: selectedProfile.fpRightHandPose = updatedPose; break;
                    case 2: selectedProfile.remoteLeftHandPose = updatedPose; break;
                    case 3: selectedProfile.remoteRightHandPose = updatedPose; break;
                    default: selectedProfile.stationSlotPose = updatedPose; break;
                }
                EditorUtility.SetDirty(selectedProfile);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawReportSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Relatório de Execução", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_report, _reportType);
            EditorGUILayout.EndVertical();
        }

        // =========================================================================
        // 1. ANALYZE
        // =========================================================================
        public static string AnalyzeDiagnostics(out bool hasErrors)
        {
            hasErrors = false;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DIAGNÓSTICO DO PROJETO ===");

            // 1. Layers
            bool l1 = CheckLayerExists("LocalPlayerModel");
            bool l2 = CheckLayerExists("FirstPersonOnly");
            bool l3 = CheckLayerExists("MinimapOnly");
            sb.AppendLine($"- Layers necessárias: LocalPlayerModel ({(l1 ? "OK" : "Ausente")}), FirstPersonOnly ({(l2 ? "OK" : "Ausente")}), MinimapOnly ({(l3 ? "OK" : "Ausente")})");
            if (!l1 || !l2 || !l3) hasErrors = true;

            // 2. Shaders e Materiais
            var maskShader = Shader.Find("ScaryParty/OutlineMaskURP");
            var fillShader = Shader.Find("ScaryParty/OutlineFillURP");
            sb.AppendLine($"- Shader ScaryParty/OutlineMaskURP: {(maskShader != null ? "OK" : "Ausente")}");
            sb.AppendLine($"- Shader ScaryParty/OutlineFillURP: {(fillShader != null ? "OK" : "Ausente")}");
            if (maskShader == null || fillShader == null) hasErrors = true;

            // 3. PlayerArmature Prefab
            string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (playerPrefab == null)
            {
                sb.AppendLine($"- [ERRO] PlayerArmature não encontrado em {playerPath}!");
                hasErrors = true;
            }
            else
            {
                var fpCam = playerPrefab.GetComponent<FirstPersonCameraController>();
                var camSetup = playerPrefab.GetComponent<PlayerCameraSetup>();
                var interaction = playerPrefab.GetComponent<PlayerInteraction>();
                sb.AppendLine($"- PlayerArmature Prefab: {(playerPrefab != null ? "OK" : "Ausente")}");
                sb.AppendLine($"  * FirstPersonCameraController: {(fpCam != null ? "Presente" : "Necessita migração")}");
                sb.AppendLine($"  * PlayerCameraSetup: {(camSetup != null ? "OK" : "Ausente")}");
                sb.AppendLine($"  * PlayerInteraction: {(interaction != null ? "OK" : "Ausente")}");
            }

            // 4. Catálogo e Perfis
            string catalogPath = "Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(catalogPath);
            sb.AppendLine($"- Catálogo de Apresentação: {(catalog != null ? $"OK ({catalog.profiles.Count} perfis)" : "Necessita criação")}");

            return sb.ToString();
        }

        private void RunAnalyze()
        {
            _report = AnalyzeDiagnostics(out bool hasErrors);
            _reportType = hasErrors ? MessageType.Warning : MessageType.Info;
        }

        // =========================================================================
        // 2. APPLY SETUP
        // =========================================================================
        public static void ApplyAll(Action<string> logAction = null)
        {
            void Log(string msg)
            {
                logAction?.Invoke(msg);
                Debug.Log($"[FPBuilder] {msg}");
            }

            Log("Iniciando aplicação de setup...");

            // 1. Configurar Layers
            SetupLayers();
            Log("Layers configuradas (LocalPlayerModel, FirstPersonOnly, MinimapOnly).");

            // 2. Configurar Materiais de Outline
            EnsureOutlineMaterials();
            Log("Materiais de Outline URP conferidos.");

            // 3. Configurar Catálogo e Perfis de Itens
            EnsureItemPresentationCatalog();
            Log("Catálogo de Apresentação e perfis de itens gerados/atualizados.");

            // 4. Configurar PlayerArmature Prefab
            SetupPlayerArmaturePrefab();
            Log("PlayerArmature.prefab configurado com Rig fixo e referências serializadas.");

            // 5. Configurar Prefabs de Pizzaria com OutlineTarget
            SetupPizzeriaPrefabsOutline();
            Log("Prefabs de Pizzaria configurados com OutlineTarget.");

            // 6. Atualizar Cenas Abertas / GameScene
            SetupGameScene();
            Log("GameScene conferida.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Log("Setup aplicado com sucesso.");
        }

        private void RunApply()
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                ApplyAll(msg => sb.AppendLine(msg));
                _report = sb.ToString();
                _reportType = MessageType.Info;
                LoadCatalogRef();
            }
            catch (Exception ex)
            {
                _report = $"Erro durante Apply Setup:\n{ex}";
                _reportType = MessageType.Error;
                Debug.LogException(ex);
            }
        }

        // =========================================================================
        // 3. VALIDATE
        // =========================================================================
        public static bool ValidateAll(out string report)
        {
            bool ok = true;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== RELATÓRIO DE VALIDAÇÃO ===");

            // 1. Layers
            if (!CheckLayerExists("LocalPlayerModel")) { sb.AppendLine("[ERRO] Layer 'LocalPlayerModel' ausente."); ok = false; }
            if (!CheckLayerExists("FirstPersonOnly")) { sb.AppendLine("[ERRO] Layer 'FirstPersonOnly' ausente."); ok = false; }
            if (!CheckLayerExists("MinimapOnly")) { sb.AppendLine("[ERRO] Layer 'MinimapOnly' ausente."); ok = false; }

            // 2. Shaders e Materiais
            if (Shader.Find("ScaryParty/OutlineMaskURP") == null) { sb.AppendLine("[ERRO] Shader 'ScaryParty/OutlineMaskURP' não encontrado."); ok = false; }
            if (Shader.Find("ScaryParty/OutlineFillURP") == null) { sb.AppendLine("[ERRO] Shader 'ScaryParty/OutlineFillURP' não encontrado."); ok = false; }

            var maskMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Outline/OutlineMask.mat");
            var fillMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Outline/OutlineFill.mat");
            if (maskMat == null) { sb.AppendLine("[ERRO] Material 'OutlineMask.mat' ausente em Assets/Materials/Outline/."); ok = false; }
            if (fillMat == null) { sb.AppendLine("[ERRO] Material 'OutlineFill.mat' ausente em Assets/Materials/Outline/."); ok = false; }

            // 3. PlayerArmature
            string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (playerPrefab == null)
            {
                sb.AppendLine("[ERRO] PlayerArmature.prefab não encontrado.");
                ok = false;
            }
            else
            {
                var fpCam = playerPrefab.GetComponent<FirstPersonCameraController>();
                if (fpCam == null)
                {
                    sb.AppendLine("[ERRO] FirstPersonCameraController ausente no PlayerArmature.prefab.");
                    ok = false;
                }
                else
                {
                    var pivot = playerPrefab.transform.Find("CameraPivot");
                    if (pivot == null) { sb.AppendLine("[ERRO] CameraPivot ausente no PlayerArmature."); ok = false; }
                }
            }

            // 4. Catálogo
            string catalogPath = "Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(catalogPath);
            if (catalog == null)
            {
                sb.AppendLine("[ERRO] ItemPresentationCatalog.asset ausente.");
                ok = false;
            }
            else
            {
                sb.AppendLine($"[OK] Catálogo contém {catalog.profiles.Count} perfis vinculados.");
            }

            if (ok)
            {
                sb.AppendLine("\n>>> SUCESSO: Todos os testes de validação passaram com êxito! <<<");
            }
            else
            {
                sb.AppendLine("\n>>> FALHA: Foram encontrados problemas que impedem a execução ideal. <<<");
            }

            report = sb.ToString();
            return ok;
        }

        private void RunValidate()
        {
            bool success = ValidateAll(out _report);
            _reportType = success ? MessageType.Info : MessageType.Error;
        }

        // =========================================================================
        // IMPLEMENTAÇÕES INTERNAS DO SETUP
        // =========================================================================

        public static void SetupLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProp = tagManager.FindProperty("layers");

            EnsureLayer(layersProp, "MinimapOnly", 8);
            EnsureLayer(layersProp, "LocalPlayerModel", 9);
            EnsureLayer(layersProp, "FirstPersonOnly", 10);

            tagManager.ApplyModifiedProperties();
        }

        private static bool CheckLayerExists(string layerName)
        {
            return LayerMask.NameToLayer(layerName) >= 0;
        }

        private static void EnsureLayer(SerializedProperty layersProp, string layerName, int preferredIndex)
        {
            for (int i = 8; i < layersProp.arraySize; i++)
            {
                if (layersProp.GetArrayElementAtIndex(i).stringValue == layerName)
                    return;
            }

            if (preferredIndex < layersProp.arraySize && string.IsNullOrEmpty(layersProp.GetArrayElementAtIndex(preferredIndex).stringValue))
            {
                layersProp.GetArrayElementAtIndex(preferredIndex).stringValue = layerName;
                return;
            }

            for (int i = 8; i < layersProp.arraySize; i++)
            {
                var sp = layersProp.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(sp.stringValue))
                {
                    sp.stringValue = layerName;
                    return;
                }
            }
        }

        public static void EnsureOutlineMaterials()
        {
            string dir = "Assets/Materials/Outline";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string maskMatPath = "Assets/Materials/Outline/OutlineMask.mat";
            var maskMat = AssetDatabase.LoadAssetAtPath<Material>(maskMatPath);
            if (maskMat == null)
            {
                var s = Shader.Find("ScaryParty/OutlineMaskURP");
                if (s != null)
                {
                    maskMat = new Material(s);
                    AssetDatabase.CreateAsset(maskMat, maskMatPath);
                }
            }

            string fillMatPath = "Assets/Materials/Outline/OutlineFill.mat";
            var fillMat = AssetDatabase.LoadAssetAtPath<Material>(fillMatPath);
            if (fillMat == null)
            {
                var s = Shader.Find("ScaryParty/OutlineFillURP");
                if (s != null)
                {
                    fillMat = new Material(s);
                    AssetDatabase.CreateAsset(fillMat, fillMatPath);
                }
            }
        }

        public static void SetupPlayerArmaturePrefab()
        {
            string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
            var contents = PrefabUtility.LoadPrefabContents(playerPath);
            try
            {
                // 1. Garantir CameraPivot
                var pivotT = contents.transform.Find("CameraPivot");
                if (pivotT == null)
                {
                    var pivotGo = new GameObject("CameraPivot");
                    pivotGo.transform.SetParent(contents.transform, false);
                    pivotGo.transform.localPosition = new Vector3(0f, 1.65f, 0.05f);
                    pivotGo.transform.localRotation = Quaternion.identity;
                    pivotT = pivotGo.transform;
                }

                // 2. Garantir MainCamera
                Camera playerCam = null;
                var camSetup = contents.GetComponent<PlayerCameraSetup>();
                if (camSetup != null && camSetup.mainCamera != null)
                {
                    playerCam = camSetup.mainCamera.GetComponent<Camera>();
                }
                if (playerCam == null)
                {
                    playerCam = contents.GetComponentInChildren<Camera>(true);
                }

                if (playerCam != null)
                {
                    // Move MainCamera para ser filha do CameraPivot se ainda não for
                    if (playerCam.transform.parent != pivotT)
                    {
                        playerCam.transform.SetParent(pivotT, false);
                        playerCam.transform.localPosition = Vector3.zero;
                        playerCam.transform.localRotation = Quaternion.identity;
                    }
                }

                // 3. Garantir LeftHandAnchor e RightHandAnchor sob CameraPivot
                var leftT = pivotT.Find("LeftHandAnchor");
                if (leftT == null)
                {
                    var leftGo = new GameObject("LeftHandAnchor");
                    leftGo.transform.SetParent(pivotT, false);
                    leftGo.transform.localPosition = new Vector3(-0.32f, -0.25f, 0.45f);
                    leftGo.transform.localRotation = Quaternion.identity;
                    leftT = leftGo.transform;
                }

                var rightT = pivotT.Find("RightHandAnchor");
                if (rightT == null)
                {
                    var rightGo = new GameObject("RightHandAnchor");
                    rightGo.transform.SetParent(pivotT, false);
                    rightGo.transform.localPosition = new Vector3(0.32f, -0.25f, 0.45f);
                    rightGo.transform.localRotation = Quaternion.identity;
                    rightT = rightGo.transform;
                }

                // 4. Configurar FirstPersonCameraController
                var fpCam = contents.GetComponent<FirstPersonCameraController>();
                if (fpCam == null)
                {
                    fpCam = contents.AddComponent<FirstPersonCameraController>();
                }

                var soFp = new SerializedObject(fpCam);
                soFp.FindProperty("_cameraPivot").objectReferenceValue = pivotT;
                if (playerCam != null)
                    soFp.FindProperty("_playerCamera").objectReferenceValue = playerCam;
                soFp.FindProperty("_leftHandAnchor").objectReferenceValue = leftT;
                soFp.FindProperty("_rightHandAnchor").objectReferenceValue = rightT;
                soFp.FindProperty("eyeHeight").floatValue = 1.65f;
                soFp.FindProperty("forwardOffset").floatValue = 0.05f;
                soFp.FindProperty("enableHandSwayBob").boolValue = false;
                soFp.ApplyModifiedPropertiesWithoutUndo();

                // 5. Configurar PlayerInteraction
                var interaction = contents.GetComponent<PlayerInteraction>();
                if (interaction != null && playerCam != null)
                {
                    var soInt = new SerializedObject(interaction);
                    soInt.FindProperty("mainCamera").objectReferenceValue = playerCam;
                    soInt.FindProperty("interactionDistance").floatValue = 3.0f;
                    soInt.FindProperty("obstacleLayer").intValue = 1 << 0; // Default
                    soInt.ApplyModifiedPropertiesWithoutUndo();
                }

                // 6. Configurar OutlineService
                var outlineSvc = contents.GetComponent<OutlineService>();
                if (outlineSvc == null)
                {
                    outlineSvc = contents.AddComponent<OutlineService>();
                }

                PrefabUtility.SaveAsPrefabAsset(contents, playerPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static void SetupPizzeriaPrefabsOutline()
        {
            string[] prefabs = new[]
            {
                "Assets/Prefabs/Pizzeria/PizzaBox.prefab",
                "Assets/Prefabs/Pizzeria/PizzaWIP.prefab",
                "Assets/Prefabs/Pizzeria/Tool_Knife.prefab",
                "Assets/Prefabs/Pizzeria/Tool_Grater.prefab",
                "Assets/Prefabs/Pizzeria/SupplyCrate.prefab",
                "Assets/Prefabs/Pizzeria/Ingredient_Dough.prefab",
                "Assets/Prefabs/Pizzeria/Ingredient_Cheese.prefab",
                "Assets/Prefabs/Pizzeria/Ingredient_Calabresa.prefab"
            };

            foreach (var path in prefabs)
            {
                if (!File.Exists(path)) continue;
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var target = contents.GetComponent<OutlineTarget>();
                    if (target == null)
                    {
                        target = contents.AddComponent<OutlineTarget>();
                    }
                    target.RebuildRendererCache();
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        public static void EnsureItemPresentationCatalog()
        {
            string dir = "Assets/ScriptableObjects/Pizzeria/Profiles";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string catalogPath = "Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = CreateInstance<ItemPresentationCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }

            // Definições padrão para gerar perfis
            var defs = new (string key, ItemCategory cat, int defId, bool isTool, string modelPath, Vector3 fpLPos, Vector3 fpRPos, Vector3 fpRot)[]
            {
                ("Ingredient_Dough", ItemCategory.Dough, 1, false, "Assets/Prefabs/Pizzeria/Ingredient_Dough.prefab", new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_MassaAberta", ItemCategory.PizzaBase, 2, false, "Assets/Prefabs/Pizzeria/PizzaWIP.prefab", new Vector3(0f, -0.1f, 0.35f), new Vector3(0f, -0.1f, 0.35f), new Vector3(0f, 0f, 0f)),
                ("Ingredient_Sauce", ItemCategory.Sauce, 3, false, null, new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_Cheese", ItemCategory.Cheese, 4, false, "Assets/Prefabs/Pizzeria/Ingredient_Cheese.prefab", new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_GratedCheese", ItemCategory.Cheese, 5, false, null, new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_Pepperoni", ItemCategory.Topping, 6, false, "Assets/Prefabs/Pizzeria/Ingredient_Calabresa.prefab", new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_SlicedPepperoni", ItemCategory.Topping, 7, false, null, new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_Mushroom", ItemCategory.Topping, 8, false, null, new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("Ingredient_SlicedMushroom", ItemCategory.Topping, 9, false, null, new Vector3(-0.02f, -0.05f, 0.2f), new Vector3(0.02f, -0.05f, 0.2f), Vector3.zero),
                ("PizzaBox", ItemCategory.Box, 0, false, "Assets/Prefabs/Pizzeria/PizzaBox.prefab", new Vector3(0f, -0.15f, 0.4f), new Vector3(0f, -0.15f, 0.4f), new Vector3(0f, 0f, 0f)),
                ("PizzaBase", ItemCategory.PizzaBase, 0, false, "Assets/Prefabs/Pizzeria/PizzaWIP.prefab", new Vector3(0f, -0.12f, 0.35f), new Vector3(0f, -0.12f, 0.35f), Vector3.zero),
                ("Pizza", ItemCategory.Pizza, 0, false, "Assets/Prefabs/Pizzeria/PizzaWIP.prefab", new Vector3(0f, -0.12f, 0.35f), new Vector3(0f, -0.12f, 0.35f), Vector3.zero),
                ("Tool_Grater", ItemCategory.Other, 1, true, "Assets/Prefabs/Pizzeria/Tool_Grater.prefab", new Vector3(-0.05f, -0.05f, 0.25f), new Vector3(0.05f, -0.05f, 0.25f), new Vector3(25f, -15f, 0f)),
                ("Tool_Knife", ItemCategory.Other, 2, true, "Assets/Prefabs/Pizzeria/Tool_Knife.prefab", new Vector3(-0.05f, -0.05f, 0.25f), new Vector3(0.05f, -0.05f, 0.25f), new Vector3(30f, 10f, 0f))
            };

            foreach (var d in defs)
            {
                string profPath = $"{dir}/Profile_{d.key}.asset";
                var profile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(profPath);
                bool isNew = false;
                if (profile == null)
                {
                    profile = CreateInstance<ItemPresentationProfile>();
                    isNew = true;
                }

                profile.presentationKey = d.key;
                profile.category = d.cat;
                profile.definitionId = d.defId;
                profile.isTool = d.isTool;

                if (!string.IsNullOrEmpty(d.modelPath))
                {
                    profile.visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(d.modelPath);
                }

                // Apenas define poses padrão se o perfil for recém-criado para não apagar ajustes manuais
                if (isNew)
                {
                    profile.fpLeftHandPose = new ItemPoseOffset { position = d.fpLPos, rotationEuler = d.fpRot, scale = Vector3.one };
                    profile.fpRightHandPose = new ItemPoseOffset { position = d.fpRPos, rotationEuler = d.fpRot, scale = Vector3.one };
                    profile.remoteLeftHandPose = new ItemPoseOffset { position = new Vector3(0, 0, 0.1f), rotationEuler = Vector3.zero, scale = Vector3.one };
                    profile.remoteRightHandPose = new ItemPoseOffset { position = new Vector3(0, 0, 0.1f), rotationEuler = Vector3.zero, scale = Vector3.one };
                    profile.stationSlotPose = new ItemPoseOffset { position = Vector3.zero, rotationEuler = Vector3.zero, scale = Vector3.one };
                    profile.isPlaceholder = profile.visualPrefab == null;

                    AssetDatabase.CreateAsset(profile, profPath);
                }
                else
                {
                    EditorUtility.SetDirty(profile);
                }

                if (!catalog.profiles.Contains(profile))
                {
                    catalog.profiles.Add(profile);
                }
            }

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        public static void SetupGameScene()
        {
            string gameScenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
            if (!File.Exists(gameScenePath)) return;

            var activeScene = SceneManager.GetActiveScene();
            bool isCurrent = activeScene.path == gameScenePath;

            Scene sceneToEdit = isCurrent ? activeScene : EditorSceneManager.OpenScene(gameScenePath, OpenSceneMode.Additive);
            try
            {
                // Garante OutlineService na cena se ausente
                var svc = UnityEngine.Object.FindAnyObjectByType<OutlineService>();
                if (svc == null)
                {
                    var go = new GameObject("OutlineService");
                    go.AddComponent<OutlineService>();
                    EditorSceneManager.MarkSceneDirty(sceneToEdit);
                }

                // Garante que o visualizer referencie o catálogo
                var visualizer = UnityEngine.Object.FindAnyObjectByType<PizzeriaItemVisualizer>();
                if (visualizer != null && visualizer.catalog == null)
                {
                    visualizer.catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
                    EditorUtility.SetDirty(visualizer);
                    EditorSceneManager.MarkSceneDirty(sceneToEdit);
                }

                if (!isCurrent)
                {
                    EditorSceneManager.SaveScene(sceneToEdit);
                    EditorSceneManager.CloseScene(sceneToEdit, true);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FPBuilder] Aviso ao verificar GameScene: {ex.Message}");
            }
        }
    }
}
#endif
