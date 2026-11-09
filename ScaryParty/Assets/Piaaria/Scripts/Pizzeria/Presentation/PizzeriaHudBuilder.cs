using UnityEngine;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.Pizzeria.Presentation
{
    // Compatibility shim for prefab instances that have not yet been migrated.
    // Never constructs a second HUD at runtime.
    public class PizzeriaHudBuilder : MonoBehaviour
    {
        public static PizzeriaHudBuilder Instance { get; private set; }

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void OpenStoragePicker(int stationId) => StoragePresenter.Instance?.Open(stationId);

        public void CloseStoragePicker(bool notifyServer = true) => StoragePresenter.Instance?.Close();

        public void OpenStagingAddressPicker(ulong boxId, int slotIndex) =>
            StagingPresenter.Instance?.Open(boxId, slotIndex);
    }
}
