using System;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Presentation
{
    [Serializable]
    public struct ItemPoseOffset
    {
        public Vector3 position;
        public Vector3 rotationEuler;
        public Vector3 scale;

        public static ItemPoseOffset Identity => new ItemPoseOffset
        {
            position = Vector3.zero,
            rotationEuler = Vector3.zero,
            scale = Vector3.one
        };

        public void ApplyTo(Transform target)
        {
            if (target == null) return;
            target.localPosition = position;
            target.localEulerAngles = rotationEuler;
            target.localScale = (scale != Vector3.zero) ? scale : Vector3.one;
        }
    }

    [CreateAssetMenu(fileName = "Profile_", menuName = "Scary Party/Pizzeria/Item Presentation Profile")]
    public class ItemPresentationProfile : ScriptableObject
    {
        [Header("Identificação Estável")]
        [Tooltip("Chave de apresentação estável (ex: Ingredient_1, Tool_1, Pizza_Raw, Pizza_Baked, Boxed_Pizza)")]
        public string presentationKey;
        public ItemCategory category = ItemCategory.Other;
        public int definitionId = 0;
        public bool isTool = false;

        [Header("Visual")]
        [Tooltip("Prefab do modelo visual (sem NetworkObject ou lógica de rede/jogabilidade)")]
        public GameObject visualPrefab;

        [Tooltip("Nome do ponto de alinhamento opcional contido no prefab")]
        public string gripPointName = "GripPoint";

        [Header("Poses FP (Sockets da Câmera em Primeira Pessoa)")]
        public ItemPoseOffset fpLeftHandPose = ItemPoseOffset.Identity;
        public ItemPoseOffset fpRightHandPose = ItemPoseOffset.Identity;

        [Header("Poses Remotas (Mão do personagem em Terceira Pessoa)")]
        public ItemPoseOffset remoteLeftHandPose = ItemPoseOffset.Identity;
        public ItemPoseOffset remoteRightHandPose = ItemPoseOffset.Identity;

        [Header("Pose de Bancada (Ajuste sobre âncora de slot da estação)")]
        public ItemPoseOffset stationSlotPose = ItemPoseOffset.Identity;

        [Header("Metadados")]
        public bool isPlaceholder = false;
        [TextArea] public string notes;
    }
}
