using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Navigation.Core;

namespace ScaryParty.City.Editor
{
    public class CityNavigationValidatorWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Scary Party/City/Navigation & Population Validator")]
        public static void OpenWindow()
        {
            GetWindow<CityNavigationValidatorWindow>("City Nav Validator").Show();
        }

        [Title("Validação de Navegação e População", "Ferramenta de Auditoria Procedural")]
        [InfoBox("Audita a geometria das calçadas, conectividade do NavMesh, entradas de edifícios e distribuição de delivery.")]

        [PropertySpace(10)]
        [ReadOnly]
        public string validationStatus = "Não auditado";

        [ReadOnly]
        public int totalBuildingsFound = 0;

        [ReadOnly]
        public int validEntrancesOnNavMesh = 0;

        [ReadOnly]
        public int reachableToPizzeria = 0;

        [ReadOnly]
        public int deliveryPointsCount = 0;

        [ReadOnly]
        public int pedestrianCrossingsCount = 0;

        [Button("Validar Cidade Atual no Scene", ButtonSizes.Large)]
        [GUIColor(0.2f, 0.8f, 0.3f)]
        public void ValidateCurrentCity()
        {
            CityGenerator generator = UnityEngine.Object.FindFirstObjectByType<CityGenerator>();
            if (generator == null)
            {
                validationStatus = "Erro: Nenhum CityGenerator encontrado na cena.";
                return;
            }

            var buildings = UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
            totalBuildingsFound = buildings.Length;

            var dps = UnityEngine.Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
            deliveryPointsCount = dps.Length;

            var crossings = UnityEngine.Object.FindObjectsByType<PedestrianCrossing>(FindObjectsSortMode.None);
            pedestrianCrossingsCount = crossings.Length;

            Vector3 pizzeriaEntrance = Vector3.zero;
            if (generator.CityData != null && generator.CityData.pizzariaPosition != Vector3.zero)
            {
                pizzeriaEntrance = generator.CityData.pizzariaPosition;
            }

            var report = NavigationValidationService.ValidateCityNavigation(pizzeriaEntrance, buildings);

            validEntrancesOnNavMesh = report.ValidEntrances;
            reachableToPizzeria = report.ReachableEntrancesToPizzeria;
            validationStatus = report.Summary;

            Debug.Log($"[CityNavigationValidator] {validationStatus}");
        }

        [Button("Executar Bateria de Testes (20 Seeds)", ButtonSizes.Medium)]
        [GUIColor(0.3f, 0.6f, 0.9f)]
        public void RunBatchValidation20Seeds()
        {
            CityGenerator generator = UnityEngine.Object.FindFirstObjectByType<CityGenerator>();
            if (generator == null || generator.config == null)
            {
                validationStatus = "Erro: CityGenerator ou CityConfig não configurados.";
                return;
            }

            int successCount = 0;
            int totalSeeds = 20;

            for (int i = 1; i <= totalSeeds; i++)
            {
                int testSeed = i * 1337 + 42;
                generator.config.seed = testSeed;
                generator.GenerateCity();

                var buildings = UnityEngine.Object.FindObjectsByType<CityBuilding>(FindObjectsSortMode.None);
                Vector3 pEntrance = generator.CityData != null ? generator.CityData.pizzariaPosition : Vector3.zero;
                var report = NavigationValidationService.ValidateCityNavigation(pEntrance, buildings);

                if (report.ValidEntrances > 0 && report.ValidEntrances == report.TotalBuildings)
                {
                    successCount++;
                }
            }

            validationStatus = $"Bateria de 20 Seeds Concluída: {successCount}/{totalSeeds} seeds com 100% das entradas no NavMesh.";
            Debug.Log($"[CityNavigationValidator] {validationStatus}");
        }
    }
}
