using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class AuditR2BaselineTests
    {
        [Test]
        public void PizzeriaLayoutProfile_GetActivePatioRect_SwitchesByVariant()
        {
            var profile = ScriptableObject.CreateInstance<PizzeriaLayoutProfile>();
            try
            {
                profile.variant = PizzeriaLayoutVariant.L_RecuoOperacional;
                Assert.AreEqual(profile.patioRectL, profile.GetActivePatioRect());

                profile.variant = PizzeriaLayoutVariant.T_PatioLateral;
                Assert.AreEqual(profile.patioRectT, profile.GetActivePatioRect());
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PizzeriaExteriorAnchors_CompletenessChecks_RequireAllTransforms()
        {
            var go = new GameObject("AnchorsTest");
            try
            {
                var anchors = go.AddComponent<PizzeriaExteriorAnchors>();
                Assert.IsFalse(anchors.HasCompleteExteriorAnchors());
                Assert.IsFalse(anchors.HasCompleteSpawnAnchors());

                anchors.motorcycleSpawn_01 = go.transform;
                anchors.motorcycleRecovery = go.transform;
                anchors.dismountLeft = go.transform;
                anchors.dismountRight = go.transform;
                anchors.dispatch = go.transform;
                anchors.supplierReceiving = go.transform;
                anchors.weaponRack = go.transform;
                anchors.practiceTarget = go.transform;
                anchors.practiceShooter = go.transform;
                anchors.exteriorGround = go.transform;
                anchors.streetExit = go.transform;
                Assert.IsTrue(anchors.HasCompleteExteriorAnchors());

                anchors.playerSpawns = new[] { go.transform, go.transform, go.transform, go.transform };
                anchors.playerRecovery = go.transform;
                anchors.employeeArrival = go.transform;
                anchors.employeeSpawnCashier = go.transform;
                anchors.employeeSpawnCook = go.transform;
                anchors.employeeWaiting = go.transform;
                anchors.employeeRecovery = go.transform;
                Assert.IsTrue(anchors.HasCompleteSpawnAnchors());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
