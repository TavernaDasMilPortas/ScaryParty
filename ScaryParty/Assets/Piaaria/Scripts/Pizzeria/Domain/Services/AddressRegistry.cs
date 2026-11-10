using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Domain.Services
{
    public class AddressRegistry
    {
        private readonly Dictionary<AddressId, DomainVector3> _positions = new Dictionary<AddressId, DomainVector3>();

        public void Register(AddressId id, DomainVector3 position)
        {
            _positions[id] = position;
        }

        public void Clear()
        {
            _positions.Clear();
        }

        public bool TryGetPosition(AddressId id, out DomainVector3 position)
        {
            return _positions.TryGetValue(id, out position);
        }

        public bool Exists(AddressId id) => _positions.ContainsKey(id);

        public IReadOnlyCollection<AddressId> GetAllIds() => _positions.Keys;

        public int ComputeHash()
        {
            int hash = 17;
            var sortedKeys = new List<AddressId>(_positions.Keys);
            sortedKeys.Sort((a, b) => a.Value.CompareTo(b.Value));
            for (int i = 0; i < sortedKeys.Count; i++)
            {
                var key = sortedKeys[i];
                hash = hash * 31 + key.Value.GetHashCode();
                hash = hash * 31 + _positions[key].GetHashCode();
            }
            return hash;
        }
    }
}
