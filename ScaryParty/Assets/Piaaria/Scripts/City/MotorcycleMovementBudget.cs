using UnityEngine;

namespace ScaryParty.City
{
    /// <summary>
    /// Orçamento temporal de deslocamento da moto no servidor.
    /// Garante que a distância máxima por segundo independa da taxa de envio de RPCs.
    /// </summary>
    public sealed class MotorcycleMovementBudget
    {
        public float MaxSpeedMps { get; }
        public float MaxBurstMeters { get; }
        public float AvailableBudgetMeters { get; private set; }
        public double LastServerTime { get; private set; }
        public int ViolationCount { get; private set; }

        public MotorcycleMovementBudget(float maxSpeedMps = 22f, float maxBurstMeters = 1.5f)
        {
            MaxSpeedMps = Mathf.Max(1f, maxSpeedMps);
            MaxBurstMeters = Mathf.Max(0.25f, maxBurstMeters);
            Reset();
        }

        public void Reset()
        {
            LastServerTime = -1.0;
            AvailableBudgetMeters = 0f;
        }

        public Vector3 ConsumeStep(double serverTime, Vector3 currentPos, Vector3 clampedTargetPos, out float allowedDistance)
        {
            float realDt = LastServerTime > 0.0 ? Mathf.Clamp((float)(serverTime - LastServerTime), 0f, 0.25f) : 0.05f;
            LastServerTime = serverTime;

            AvailableBudgetMeters = Mathf.Min(MaxBurstMeters, AvailableBudgetMeters + MaxSpeedMps * realDt);

            float requestedDist = Vector3.Distance(currentPos, clampedTargetPos);
            allowedDistance = Mathf.Min(requestedDist, AvailableBudgetMeters);
            if (requestedDist > AvailableBudgetMeters + 0.35f)
            {
                ViolationCount++;
            }

            Vector3 safePos = Vector3.MoveTowards(currentPos, clampedTargetPos, allowedDistance);
            AvailableBudgetMeters = Mathf.Max(0f, AvailableBudgetMeters - Vector3.Distance(currentPos, safePos));
            return safePos;
        }
    }
}
