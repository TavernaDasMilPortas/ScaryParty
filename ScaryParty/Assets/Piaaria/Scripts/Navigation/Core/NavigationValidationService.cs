using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Navigation.Core
{
    public struct ValidationReport
    {
        public int TotalBuildings;
        public int ValidEntrances;
        public int ReachableEntrancesToPizzeria;
        public int DisconnectedEntrances;
        public bool PizzeriaReachable;
        public string Summary;
    }

    /// <summary>
    /// Validates path connectivity between buildings, crosswalks, and the pizzeria.
    /// Can be executed in Editor or at runtime after city generation.
    /// </summary>
    public static class NavigationValidationService
    {
        public static ValidationReport ValidateCityNavigation(Vector3 pizzeriaEntrance, IReadOnlyList<CityBuilding> buildings)
        {
            ValidationReport report = new ValidationReport();
            if (buildings == null || buildings.Count == 0)
            {
                report.Summary = "No buildings registered to validate.";
                return report;
            }

            report.TotalBuildings = buildings.Count;
            bool hasPizzeria = pizzeriaEntrance != Vector3.zero;
            report.PizzeriaReachable = hasPizzeria && NavMesh.SamplePosition(pizzeriaEntrance, out _, 2.5f, NavMesh.AllAreas);

            NavMeshPath path = new NavMeshPath();

            for (int i = 0; i < buildings.Count; i++)
            {
                var b = buildings[i];
                if (b == null) continue;

                Vector3 entrance = b.entrancePosition;
                if (!NavMesh.SamplePosition(entrance, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                {
                    report.DisconnectedEntrances++;
                    continue;
                }

                report.ValidEntrances++;

                if (hasPizzeria)
                {
                    if (NavMesh.CalculatePath(hit.position, pizzeriaEntrance, NavMesh.AllAreas, path) &&
                        path.status == NavMeshPathStatus.PathComplete)
                    {
                        report.ReachableEntrancesToPizzeria++;
                    }
                    else
                    {
                        report.DisconnectedEntrances++;
                    }
                }
            }

            report.Summary = $"Navigation Validation: {report.ValidEntrances}/{report.TotalBuildings} entrances sampled on NavMesh. " +
                             $"{(hasPizzeria ? $"{report.ReachableEntrancesToPizzeria} connected to Pizzeria." : "No pizzeria placed.")}";

            return report;
        }
    }
}
