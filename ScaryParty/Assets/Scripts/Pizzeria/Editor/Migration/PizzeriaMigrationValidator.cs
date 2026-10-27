#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public class ValidationEntry
    {
        public string category;
        public string checkName;
        public bool passed;
        public string message;
    }

    public class ValidationReport
    {
        public bool allPassed = true;
        public List<ValidationEntry> entries = new List<ValidationEntry>();

        public void Add(string category, string checkName, bool passed, string message)
        {
            entries.Add(new ValidationEntry
            {
                category = category,
                checkName = checkName,
                passed = passed,
                message = message
            });

            if (!passed) allPassed = false;
        }

        public string GenerateTextSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("RELATÓRIO DE VALIDAÇÃO DA MIGRAÇÃO DA PIZZARIA");
            sb.AppendLine($"Status Geral: {(allPassed ? "✅ APROVADO COM SUCESSO" : "❌ FALHAS ENCONTRADAS")}");
            sb.AppendLine("================================================================================");

            string currentCat = "";
            foreach (var e in entries)
            {
                if (e.category != currentCat)
                {
                    currentCat = e.category;
                    sb.AppendLine($"\n--- [{currentCat}] ---");
                }

                string status = e.passed ? "✅ PASS" : "❌ FAIL";
                sb.AppendLine($"  {status} | {e.checkName}: {e.message}");
            }

            sb.AppendLine("\n================================================================================");
            return sb.ToString();
        }
    }

    public static class PizzeriaMigrationValidator
    {
        public const string ExpectedCanonicalGuid = "c13e4e6357835224b980d7fe639d0b83";

        public static ValidationReport Validate(PizzeriaFbxImportProfile profile = null)
        {
            var report = new ValidationReport();

            if (profile == null)
            {
                profile = AssetDatabase.LoadAssetAtPath<PizzeriaFbxImportProfile>("Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset");
            }

            // 1. Validação do FBX de Origem
            ValidateSourceFbx(profile, report);

            // 2. Validação do Prefab Canônico
            ValidateCanonicalPrefab(report);

            // 3. Validação dos Modelos Visuais e Perfis de Apresentação
            ValidateVisualsAndProfiles(report);

            // 4. Validação de Rede e Registro de Prefabs
            ValidateNetworkRegistration(report);

            return report;
        }

        private static void ValidateSourceFbx(PizzeriaFbxImportProfile profile, ValidationReport report)
        {
            string path = profile != null ? profile.fbxAssetPath : PizzeriaFbxAnalyzer.DefaultFbxPath;
            bool exists = File.Exists(path);
            report.Add("FBX Origem", "Arquivo FBX Presente", exists, exists ? path : $"Arquivo não encontrado em: {path}");

            if (exists)
            {
                var fbxGo = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                report.Add("FBX Origem", "Asset FBX Válido", fbxGo != null, fbxGo != null ? $"Carregado com {fbxGo.transform.childCount} nós principais" : "Falha ao carregar GameObject do FBX");
            }
        }

        private static void ValidateCanonicalPrefab(ValidationReport report)
        {
            string path = PizzeriaFunctionalPrefabBuilder.CanonicalPrefabPath;
            bool exists = File.Exists(path);
            report.Add("Prefab Canônico", "Arquivo Prefab Presente", exists, exists ? path : $"Prefab ausente em: {path}");

            if (!exists) return;

            string guid = AssetDatabase.AssetPathToGUID(path);
            bool guidMatch = guid.Equals(ExpectedCanonicalGuid, StringComparison.OrdinalIgnoreCase);
            report.Add("Prefab Canônico", "Preservação de GUID Canônico", guidMatch, $"GUID atual: {guid} (Esperado: {ExpectedCanonicalGuid})");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                report.Add("Prefab Canônico", "Carregamento de Prefab", false, "Não foi possível carregar GameObject do Prefab");
                return;
            }

            // Componentes Raiz
            report.Add("Componentes Raiz", "NetworkObject", prefab.GetComponent<NetworkObject>() != null, "NetworkObject presente na raiz");
            var rootComp = prefab.GetComponent<PizzeriaRoot>();
            report.Add("Componentes Raiz", "PizzeriaRoot", rootComp != null, "PizzeriaRoot presente na raiz");
            var footprint = prefab.GetComponent<PizzeriaFootprint>();
            report.Add("Componentes Raiz", "PizzeriaFootprint", footprint != null, "PizzeriaFootprint presente na raiz");
            report.Add("Componentes Raiz", "PizzeriaNetworkState", prefab.GetComponent<PizzeriaNetworkState>() != null, "PizzeriaNetworkState presente");
            report.Add("Componentes Raiz", "PizzeriaCommandHandler", prefab.GetComponent<PizzeriaCommandHandler>() != null, "PizzeriaCommandHandler presente");

            // Âncoras e Config
            if (rootComp != null)
            {
                bool configAssigned = rootComp.Config != null;
                report.Add("PizzeriaRoot", "PizzeriaConfig Atribuído", configAssigned, configAssigned ? $"Config: {rootComp.Config.name}" : "PizzeriaConfig nulo no inspector de PizzeriaRoot");

                bool spawnsOk = rootComp.playerSpawnAnchors != null && rootComp.playerSpawnAnchors.Length >= 4;
                report.Add("PizzeriaRoot", "Spawns de Jogador (>= 4)", spawnsOk, spawnsOk ? $"{rootComp.playerSpawnAnchors.Length} spawns configurados" : "Spawns incompletos");

                bool entOk = rootComp.entranceAnchor != null;
                report.Add("PizzeriaRoot", "EntranceAnchor", entOk, entOk ? rootComp.entranceAnchor.name : "EntranceAnchor nulo");

                bool recOk = rootComp.receivingAnchor != null;
                report.Add("PizzeriaRoot", "ReceivingAnchor", recOk, recOk ? rootComp.receivingAnchor.name : "ReceivingAnchor nulo");

                bool recovOk = rootComp.recoveryAnchor != null;
                report.Add("PizzeriaRoot", "RecoveryAnchor", recovOk, recovOk ? rootComp.recoveryAnchor.name : "RecoveryAnchor nulo");
            }

            // Footprint
            if (footprint != null)
            {
                bool sizeValid = footprint.footprintSize.x > 20f && footprint.footprintSize.y > 20f;
                report.Add("Footprint", "Dimensões Reais da Planta", sizeValid, $"Dimensões: {footprint.footprintSize.x:F1}m x {footprint.footprintSize.y:F1}m");

                bool dirValid = footprint.entranceDirectionLocal.sqrMagnitude > 0.5f;
                report.Add("Footprint", "Vetor de Fachada", dirValid, $"Direção local da entrada: ({footprint.entranceDirectionLocal.x:F1}, {footprint.entranceDirectionLocal.y:F1})");
            }

            // Estações
            ValidateStations(prefab, report);
        }

        private static void ValidateStations(GameObject prefab, ValidationReport report)
        {
            var stations = prefab.GetComponentsInChildren<StationView>(true);
            var foundIds = new HashSet<int>();

            foreach (var st in stations)
            {
                foundIds.Add(st.stationId);
            }

            // Verifica IDs canônicos essenciais 1 a 16
            int missingCount = 0;
            for (int id = 1; id <= 16; id++)
            {
                if (!foundIds.Contains(id))
                {
                    report.Add("Estações Canônicas", $"Estação ID {id}", false, "Estação ausente no prefab!");
                    missingCount++;
                }
            }

            if (missingCount == 0)
            {
                report.Add("Estações Canônicas", "Estações Canônicas 1 a 16", true, "Todas as 16 estações canônicas estão presentes!");
            }

            // Fornos adicionais (17 e 18)
            bool extraOvens = foundIds.Contains(17) && foundIds.Contains(18);
            report.Add("Estações Canônicas", "Fornos Adicionais (17 e 18)", extraOvens, extraOvens ? "Fornos 2 e 3 presentes!" : "Fornos adicionais não encontrados");

            // Slots das estações
            int emptySlotStations = 0;
            foreach (var st in stations)
            {
                // Estações de trabalho que requerem slots
                if (st is PrepStation || st is AssemblyStation || st is OvenStation || st is StagingStation || st is PackagingStation)
                {
                    if (st.slotAnchors == null || st.slotAnchors.Length == 0)
                    {
                        report.Add("Slots de Estação", $"Slots {st.stationName} (ID {st.stationId})", false, "Array slotAnchors vazio!");
                        emptySlotStations++;
                    }
                }
            }

            if (emptySlotStations == 0)
            {
                report.Add("Slots de Estação", "Âncoras de Slots Operacionais", true, "Todas as estações operacionais possuem slotAnchors configurados!");
            }
        }

        private static void ValidateVisualsAndProfiles(ValidationReport report)
        {
            string knifeVisual = $"{PizzeriaModelPrefabBuilder.VisualsFolder}/Visual_Knife.prefab";
            bool knifeExists = File.Exists(knifeVisual);
            report.Add("Modelos Visuais", "Visual_Knife.prefab", knifeExists, knifeExists ? knifeVisual : "Arquivo não encontrado");

            string panVisual = $"{PizzeriaModelPrefabBuilder.VisualsFolder}/Visual_PizzaPan.prefab";
            bool panExists = File.Exists(panVisual);
            report.Add("Modelos Visuais", "Visual_PizzaPan.prefab", panExists, panExists ? panVisual : "Arquivo não encontrado");

            string boxVisual = $"{PizzeriaModelPrefabBuilder.VisualsFolder}/Visual_PizzaBox.prefab";
            bool boxExists = File.Exists(boxVisual);
            report.Add("Modelos Visuais", "Visual_PizzaBox.prefab", boxExists, boxExists ? boxVisual : "Arquivo não encontrado");

            // Perfis de Apresentação
            var knifeProf = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{PizzeriaModelPrefabBuilder.ProfilesFolder}/Profile_Tool_Knife.asset");
            bool knifeProfOk = knifeProf != null && knifeProf.visualPrefab != null;
            report.Add("Apresentação", "Profile_Tool_Knife", knifeProfOk, knifeProfOk ? $"Prefab visual: {knifeProf.visualPrefab.name}" : "visualPrefab nulo ou perfil ausente");

            var pizzaProf = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{PizzeriaModelPrefabBuilder.ProfilesFolder}/Profile_Pizza.asset");
            bool pizzaProfOk = pizzaProf != null && pizzaProf.visualPrefab != null;
            report.Add("Apresentação", "Profile_Pizza", pizzaProfOk, pizzaProfOk ? $"Prefab visual: {pizzaProf.visualPrefab.name}" : "visualPrefab nulo ou perfil ausente");
        }

        private static void ValidateNetworkRegistration(ValidationReport report)
        {
            string listPath = "Assets/DefaultNetworkPrefabs.asset";
            var netList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            bool listExists = netList != null;
            report.Add("Rede Netcode", "DefaultNetworkPrefabs.asset", listExists, listExists ? "Asset carregado" : "DefaultNetworkPrefabs.asset não encontrado");

            if (listExists)
            {
                var canonicalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaFunctionalPrefabBuilder.CanonicalPrefabPath);
                bool registered = false;
                if (canonicalPrefab != null)
                {
                    foreach (var entry in netList.PrefabList)
                    {
                        if (entry.Prefab == canonicalPrefab)
                        {
                            registered = true;
                            break;
                        }
                    }
                }
                report.Add("Rede Netcode", "PizzeriaPrefab Registrado", registered, registered ? "PizzeriaPrefab registrado na lista de rede" : "PizzeriaPrefab não está em DefaultNetworkPrefabs.asset");
            }
        }
    }
}
#endif
