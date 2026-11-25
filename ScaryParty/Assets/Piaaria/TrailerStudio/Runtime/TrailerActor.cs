using UnityEngine;
using ScaryParty.Player;

namespace Piaaria.TrailerStudio
{
    public class TrailerActor : MonoBehaviour
    {
        public TrailerActorId actorId;
        public CharacterVisualController visualController;
        public Animator animator;

        private GameObject _attachedProp;
        private Vector3 _moveStart;
        private Vector3 _moveEnd;
        private float _moveDuration;
        private float _moveElapsed;
        private bool _isMoving;

        public void Initialize(TrailerActorId id, CharacterVisualController visual, Color color)
        {
            actorId = id;
            visualController = visual;
            animator = visual != null ? visual.GetComponentInChildren<Animator>() : GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            if (visualController != null)
            {
                visualController.SetColor(color);
            }
        }

        public void ResetActor(Vector3 position, Quaternion rotation, Color color)
        {
            _isMoving = false;
            _moveElapsed = 0f;
            _moveDuration = 0f;
            transform.SetPositionAndRotation(position, rotation);
            DetachProp();

            if (visualController != null)
            {
                visualController.SetColor(color);
                visualController.SetLocomotion(0f, 1f, true);
                visualController.SetPreparing(false);
                visualController.SetSitting(false, false);
            }

            if (animator != null)
            {
                animator.speed = 1f;
                animator.Rebind();
                animator.Update(0f);
            }
        }

        public void StartMove(Vector3 targetPosition, float duration)
        {
            _moveStart = transform.position;
            _moveEnd = targetPosition;
            _moveDuration = Mathf.Max(0.01f, duration);
            _moveElapsed = 0f;
            _isMoving = true;
            if (visualController != null)
                visualController.SetLocomotion(2.0f, 1f, true);
        }

        public void TickMove(float dt)
        {
            if (!_isMoving) return;
            _moveElapsed += dt;
            float t = Mathf.Clamp01(_moveElapsed / _moveDuration);
            Vector3 nextPos = Vector3.Lerp(_moveStart, _moveEnd, t);
            Vector3 dir = _moveEnd - _moveStart;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, 360f * dt);
            }
            transform.position = nextPos;

            if (t >= 1f)
            {
                _isMoving = false;
                if (visualController != null)
                    visualController.SetLocomotion(0f, 1f, true);
            }
        }

        public void FacePosition(Vector3 targetPosition)
        {
            Vector3 dir = targetPosition - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        public void SetPreparing(bool preparing)
        {
            if (visualController != null)
                visualController.SetPreparing(preparing);
        }

        public void SetSitting(bool sitting, bool facingRight)
        {
            if (visualController != null)
                visualController.SetSitting(sitting, facingRight);
        }

        public void AttachProp(GameObject prop, bool rightHand = true)
        {
            DetachProp();
            if (prop == null) return;
            _attachedProp = prop;
            Transform bone = visualController != null
                ? (rightHand ? visualController.RightHandBone : visualController.LeftHandBone)
                : transform;
            if (bone == null) bone = transform;
            prop.transform.SetParent(bone, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.SetActive(true);
        }

        public GameObject DetachProp()
        {
            GameObject p = _attachedProp;
            _attachedProp = null;
            return p;
        }

        public void SetPaused(bool paused)
        {
            if (animator != null)
                animator.speed = paused ? 0f : 1f;
        }
    }
}
