using UnityEngine;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Stations
{
    public class StorageStation : StationView
    {
        [Header("Configuração de Armazenamento")]
        public int[] acceptedIngredientIds;

        public override string InteractPrompt => "[E] Abrir " + stationName;

        public override void OnInteract(GameObject interactor)
        {
            var state = PizzeriaNetworkState.Instance;
            if (state == null) return;
            
            bool hasItemOnStation = false;
            for (int i = 0; i < state.Items.Count; i++) {
                if (state.Items[i].LocationType == (byte)LocationType.StationSlot && state.Items[i].HolderId == (ulong)stationId) {
                    hasItemOnStation = true; break;
                }
            }

            if (hasItemOnStation)
            {
                TryPickOrPlace(interactor);
            }
            else
            {
                if (PizzeriaHudBuilder.Instance != null)
                {
                    PizzeriaHudBuilder.Instance.OpenStoragePicker(stationId);
                }
            }
        }
    }
}
