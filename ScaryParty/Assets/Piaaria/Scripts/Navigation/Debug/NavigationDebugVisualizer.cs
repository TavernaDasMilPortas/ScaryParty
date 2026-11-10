using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Navigation.Agents;

namespace ScaryParty.Navigation.Debug
{
    /// <summary>
    /// Debug visualizer for city navigation: displays building entrance anchors,
    /// crosswalk waiting points, traffic signal states, and NPC paths in Scene view.
    /// </summary>
    public class NavigationDebugVisualizer : MonoBehaviour
    {
        [Header("Visualization Options")]
        public bool showBuildingAnchors = true;
        public bool showCrosswalks = true;
        public bool showActiveNpcPaths = true;

        private void OnDrawGizmos()
        {
            if (showBuildingAnchors && CityBuildingRegistry.Instance != null)
            {
                var buildings = CityBuildingRegistry.Instance.AllBuildings;
                for (int i = 0; i < buildings.Count; i++)
                {
                    var b = buildings[i];
                    if (b == null) continue;

                    Gizmos.color = Color.yellow;
                    Gizmos.DrawSphere(b.entrancePosition, 0.4f);

                    if (b.sidewalkApproachAnchor != null)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawSphere(b.sidewalkApproachAnchor.position, 0.3f);
                        Gizmos.DrawLine(b.entrancePosition, b.sidewalkApproachAnchor.position);
                    }
                }
            }

            if (showCrosswalks)
            {
                var crossings = FindObjectsByType<PedestrianCrossing>(FindObjectsSortMode.None);
                for (int i = 0; i < crossings.Length; i++)
                {
                    var c = crossings[i];
                    if (c == null) continue;

                    Gizmos.color = c.CanBeginCrossing() ? Color.green : Color.red;
                    Gizmos.DrawSphere(c.waitingPointA, 0.4f);
                    Gizmos.DrawSphere(c.waitingPointB, 0.4f);
                    Gizmos.DrawLine(c.waitingPointA, c.waitingPointB);
                }
            }

            if (showActiveNpcPaths)
            {
                var motors = FindObjectsByType<NpcNavigationMotor>(FindObjectsSortMode.None);
                for (int i = 0; i < motors.Length; i++)
                {
                    var m = motors[i];
                    if (m == null || m.Agent == null || !m.Agent.hasPath) continue;

                    var corners = m.Agent.path.corners;
                    if (corners != null && corners.Length > 1)
                    {
                        Gizmos.color = Color.magenta;
                        for (int j = 0; j < corners.Length - 1; j++)
                        {
                            Gizmos.DrawLine(corners[j], corners[j + 1]);
                        }
                    }
                }
            }
        }
    }
}
