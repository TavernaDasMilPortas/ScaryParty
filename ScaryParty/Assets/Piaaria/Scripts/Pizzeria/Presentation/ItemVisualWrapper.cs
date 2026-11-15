using System;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Presentation.Feedback;

namespace ScaryParty.Pizzeria.Presentation
{
    [DisallowMultipleComponent]
    public class ItemVisualWrapper : MonoBehaviour
    {
        public ulong ItemId { get; set; }
        public int DefinitionId { get; set; }
        public byte Category { get; set; }
        public byte PrepStage { get; set; }
        public bool IsTool { get; set; }
        public bool IsFallbackPrimitive { get; set; }
        public int VisualGeneration { get; private set; }
        public bool IsInTransit { get; set; }
        public ItemPresentationProfile CurrentProfile { get; private set; }
        public GameObject VisualInstance { get; private set; }
        public Transform PosePivot { get; private set; }
        public Transform CosmeticPivot { get; private set; }
        public GameFeelVisualPivot FeelPivot { get; private set; }
        public Transform GripPoint { get; private set; }
        public Transform SupportPoint { get; private set; }
        public Vector3 LastHandPosition { get; private set; }
        public float LastHandTime { get; private set; } = float.NegativeInfinity;

        public void EnsurePivots()
        {
            if (PosePivot == null)
            {
                var foundPose = transform.Find("PosePivot");
                if (foundPose != null)
                {
                    PosePivot = foundPose;
                }
                else
                {
                    var poseGo = new GameObject("PosePivot");
                    PosePivot = poseGo.transform;
                    PosePivot.SetParent(transform, false);
                    PosePivot.localPosition = Vector3.zero;
                    PosePivot.localRotation = Quaternion.identity;
                    PosePivot.localScale = Vector3.one;
                }
            }

            if (CosmeticPivot == null)
            {
                var foundCosmetic = PosePivot.Find("CosmeticPivot");
                if (foundCosmetic != null)
                {
                    CosmeticPivot = foundCosmetic;
                }
                else
                {
                    var cosmeticGo = new GameObject("CosmeticPivot");
                    CosmeticPivot = cosmeticGo.transform;
                    CosmeticPivot.SetParent(PosePivot, false);
                    CosmeticPivot.localPosition = Vector3.zero;
                    CosmeticPivot.localRotation = Quaternion.identity;
                    CosmeticPivot.localScale = Vector3.one;
                }
            }

            if (FeelPivot == null)
            {
                FeelPivot = CosmeticPivot.GetComponent<GameFeelVisualPivot>()
                         ?? CosmeticPivot.gameObject.AddComponent<GameFeelVisualPivot>();
                FeelPivot.SetExplicitBasePose(Vector3.zero, Quaternion.identity, Vector3.one);
            }
        }

        public void BindVisual(ItemPresentationProfile profile, GameObject visualInstance)
        {
            EnsurePivots();
            VisualGeneration++;
            FeelPivot?.StopAndReset();
            if (FeelPivot != null)
            {
                FeelPivot.VisualGeneration = VisualGeneration;
            }

            CurrentProfile = profile;
            VisualInstance = visualInstance;

            if (visualInstance != null)
            {
                StripNetworkComponents(visualInstance);

                visualInstance.transform.SetParent(CosmeticPivot, false);

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

        private static void StripNetworkComponents(GameObject target)
        {
            if (target == null) return;

            // Remove NetworkBehaviours primeiro (ex: PhysicalItem) para evitar quebra de dependências
            var behaviours = target.GetComponentsInChildren<NetworkBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] != null)
                {
                    DestroyImmediate(behaviours[i]);
                }
            }

            // Remove NetworkObjects para evitar que o Netcode intercepte transform.SetParent()
            // e reverta o parent ao mesmo tempo que lança erro de objeto não spawnado
            var netObjects = target.GetComponentsInChildren<NetworkObject>(true);
            for (int i = 0; i < netObjects.Length; i++)
            {
                if (netObjects[i] != null)
                {
                    DestroyImmediate(netObjects[i]);
                }
            }

            // Remove Rigidbodies para evitar que a física puxe o visual da mão
            var rigidbodies = target.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < rigidbodies.Length; i++)
            {
                if (rigidbodies[i] != null)
                {
                    DestroyImmediate(rigidbodies[i]);
                }
            }
        }

        private Transform _surfaceAnchor;
        private ItemPoseOffset _surfacePose;
        private bool _followsSurface;

        private void LateUpdate()
        {
            if (IsInTransit) return;

            if (!_followsSurface && transform.parent != null)
            {
                LastHandPosition = VisualInstance != null ? VisualInstance.transform.position : transform.position;
                LastHandTime = Time.unscaledTime;
            }
            if (!_followsSurface) return;
            if (_surfaceAnchor == null)
            {
                Destroy(gameObject);
                return;
            }

            UpdateSurfacePose();
        }

        private void UpdateSurfacePose()
        {
            transform.SetPositionAndRotation(
                _surfaceAnchor.TransformPoint(_surfacePose.position),
                _surfaceAnchor.rotation * Quaternion.Euler(_surfacePose.rotationEuler));
        }

        public void AttachTo(Transform parent, ItemPoseOffset pose, int targetLayer, bool useSupportPoint = false)
        {
            EnsurePivots();
            _followsSurface = useSupportPoint && parent != null;
            _surfaceAnchor = _followsSurface ? parent : null;
            _surfacePose = pose;
            // Surface visuals follow the anchor without inheriting non-uniform scale or shear.
            transform.SetParent(_followsSurface ? null : parent, false);
            SetLayerRecursively(gameObject, targetLayer);

            // Salva temporariamente qualquer offset cosmético ativo no CosmeticPivot para não contaminar a medição de alinhamento
            Vector3 savedCosmPos = CosmeticPivot.localPosition;
            Quaternion savedCosmRot = CosmeticPivot.localRotation;
            Vector3 savedCosmScale = CosmeticPivot.localScale;
            CosmeticPivot.localPosition = Vector3.zero;
            CosmeticPivot.localRotation = Quaternion.identity;
            CosmeticPivot.localScale = Vector3.one;

            PosePivot.localPosition = Vector3.zero;
            PosePivot.localRotation = Quaternion.identity;
            PosePivot.localScale = Vector3.one;

            Transform anchorPoint = (useSupportPoint && SupportPoint != null) ? SupportPoint : GripPoint;

            // Se o modelo contiver um ponto de alinhamento, compensa em PosePivot preservando escala e CosmeticPivot
            if (anchorPoint != null && VisualInstance != null)
            {
                VisualInstance.transform.localPosition = Vector3.zero;
                VisualInstance.transform.localRotation = Quaternion.identity;

                Quaternion relRot = Quaternion.Inverse(transform.rotation) * anchorPoint.rotation;
                Vector3 relPos = transform.InverseTransformPoint(anchorPoint.position);

                Quaternion invRot = Quaternion.Inverse(relRot);
                PosePivot.localRotation = invRot;
                PosePivot.localPosition = -(invRot * relPos);
            }
            else if (VisualInstance != null)
            {
                VisualInstance.transform.localPosition = Vector3.zero;
                VisualInstance.transform.localRotation = Quaternion.identity;
            }

            CosmeticPivot.localPosition = savedCosmPos;
            CosmeticPivot.localRotation = savedCosmRot;
            CosmeticPivot.localScale = savedCosmScale;

            pose.ApplyTo(transform);
            if (_followsSurface && !IsInTransit) UpdateSurfacePose();
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
