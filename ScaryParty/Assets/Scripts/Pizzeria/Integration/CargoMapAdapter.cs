using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Integration
{
    /// <summary>
    /// Integra caixas carregadas nas mÃ£os e mochila com o minimapa.
    /// Gera rotas dinÃ¢micas a partir do AddressRegistry para os destinos rotulados.
    /// </summary>
    public class CargoMapAdapter : MonoBehaviour
    {
        // The canonical UI map resolves replicated cargo for every owner. This adapter remains
        // for compatibility with existing prefab references but does not author duplicate routes.
        private bool TryFindDeliveryPointPosition(int destId, out Vector3 position)
        {
            position = default;
            // Consulta primeiramente o AddressRegistry autoritativo
            var root = PizzeriaRoot.Instance;
            if (root != null && root.AddressRegistry != null)
            {
                if (root.AddressRegistry.TryGetPosition(new AddressId(destId), out var dPos))
                {
                    position = new Vector3(dPos.X, dPos.Y, dPos.Z);
                    return true;
                }
            }

            // Fallback para varredura de DeliveryPoint na cena
            var points = Object.FindObjectsByType<DeliveryPoint>(FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i].AddressId == destId)
                {
                    position = points[i].transform.position;
                    return true;
                }
            }
            return false;
        }

    }
}
