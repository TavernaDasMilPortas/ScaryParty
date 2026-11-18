using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Combat.Monsters;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class P1_MonsterEmployeeClientSyncTests
    {
        [Test]
        public void NetMonsterDto_Equals_DetectsYawRotationChangeAboveTwoDegrees()
        {
            var a = new NetMonsterDto
            {
                MonsterId = 1,
                Species = (byte)NpcSpecies.Vampire,
                BehaviorState = (byte)NpcBehaviorState.Routine,
                TelegraphKind = 0,
                CurrentHp = 130,
                MaxHp = 130,
                PosX = 10f,
                PosY = 0f,
                PosZ = 10f,
                Yaw = 0f,
                IsAlerted = false,
                Revision = 1
            };

            var b = a;
            b.Yaw = 45f;
            Assert.IsFalse(a.Equals(b), "NetMonsterDto.Equals deve detectar rotação Yaw >= 2 graus.");

            var c = a;
            c.Yaw = 1.0f;
            Assert.IsTrue(a.Equals(c), "NetMonsterDto.Equals deve tolerar micro-oscilação < 2 graus.");
        }

        [Test]
        public void NetEmployeeDto_Equals_TracksYawHpAndUnitProgress()
        {
            var a = new NetEmployeeDto
            {
                EmployeeId = 1,
                Role = (byte)EmployeeRole.Pizzaiolo,
                Level = 1,
                IsAlive = true,
                CurrentHp = 100,
                MaxHp = 100,
                PosX = 1f,
                PosY = 0f,
                PosZ = 2f,
                Yaw = 15f,
                UnitsCompleted = 0,
                UnitsRequired = 3,
                Revision = 1
            };

            var b = a;
            b.UnitsCompleted = 1;
            Assert.IsFalse(a.Equals(b), "NetEmployeeDto.Equals deve detectar progresso de unidade.");

            var c = a;
            c.CurrentHp = 75;
            Assert.IsFalse(a.Equals(c), "NetEmployeeDto.Equals deve detectar dano no funcionário.");

            var d = a;
            d.Yaw = 95f;
            Assert.IsFalse(a.Equals(d), "NetEmployeeDto.Equals deve detectar mudança de Yaw.");
        }

        [Test]
        public void MonsterNpcController_ClientProxy_ReceivesHpStateTelegraphWithoutDomainState()
        {
            var go = new GameObject("TestMonsterProxy");
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(go.transform, false);
            try
            {
                var ctrl = go.AddComponent<MonsterNpcController>();
                var initial = new NetMonsterDto
                {
                    MonsterId = 77,
                    Species = (byte)NpcSpecies.Werewolf,
                    BehaviorState = (byte)NpcBehaviorState.Routine,
                    TelegraphKind = 0,
                    CurrentHp = 160,
                    MaxHp = 160,
                    PosX = 3f,
                    PosY = 0f,
                    PosZ = 4f,
                    Yaw = 90f,
                    IsAlerted = false,
                    Revision = 1
                };

                ctrl.ConfigureClientProxy(77, NpcSpecies.Werewolf, null, initial);
                Assert.IsTrue(ctrl.IsAlive);
                Assert.AreEqual(160, ctrl.CurrentHealth);
                Assert.AreEqual(NpcSpecies.Werewolf, ctrl.Species);
                Assert.AreEqual(0, ctrl.CurrentTelegraphKind);

                var lunge = initial;
                lunge.BehaviorState = (byte)NpcBehaviorState.Attacking;
                lunge.TelegraphKind = 2;
                lunge.CurrentHp = 110;
                lunge.IsAlerted = true;
                lunge.Revision = 2;
                ctrl.ApplyReplicatedDto(lunge);

                Assert.AreEqual(NpcBehaviorState.Attacking, ctrl.CurrentBehaviorState);
                Assert.AreEqual(110, ctrl.CurrentHealth);
                Assert.AreEqual(2, ctrl.CurrentTelegraphKind);

                var dead = lunge;
                dead.BehaviorState = (byte)NpcBehaviorState.Dead;
                dead.CurrentHp = 0;
                dead.Revision = 3;
                ctrl.ApplyReplicatedDto(dead);

                Assert.IsFalse(ctrl.IsAlive);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MonsterPopulationDirector_SyncClientProxiesFromSnapshot_CreatesUpdatesRemovesWithoutDuplicates()
        {
            var hostGo = new GameObject("TestMonsterDirectorHost");
            MonsterPopulationDirector director = null;
            try
            {
                director = hostGo.AddComponent<MonsterPopulationDirector>();

                var dto10 = new NetMonsterDto
                {
                    MonsterId = 10,
                    Species = (byte)NpcSpecies.Vampire,
                    BehaviorState = (byte)NpcBehaviorState.Routine,
                    TelegraphKind = 1,
                    CurrentHp = 130,
                    MaxHp = 130,
                    PosX = 2f,
                    PosY = 3f,
                    PosZ = 4f,
                    Yaw = 30f,
                    IsAlerted = false,
                    Revision = 1
                };
                var dto20 = new NetMonsterDto
                {
                    MonsterId = 20,
                    Species = (byte)NpcSpecies.Zombie,
                    BehaviorState = (byte)NpcBehaviorState.Aggressive,
                    TelegraphKind = 0,
                    CurrentHp = 90,
                    MaxHp = 90,
                    PosX = -5f,
                    PosY = 0f,
                    PosZ = 6f,
                    Yaw = 120f,
                    IsAlerted = true,
                    Revision = 1
                };

                director.SyncClientProxiesFromSnapshot(new List<NetMonsterDto> { dto10, dto20 });
                Assert.AreEqual(2, director.ClientProxyCount, "Snapshot inicial deve criar exatamente 1 proxy por ID.");
                Assert.IsTrue(director.ClientProxies.TryGetValue(10, out var proxy10Before));
                Assert.NotNull(proxy10Before);
                var col10 = proxy10Before.GetComponent<Collider>();
                Assert.NotNull(col10);
                Assert.IsTrue(col10.enabled);

                // Atualizar 10 para morto, manter 20 e adicionar 30 -> não duplica existentes
                var dto10Dead = dto10;
                dto10Dead.BehaviorState = (byte)NpcBehaviorState.Dead;
                dto10Dead.CurrentHp = 0;
                dto10Dead.Revision = 2;

                var dto30 = new NetMonsterDto
                {
                    MonsterId = 30,
                    Species = (byte)NpcSpecies.Ghost,
                    BehaviorState = (byte)NpcBehaviorState.Routine,
                    TelegraphKind = 3,
                    CurrentHp = 80,
                    MaxHp = 80,
                    PosX = 8f,
                    PosY = 1f,
                    PosZ = -2f,
                    Yaw = 210f,
                    IsAlerted = false,
                    Revision = 1
                };

                director.SyncClientProxiesFromSnapshot(new List<NetMonsterDto> { dto10Dead, dto20, dto30 });
                Assert.AreEqual(3, director.ClientProxyCount);
                Assert.AreSame(proxy10Before, director.ClientProxies[10], "Proxy existente deve ser atualizado sem duplicação.");
                Assert.IsFalse(director.ClientProxies[10].IsAlive);
                Assert.IsFalse(col10.enabled, "Proxy morto deve desabilitar Collider no cliente.");

                // Remover 20 do snapshot -> proxy 20 deve ser destruído
                director.SyncClientProxiesFromSnapshot(new List<NetMonsterDto> { dto10Dead, dto30 });
                Assert.AreEqual(2, director.ClientProxyCount);
                Assert.IsFalse(director.ClientProxies.ContainsKey(20));

                director.ClearOnSessionEnd();
                Assert.AreEqual(0, director.ClientProxyCount, "ClearOnSessionEnd deve destruir todos os proxies.");
            }
            finally
            {
                if (director != null) director.ClearOnSessionEnd();
                Object.DestroyImmediate(hostGo);
            }
        }

        [Test]
        public void EmployeePopulationDirector_SyncClientProxiesFromSnapshot_CreatesUpdatesRemovesAndCleansUp()
        {
            EmployeePopulationDirector.ClearAll();
            try
            {
                var emp1 = new NetEmployeeDto
                {
                    EmployeeId = 1,
                    Role = (byte)EmployeeRole.Cashier,
                    Level = 1,
                    IsAlive = true,
                    CurrentHp = 100,
                    MaxHp = 100,
                    PosX = 1f,
                    PosY = 0f,
                    PosZ = 1f,
                    Yaw = 0f,
                    Revision = 1
                };
                var emp2 = new NetEmployeeDto
                {
                    EmployeeId = 2,
                    Role = (byte)EmployeeRole.Pizzaiolo,
                    Level = 1,
                    IsAlive = true,
                    CurrentHp = 100,
                    MaxHp = 100,
                    PosX = 4f,
                    PosY = 0f,
                    PosZ = 2f,
                    Yaw = 90f,
                    UnitsCompleted = 0,
                    UnitsRequired = 2,
                    Revision = 1
                };

                EmployeePopulationDirector.SyncClientProxiesFromSnapshot(new List<NetEmployeeDto> { emp1, emp2 });
                Assert.AreEqual(2, EmployeePopulationDirector.ClientProxyCount);
                Assert.IsTrue(EmployeePopulationDirector.TryGetClientProxy(2, out var proxy2Before));
                Assert.NotNull(proxy2Before);
                var col2 = proxy2Before.GetComponent<Collider>();
                Assert.NotNull(col2);
                Assert.IsTrue(col2.enabled);

                // Matar emp2 e adicionar emp3
                var emp2Dead = emp2;
                emp2Dead.IsAlive = false;
                emp2Dead.CurrentHp = 0;
                emp2Dead.Revision = 2;

                var emp3 = new NetEmployeeDto
                {
                    EmployeeId = 3,
                    Role = (byte)EmployeeRole.Pizzaiolo,
                    Level = 2,
                    IsAlive = true,
                    CurrentHp = 100,
                    MaxHp = 100,
                    PosX = 6f,
                    PosY = 0f,
                    PosZ = 3f,
                    Yaw = 180f,
                    Revision = 1
                };

                EmployeePopulationDirector.SyncClientProxiesFromSnapshot(new List<NetEmployeeDto> { emp1, emp2Dead, emp3 });
                Assert.AreEqual(3, EmployeePopulationDirector.ClientProxyCount);
                Assert.IsTrue(EmployeePopulationDirector.TryGetClientProxy(2, out var proxy2After));
                Assert.AreSame(proxy2Before, proxy2After);
                Assert.IsFalse(proxy2After.IsAlive);
                Assert.IsFalse(col2.enabled, "Proxy de funcionário morto deve desabilitar Collider.");

                // Remover emp2 -> destruído
                EmployeePopulationDirector.SyncClientProxiesFromSnapshot(new List<NetEmployeeDto> { emp1, emp3 });
                Assert.AreEqual(2, EmployeePopulationDirector.ClientProxyCount);
                Assert.IsFalse(EmployeePopulationDirector.TryGetClientProxy(2, out _));

                EmployeePopulationDirector.ClearAll();
                Assert.AreEqual(0, EmployeePopulationDirector.ClientProxyCount);
            }
            finally
            {
                EmployeePopulationDirector.ClearAll();
            }
        }
    }
}
