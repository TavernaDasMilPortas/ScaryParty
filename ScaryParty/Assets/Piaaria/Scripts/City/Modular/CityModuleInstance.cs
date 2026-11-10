using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Componente presente na raiz de cada prefab de módulo instanciado no mundo.
    /// Identifica a peça, o prédio e o pavimento, e mantém as referências de hierarquia.
    /// </summary>
    [SelectionBase]
    public class CityModuleInstance : MonoBehaviour
    {
        [Header("Identidade e Hierarquia")]
        public CityModuleId moduleId;
        public string buildingId;
        public int blockId = -1;
        public int floorIndex = 0;
        public int slotIndex = 0;
        public FacadeSide side = FacadeSide.Front;

        [Header("Sub-estruturas Padronizadas")]
        public Transform visualRoot;
        public Transform collisionRoot;
        public Transform anchorsRoot;
        public Transform movingPartsRoot;

        [Header("Âncoras Específicas")]
        public Transform doorAnchor;
        public Transform approachAnchor;
        public Transform interiorAnchor;
        public Transform deliveryAnchor;
        public Transform awningAnchor;

        [Header("Partes Móveis")]
        public Transform doorLeafLeft;
        public Transform doorLeafRight;

        public void AutoWireHierarchy()
        {
            if (visualRoot == null) visualRoot = transform.Find("Visual");
            if (collisionRoot == null) collisionRoot = transform.Find("Collision");
            if (anchorsRoot == null) anchorsRoot = transform.Find("Anchors");
            if (movingPartsRoot == null) movingPartsRoot = transform.Find("MovingParts");

            if (anchorsRoot != null)
            {
                if (doorAnchor == null) doorAnchor = anchorsRoot.Find("DoorAnchor");
                if (approachAnchor == null) approachAnchor = anchorsRoot.Find("ApproachAnchor");
                if (interiorAnchor == null) interiorAnchor = anchorsRoot.Find("InteriorAnchor");
                if (deliveryAnchor == null) deliveryAnchor = anchorsRoot.Find("DeliveryAnchor");
                if (awningAnchor == null) awningAnchor = anchorsRoot.Find("AwningAnchor");
            }

            if (movingPartsRoot != null)
            {
                if (doorLeafLeft == null) doorLeafLeft = movingPartsRoot.Find("DoorLeaf_Left") ?? movingPartsRoot.Find("DoorLeaf");
                if (doorLeafRight == null) doorLeafRight = movingPartsRoot.Find("DoorLeaf_Right");
            }
        }

        private void Reset()
        {
            AutoWireHierarchy();
        }

        private void OnValidate()
        {
            AutoWireHierarchy();
        }

        private void OnDrawGizmosSelected()
        {
            // Desenha bounds locais em gizmo
            Gizmos.matrix = transform.localToWorldMatrix;

            // Gizmo das âncoras
            if (doorAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(doorAnchor.localPosition, 0.15f);
            }
            if (approachAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(approachAnchor.localPosition, 0.15f);
            }
            if (deliveryAnchor != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f);
                Gizmos.DrawWireSphere(deliveryAnchor.localPosition, 0.15f);
            }
            if (awningAnchor != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(awningAnchor.localPosition, 0.1f);
            }

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
