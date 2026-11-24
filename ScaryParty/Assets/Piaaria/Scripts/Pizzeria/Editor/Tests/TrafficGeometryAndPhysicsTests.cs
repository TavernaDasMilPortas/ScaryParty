using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class TrafficGeometryAndPhysicsTests
    {
        [Test]
        public void ComputeSafeLaneOffset_ScalesWithStreetWidthAndNeverExceedsCurbMargin()
        {
            float vehW = TrafficManager.VehicleDimensions.x;
            float narrow = TrafficManager.ComputeSafeLaneOffset(4.5f, vehW);
            float standard = TrafficManager.ComputeSafeLaneOffset(6.0f, vehW);
            float wide = TrafficManager.ComputeSafeLaneOffset(10.0f, vehW);

            Assert.Less(narrow, standard);
            Assert.Less(standard, wide);
            Assert.LessOrEqual(narrow + vehW * 0.5f, 4.5f * 0.5f - 0.19f);
            Assert.LessOrEqual(standard + vehW * 0.5f, 6.0f * 0.5f - 0.19f);
            Assert.LessOrEqual(wide + vehW * 0.5f, 10.0f * 0.5f - 0.19f);
        }

        [Test]
        public void BuildLaneAndCornerWaypoints_KeepsEnvelopeInsideAvenueOn90DegreeTurn()
        {
            var centerPath = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f)
            };

            float streetWidth = 7.0f;
            float vehW = TrafficManager.VehicleDimensions.x;
            var waypoints = TrafficManager.BuildLaneAndCornerWaypoints(centerPath, streetWidth, vehW);

            Assert.GreaterOrEqual(waypoints.Count, 4);
            Assert.IsTrue(TrafficManager.IsEnvelopeContainedInCorridor(waypoints, centerPath, streetWidth, vehW));
        }

        [Test]
        public void VehicleDimensions_ArePositiveAndConsistent()
        {
            Assert.Greater(TrafficManager.VehicleDimensions.x, 1.5f);
            Assert.Greater(TrafficManager.VehicleDimensions.y, 0.8f);
            Assert.Greater(TrafficManager.VehicleDimensions.z, 3.0f);
        }

        [Test]
        public void BuildLaneAndCornerWaypoints_MapsEdgeIdsAlongMultiSegmentRoute()
        {
            var centerPath = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(0f, 0f, 20f),
                new Vector3(20f, 0f, 20f)
            };

            var waypoints = TrafficManager.BuildLaneAndCornerWaypoints(
                centerPath,
                7.0f,
                TrafficManager.VehicleDimensions.x,
                (a, b) => a.x == b.x ? 101 : 202,
                out var edgeIds);

            Assert.AreEqual(waypoints.Count, edgeIds.Count);
            Assert.AreEqual(101, edgeIds[0]);
            Assert.AreEqual(202, edgeIds[edgeIds.Count - 1]);
        }

        [Test]
        public void MotorcycleExitDriveway_AllowsCrossingSidewalkBetweenPatioAndStreet()
        {
            var parkPos = new Vector3(10f, 0.2f, 10f);
            var exitAnchor = new Vector3(10f, 0.2f, 15f);
            float sidewalkWidth = 3.5f;

            // Ponto sobre a calçada a 4m além da âncora de saída
            Assert.IsTrue(ScaryParty.City.DeliveryMotorcycleController.IsPointInExitDriveway(
                new Vector3(10f, 0.2f, 19f), parkPos, Quaternion.identity, exitAnchor, sidewalkWidth));

            // Ponto muito afastado lateralmente para dentro dos prédios vizinhos
            Assert.IsFalse(ScaryParty.City.DeliveryMotorcycleController.IsPointInExitDriveway(
                new Vector3(20f, 0.2f, 19f), parkPos, Quaternion.identity, exitAnchor, sidewalkWidth));
        }
    }
}
