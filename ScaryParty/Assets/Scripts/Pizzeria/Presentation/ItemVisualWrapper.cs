using System;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    [DisallowMultipleComponent]
    public class ItemVisualWrapper : MonoBehaviour
    {
        public ulong ItemId { get; set; }
        public bool IsTool { get; set; }
        public ItemPresentationProfile CurrentProfile { get; private set; }
        public GameObject VisualInstance { get; private set; }
        public Transform GripPoint { get; private set; }

        public void BindVisual(ItemPresentationProfile profile, GameObject visualInstance)
        {
            CurrentProfile = profile;
            VisualInstance = visualInstance;

            if (visualInstance != null)
            {
                visualInstance.transform.SetParent(transform, false);

                // Desativa colisores para evitar bloquear raycasts e empurrar o jogador
                var colliders = visualInstance.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < colliders.Length; i++)
                {
                    colliders[i].enabled = false;
                }

                GripPoint = !string.IsNullOrEmpty(profile.gripPointName)
                    ? FindDeepChild(visualInstance.transform, profile.gripPointName)
                    : null;
            }
        }

        public void AttachTo(Transform parent, ItemPoseOffset pose, int targetLayer)
        {
            transform.SetParent(parent, false);
            SetLayerRecursively(gameObject, targetLayer);

            // Se o modelo contiver um GripPoint, compensa o offset do modelo para que o GripPoint fique no centro do socket
            if (GripPoint != null && VisualInstance != null)
            {
                VisualInstance.transform.localPosition = -GripPoint.localPosition;
                VisualInstance.transform.localRotation = Quaternion.Inverse(GripPoint.localRotation);
            }
            else if (VisualInstance != null)
            {
                VisualInstance.transform.localPosition = Vector3.zero;
                VisualInstance.transform.localRotation = Quaternion.identity;
            }

            pose.ApplyTo(transform);
        }

        public static void SetLayerRecursively(GameObject obj, int layer)
        {
            if (obj == null) return;
            obj.layer = layer;
            foreach (Transform t in obj.transform)
            {
                SetLayerRecursively(t.gameObject, layer);
            }
        }

        private static Transform FindDeepChild(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName)) return null;
            foreach (Transform child in root)
            {
                if (child.name.Equals(childName, StringComparison.OrdinalIgnoreCase)) return child;
                var res = FindDeepChild(child, childName);
                if (res != null) return res;
            }
            return null;
        }
    }
}
