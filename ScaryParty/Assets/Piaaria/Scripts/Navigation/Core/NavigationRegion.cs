using System;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Navigation.Core
{
    /// <summary>
    /// Logical areas and cost masks for city and interior navigation.
    /// Allows different agent profiles (Pedestrians, Customers, Monsters)
    /// to navigate with appropriate spatial preferences.
    /// </summary>
    public static class NavigationRegion
    {
        public const string AreaDefault = "Walkable";
        public const string AreaNotWalkable = "Not Walkable";
        public const string AreaJump = "Jump";
        
        // Custom area names
        public const string AreaSidewalk = "Sidewalk";
        public const string AreaCrosswalk = "Crosswalk";
        public const string AreaRoad = "Road";
        public const string AreaBuildingEntrance = "BuildingEntrance";
        public const string AreaPizzeriaInterior = "PizzeriaInterior";

        public static int GetAreaMask(string areaName)
        {
            int areaIndex = NavMesh.GetAreaFromName(areaName);
            if (areaIndex < 0) return NavMesh.AllAreas;
            return 1 << areaIndex;
        }

        public static int GetPedestrianAreaMask()
        {
            // Pedestrians prefer Sidewalks, Crosswalks, BuildingEntrances, and Default walkable.
            // They avoid running on active roads unless crossing.
            int mask = 0;
            mask |= GetAreaMask(AreaDefault);
            mask |= GetAreaMask(AreaSidewalk);
            mask |= GetAreaMask(AreaCrosswalk);
            mask |= GetAreaMask(AreaBuildingEntrance);
            mask |= GetAreaMask(AreaPizzeriaInterior);
            return mask != 0 ? mask : NavMesh.AllAreas;
        }

        public static int GetMonsterAreaMask()
        {
            // Monsters can walk on all areas, including roads and open terrain.
            return NavMesh.AllAreas;
        }
    }
}
