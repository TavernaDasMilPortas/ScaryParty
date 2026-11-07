using UnityEngine;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Player;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Authoring
{
    public class DeliveryBagAuthoring : MonoBehaviour, IInteractable
    {
        [SerializeField] private ulong backpackId;
        [SerializeField] private Renderer bagRenderer;
        [SerializeField] private Collider interactionCollider;
        public ulong BackpackId => backpackId;
        public Renderer BagRenderer => bagRenderer;
        public Collider InteractionCollider => interactionCollider;
        public Transform BagVisual => bagRenderer != null ? bagRenderer.transform : transform;
        public Vector3 VisualLocalPosition => _visualLocalPosition;
        public Quaternion VisualLocalRotation => _visualLocalRotation;
        private bool _visible;
        private bool _initialized;
        private Vector3 _visualLocalPosition;
        private Quaternion _visualLocalRotation;
        public string InteractPrompt
        {
            get
            {
                if (!TryGetState(out var bag) || bag.EquippedBy != BackpackState.NoOwner) return string.Empty;
                var localPlayer = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject;
                var controller = localPlayer != null ? localPlayer.GetComponent<BackpackController>() : null;
                bool wearing = controller != null && controller.TryGetEquippedBag(out _);
                var inventory = localPlayer != null ? localPlayer.GetComponent<PlayerInventoryAdapter>() : null;
                bool holdingBox = controller != null && inventory != null && controller.HasBoxInHand(inventory.ActiveHand);
                string equip = wearing ? "Outra mochila equipada" : "[E] Vestir mochila";
                string store = bag.ItemCount >= bag.Capacity ? "Mochila cheia" : holdingBox ? "[F] Guardar pizza" : "Segure caixa na mão ativa";
                return $"{equip}  {store} ({bag.ItemCount}/{bag.Capacity})  [I] Inspecionar";
            }
        }

        private bool TryGetState(out NetBackpackDto bag)
        {
            bag = default;
            var state = PizzeriaNetworkState.Instance;
            if (state == null || state.Backpacks == null) return false;
            for (int i = 0; i < state.Backpacks.Count; i++)
            {
                if (state.Backpacks[i].BackpackId != backpackId) continue;
                bag = state.Backpacks[i];
                return true;
            }
            return false;
        }

        private void Awake()
        {
            _visualLocalPosition = BagVisual.localPosition;
            _visualLocalRotation = BagVisual.localRotation;
            if (interactionCollider is BoxCollider box && bagRenderer != null &&
                bagRenderer.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh != null)
            {
                box.center = mesh.sharedMesh.bounds.center;
                box.size = mesh.sharedMesh.bounds.size;
            }
        }

        private void LateUpdate()
        {
            bool available = TryGetState(out var bag) && bag.EquippedBy == BackpackState.NoOwner;
            if (!_initialized || _visible != available)
            {
                _initialized = true;
                _visible = available;
                if (bagRenderer != null) bagRenderer.enabled = available;
                if (interactionCollider != null) interactionCollider.enabled = available;
                if (!available && ScaryParty.Interaction.OutlineService.Instance != null)
                {
                    var outline = GetComponent<ScaryParty.Interaction.OutlineTarget>();
                    if (outline != null) ScaryParty.Interaction.OutlineService.Instance.ClearTarget(outline);
                }
            }
            if (available)
            {
                transform.position = new Vector3(bag.PosX, bag.PosY, bag.PosZ);
                transform.rotation = new Quaternion(bag.RotX, bag.RotY, bag.RotZ, bag.RotW);
            }
        }

        public void OnInteract(GameObject player)
        {
            if (!TryGetState(out var bag) || bag.EquippedBy != BackpackState.NoOwner) return;
            LogisticsCommands.Instance?.EquipBackpackServerRpc(backpackId);
        }

        public void StoreFromHand(GameObject player)
        {
            if (player == null || !TryGetState(out var bag) || bag.EquippedBy != BackpackState.NoOwner) return;
            var controller = player.GetComponent<BackpackController>();
            var inventory = player.GetComponent<PlayerInventoryAdapter>();
            if (controller != null && inventory != null) controller.TryStoreFromHand(inventory.ActiveHand, backpackId);
        }

        public void OnFocus() { }
        public void OnLoseFocus() { }
    }
}
