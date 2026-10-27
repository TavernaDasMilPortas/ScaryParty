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
        public Transform SupportPoint { get; private set; }

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

                GripPoint = (profile != null && !string.IsNullOrEmpty(profile.gripPointName))
                    ? FindDeepChild(visualInstance.transform, profile.gripPointName)
                    : null;

                SupportPoint = (profile != null && !string.IsNullOrEmpty(profile.supportPointName))
                    ? FindDeepChild(visualInstance.transform, profile.supportPointName)
                    : null;
            }
        }

        public void AttachTo(Transform parent, ItemPoseOffset pose, int targetLayer, bool useSupportPoint = false)
        {
            transform.SetParent(parent, false);
            SetLayerRecursively(gameObject, targetLayer);

            Transform anchorPoint = (useSupportPoint && SupportPoint != null) ? SupportPoint : GripPoint;

            // Se o modelo contiver um ponto de alinhamento, compensa a transformação para que o ponto fique na origem
            if (anchorPoint != null && VisualInstance != null)
            {
                VisualInstance.transform.localPosition = Vector3.zero;
                VisualInstance.transform.localRotation = Quaternion.identity;

                Quaternion relRot = Quaternion.Inverse(transform.rotation) * anchorPoint.rotation;
                Vector3 relPos = transform.InverseTransformPoint(anchorPoint.position);

                Quaternion invRot = Quaternion.Inverse(relRot);
                VisualInstance.transform.localRotation = invRot;
                VisualInstance.transform.localPosition = -(invRot * relPos);
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

        public static Transform FindDeepChild(Transform root, string childName)
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
