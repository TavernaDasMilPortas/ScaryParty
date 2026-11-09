using System;

namespace ScaryParty.Pizzeria.Domain.Types
{
    public enum DestinationKind : byte
    {
        None = 0,
        DeliveryAddress = 1,
        Table = 2
    }

    public enum OrderOrigin : byte
    {
        Phone = 1,
        CounterCustomer = 2,
        DeliveryCall = 1,
        WalkInCustomer = 2
    }

    /// <summary>
    /// Referência tipada para um destino de pedido: Endereço de Delivery ou Mesa do Salão.
    /// </summary>
    [Serializable]
    public readonly struct DestinationRef : IEquatable<DestinationRef>
    {
        public readonly DestinationKind Kind;
        public readonly int Id;

        public static DestinationRef None => new DestinationRef(DestinationKind.None, -1);

        public bool IsValid => Kind != DestinationKind.None && Id >= 0;
        public bool IsTable => Kind == DestinationKind.Table && Id > 0;
        public bool IsDeliveryAddress => Kind == DestinationKind.DeliveryAddress && Id >= 0;

        public DestinationRef(DestinationKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        public static DestinationRef ForAddress(AddressId addressId)
            => new DestinationRef(DestinationKind.DeliveryAddress, addressId.Value);

        public static DestinationRef ForAddress(int addressIdValue)
            => new DestinationRef(DestinationKind.DeliveryAddress, addressIdValue);

        public static DestinationRef ForTable(int tableId)
            => new DestinationRef(DestinationKind.Table, tableId);

        public string FormatDisplay()
        {
            return Kind switch
            {
                DestinationKind.Table => $"Mesa {Id}",
                DestinationKind.DeliveryAddress => AddressFormat.Display(Id),
                _ => "Sem destino"
            };
        }

        public bool Equals(DestinationRef other) => Kind == other.Kind && Id == other.Id;
        public override bool Equals(object obj) => obj is DestinationRef other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((byte)Kind, Id);
        public static bool operator ==(DestinationRef a, DestinationRef b) => a.Equals(b);
        public static bool operator !=(DestinationRef a, DestinationRef b) => !a.Equals(b);
        public override string ToString() => FormatDisplay();
    }
}
