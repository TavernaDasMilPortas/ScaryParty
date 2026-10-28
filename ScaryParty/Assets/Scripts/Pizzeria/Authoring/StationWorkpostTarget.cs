using UnityEngine;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Authoring
{
    /// <summary>
    /// Sub-alvo de interação para bancadas longas ou objetos com múltiplos postos/slots.
    /// A área de interação do slot é o(s) collider(s) deste objeto (ou dos filhos):
    /// - Slot vinculado a objeto físico (ex: prato): usa os colliders do próprio objeto e o outline vai nele.
    /// - Slot sem objeto: usa um collider menor gerado pelo builder e o outline vai na estação.
    /// </summary>
    public class StationWorkpostTarget : MonoBehaviour
    {
        public StationView targetStation;
        public int slotIndex;
        public Transform slotAnchor;

        [Tooltip("Contorno do objeto do slot. Vazio = slot sem objeto, o contorno vai na estação inteira.")]
        public ScaryParty.Interaction.OutlineTarget outlineTarget;
    }
}
