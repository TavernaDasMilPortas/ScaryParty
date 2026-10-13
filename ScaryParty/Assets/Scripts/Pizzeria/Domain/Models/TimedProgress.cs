using System;

namespace ScaryParty.Pizzeria.Domain
{
    public struct TimedProgress
    {
        public float Accumulated;
        public float Duration;
        public float LastTick;
        public float LeaseExpiry;

        public TimedProgress(float duration, float now, float leaseDuration)
        {
            Accumulated = 0f;
            Duration = duration;
            LastTick = now;
            LeaseExpiry = now + leaseDuration;
        }

        public void ExtendLease(float now, float leaseDuration)
        {
            LeaseExpiry = now + leaseDuration;
        }

        public float Materialize(float now)
        {
            float effectiveNow = Math.Min(now, LeaseExpiry);
            float delta = effectiveNow - LastTick;
            if (delta > 0)
            {
                Accumulated += delta;
            }
            LastTick = now;
            return Math.Max(0f, delta);
        }

        public bool IsComplete => Accumulated >= Duration;
        public float Normalized => Duration > 0 ? Math.Clamp(Accumulated / Duration, 0f, 1f) : 1f;
    }
}
