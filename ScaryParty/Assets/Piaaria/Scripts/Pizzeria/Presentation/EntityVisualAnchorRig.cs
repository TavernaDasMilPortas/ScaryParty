using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Rig explícito de âncoras visuais para substituição segura de malhas provisórias por assets finais (P5).
    /// Mantém pivôs configuráveis independentemente de ossos importados ou "tails" do Blender.
    /// </summary>
    public class EntityVisualAnchorRig : MonoBehaviour
    {
        public enum RigRole
        {
            Generic = 0,
            Weapon = 1,
            CharacterAccessories = 2,
            Motorcycle = 3,
            Station = 4,
            Monster = 5
        }

        [Header("Perfil e Gizmos")]
        public RigRole rigRole = RigRole.Generic;
        public string placeholderId;

        [Header("Âncoras de Arma")]
        public Transform gripAnchor;
        public Transform aimSightAnchor;
        public Transform muzzleAnchor;
        public Transform slingAnchor;

        [Header("Âncoras de Personagem / Funcionário")]
        public Transform headAccessoryAnchor;
        public Transform backpackBackAnchor;
        public Transform apronAnchor;

        [Header("Âncoras de Moto")]
        public Transform seatAnchor;
        public Transform handlebarLeftAnchor;
        public Transform handlebarRightAnchor;
        public Transform leftDismountAnchor;
        public Transform rightDismountAnchor;
        public Transform cargoAnchor;

        [Header("Âncoras de Bancada / Estação")]
        public Transform[] stationSlots;

        public Transform ResolveAnchor(ref Transform field, string childName, Vector3 defaultLocalPos, Vector3 defaultLocalEuler)
        {
            if (field != null) return field;
            Transform existing = transform.Find(childName);
            if (existing != null)
            {
                field = existing;
                return field;
            }

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = defaultLocalPos;
            go.transform.localRotation = Quaternion.Euler(defaultLocalEuler);
            field = go.transform;
            return field;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            DrawAnchorGizmo(gripAnchor, Color.green, "Grip", 0.04f);
            DrawAnchorGizmo(aimSightAnchor, Color.cyan, "Sight", 0.03f);
            DrawAnchorGizmo(muzzleAnchor, Color.red, "Muzzle", 0.035f);
            DrawAnchorGizmo(slingAnchor, new Color(1f, 0.55f, 0.1f), "Sling", 0.06f);
            DrawAnchorGizmo(headAccessoryAnchor, Color.yellow, "HeadAcc", 0.07f);
            DrawAnchorGizmo(backpackBackAnchor, Color.magenta, "BackPack", 0.08f);
            DrawAnchorGizmo(apronAnchor, new Color(0.4f, 0.8f, 1f), "Apron", 0.07f);
            DrawAnchorGizmo(seatAnchor, Color.green, "Seat", 0.10f);
            DrawAnchorGizmo(handlebarLeftAnchor, Color.cyan, "GripL", 0.06f);
            DrawAnchorGizmo(handlebarRightAnchor, Color.cyan, "GripR", 0.06f);
            DrawAnchorGizmo(leftDismountAnchor, Color.blue, "ExitL", 0.14f);
            DrawAnchorGizmo(rightDismountAnchor, Color.blue, "ExitR", 0.14f);
            DrawAnchorGizmo(cargoAnchor, new Color(1f, 0.65f, 0.1f), "Cargo", 0.12f);

            if (stationSlots != null)
            {
                for (int i = 0; i < stationSlots.Length; i++)
                {
                    DrawAnchorGizmo(stationSlots[i], new Color(0.4f, 1f, 0.4f), $"Slot{i}", 0.08f);
                }
            }
        }

        private static void DrawAnchorGizmo(Transform t, Color color, string label, float radius)
        {
            if (t == null) return;
            Gizmos.color = color;
            Gizmos.DrawWireSphere(t.position, radius);
            Gizmos.DrawRay(t.position, t.forward * (radius * 2.5f));
            UnityEditor.Handles.color = color;
            UnityEditor.Handles.Label(t.position + Vector3.up * (radius * 1.4f), label);
        }
#endif
    }
}
