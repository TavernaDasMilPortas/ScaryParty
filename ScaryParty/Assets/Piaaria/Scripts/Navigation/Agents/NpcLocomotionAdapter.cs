using UnityEngine;
using ScaryParty.Player;

namespace ScaryParty.Navigation.Agents
{
    /// <summary>
    /// Bridges the NpcNavigationMotor speed and state into CharacterVisualController / Animator.
    /// Ensures consistent locomotion, idle, sitting, and turning visuals across all NPCs.
    /// </summary>
    [RequireComponent(typeof(NpcNavigationMotor))]
    public class NpcLocomotionAdapter : MonoBehaviour
    {
        public CharacterVisualController visualController;
        public NpcNavigationMotor motor;

        [Header("Tuning")]
        [SerializeField] private float _minAnimSpeedThreshold = 0.05f;
        [SerializeField] private float _walkAnimSpeed = 2.2f;

        private void Awake()
        {
            if (motor == null) motor = GetComponent<NpcNavigationMotor>();
            if (visualController == null) visualController = GetComponentInChildren<CharacterVisualController>();
        }

        public void BindVisual(CharacterVisualController visual)
        {
            visualController = visual;
            if (visualController != null)
            {
                visualController.ResolveReferences();
                if (visualController.Animator != null)
                {
                    visualController.Animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                    visualController.Animator.applyRootMotion = false;
                }
            }
        }

        private void Update()
        {
            if (visualController == null || motor == null) return;

            float currentSpeed = motor.CurrentSpeed;
            float animSpeed = currentSpeed > _minAnimSpeedThreshold ? Mathf.Clamp(currentSpeed, 0f, _walkAnimSpeed) : 0f;

            visualController.SetLocomotion(animSpeed, 1f, true);
        }

        public void SetSitting(bool sitting, bool triggerTransition = true)
        {
            if (visualController != null)
            {
                visualController.SetSitting(sitting, triggerTransition);
            }
        }
    }
}
