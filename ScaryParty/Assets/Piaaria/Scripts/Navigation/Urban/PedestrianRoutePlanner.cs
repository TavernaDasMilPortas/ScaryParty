using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Navigation.Core;

namespace ScaryParty.Navigation.Urban
{
    /// <summary>
    /// High-level route planner for pedestrians.
    /// Evaluates paths across sidewalks, identifies when crosswalk crossings are needed,
    /// and ensures agents prioritize pedestrian areas over roadways.
    /// </summary>
    public static class PedestrianRoutePlanner
    {
        public static bool TryFindPedestrianPath(Vector3 start, Vector3 destination, out NavMeshPath path)
        {
            path = new NavMeshPath();
            int pedestrianMask = NavigationRegion.GetPedestrianAreaMask();

            if (!NavMesh.SamplePosition(start, out NavMeshHit startHit, 3.0f, pedestrianMask))
            {
                if (!NavMesh.SamplePosition(start, out startHit, 3.0f, NavMesh.AllAreas))
                    return false;
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit destHit, 3.0f, pedestrianMask))
            {
                if (!NavMesh.SamplePosition(destination, out destHit, 3.0f, NavMesh.AllAreas))
                    return false;
            }

            if (NavMesh.CalculatePath(startHit.position, destHit.position, pedestrianMask, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                    return true;
            }

            // Fallback to all areas if strict pedestrian areas cannot connect
            if (NavMesh.CalculatePath(startHit.position, destHit.position, NavMesh.AllAreas, path))
            {
                return path.status == NavMeshPathStatus.PathComplete;
            }

            return false;
        }

        public static PedestrianCrossing FindNearestCrosswalk(Vector3 position, IReadOnlyList<PedestrianCrossing> crossings, float maxRadius = 30f)
        {
            if (crossings == null || crossings.Count == 0) return null;

            PedestrianCrossing nearest = null;
            float bestDistSqr = maxRadius * maxRadius;

            for (int i = 0; i < crossings.Count; i++)
            {
                var c = crossings[i];
                if (c == null) continue;

                float d2 = (c.transform.position - position).sqrMagnitude;
                if (d2 < bestDistSqr)
                {
                    bestDistSqr = d2;
                    nearest = c;
                }
            }

            return nearest;
        }
    }
}
