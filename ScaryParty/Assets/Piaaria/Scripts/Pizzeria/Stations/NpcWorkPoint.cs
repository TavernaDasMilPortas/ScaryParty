using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Stations
{
    public enum NpcWorkPointRole : byte
    {
        Generic = 0,
        CounterInside = 1,
        PhoneUse = 2,
        TicketBoard = 3,
        StorageAccess = 4,
        OvenFront = 5,
        TableServe = 6
    }

    /// <summary>
    /// Ponto de alinhamento físico no piso para NPCs contratados trabalharem em frente a um objeto interagível ou slot.
    /// O forward do transform aponta na direção para a qual o NPC deve olhar ao executar a ação.
    /// </summary>
    [DisallowMultipleComponent]
    public class NpcWorkPoint : MonoBehaviour
    {
        [Header("Vínculo de Trabalho")]
        public Component targetStation;
        [Tooltip("-1 = estação inteira; >= 0 = índice de slot específico")]
        public int slotIndex = -1;
        public NpcWorkPointRole role = NpcWorkPointRole.Generic;
        public float standRadius = 0.25f;
        [Tooltip("Se marcado, a ferramenta automática de geração não reposiciona este ponto.")]
        public bool manualLocked;

        private ulong _reservedByActorKey;
        private double _reserveExpiresAt;

        public ulong ReservedByActorKey => _reservedByActorKey;

        private void OnEnable()
        {
            NpcWorkPointRegistry.Register(this);
        }

        private void OnDisable()
        {
            NpcWorkPointRegistry.Unregister(this);
            _reservedByActorKey = 0UL;
            _reserveExpiresAt = 0.0;
        }

        public bool IsFreeFor(ulong actorKey, double now)
        {
            if (_reservedByActorKey == 0UL || _reservedByActorKey == actorKey) return true;
            return now >= _reserveExpiresAt;
        }

        public bool TryReserve(ulong actorKey, double now, double ttlSeconds = 8.0)
        {
            if (!IsFreeFor(actorKey, now)) return false;
            _reservedByActorKey = actorKey;
            _reserveExpiresAt = now + ttlSeconds;
            return true;
        }

        public void Release(ulong actorKey)
        {
            if (_reservedByActorKey == actorKey)
            {
                _reservedByActorKey = 0UL;
                _reserveExpiresAt = 0.0;
            }
        }

        private void OnDrawGizmos()
        {
            DrawGizmo(selected: false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmo(selected: true);
        }

        private void DrawGizmo(bool selected)
        {
            Gizmos.color = selected
                ? new Color(0.15f, 1f, 0.45f, 0.95f)
                : new Color(0.15f, 0.80f, 1f, 0.45f);
            Vector3 pos = transform.position + Vector3.up * 0.03f;
            Gizmos.DrawWireSphere(pos, Mathf.Max(0.15f, standRadius));
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            fwd.Normalize();
            Gizmos.DrawLine(pos, pos + fwd * 0.55f);
        }
    }

    public static class NpcWorkPointRegistry
    {
        private static readonly List<NpcWorkPoint> _points = new List<NpcWorkPoint>();

        public static IReadOnlyList<NpcWorkPoint> AllPoints => _points;

        public static void Register(NpcWorkPoint point)
        {
            if (point != null && !_points.Contains(point))
            {
                _points.Add(point);
            }
        }

        public static void Unregister(NpcWorkPoint point)
        {
            if (point != null)
            {
                _points.Remove(point);
            }
        }

        public static void ReleaseAllForActor(ulong actorKey)
        {
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i] != null)
                {
                    _points[i].Release(actorKey);
                }
            }
        }

        public static bool TryResolve(Component station, int slotIndex, ulong actorKey, double now, out NpcWorkPoint resolved)
        {
            resolved = null;
            if (station == null) return false;

            NpcWorkPoint fallbackAnySlot = null;
            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                if (p.targetStation != station) continue;
                if (!p.IsFreeFor(actorKey, now)) continue;

                if (p.slotIndex == slotIndex)
                {
                    resolved = p;
                    return true;
                }
                if (p.slotIndex < 0 && fallbackAnySlot == null)
                {
                    fallbackAnySlot = p;
                }
            }

            if (fallbackAnySlot != null)
            {
                resolved = fallbackAnySlot;
                return true;
            }
            return false;
        }

        public static bool TryResolveByRole(NpcWorkPointRole role, ulong actorKey, double now, out NpcWorkPoint resolved)
        {
            resolved = null;
            for (int i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if (p == null || !p.isActiveAndEnabled) continue;
                if (p.role != role) continue;
                if (!p.IsFreeFor(actorKey, now)) continue;
                resolved = p;
                return true;
            }
            return false;
        }
    }
}
