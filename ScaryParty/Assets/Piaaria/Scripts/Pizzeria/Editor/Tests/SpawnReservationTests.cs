using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Composition;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class SpawnReservationTests
    {
        [Test]
        public void SpawnResolver_RejectsBlockedPrimaryAndSelectsValidAlternative()
        {
            Vector3 primary = new Vector3(0f, 0f, 0f);
            Vector3 alt = new Vector3(10f, 0f, 0f);

            var req = new SpawnRequest
            {
                OwnerId = 1,
                OwnerKind = SpawnOwnerKind.Employee,
                PrimaryPosition = primary,
                AlternativePositions = new[] { alt }
            };

            bool ok = SpawnResolver.TryResolveCandidate(
                in req,
                (Vector3 cand, in SpawnRequest r, out Vector3 resolved, out SpawnFailure failure) =>
                {
                    if (Vector3.Distance(cand, alt) < 0.01f)
                    {
                        resolved = cand;
                        failure = default;
                        return true;
                    }
                    resolved = Vector3.zero;
                    failure = new SpawnFailure(SpawnFailureReason.CapsuleBlocked, "Bloqueado");
                    return false;
                },
                out Vector3 finalPos,
                out SpawnFailure lastFailure);

            Assert.IsTrue(ok);
            Assert.AreEqual(alt, finalPos);
            Assert.AreEqual(SpawnFailureReason.None, lastFailure.Reason);
        }

        [Test]
        public void SpawnResolver_FailsWithNoNavMeshWhenAllCandidatesFail()
        {
            var req = new SpawnRequest
            {
                OwnerId = 2,
                OwnerKind = SpawnOwnerKind.Employee,
                PrimaryPosition = Vector3.zero
            };

            bool ok = SpawnResolver.TryResolveCandidate(
                in req,
                (Vector3 cand, in SpawnRequest r, out Vector3 resolved, out SpawnFailure failure) =>
                {
                    resolved = Vector3.zero;
                    failure = new SpawnFailure(SpawnFailureReason.NoNavMeshSample, "Sem NavMesh");
                    return false;
                },
                out _,
                out SpawnFailure lastFailure);

            Assert.IsFalse(ok);
            Assert.AreEqual(SpawnFailureReason.NoNavMeshSample, lastFailure.Reason);
        }

        [Test]
        public void SpawnReservationService_PreventsCollisions_QueuesWhenFull_AndReusesAfterRelease()
        {
            var service = new SpawnReservationService();

            // Capacidade 2: reserva slots determinísticos sem colisão
            Assert.IsTrue(service.TryReserve(101, SpawnOwnerKind.Employee, 2, 0, _ => true, out var r1, out _));
            Assert.IsTrue(service.TryReserve(102, SpawnOwnerKind.Employee, 2, 0, _ => true, out var r2, out _));
            Assert.AreNotEqual(r1.SlotIndex, r2.SlotIndex);

            // Capacidade cheia: não reutiliza slot silenciosamente
            Assert.IsFalse(service.TryReserve(103, SpawnOwnerKind.Employee, 2, 0, _ => true, out _, out var fullFail));
            Assert.AreEqual(SpawnFailureReason.CapacityExhausted, fullFail.Reason);

            // Release libera slot para reuso
            Assert.IsTrue(service.ReleaseByOwner(101, SpawnOwnerKind.Employee));
            Assert.IsTrue(service.TryReserve(103, SpawnOwnerKind.Employee, 2, 0, _ => true, out var r3, out _));
            Assert.AreEqual(r1.SlotIndex, r3.SlotIndex);
        }
    }
}
