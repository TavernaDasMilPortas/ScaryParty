#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public static class PizzeriaMigrationRunner
    {
        public static void RunAnalysisFromCommandLine()
        {
            try
            {
                Debug.Log("[PizzeriaMigration] Executando Análise Determinística do FBX...");
                var result = PizzeriaFbxAnalyzer.Analyze();
                string report = PizzeriaFbxAnalyzer.GenerateReport(result);

                string outPath = Path.Combine(Directory.GetCurrentDirectory(), "PizzeriaFbxInventoryReport.txt");
                File.WriteAllText(outPath, report);
                Debug.Log($"[PizzeriaMigration] ✅ Relatório de análise salvo em: {outPath}");
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PizzeriaMigration-ERROR] Falha na análise: {ex}");
                EditorApplication.Exit(1);
            }
        }

        public static void RunMigrationFromCommandLine()
        {
            try
            {
                Debug.Log("[PizzeriaMigration] ========================================");
                Debug.Log("[PizzeriaMigration] INICIANDO MIGRAÇÃO MODULAR COMPLETA (CLI)");
                Debug.Log("[PizzeriaMigration] ========================================");

                // 1. Carrega ou cria perfil
                string profilePath = "Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset";
                var profile = AssetDatabase.LoadAssetAtPath<PizzeriaFbxImportProfile>(profilePath);
                if (profile == null)
                {
                    if (!Directory.Exists("Assets/ScriptableObjects/Pizzeria/Import"))
                        Directory.CreateDirectory("Assets/ScriptableObjects/Pizzeria/Import");

                    profile = PizzeriaFbxImportProfile.CreateDefaultProfile();
                    AssetDatabase.CreateAsset(profile, profilePath);
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[PizzeriaMigration] Perfil padrão criado em: {profilePath}");
                }

                // 2. Extrai modelos visuais e conecta perfis
                Debug.Log("[PizzeriaMigration] Extraindo modelos visuais reutilizáveis...");
                PizzeriaModelPrefabBuilder.BuildVisualPrefabs(profile);
                PizzeriaModelPrefabBuilder.LinkVisualsToPresentationProfiles();

                // 3. Constrói Prefab Funcional Modular
                Debug.Log("[PizzeriaMigration] Construindo Prefab Funcional modular da Pizzaria...");
                var prefab = PizzeriaFunctionalPrefabBuilder.BuildOrUpdateModularPrefab(profile);

                // 4. Validação
                Debug.Log("[PizzeriaMigration] Executando validação de integridade...");
                var report = PizzeriaMigrationValidator.Validate(profile);
                string reportSummary = report.GenerateTextSummary();

                string outPath = Path.Combine(Directory.GetCurrentDirectory(), "PizzeriaMigrationValidationReport.txt");
                File.WriteAllText(outPath, reportSummary);
                Debug.Log(reportSummary);

                if (report.allPassed)
                {
                    Debug.Log("[PizzeriaMigration] ✅ MIGRAÇÃO CONCLUÍDA E VALIDADA COM 100% DE SUCESSO!");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError("[PizzeriaMigration] ❌ MIGRAÇÃO CONCLUÍDA COM AVISOS/FALHAS! Verifique o relatório.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PizzeriaMigration-ERROR] Exceção crítica durante a migração: {ex}");
                EditorApplication.Exit(2);
            }
        }

        public static void RunValidationFromCommandLine()
        {
            try
            {
                Debug.Log("[PizzeriaMigration] Executando Validação (CLI)...");
                var report = PizzeriaMigrationValidator.Validate();
                string summary = report.GenerateTextSummary();
                Debug.Log(summary);

                string outPath = Path.Combine(Directory.GetCurrentDirectory(), "PizzeriaMigrationValidationReport.txt");
                File.WriteAllText(outPath, summary);

                EditorApplication.Exit(report.allPassed ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PizzeriaMigration-ERROR] Falha na validação: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
