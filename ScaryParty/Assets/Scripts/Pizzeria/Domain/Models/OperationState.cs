using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class OperationState
    {
        public OperationId Id { get; }
        public StationSlotId SlotId { get; }
        public ItemId InputItemId { get; set; }
        public ToolItemId ToolItemId { get; set; }
        public int ProcessId { get; }
        public ulong WorkerId { get; set; }
        public float AccumulatedProgress { get; set; }
        public double OperationStartTime { get; set; }
        public float CapturedDuration { get; set; }
        public double LeaseExpirationTime { get; set; }
        public uint Revision { get; set; } = 1;

        public bool IsActive(double now) => WorkerId != 0 && LeaseExpirationTime > now;

        public OperationState(OperationId id, StationSlotId slotId, ItemId inputItemId, ToolItemId toolItemId, int processId, ulong workerId, float duration, double startTime, double leaseDuration)
        {
            Id = id;
            SlotId = slotId;
            InputItemId = inputItemId;
            ToolItemId = toolItemId;
            ProcessId = processId;
            WorkerId = workerId;
            CapturedDuration = duration;
            OperationStartTime = startTime;
            LeaseExpirationTime = startTime + leaseDuration;
            AccumulatedProgress = 0f;
        }

        public float ComputeCurrentProgress(double now)
        {
            if (CapturedDuration <= 0.001f) return 1f;
            if (WorkerId == 0) return AccumulatedProgress;

            double elapsed = now - OperationStartTime;
            if (elapsed < 0) elapsed = 0;
            float current = AccumulatedProgress + (float)(elapsed / CapturedDuration);
            return current > 1f ? 1f : current;
        }

        public void Pause(double now)
        {
            AccumulatedProgress = ComputeCurrentProgress(now);
            WorkerId = 0;
            LeaseExpirationTime = 0;
            Revision++;
        }

        public void Resume(ulong workerId, double now, double leaseDuration)
        {
            WorkerId = workerId;
            OperationStartTime = now;
            LeaseExpirationTime = now + leaseDuration;
            Revision++;
        }
    }
}
