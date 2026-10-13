using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class OperationState
    {
        public const ulong NoWorker = ulong.MaxValue;
        public const ulong ServerWorker = ulong.MaxValue - 1;

        public OperationId Id { get; }
        public StationSlotId SlotId { get; }
        public OperationKind Kind { get; }
        public OperationPhase Phase { get; set; }
        public TimedProgress Progress;
        public ItemId InputItemId { get; set; }
        public ToolItemId ToolItemId { get; set; }
        public int ProcessId { get; }
        public ulong WorkerId { get; set; } = NoWorker;
        public ResumePolicy ResumePolicy { get; }
        public uint Revision { get; set; } = 1;

        public OperationState(OperationId id, StationSlotId slotId, OperationKind kind, ResumePolicy resumePolicy, ItemId inputItemId, ToolItemId toolItemId, int processId, ulong workerId, float duration, float now, float leaseDuration)
        {
            Id = id;
            SlotId = slotId;
            Kind = kind;
            ResumePolicy = resumePolicy;
            InputItemId = inputItemId;
            ToolItemId = toolItemId;
            ProcessId = processId;
            WorkerId = workerId;
            Phase = OperationPhase.Running;
            Progress = new TimedProgress(duration, now, leaseDuration);
        }

        public void Pause(float now)
        {
            if (Phase != OperationPhase.Running) return;
            Progress.Materialize(now);
            Phase = OperationPhase.Paused;
            WorkerId = NoWorker;
            Revision++;
        }

        public void Resume(ulong workerId, float now, float leaseDuration)
        {
            if (Phase != OperationPhase.Paused) return;
            WorkerId = workerId;
            Phase = OperationPhase.Running;
            Progress.LastTick = now;
            Progress.LeaseExpiry = now + leaseDuration;
            Revision++;
        }

        public void Cancel()
        {
            Phase = OperationPhase.Cancelled;
            WorkerId = NoWorker;
            Revision++;
        }

        public void Complete(float now)
        {
            Progress.Materialize(now);
            Phase = OperationPhase.Completed;
            WorkerId = NoWorker;
            Revision++;
        }

        public float ComputeCurrentProgress(double now)
        {
            if (Phase != OperationPhase.Running)
                return Progress.Normalized;
            float currentNow = (float)now;
            float effectiveNow = System.Math.Min(currentNow, Progress.LeaseExpiry);
            float delta = effectiveNow - Progress.LastTick;
            float total = Progress.Accumulated + (delta > 0 ? delta : 0f);
            return Progress.Duration > 0 ? System.Math.Clamp(total / Progress.Duration, 0f, 1f) : 1f;
        }

        public float ComputeCurrentProgress(float now) => ComputeCurrentProgress((double)now);
        public float AccumulatedProgress => Progress.Accumulated;
    }
}
