using System;
using System.Collections.Generic;

namespace ScaryParty.Pizzeria.Domain.Ports
{
    public interface IClock
    {
        double Now { get; }
    }

    public class SystemClock : IClock
    {
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public double Now => (DateTime.UtcNow - Epoch).TotalSeconds;
    }

    public class SimulatedClock : IClock
    {
        public double CurrentTime { get; set; }
        public double Now => CurrentTime;
        public void Advance(double seconds) => CurrentTime += seconds;
    }

    public interface IEconomyPort
    {
        bool CreditPersonalMoney(ulong playerId, int amount);
        bool CreditRestaurantBudget(int amount);
        bool DebitRestaurantBudget(int amount);
    }

    public interface IDestinationCatalog
    {
        bool DestinationExists(int destinationId);
        IReadOnlyList<int> GetAllDestinations();
    }
}
