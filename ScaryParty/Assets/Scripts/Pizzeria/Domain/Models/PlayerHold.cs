using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Models
{
    public class PlayerHold
    {
        public ulong PlayerId { get; }
        public PlayerHoldKind Kind { get; }
        public ItemId TargetItemId { get; }
        public OrderId TargetOrderId { get; }
        public TimedProgress Progress;
        public OperationPhase Phase { get; set; }
        public uint Revision { get; set; } = 1;

        public PlayerHold(ulong playerId, PlayerHoldKind kind, ItemId targetItemId, OrderId targetOrderId, float duration, float now, float leaseDuration)
        {
            PlayerId = playerId;
            Kind = kind;
            TargetItemId = targetItemId;
            TargetOrderId = targetOrderId;
            Progress = new TimedProgress(duration, now, leaseDuration);
            Phase = OperationPhase.Running;
        }

        public void Cancel()
        {
            Phase = OperationPhase.Cancelled;
            Revision++;
        }

        public void Complete(float now)
        {
            Progress.Materialize(now);
            Phase = OperationPhase.Completed;
            Revision++;
        }
    }
}
