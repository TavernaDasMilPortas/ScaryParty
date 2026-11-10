using UnityEngine;

namespace ScaryParty.Player
{
    /// <summary>
    /// Controlador visual compartilhado para o personagem canônico (CharacterVisual.prefab).
    /// Mantém referências explícitas ao Animator, SkinnedMeshRenderer, CharacterAppearance e ossos/sockets,
    /// e centraliza a escrita de parâmetros de animação para Players, Previews e Clientes (NPCs).
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterVisualController : MonoBehaviour
    {
        public static readonly int AnimSpeed = Animator.StringToHash("Speed");
        public static readonly int AnimMotionSpeed = Animator.StringToHash("MotionSpeed");
        public static readonly int AnimGrounded = Animator.StringToHash("Grounded");
        public static readonly int AnimJump = Animator.StringToHash("Jump");
        public static readonly int AnimFreeFall = Animator.StringToHash("FreeFall");
        public static readonly int AnimIsPreparing = Animator.StringToHash("IsPreparing");
        public static readonly int AnimIsSitting = Animator.StringToHash("IsSitting");
        public static readonly int AnimSitDown = Animator.StringToHash("SitDown");
        public static readonly int AnimStandUp = Animator.StringToHash("StandUp");

        [Header("Referências Principais")]
        [SerializeField] private Animator _animator;
        [SerializeField] private SkinnedMeshRenderer _skinnedMeshRenderer;
        [SerializeField] private CharacterAppearance _appearance;

        [Header("Ossos e Sockets do Rig (Generic: central / chest / head / arm2.l / arm2.r)")]
        [SerializeField] private Transform _rootBone;      // central
        [SerializeField] private Transform _chestBone;     // chest
        [SerializeField] private Transform _neckBone;      // neck
        [SerializeField] private Transform _headBone;      // head
        [SerializeField] private Transform _leftHandBone;  // arm2.l
        [SerializeField] private Transform _rightHandBone; // arm2.r

        [Header("Índice da Camada de Preparo (UpperBody)")]
        [SerializeField] private int _prepareLayerIndex = 1;

        private bool _isPreparing;
        private bool _isSitting;
        private float _currentPrepareLayerWeight;

        public Animator Animator
        {
            get
            {
                if (_animator == null) ResolveReferences();
                return _animator;
            }
        }

        public SkinnedMeshRenderer SkinnedMeshRenderer
        {
            get
            {
                if (_skinnedMeshRenderer == null) ResolveReferences();
                return _skinnedMeshRenderer;
            }
        }

        public CharacterAppearance Appearance
        {
            get
            {
                if (_appearance == null) ResolveReferences();
                return _appearance;
            }
        }

        public Transform RootBone { get { if (_rootBone == null) ResolveReferences(); return _rootBone; } }
        public Transform ChestBone { get { if (_chestBone == null) ResolveReferences(); return _chestBone; } }
        public Transform NeckBone { get { if (_neckBone == null) ResolveReferences(); return _neckBone; } }
        public Transform HeadBone { get { if (_headBone == null) ResolveReferences(); return _headBone; } }
        public Transform LeftHandBone { get { if (_leftHandBone == null) ResolveReferences(); return _leftHandBone; } }
        public Transform RightHandBone { get { if (_rightHandBone == null) ResolveReferences(); return _rightHandBone; } }

        public bool IsPreparing => _isPreparing;
        public bool IsSitting => _isSitting;

        private void Awake()
        {
            ResolveReferences();
            if (_animator != null)
            {
                _animator.applyRootMotion = false;
            }
        }

        public void ResolveReferences()
        {
            if (_animator == null)
                _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>(true);

            if (_skinnedMeshRenderer == null)
                _skinnedMeshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(true);

            if (_appearance == null)
            {
                _appearance = GetComponent<CharacterAppearance>() ?? GetComponentInChildren<CharacterAppearance>(true);
                if (_appearance == null)
                    _appearance = gameObject.AddComponent<CharacterAppearance>();
            }

            if (_rootBone == null) _rootBone = FindBone("central");
            if (_chestBone == null) _chestBone = FindBone("chest");
            if (_neckBone == null) _neckBone = FindBone("neck");
            if (_headBone == null) _headBone = FindBone("head");
            if (_leftHandBone == null) _leftHandBone = FindBone("arm2.l") ?? FindBone("LeftHand");
            if (_rightHandBone == null) _rightHandBone = FindBone("arm2.r") ?? FindBone("RightHand");
        }

        public void BindReferences(
            Animator animator,
            SkinnedMeshRenderer smr,
            CharacterAppearance appearance,
            Transform rootBone,
            Transform chestBone,
            Transform neckBone,
            Transform headBone,
            Transform leftHandBone,
            Transform rightHandBone)
        {
            _animator = animator;
            _skinnedMeshRenderer = smr;
            _appearance = appearance;
            _rootBone = rootBone;
            _chestBone = chestBone;
            _neckBone = neckBone;
            _headBone = headBone;
            _leftHandBone = leftHandBone;
            _rightHandBone = rightHandBone;

            if (_animator != null)
                _animator.applyRootMotion = false;
        }

        private Transform FindBone(string exactOrPartialName)
        {
            var all = GetComponentsInChildren<Transform>(true);
            string targetLower = exactOrPartialName.ToLowerInvariant();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && string.Equals(all[i].name, exactOrPartialName, System.StringComparison.OrdinalIgnoreCase))
                    return all[i];
            }
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.ToLowerInvariant().Contains(targetLower))
                    return all[i];
            }
            return null;
        }

        public void SetLocomotion(float speed, float motionSpeed = 1f, bool grounded = true)
        {
            var anim = Animator;
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
            anim.SetFloat(AnimSpeed, speed);
            anim.SetFloat(AnimMotionSpeed, motionSpeed);
            anim.SetBool(AnimGrounded, grounded);
        }

        public void SetLocomotion(float speed, float motionSpeed, bool grounded, bool jump, bool freeFall)
        {
            SetLocomotion(speed, motionSpeed, grounded);
            SetJumpState(jump, freeFall);
        }

        public void SetJumpState(bool jump, bool freeFall)
        {
            var anim = Animator;
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;
            anim.SetBool(AnimJump, jump);
            anim.SetBool(AnimFreeFall, freeFall);
        }

        public void SetPreparing(bool preparing)
        {
            _isPreparing = preparing;
            var anim = Animator;
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

            anim.SetBool(AnimIsPreparing, preparing);

            if (_prepareLayerIndex > 0 && _prepareLayerIndex < anim.layerCount)
            {
                float targetWeight = preparing ? 1f : 0f;
                _currentPrepareLayerWeight = targetWeight;
                anim.SetLayerWeight(_prepareLayerIndex, targetWeight);
            }
        }

        public void SetSitting(bool sitting, bool triggerTransition = true)
        {
            if (_isSitting == sitting && !triggerTransition) return;
            bool changed = _isSitting != sitting;
            _isSitting = sitting;

            var anim = Animator;
            if (anim == null || !anim.isActiveAndEnabled || anim.runtimeAnimatorController == null) return;

            anim.SetBool(AnimIsSitting, sitting);
            if (sitting)
            {
                anim.SetFloat(AnimSpeed, 0f);
                anim.SetFloat(AnimMotionSpeed, 0f);
                if (triggerTransition && changed)
                {
                    anim.ResetTrigger(AnimStandUp);
                    anim.SetTrigger(AnimSitDown);
                }
            }
            else if (triggerTransition && changed)
            {
                anim.ResetTrigger(AnimSitDown);
                anim.SetTrigger(AnimStandUp);
            }
        }

        public void SetColor(Color color)
        {
            if (Appearance != null)
            {
                Appearance.ApplyPrimaryColor(color);
            }
        }
    }
}
