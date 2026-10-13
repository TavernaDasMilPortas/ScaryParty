using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Integration
{
    /// <summary>
    /// Adaptador de entrega entre pontos do mundo e o subsistema de logística autoritativo.
    /// </summary>
    public static class DeliveryPointAdapter
    {
        public static bool TrySubmitDelivery(int addressId)
        {
            var netState = PizzeriaNetworkState.Instance;
            var logistics = LogisticsCommands.Instance;
            if (netState == null || logistics == null || NetworkManager.Singleton == null) return false;

            ulong myId = NetworkManager.Singleton.LocalClientId;
            var carriedBoxes = new List<ulong>();

            // Coleta todas as caixas transportadas pelo jogador local (mãos ou mochila)
            for (int i = 0; i < netState.Items.Count; i++)
            {
                var item = netState.Items[i];
                bool isCarriedByMe = item.HolderId == myId &&
                    (item.LocationType == (byte)LocationType.Hand || item.LocationType == (byte)LocationType.Backpack);

                if (isCarriedByMe && (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box))
                {
                    // Prioriza caixas com etiqueta do destino ou aceita todas se não houver etiqueta
                    if (item.LabelDestinationId == addressId || item.LabelDestinationId <= 0)
                    {
                        carriedBoxes.Add(item.ItemId);
                    }
                }
            }

            // Se não achou nenhuma com etiqueta específica, inclui todas as caixas carregadas
            if (carriedBoxes.Count == 0)
            {
                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    bool isCarriedByMe = item.HolderId == myId &&
                        (item.LocationType == (byte)LocationType.Hand || item.LocationType == (byte)LocationType.Backpack);

                    if (isCarriedByMe && (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box))
                    {
                        carriedBoxes.Add(item.ItemId);
                    }
                }
            }

            if (carriedBoxes.Count == 0)
            {
                NotificationManager.Show("Você não possui nenhuma pizza para entregar aqui!", NotificationType.Warning);
                return false;
            }

            ulong attemptId = ((ulong)System.DateTime.UtcNow.Ticks) ^ ((ulong)UnityEngine.Random.Range(1, 1000000));
            logistics.SubmitDeliveryServerRpc(addressId, carriedBoxes.ToArray(), attemptId);
            return true;
        }
    }
}
