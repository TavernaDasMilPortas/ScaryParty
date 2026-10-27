using UnityEngine;
using StarterAssets;
using Unity.Netcode;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace ScaryParty.Player
{
    [DefaultExecutionOrder(-10)]
    public class FirstPersonCameraController : NetworkBehaviour
    {
        public static FirstPersonCameraController LocalInstance { get; private set; }

        [Header("Rig Serializado")]
        [Tooltip("Pivot fixo da visão (altura dos olhos), independente do osso da cabeça")]
        [SerializeField] private Transform _cameraPivot;
        [Tooltip("Câmera de primeira pessoa")]
        [SerializeField] private Camera _playerCamera;
        [Tooltip("Âncora da mão esquerda (filho da câmera ou pivot)")]
        [SerializeField] private Transform _leftHandAnchor;
        [Tooltip("Âncora da mão direita (filho da câmera ou pivot)")]
        [SerializeField] private Transform _rightHandAnchor;

        [Header("Configurações de Visão")]
        [Tooltip("Altura fixa dos olhos em relação à raiz do jogador")]
        public float eyeHeight = 1.65f;
        [Tooltip("Avanço da câmera para frente da raiz")]
        public float forwardOffset = 0.05f;
        public float mouseSensitivity = 1.0f;
        public float gamepadSensitivity = 60.0f;
        [Tooltip("Limite superior de inclinação em graus (olhar para baixo, ângulo Euler X positivo)")]
        public float topClamp = 89.0f;
        [Tooltip("Limite inferior de inclinação em graus (olhar para cima, ângulo Euler X negativo)")]
        public float bottomClamp = -85.0f;
        [Tooltip("Inverte o controle vertical da visão (olhar para cima/baixo)")]
        public bool invertPitch = false;
        [Tooltip("Inverte o controle horizontal da visão (olhar para esquerda/direita)")]
        public bool invertYaw = false;

        [Header("Efeitos de Mão (Desativados por padrão)")]
        public bool enableHandSwayBob = false;
        public float handSwayAmount = 0.0f;
        public float handBobAmount = 0.0f;

        public Transform CameraPivot => _cameraPivot;
        public Camera PlayerCamera => _playerCamera;
        public Transform LeftHandAnchor => _leftHandAnchor;
        public Transform RightHandAnchor => _rightHandAnchor;

        private ThirdPersonController _tpc;
        private StarterAssetsInputs _starterInputs;
        private CharacterController _cc;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput _playerInput;
#endif

        private Vector3 _leftAnchorBasePos = new Vector3(-0.32f, -0.25f, 0.45f);
        private Vector3 _rightAnchorBasePos = new Vector3(0.32f, -0.25f, 0.45f);
        
        private float _yaw;
        private float _pitch;

        private void Awake()
        {
            _tpc = GetComponent<ThirdPersonController>();
            _starterInputs = GetComponent<StarterAssetsInputs>();
            _cc = GetComponent<CharacterController>();
#if ENABLE_INPUT_SYSTEM
            _playerInput = GetComponent<PlayerInput>();
#endif
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (!IsOwner)
            {
                if (_playerCamera != null)
                {
                    _playerCamera.gameObject.SetActive(false);
                    var listener = _playerCamera.GetComponent<AudioListener>();
                    if (listener != null) listener.enabled = false;
                }
                enabled = false;
                return;
            }

            LocalInstance = this;

            EnsureRig();

            // Inicializa yaw e pitch alinhados ao jogador atual
            _yaw = transform.eulerAngles.y;
            _pitch = 0f;

            if (_playerCamera != null)
            {
                _playerCamera.gameObject.SetActive(true);
                var listener = _playerCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = true;

                ConfigureCameraCulling(_playerCamera);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (LocalInstance == this)
            {
                LocalInstance = null;
            }
            base.OnNetworkDespawn();
        }

        public override void OnDestroy()
        {
            if (LocalInstance == this)
            {
                LocalInstance = null;
            }
            base.OnDestroy();
        }

        public void SetYawPitch(float yaw, float pitch = 0f)
        {
            _yaw = yaw;
            _pitch = Mathf.Clamp(pitch, bottomClamp, topClamp);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_cameraPivot != null)
            {
                _cameraPivot.localPosition = new Vector3(0f, eyeHeight, forwardOffset);
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// Retorna true se qualquer menu, modal ou interface estiver aberta, ou se o cursor estiver liberado/visível.
        /// </summary>
        public bool IsMenuOrInputBlocked()
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked) return true;
            if (_starterInputs != null && (!_starterInputs.cursorInputForLook || !_starterInputs.cursorLocked)) return true;
            if (Cursor.lockState != CursorLockMode.Locked) return true;
            if (Cursor.visible) return true;
            return false;
        }

        private void Update()
        {
            if (!IsOwner) return;

            // Quando estiver em qualquer menu, o mouse não pode rotacionar a câmera
            if (IsMenuOrInputBlocked())
            {
                if (_starterInputs != null)
                {
                    _starterInputs.look = Vector2.zero;
                }
                return;
            }

            if (_starterInputs != null)
            {
                bool isMouse = true;
#if ENABLE_INPUT_SYSTEM
                if (_playerInput != null)
                {
                    isMouse = _playerInput.currentControlScheme == "KeyboardMouse";
                }
#endif
                float deltaMult = isMouse ? 1.0f : Time.deltaTime;
                float sens = isMouse ? mouseSensitivity : gamepadSensitivity;

                float yawFactor = invertYaw ? -1f : 1f;
                _yaw += _starterInputs.look.x * sens * deltaMult * yawFactor;

                // Em Unity Euler X, rotação positiva inclina a câmera para baixo e negativa para cima.
                // Padrão (invertPitch == false): mover mouse para cima (look.y > 0) deve inclinar para cima (-1f).
                // Invertido (invertPitch == true): mover mouse para cima inclina para baixo (+1f).
                float pitchFactor = invertPitch ? 1f : -1f;
                _pitch = Mathf.Clamp(_pitch + (_starterInputs.look.y * sens * deltaMult * pitchFactor), bottomClamp, topClamp);

                // O yaw gira a raiz do personagem no plano horizontal
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

                // O pitch gira apenas o pivot da câmera. Roll é rigorosamente 0.
                if (_cameraPivot != null)
                {
                    _cameraPivot.localPosition = new Vector3(0f, eyeHeight, forwardOffset);
                    _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                }
            }
        }

        private void LateUpdate()
        {
            if (!IsOwner) return;

            // Mantém posição fixa do pivot sem oscilação de animações de esqueleto
            if (_cameraPivot != null)
            {
                _cameraPivot.localPosition = new Vector3(0f, eyeHeight, forwardOffset);
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

                if (_playerCamera != null)
                {
                    _playerCamera.transform.position = _cameraPivot.position;
                    _playerCamera.transform.rotation = _cameraPivot.rotation;
                }

                // Sincroniza CinemachineCameraTarget para não quebrar dependências de terceiros
                if (_tpc != null && _tpc.CinemachineCameraTarget != null)
                {
                    _tpc.CinemachineCameraTarget.transform.position = _cameraPivot.position;
                    _tpc.CinemachineCameraTarget.transform.rotation = _cameraPivot.rotation;
                }
            }

            UpdateHandAnchors();
        }

        private void UpdateHandAnchors()
        {
            if (_leftHandAnchor == null || _rightHandAnchor == null) return;

            if (!enableHandSwayBob)
            {
                // Posição e rotação estáveis e travadas na câmera (sem atraso de frame nem oscilação)
                _leftHandAnchor.localPosition = _leftAnchorBasePos;
                _leftHandAnchor.localRotation = Quaternion.identity;

                _rightHandAnchor.localPosition = _rightAnchorBasePos;
                _rightHandAnchor.localRotation = Quaternion.identity;
                return;
            }

            // Caso o usuário habilite sway/bob opcional nas mãos (nunca na câmera)
            float speed = _cc != null ? new Vector3(_cc.velocity.x, 0, _cc.velocity.z).magnitude : 0f;
            float lookX = _starterInputs != null ? _starterInputs.look.x : 0f;
            float lookY = _starterInputs != null ? _starterInputs.look.y : 0f;

            Vector3 swayOffset = new Vector3(
                Mathf.Clamp(-lookX * handSwayAmount, -0.05f, 0.05f),
                Mathf.Clamp(-lookY * handSwayAmount, -0.05f, 0.05f),
                0f
            );

            _leftHandAnchor.localPosition = _leftAnchorBasePos + swayOffset;
            _rightHandAnchor.localPosition = _rightAnchorBasePos + swayOffset;
        }

        public void EnsureRig()
        {
            if (_cameraPivot == null)
            {
                var pivotT = transform.Find("CameraPivot");
                if (pivotT == null)
                {
                    var pivotGo = new GameObject("CameraPivot");
                    pivotGo.transform.SetParent(transform, false);
                    pivotGo.transform.localPosition = new Vector3(0f, eyeHeight, forwardOffset);
                    pivotT = pivotGo.transform;
                }
                _cameraPivot = pivotT;
            }

            if (_playerCamera == null)
            {
                var camSetup = GetComponent<PlayerCameraSetup>();
                if (camSetup != null && camSetup.mainCamera != null)
                {
                    _playerCamera = camSetup.mainCamera.GetComponent<Camera>();
                }
                else
                {
                    _playerCamera = GetComponentInChildren<Camera>(true);
                }
            }

            Transform parentForAnchors = _playerCamera != null ? _playerCamera.transform : _cameraPivot;

            if (_leftHandAnchor == null && parentForAnchors != null)
            {
                var leftT = parentForAnchors.Find("LeftHandAnchor");
                if (leftT == null)
                {
                    var leftGo = new GameObject("LeftHandAnchor");
                    leftGo.transform.SetParent(parentForAnchors, false);
                    leftGo.transform.localPosition = _leftAnchorBasePos;
                    leftT = leftGo.transform;
                }
                _leftHandAnchor = leftT;
            }

            if (_rightHandAnchor == null && parentForAnchors != null)
            {
                var rightT = parentForAnchors.Find("RightHandAnchor");
                if (rightT == null)
                {
                    var rightGo = new GameObject("RightHandAnchor");
                    rightGo.transform.SetParent(parentForAnchors, false);
                    rightGo.transform.localPosition = _rightAnchorBasePos;
                    rightT = rightGo.transform;
                }
                _rightHandAnchor = rightT;
            }
        }

        private void ConfigureCameraCulling(Camera cam)
        {
            if (cam == null) return;

            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            if (minimapLayer >= 0) cam.cullingMask &= ~(1 << minimapLayer);

            int localPlayerLayer = LayerMask.NameToLayer("LocalPlayerModel");
            if (localPlayerLayer >= 0) cam.cullingMask &= ~(1 << localPlayerLayer);

            int firstPersonLayer = LayerMask.NameToLayer("FirstPersonOnly");
            if (firstPersonLayer >= 0) cam.cullingMask |= (1 << firstPersonLayer);
        }
    }
}
