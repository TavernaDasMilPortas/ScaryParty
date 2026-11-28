using System;
using UnityEngine;
using ScaryParty.Player;

namespace ScaryParty.Pizzeria.Presentation
{
    public enum AttachmentEndpoint { Head, Tail, Custom }

    [Serializable]
    public struct BoneAttachmentBinding
    {
        [Tooltip("Desativado preserva o encaixe legado. Ativado resolve o caminho no CharacterVisual.")]
        public bool enabled;
        [Tooltip("Caminho relativo ao CharacterVisualController, incluindo Armature. Não é o nome do GripPoint.")]
        public string bonePath;
        [Tooltip("Head = origem do bone. Tail = extremidade calibrada. Custom = ponto local manual.")]
        public AttachmentEndpoint endpoint;
        [Tooltip("Extremidade em coordenadas locais do bone, medida no rig de referência. Não é inferida em runtime.")]
        public Vector3 tailLocal;
        [Tooltip("Ponto local personalizado, independente do offset do item.")]
        public Vector3 customLocal;

        public Vector3 Point => endpoint == AttachmentEndpoint.Tail ? tailLocal :
            endpoint == AttachmentEndpoint.Custom ? customLocal : Vector3.zero;

        public Transform Resolve(Transform characterRoot)
        {
            if (!enabled || characterRoot == null || string.IsNullOrEmpty(bonePath)) return null;
            return characterRoot.Find(bonePath);
        }

        public static Transform ResolveRemote(Transform legacyHand, BoneAttachmentBinding binding, ref ItemPoseOffset pose)
        {
            if (!binding.enabled || legacyHand == null) return legacyHand;
            var character = legacyHand.GetComponentInParent<CharacterVisualController>(true);
            var bone = binding.Resolve(character != null ? character.transform : null);
            if (bone == null) return legacyHand;
            pose.position += binding.Point;
            return bone;
        }
    }
}
