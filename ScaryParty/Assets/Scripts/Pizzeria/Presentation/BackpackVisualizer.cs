using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Presentation
{
    public class BackpackVisualizer : MonoBehaviour
    {
        [Header("Visual da mochila")]
        public GameObject deliveryBagPrefab; // Legacy serialized reference; authored bags provide the mesh used at runtime.
        [SerializeField] private Transform backpackMount;
        [SerializeField] private Vector3 mountOffset = new Vector3(0f, 0.05f, -0.22f);
        [SerializeField] private Vector3 mountScale = new Vector3(0.5f, 0.5f, 0.5f);
        [SerializeField] private Vector3 mountEuler = new Vector3(0f, 180f, 0f);

        private GameObject _visual;
        private NetworkObject _player;
        private ulong _displayedBagId;

        private void Awake() => _player = GetComponent<NetworkObject>();

        private void Update()
        {
            ulong owner = _player != null ? _player.OwnerClientId :
                (NetworkManager.Singleton != null ? NetworkManager.Singleton.LocalClientId : ulong.MaxValue);
            bool equipped = CarriedCargoResolver.TryGetEquippedBackpack(PizzeriaNetworkState.Instance, owner, out var bag);
            if (!equipped)
            {
                if (_visual != null)
                {
                    Destroy(_visual);
                    _visual = null;
                    _displayedBagId = 0;
                }
                return;
            }
            if (_visual != null && _displayedBagId != bag.BackpackId)
            {
                Destroy(_visual);
                _visual = null;
            }
            if (_visual == null)
            {
                Vector3 visualScale = mountScale;
                Quaternion visualRotation = Quaternion.Euler(mountEuler);
                _displayedBagId = bag.BackpackId;
                Transform mount = backpackMount;
                if (mount == null)
                {
                    var animator = GetComponentInChildren<Animator>();
                    if (animator != null && animator.isHuman)
                        mount = animator.GetBoneTransform(HumanBodyBones.Chest) ?? animator.GetBoneTransform(HumanBodyBones.Spine);
                }
                var authored = FindObjectsByType<ScaryParty.Pizzeria.Authoring.DeliveryBagAuthoring>(FindObjectsSortMode.None);
                foreach (var authoredBag in authored)
                {
                    if (authoredBag.BagRenderer == null || authoredBag.BackpackId != _displayedBagId) continue;
                    var mesh = authoredBag.BagRenderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null) break;
                    _visual = new GameObject("EquippedDeliveryBag");
                    _visual.transform.SetParent(mount != null ? mount : transform, false);
                    _visual.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
                    _visual.AddComponent<MeshRenderer>().sharedMaterials = authoredBag.BagRenderer.sharedMaterials;
                    Vector3 sourceScale = authoredBag.BagVisual.lossyScale;
                    visualRotation = Quaternion.Euler(mountEuler) * authoredBag.VisualLocalRotation;
                    // Preserve the authored proportions, then fit the largest axis to a wearable size.
                    Vector3 bounds = Vector3.Scale(mesh.sharedMesh.bounds.size, sourceScale);
                    float maxDimension = Mathf.Max(Mathf.Abs(bounds.x), Mathf.Abs(bounds.y), Mathf.Abs(bounds.z));
                    if (maxDimension > 0.0001f)
                        visualScale = sourceScale * (0.65f / maxDimension);
                    break;
                }
                if (_visual == null) return;
                _visual.name = "EquippedDeliveryBag";
                _visual.transform.localPosition = mountOffset;
                _visual.transform.localRotation = visualRotation;
                _visual.transform.localScale = visualScale;
                foreach (var col in _visual.GetComponentsInChildren<Collider>(true)) Destroy(col);
                int localLayer = LayerMask.NameToLayer("LocalPlayerModel");
                if (_player != null && _player.IsOwner && localLayer >= 0)
                    SetLayer(_visual.transform, localLayer);
            }
            _visual.SetActive(true);
        }

        private void OnDestroy() { if (_visual != null) Destroy(_visual); }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            foreach (Transform child in root) SetLayer(child, layer);
        }
    }
}
