using UnityEngine;

namespace ScaryParty.Navigation.Core
{
    public enum NavigationAnchorType
    {
        Generic,
        BuildingEntrance,
        SidewalkApproach,
        CrosswalkWaitingPoint,
        PizzeriaEntrance,
        QueueSlot,
        DiningSeat
    }

    /// <summary>
    /// Explicit navigation waypoint/anchor placed in the city or interior.
    /// Used by pathfinders and directors to query verified walkable locations.
    /// </summary>
    public class NavigationAnchor : MonoBehaviour
    {
        public NavigationAnchorType anchorType = NavigationAnchorType.Generic;
        public string anchorId;
        public float samplingRadius = 1.5f;

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;

        private void OnDrawGizmos()
        {
            Gizmos.color = anchorType == NavigationAnchorType.BuildingEntrance ? Color.yellow :
                           anchorType == NavigationAnchorType.CrosswalkWaitingPoint ? Color.green :
                           anchorType == NavigationAnchorType.PizzeriaEntrance ? Color.magenta : Color.blue;

            Gizmos.DrawSphere(transform.position, 0.25f);
        }
    }
}
