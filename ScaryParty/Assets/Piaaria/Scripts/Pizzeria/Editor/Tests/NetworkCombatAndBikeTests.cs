using NUnit.Framework;
using UnityEngine;
using ScaryParty.City;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class NetworkCombatAndBikeTests
    {
        [Test]
        public void MotorcycleMovementBudget_MaxDistancePerSecond_IsIndependentOfRpcRate()
        {
            float SimulateOneSecond(int rpcPerSecond)
            {
                var budget = new MotorcycleMovementBudget(22f, 1.5f);
                Vector3 pos = Vector3.zero;
                double t = 10.0;
                double step = 1.0 / rpcPerSecond;
                for (int i = 0; i < rpcPerSecond; i++)
                {
                    t += step;
                    // Cliente tenta enviar teleporte distante a cada RPC
                    pos = budget.ConsumeStep(t, pos, pos + new Vector3(0f, 0f, 50f), out _);
                }
                return pos.z;
            }

            float dist10 = SimulateOneSecond(10);
            float dist20 = SimulateOneSecond(20);
            float dist50 = SimulateOneSecond(50);
            float dist100 = SimulateOneSecond(100);

            // Todas as taxas devem convergir para ~22m/s (com tolerância do cap de burst)
            Assert.That(dist10, Is.InRange(21.5f, 23.5f));
            Assert.That(dist20, Is.InRange(21.5f, 23.5f));
            Assert.That(dist50, Is.InRange(21.5f, 23.5f));
            Assert.That(dist100, Is.InRange(21.5f, 23.5f));
            Assert.Less(Mathf.Abs(dist100 - dist10), 1.0f);
        }

        [Test]
        public void MotorcycleMovementBudget_Reset_ClearsAccumulatedTimeAndBudget()
        {
            var budget = new MotorcycleMovementBudget(22f, 1.5f);
            Vector3 p1 = budget.ConsumeStep(10.0, Vector3.zero, new Vector3(0f, 0f, 10f), out _);
            Assert.Greater(p1.z, 0f);

            budget.Reset();
            Assert.AreEqual(-1.0, budget.LastServerTime);
            Assert.AreEqual(0f, budget.AvailableBudgetMeters);
        }

        [Test]
        public void SpectatorSentinel_UsesMaxValueInsteadOfHostZero()
        {
            Assert.AreEqual(ulong.MaxValue, ScaryParty.Combat.PlayerHealth.NoSpectatorTarget);
            Assert.AreEqual(ulong.MaxValue, ScaryParty.Pizzeria.Network.PizzeriaCommandHandler.NoSpectatorTarget);
        }

        [Test]
        public void NeutralBenchClassifier_AcceptsPrepAndRejectsTrashStorageOven()
        {
            var go = new GameObject("TestStations_Lifecycle");
            try
            {
                var trash = go.AddComponent<ScaryParty.Pizzeria.Stations.TrashStation>();
                var storage = go.AddComponent<ScaryParty.Pizzeria.Stations.StorageStation>();
                var oven = go.AddComponent<ScaryParty.Pizzeria.Stations.OvenStation>();
                var prepGo = new GameObject("Prep");
                prepGo.transform.SetParent(go.transform);
                var prep = prepGo.AddComponent<ScaryParty.Pizzeria.Stations.PrepStation>();
                var baseGo = new GameObject("Neutral");
                baseGo.transform.SetParent(go.transform);
                var neutral = baseGo.AddComponent<ScaryParty.Pizzeria.Stations.StationView>();

                Assert.IsFalse(ScaryParty.Pizzeria.Network.PlayerLifecycleBridge.IsNeutralBenchStation(trash));
                Assert.IsFalse(ScaryParty.Pizzeria.Network.PlayerLifecycleBridge.IsNeutralBenchStation(storage));
                Assert.IsFalse(ScaryParty.Pizzeria.Network.PlayerLifecycleBridge.IsNeutralBenchStation(oven));
                Assert.IsTrue(ScaryParty.Pizzeria.Network.PlayerLifecycleBridge.IsNeutralBenchStation(prep));
                Assert.IsTrue(ScaryParty.Pizzeria.Network.PlayerLifecycleBridge.IsNeutralBenchStation(neutral));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
