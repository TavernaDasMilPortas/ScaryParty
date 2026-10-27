#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public class PizzeriaFbxMigrationWindow : EditorWindow
    {
        private PizzeriaFbxImportProfile _profile;
        private Vector2 _scrollPos;
        private string _statusMessage = "Selecione o perfil e execute as etapas de migração.";
        private MessageType _statusType = MessageType.Info;
        private ValidationReport _lastReport;

        [MenuItem("Tools/Scary Party/Pizzeria/FBX Modular Migration Window", false, 5)]
        [MenuItem("Tools/Scary Party/Pizzeria/Importar e Integrar Pizzaria FBX", false, 6)]
        public static void OpenWindow()
        {
            var win = GetWindow<PizzeriaFbxMigrationWindow>("Migração Pizzaria FBX");
            win.minSize = new Vector2(600, 700);
            win.Show();
        }

        private void OnEnable()
        {
            LoadOrCreateProfile();
        }

        private void LoadOrCreateProfile()
        {
            string profilePath = "Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset";
            _profile = AssetDatabase.LoadAssetAtPath<PizzeriaFbxImportProfile>(profilePath);
            if (_profile == null)
            {
                if (!Directory.Exists("Assets/ScriptableObjects/Pizzeria/Import"))
                    Directory.CreateDirectory("Assets/ScriptableObjects/Pizzeria/Import");

                _profile = PizzeriaFbxImportProfile.CreateDefaultProfile();
                AssetDatabase.CreateAsset(_profile, profilePath);
                AssetDatabase.SaveAssets();
            }
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Migração Modular da Pizzaria (FBX)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Pipeline de importação determinística, extração de modelos visuais, montagem de prefab funcional L e integração urbana.", MessageType.Info);

            EditorGUILayout.Space(10);
            _profile = (PizzeriaFbxImportProfile)EditorGUILayout.ObjectField("Perfil de Importação:", _profile, typeof(PizzeriaFbxImportProfile), false);

            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Selecione ou crie um PizzeriaFbxImportProfile para prosseguir.", MessageType.Warning);
                if (GUILayout.Button("Criar Perfil Padrão"))
                {
                    LoadOrCreateProfile();
                }
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Ações do Pipeline", EditorStyles.boldLabel);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            {
                if (GUILayout.Button("1. Analisar FBX (Inventário Determinístico)", GUILayout.Height(30)))
                {
                    RunAnalysis();
                }

                EditorGUILayout.Space(4);
                if (GUILayout.Button("2. Extrair Modelos Visuais Reutilizáveis", GUILayout.Height(30)))
                {
                    ExtractVisuals();
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
                if (GUILayout.Button("3. Importar e Integrar Pizzaria (Execução Completa)", GUILayout.Height(38)))
                {
                    RunFullIntegration();
                }
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space(4);
                if (GUILayout.Button("4. Validar Estrutura e Integração", GUILayout.Height(30)))
                {
                    RunValidation();
                }

                EditorGUILayout.Space(4);
                GUI.backgroundColor = new Color(1.0f, 0.5f, 0.5f);
                if (GUILayout.Button("5. Restaurar Backup Anterior", GUILayout.Height(26)))
                {
                    RestoreBackup();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Diagnóstico e Status", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_statusMessage, _statusType);

            if (_lastReport != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Resultados da Validação:", EditorStyles.boldLabel);
                _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Height(280));
                foreach (var entry in _lastReport.entries)
                {
                    EditorGUILayout.BeginHorizontal();
                    var icon = entry.passed ? "✅" : "❌";
                    EditorGUILayout.LabelField($"{icon} [{entry.category}] {entry.checkName}", GUILayout.Width(260));
                    EditorGUILayout.LabelField(entry.message, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void RunAnalysis()
        {
            try
            {
                var result = PizzeriaFbxAnalyzer.Analyze(_profile.fbxAssetPath);
                string report = PizzeriaFbxAnalyzer.GenerateReport(result);
                string outPath = Path.Combine(Directory.GetCurrentDirectory(), "PizzeriaFbxInventoryReport.txt");
                File.WriteAllText(outPath, report);

                _statusMessage = $"Análise concluída com sucesso! {result.totalNodes} nós detectados, {result.nodesWithMesh} com malha. Relatório salvo em PizzeriaFbxInventoryReport.txt.";
                _statusType = MessageType.Info;
                Debug.Log($"[PizzeriaMigration] Relatório salvo em: {outPath}");
            }
            catch (Exception ex)
            {
                _statusMessage = $"Falha na análise: {ex.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[PizzeriaMigration-ERROR] {ex}");
            }
        }

        private void ExtractVisuals()
        {
            try
            {
                PizzeriaModelPrefabBuilder.BuildVisualPrefabs(_profile);
                PizzeriaModelPrefabBuilder.LinkVisualsToPresentationProfiles();
                _statusMessage = "Modelos visuais extraídos e vinculados aos Presentation Profiles com sucesso!";
                _statusType = MessageType.Info;
            }
            catch (Exception ex)
            {
                _statusMessage = $"Falha na extração de visuais: {ex.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[PizzeriaMigration-ERROR] {ex}");
            }
        }

        private void RunFullIntegration()
        {
            try
            {
                var prefab = PizzeriaFunctionalPrefabBuilder.BuildOrUpdateModularPrefab(_profile);
                _lastReport = PizzeriaMigrationValidator.Validate(_profile);

                if (_lastReport.allPassed)
                {
                    _statusMessage = "Pizzaria modular construída, integrada e validada com 100% de aprovação!";
                    _statusType = MessageType.Info;
                }
                else
                {
                    _statusMessage = "Pizzaria construída, mas foram detectados avisos na validação. Veja o log abaixo.";
                    _statusType = MessageType.Warning;
                }
            }
            catch (Exception ex)
            {
                _statusMessage = $"Falha na integração: {ex.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[PizzeriaMigration-ERROR] {ex}");
            }
        }

        private void RunValidation()
        {
            try
            {
                _lastReport = PizzeriaMigrationValidator.Validate(_profile);
                _statusMessage = _lastReport.allPassed
                    ? "Validação concluída: Todas as verificações passaram!"
                    : "Validação concluída com falhas. Verifique a lista detalhada abaixo.";
                _statusType = _lastReport.allPassed ? MessageType.Info : MessageType.Warning;
            }
            catch (Exception ex)
            {
                _statusMessage = $"Falha na validação: {ex.Message}";
                _statusType = MessageType.Error;
                Debug.LogError($"[PizzeriaMigration-ERROR] {ex}");
            }
        }

        private void RestoreBackup()
        {
            if (EditorUtility.DisplayDialog("Restaurar Backup", "Deseja restaurar o backup anterior do PizzeriaPrefab?", "Sim", "Cancelar"))
            {
                bool ok = PizzeriaFunctionalPrefabBuilder.RestoreBackup();
                _statusMessage = ok ? "Backup restaurado com sucesso!" : "Falha ao restaurar backup.";
                _statusType = ok ? MessageType.Info : MessageType.Error;
            }
        }
    }
}
#endif
