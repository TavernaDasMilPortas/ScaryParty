#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [InitializeOnLoad]
    public static class PizzeriaContentStudioTestRunner
    {
        private const string ResultFilePath = "Temp/StudioTestsResult.txt";

        static PizzeriaContentStudioTestRunner()
        {
            EditorApplication.delayCall += RunAllStudioTests;
        }

        [MenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Executar Testes de Autoria & Slots", false, 111)]
        public static void RunAllStudioTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("===============================================================");
            sb.AppendLine("  EXECUTANDO SUITE DE TESTES: CRIADOR VISUAL DE CONTEÚDO ODIN  ");
            sb.AppendLine("===============================================================");

            int totalTests = 0;
            int passedTests = 0;
            int failedTests = 0;

            // 1. Testes de Layout e Critério de Aceite das 2 Bancadas
            var layoutTests = new PizzeriaSlotLayoutTests();
            RunTest("AcceptanceCriterion_TwoCountersShareGrid_OneWithManualShift_SpacingUpdatePreservesManualSlot",
                () => {
                    layoutTests.SetUp();
                    try { layoutTests.AcceptanceCriterion_TwoCountersShareGrid_OneWithManualShift_SpacingUpdatePreservesManualSlot(); }
                    finally { layoutTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            RunTest("StationSlotLayout_PresetTypes_ProduceCorrectSlotCountsAndPositions",
                () => {
                    layoutTests.SetUp();
                    try { layoutTests.StationSlotLayout_PresetTypes_ProduceCorrectSlotCountsAndPositions(); }
                    finally { layoutTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            RunTest("StationSlotLayout_Validation_DetectsAnomalies",
                () => {
                    layoutTests.SetUp();
                    try { layoutTests.StationSlotLayout_Validation_DetectsAnomalies(); }
                    finally { layoutTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            // 2. Testes do Estúdio, Pinning e Avaliação de Altura de Corte
            var studioTests = new PizzeriaContentStudioTests();
            RunTest("StudioSession_PinAssembly_LocksBenchWithTomatoAndKnifeAcrossInspections",
                () => {
                    studioTests.SetUp();
                    try { studioTests.StudioSession_PinAssembly_LocksBenchWithTomatoAndKnifeAcrossInspections(); }
                    finally { studioTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            RunTest("ProcessSimulation_ScrubberProgress_ControlsStagesAccurately",
                () => {
                    studioTests.SetUp();
                    try { studioTests.ProcessSimulation_ScrubberProgress_ControlsStagesAccurately(); }
                    finally { studioTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            RunTest("ToolContactAndCuttingHeight_EvaluatesContactCorrectly",
                () => {
                    studioTests.SetUp();
                    try { studioTests.ToolContactAndCuttingHeight_EvaluatesContactCorrectly(); }
                    finally { studioTests.TearDown(); }
                },
                ref totalTests, ref passedTests, ref failedTests, sb);

            sb.AppendLine("===============================================================");
            sb.AppendLine($"  RESULTADO: {passedTests}/{totalTests} TESTES PASSARAM ({failedTests} falhas)");
            sb.AppendLine("===============================================================");

            string output = sb.ToString();
            try
            {
                File.WriteAllText(ResultFilePath, output);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Não foi possível gravar {ResultFilePath}: {ex.Message}");
            }

            if (failedTests == 0)
            {
                Debug.Log($"<color=green>[PIZZERIA_STUDIO_TESTS_PASSED]</color>\n{output}");
            }
            else
            {
                Debug.LogError($"<color=red>[PIZZERIA_STUDIO_TESTS_FAILED]</color>\n{output}");
            }
        }

        private static void RunTest(
            string testName,
            Action testAction,
            ref int total,
            ref int passed,
            ref int failed,
            StringBuilder sb)
        {
            total++;
            try
            {
                testAction();
                passed++;
                sb.AppendLine($"  [OK] {testName}");
            }
            catch (Exception ex)
            {
                failed++;
                sb.AppendLine($"  [FAIL] {testName}: {ex.Message}");
            }
        }
    }
}
#endif
