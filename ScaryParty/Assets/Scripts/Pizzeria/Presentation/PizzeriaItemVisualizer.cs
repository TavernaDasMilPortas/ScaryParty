using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Presentation
{
    public class PizzeriaItemVisualizer : NetworkBehaviour
    {
        public static PizzeriaItemVisualizer Instance { get; private set; }

        private Dictionary<ulong, GameObject> _visuals = new Dictionary<ulong, GameObject>();

        public GameObject pizzaBoxPrefab; // Optional: If null, we use a brown cube
        public GameObject pizzaBasePrefab; // Optional: If null, we use a cylinder
        public GameObject doughPrefab; // Optional: If null, we use a sphere

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.Items.OnListChanged += OnItemsChanged;
                // Render existing items
                foreach (var item in PizzeriaNetworkState.Instance.Items)
                {
                    UpdateOrSpawnVisual(item);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (PizzeriaNetworkState.Instance != null)
            {
                PizzeriaNetworkState.Instance.Items.OnListChanged -= OnItemsChanged;
            }
        }

        private void OnItemsChanged(NetworkListEvent<NetItemDto> changeEvent)
        {
            if (changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Remove ||
                changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.RemoveAt)
            {
                if (_visuals.TryGetValue(changeEvent.Value.ItemId, out var go))
                {
                    Destroy(go);
                    _visuals.Remove(changeEvent.Value.ItemId);
                }
            }
            else if (changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Add ||
                     changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Insert ||
                     changeEvent.Type == NetworkListEvent<NetItemDto>.EventType.Value)
            {
                UpdateOrSpawnVisual(changeEvent.Value);
            }
        }

        private void UpdateOrSpawnVisual(NetItemDto item)
        {
            if (!_visuals.TryGetValue(item.ItemId, out var go))
            {
                go = CreateVisualPrimitive(item);
                _visuals[item.ItemId] = go;
            }

            // Update appearance based on state
            UpdateAppearance(go, item);

            // Update Position
            Transform parent = null;
            Vector3 targetPos = Vector3.zero;
            Quaternion targetRot = Quaternion.identity;

            if (item.LocationType == (byte)LocationType.StationSlot)
            {
                var stations = FindObjectsByType<StationView>(FindObjectsSortMode.None);
                foreach (var st in stations)
                {
                    if (st.stationId == item.SlotId)
                    {
                        parent = st.transform;
                        targetPos = st.GetSlotPosition(0);
                        targetRot = st.GetSlotRotation(0);
                        break;
                    }
                }
            }
            else if (item.LocationType == (byte)LocationType.Hand)
            {
                var players = FindObjectsByType<PlayerInteraction>(FindObjectsSortMode.None);
                foreach (var p in players)
                {
                    var netObj = p.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.NetworkObjectId == item.HolderId)
                    {
                        parent = p.transform;
                        if (parent != null)
                        {
                            targetPos = parent.position + parent.up * 1.2f + parent.forward * 0.6f;
                            targetRot = parent.rotation;
                        }
                        break;
                    }
                }
            }

            if (parent != null)
            {
                go.transform.position = Vector3.Lerp(go.transform.position, targetPos, 0.5f); // Smooth quick snap
                go.transform.rotation = Quaternion.Lerp(go.transform.rotation, targetRot, 0.5f);
            }
        }

        private GameObject CreateVisualPrimitive(NetItemDto item)
        {
            GameObject go;
            bool isIngredient = item.Category == (byte)ItemCategory.Dough || 
                                item.Category == (byte)ItemCategory.Sauce || 
                                item.Category == (byte)ItemCategory.Cheese || 
                                item.Category == (byte)ItemCategory.Topping;
                                
            if (item.PackagingState == (byte)PackagingState.Boxed || item.Category == (byte)ItemCategory.Box)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
                go.GetComponent<Renderer>().material.color = new Color(0.6f, 0.4f, 0.2f); // Brown
            }
            else if (isIngredient)
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                go.GetComponent<Renderer>().material.color = Color.white;
            }
            else
            {
                // Pizza Base
                go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
            }
            
            // Remove colliders so it doesn't interfere with physics
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            return go;
        }

        private void UpdateAppearance(GameObject go, NetItemDto item)
        {
            var rend = go.GetComponent<Renderer>();
            if (rend == null) return;

            if (item.PackagingState == (byte)PackagingState.Boxed)
            {
                rend.material.color = new Color(0.6f, 0.4f, 0.2f); // Brown cardboard
                go.transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
            }
            else if (item.Category == (byte)ItemCategory.PizzaBase)
            {
                go.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
                if (item.CookingStage == (byte)CookingStage.Uncooked) rend.material.color = new Color(1f, 0.9f, 0.7f); // Pale dough
                else if (item.CookingStage == (byte)CookingStage.Baked) rend.material.color = new Color(0.8f, 0.5f, 0.2f); // Baked crust
                else if (item.CookingStage == (byte)CookingStage.Burned) rend.material.color = Color.black;
            }
        }

        private void Update()
        {
            // Continuously lerp items to their target anchors just to keep them locked
            foreach (var item in PizzeriaNetworkState.Instance.Items)
            {
                UpdateOrSpawnVisual(item);
            }
        }
    }
}
