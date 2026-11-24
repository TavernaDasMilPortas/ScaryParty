using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace ScaryParty.Pizzeria.Composition
{
    public enum SpawnOwnerKind
    {
        Player = 0,
        Employee = 1
    }

    public enum SpawnFailureReason
    {
        None = 0,
        NavMeshNotReady = 1,
        CapacityExhausted = 2,
        NonFiniteCandidate = 3,
        NoFloorHit = 4,
        SlopeTooSteep = 5,
        CapsuleBlocked = 6,
        NoNavMeshSample = 7,
        NoCompletePath = 8,
        ExceedsMaxDistance = 9
    }

    public struct SpawnFailure
    {
        public SpawnFailureReason Reason;
        public string Detail;

        public SpawnFailure(SpawnFailureReason reason, string detail)
        {
            Reason = reason;
            Detail = detail;
        }
    }

    public struct SpawnReservation
    {
        public int ReservationId;
        public int OwnerId;
        public SpawnOwnerKind OwnerKind;
        public int SlotIndex;
        public Vector3 Position;
        public Quaternion Rotation;
        public float ReservedAtRealtime;
    }

    public struct SpawnRequest
    {
        public int OwnerId;
        public SpawnOwnerKind OwnerKind;
        public Vector3 PrimaryPosition;
        public Quaternion PrimaryRotation;
        public Vector3[] AlternativePositions;
        public Vector3? RecoveryPosition;
        public Vector3? PathDestination;
        public float ExpectedFloorY;
        public float MaxAnchorDistance;
        public float MaxSlopeDegrees;
        public float CapsuleRadius;
        public float CapsuleHeight;
        public LayerMask CollisionMask;
    }

    /// <summary>
    /// Serviço explícito de reservas de spawn por dono (jogador ou funcionário),
    /// evitando colisão de slots e reutilização silenciosa quando a capacidade esgota.
    /// </summary>
    public sealed class SpawnReservationService
    {
        private readonly Dictionary<(SpawnOwnerKind, int), SpawnReservation> _byOwner =
            new Dictionary<(SpawnOwnerKind, int), SpawnReservation>();
        private readonly HashSet<(SpawnOwnerKind, int)> _occupiedSlots =
            new HashSet<(SpawnOwnerKind, int)>();
        private int _nextReservationId = 1;

        public int ActiveReservationCount => _byOwner.Count;

        public bool TryReserve(
            int ownerId,
            SpawnOwnerKind kind,
            int maxCapacity,
            int preferredStartSlot,
            Func<int, bool> tryResolveSlotPosition,
            out SpawnReservation reservation,
            out SpawnFailure failure)
        {
            var ownerKey = (kind, ownerId);
            if (_byOwner.TryGetValue(ownerKey, out reservation))
            {
                failure = default;
                return true;
            }

            if (maxCapacity <= 0)
            {
                failure = new SpawnFailure(SpawnFailureReason.CapacityExhausted, "Capacidade de slots inválida.");
                return false;
            }

            int start = ((preferredStartSlot % maxCapacity) + maxCapacity) % maxCapacity;
            for (int offset = 0; offset < maxCapacity; offset++)
            {
                int slot = (start + offset) % maxCapacity;
                if (_occupiedSlots.Contains((kind, slot))) continue;

                if (tryResolveSlotPosition != null && !tryResolveSlotPosition(slot))
                {
                    continue;
                }

                reservation = new SpawnReservation
                {
                    ReservationId = _nextReservationId++,
                    OwnerId = ownerId,
                    OwnerKind = kind,
                    SlotIndex = slot,
                    ReservedAtRealtime = Time.realtimeSinceStartup
                };
                _byOwner[ownerKey] = reservation;
                _occupiedSlots.Add((kind, slot));
                failure = default;
                return true;
            }

            bool anyFreeSlot = false;
            for (int s = 0; s < maxCapacity; s++)
            {
                if (!_occupiedSlots.Contains((kind, s)))
                {
                    anyFreeSlot = true;
                    break;
                }
            }

            reservation = default;
            failure = anyFreeSlot
                ? new SpawnFailure(SpawnFailureReason.CapsuleBlocked, "Nenhum slot livre passou na validação física/NavMesh.")
                : new SpawnFailure(SpawnFailureReason.CapacityExhausted, $"Capacidade máxima ({maxCapacity}) atingida para {kind}.");
            return false;
        }

        public void UpdateReservationPose(int ownerId, SpawnOwnerKind kind, Vector3 pos, Quaternion rot)
        {
            var key = (kind, ownerId);
            if (_byOwner.TryGetValue(key, out var res))
            {
                res.Position = pos;
                res.Rotation = rot;
                _byOwner[key] = res;
            }
        }

        public bool ReleaseByOwner(int ownerId, SpawnOwnerKind kind)
        {
            var key = (kind, ownerId);
            if (!_byOwner.TryGetValue(key, out var res)) return false;
            _byOwner.Remove(key);
            _occupiedSlots.Remove((kind, res.SlotIndex));
            return true;
        }

        public bool ReleaseById(int reservationId)
        {
            foreach (var kvp in _byOwner)
            {
                if (kvp.Value.ReservationId == reservationId)
                {
                    _byOwner.Remove(kvp.Key);
                    _occupiedSlots.Remove((kvp.Key.Item1, kvp.Value.SlotIndex));
                    return true;
                }
            }
            return false;
        }

        public void ClearAll()
        {
            _byOwner.Clear();
            _occupiedSlots.Clear();
        }

        public void CollectPositions(SpawnOwnerKind kind, List<Vector3> outPositions, int excludeOwnerId = -1)
        {
            if (outPositions == null) return;
            outPositions.Clear();
            foreach (var kvp in _byOwner)
            {
                if (kvp.Key.Item1 != kind) continue;
                if (excludeOwnerId >= 0 && kvp.Key.Item2 == excludeOwnerId) continue;
                outPositions.Add(kvp.Value.Position);
            }
        }
    }

    /// <summary>
    /// Resolvedor determinístico de candidatos de spawn com validação física e de NavMesh.
    /// </summary>
    public static class SpawnResolver
    {
        public delegate bool CandidateCheckDelegate(
            Vector3 candidate,
            in SpawnRequest request,
            out Vector3 resolvedPos,
            out SpawnFailure failure);

        public static bool TryResolveCandidate(
            in SpawnRequest request,
            CandidateCheckDelegate validator,
            out Vector3 resolvedPos,
            out SpawnFailure lastFailure)
        {
            lastFailure = new SpawnFailure(SpawnFailureReason.NonFiniteCandidate, "Nenhum candidato avaliado.");
            resolvedPos = Vector3.zero;

            if (TryEvaluateWithRings(request.PrimaryPosition, request, validator, out resolvedPos, out lastFailure))
                return true;

            if (request.AlternativePositions != null)
            {
                for (int i = 0; i < request.AlternativePositions.Length; i++)
                {
                    if (TryEvaluateWithRings(request.AlternativePositions[i], request, validator, out resolvedPos, out lastFailure))
                        return true;
                }
            }

            if (request.RecoveryPosition.HasValue)
            {
                if (validator(request.RecoveryPosition.Value, request, out resolvedPos, out lastFailure))
                    return true;
            }

            return false;
        }

        private static bool TryEvaluateWithRings(
            Vector3 center,
            in SpawnRequest request,
            CandidateCheckDelegate validator,
            out Vector3 resolvedPos,
            out SpawnFailure lastFailure)
        {
            if (validator(center, request, out resolvedPos, out lastFailure))
                return true;

            const int radialSteps = 6;
            float[] radii = { 0.85f, 1.55f };
            for (int r = 0; r < radii.Length; r++)
            {
                for (int i = 0; i < radialSteps; i++)
                {
                    float angle = (i / (float)radialSteps) * Mathf.PI * 2f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radii[r];
                    if (validator(center + offset, request, out resolvedPos, out lastFailure))
                        return true;
                }
            }

            return false;
        }

        public static bool ValidateUnityCandidate(
            Vector3 candidate,
            in SpawnRequest request,
            out Vector3 resolvedPos,
            out SpawnFailure failure)
        {
            resolvedPos = Vector3.zero;

            if (!float.IsFinite(candidate.x) || !float.IsFinite(candidate.y) || !float.IsFinite(candidate.z))
            {
                failure = new SpawnFailure(SpawnFailureReason.NonFiniteCandidate, "Posição candidata não finita.");
                return false;
            }

            float maxDist = request.MaxAnchorDistance > 0f ? request.MaxAnchorDistance : 6f;
            if (Vector3.Distance(new Vector3(candidate.x, request.PrimaryPosition.y, candidate.z), request.PrimaryPosition) > maxDist)
            {
                failure = new SpawnFailure(SpawnFailureReason.ExceedsMaxDistance, "Candidato excede distância máxima da âncora.");
                return false;
            }

            LayerMask mask = request.CollisionMask.value != 0 ? request.CollisionMask : Physics.DefaultRaycastLayers;
            Vector3 rayOrigin = candidate + Vector3.up * 1.8f;
            if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit floorHit, 4.0f, mask, QueryTriggerInteraction.Ignore))
            {
                failure = new SpawnFailure(SpawnFailureReason.NoFloorHit, "Sem piso físico abaixo do candidato.");
                return false;
            }

            float maxSlope = request.MaxSlopeDegrees > 0f ? request.MaxSlopeDegrees : 35f;
            if (Vector3.Angle(floorHit.normal, Vector3.up) > maxSlope)
            {
                failure = new SpawnFailure(SpawnFailureReason.SlopeTooSteep, "Inclinação do piso acima do permitido.");
                return false;
            }

            if (Mathf.Abs(floorHit.point.y - request.ExpectedFloorY) > 1.2f)
            {
                failure = new SpawnFailure(SpawnFailureReason.NoFloorHit, "Altura do piso diverge do esperado.");
                return false;
            }

            if (!NavMesh.SamplePosition(floorHit.point, out NavMeshHit navHit, 1.8f, NavMesh.AllAreas))
            {
                failure = new SpawnFailure(SpawnFailureReason.NoNavMeshSample, "Sem NavMesh válido próximo ao candidato.");
                return false;
            }

            Vector3 p = navHit.position;
            float radius = request.CapsuleRadius > 0f ? request.CapsuleRadius : 0.34f;
            float height = request.CapsuleHeight > 0f ? request.CapsuleHeight : 1.8f;
            Vector3 capBottom = p + Vector3.up * (radius + 0.1f);
            Vector3 capTop = p + Vector3.up * Mathf.Max(radius + 0.15f, height - radius);
            if (Physics.CheckCapsule(capBottom, capTop, radius, mask, QueryTriggerInteraction.Ignore))
            {
                failure = new SpawnFailure(SpawnFailureReason.CapsuleBlocked, "Cápsula bloqueada por colisor.");
                return false;
            }

            if (request.PathDestination.HasValue &&
                NavMesh.SamplePosition(request.PathDestination.Value, out NavMeshHit destHit, 2.0f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(p, destHit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                {
                    failure = new SpawnFailure(SpawnFailureReason.NoCompletePath, "Sem caminho NavMesh completo até o destino.");
                    return false;
                }
            }

            resolvedPos = p;
            failure = default;
            return true;
        }
    }
}
