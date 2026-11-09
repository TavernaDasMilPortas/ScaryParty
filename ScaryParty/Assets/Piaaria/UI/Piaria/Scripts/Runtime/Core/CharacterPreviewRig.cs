using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Rig isolado de visualização 3D do personagem para a tela de Lobby e Sala de Espera.
    /// Renderiza o modelo do jogador com iluminação própria para uma RenderTexture exibida em UI Toolkit (Raw Image).
    /// Fica isolado no espaço distante (-3000, -3000, -3000) com scripts de gameplay, física e rede rigorosamente ausentes.
    /// </summary>
    public class CharacterPreviewRig : MonoBehaviour
    {
        public static CharacterPreviewRig Instance { get; private set; }

        [Header("Render Texture")]
        public RenderTexture targetTexture;
        public Camera previewCamera;
        public Light previewLight;
        public Transform modelContainer;
        public GameObject defaultModelPrefab;

        private GameObject _spawnedModel;
        private Renderer[] _renderers;
        private float _currentRotationY = 0f;
        private bool _isDragging = false;
        private Vector2 _lastMousePos;

        public RenderTexture RenderTexture => targetTexture;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            EnsureSetup();
        }

        private void OnDestroy()
        {
            if (targetTexture != null)
            {
                targetTexture.Release();
                Destroy(targetTexture);
                targetTexture = null;
            }
            if (_spawnedModel != null)
            {
                Destroy(_spawnedModel);
                _spawnedModel = null;
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void EnsureSetup()
        {
            // Posiciona o rig longe do mundo jogável
            transform.position = new Vector3(-3000f, -3000f, -3000f);

            if (targetTexture == null)
            {
                targetTexture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
                {
                    name = "Piaria_CharacterPreview_RT"
                };
                targetTexture.Create();
            }

            if (previewCamera == null)
            {
                previewCamera = GetComponentInChildren<Camera>();
                if (previewCamera == null)
                {
                    var camGo = new GameObject("PreviewCamera");
                    camGo.transform.SetParent(transform, false);
                    previewCamera = camGo.AddComponent<Camera>();
                }
            }

            previewCamera.transform.localPosition = new Vector3(0f, 0.82f, 2.35f);
            previewCamera.transform.localRotation = Quaternion.Euler(4f, 180f, 0f);
            previewCamera.targetTexture = targetTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.99f, 0.94f, 0.88f, 1f); // Creme papel suave
            previewCamera.fieldOfView = 38f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 20f;
            previewCamera.enabled = false;

            if (previewLight == null)
            {
                previewLight = GetComponentInChildren<Light>();
                if (previewLight == null)
                {
                    var lightGo = new GameObject("PreviewLight");
                    lightGo.transform.SetParent(transform, false);
                    previewLight = lightGo.AddComponent<Light>();
                }
            }

            previewLight.transform.localPosition = new Vector3(0.6f, 2.2f, 1.6f);
            previewLight.transform.localRotation = Quaternion.Euler(35f, -25f, 0f);
            previewLight.type = LightType.Directional;
            previewLight.intensity = 1.35f;
            previewLight.color = new Color(1f, 0.96f, 0.92f);

            if (modelContainer == null)
            {
                var container = transform.Find("ModelContainer");
                if (container == null)
                {
                    var cGo = new GameObject("ModelContainer");
                    cGo.transform.SetParent(transform, false);
                    cGo.transform.localPosition = Vector3.zero;
                    modelContainer = cGo.transform;
                }
                else
                {
                    modelContainer = container;
                }
            }

            EnsureModel();
        }

        private void EnsureModel()
        {
            if (_spawnedModel != null) return;

            if (defaultModelPrefab == null)
            {
#if UNITY_EDITOR
                defaultModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");
                if (defaultModelPrefab == null)
                {
                    defaultModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature_Preview.prefab");
                }
                if (defaultModelPrefab == null)
                {
                    defaultModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab");
                }
#endif
                if (defaultModelPrefab == null)
                {
                    defaultModelPrefab = Resources.Load<GameObject>("CharacterVisual")
                        ?? Resources.Load<GameObject>("PlayerArmature_Preview") 
                        ?? Resources.Load<GameObject>("PlayerArmature");
                }
            }

            if (defaultModelPrefab != null)
            {
                _spawnedModel = Instantiate(defaultModelPrefab, modelContainer);
                _spawnedModel.transform.localPosition = Vector3.zero;
                _spawnedModel.transform.localRotation = Quaternion.identity;

                // Higienização segura em ordem inversa de dependência (caso venha do PlayerArmature com scripts)
                // Remove primeiro os scripts folha (que dependem de outros)
                var weaponInput = _spawnedModel.GetComponent<WeaponInputHandler>();
                if (weaponInput != null) DestroyImmediate(weaponInput);

                var weaponCtrl = _spawnedModel.GetComponent<WeaponController>();
                if (weaponCtrl != null) DestroyImmediate(weaponCtrl);

                var tpc = _spawnedModel.GetComponent<StarterAssets.ThirdPersonController>();
                if (tpc != null) DestroyImmediate(tpc);

                var bPush = _spawnedModel.GetComponent("BasicRigidBodyPush");
                if (bPush != null) DestroyImmediate(bPush);

                var starterInputs = _spawnedModel.GetComponent<StarterAssets.StarterAssetsInputs>();
                if (starterInputs != null) DestroyImmediate(starterInputs);

                var playerInput = _spawnedModel.GetComponent<UnityEngine.InputSystem.PlayerInput>();
                if (playerInput != null) DestroyImmediate(playerInput);

                var cc = _spawnedModel.GetComponent<CharacterController>();
                if (cc != null) DestroyImmediate(cc);

                var playerState = _spawnedModel.GetComponent<PlayerState>();
                if (playerState != null) DestroyImmediate(playerState);

                var netTransform = _spawnedModel.GetComponent<Unity.Netcode.Components.NetworkTransform>();
                if (netTransform != null) DestroyImmediate(netTransform);

                var netObj = _spawnedModel.GetComponent<Unity.Netcode.NetworkObject>();
                if (netObj != null) DestroyImmediate(netObj);

                var camSetup = _spawnedModel.GetComponent<PlayerCameraSetup>();
                if (camSetup != null) DestroyImmediate(camSetup);

                var fpCam = _spawnedModel.GetComponent<ScaryParty.Player.FirstPersonCameraController>();
                if (fpCam != null) DestroyImmediate(fpCam);

                var pInteraction = _spawnedModel.GetComponent<PlayerInteraction>();
                if (pInteraction != null) DestroyImmediate(pInteraction);

                var minimap = _spawnedModel.GetComponent<MinimapController>();
                if (minimap != null) DestroyImmediate(minimap);

                var outline = _spawnedModel.GetComponent<ScaryParty.Interaction.OutlineService>();
                if (outline != null) DestroyImmediate(outline);

                StripRemainingComponentsSafely(_spawnedModel);

                // Desativa e remove com segurança câmeras de gameplay do modelo clonado
                var cams = _spawnedModel.GetComponentsInChildren<Camera>(true);
                foreach (var cam in cams)
                {
                    if (cam != null && cam != previewCamera)
                    {
                        cam.enabled = false;
                        if (cam.gameObject != _spawnedModel)
                        {
                            cam.gameObject.SetActive(false);
                            DestroyImmediate(cam.gameObject);
                        }
                    }
                }

                // Desativa listeners de áudio do modelo clonado
                var listeners = _spawnedModel.GetComponentsInChildren<AudioListener>(true);
                foreach (var l in listeners)
                {
                    if (l != null)
                    {
                        l.enabled = false;
                        if (l.gameObject != _spawnedModel)
                        {
                            l.gameObject.SetActive(false);
                            DestroyImmediate(l.gameObject);
                        }
                    }
                }

                // Desativa quaisquer colliders remanescentes
                var colliders = _spawnedModel.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                {
                    if (c != null) c.enabled = false;
                }

                _renderers = _spawnedModel.GetComponentsInChildren<Renderer>(true);
            }
        }

        private static void StripRemainingComponentsSafely(GameObject go)
        {
            int safetyLimit = 50;
            while (safetyLimit-- > 0)
            {
                var comps = go.GetComponents<Component>();
                Component compToRemove = null;

                foreach (var c in comps)
                {
                    if (c == null || c is Transform || c is Animator ||
                        c is ScaryParty.Player.CharacterAppearance ||
                        c is ScaryParty.Player.CharacterVisualController)
                        continue;

                    if (!IsRequiredByAny(go, c))
                    {
                        compToRemove = c;
                        break;
                    }
                }

                if (compToRemove == null) break;

                DestroyImmediate(compToRemove);
            }
        }

        private static bool IsRequiredByAny(GameObject go, Component target)
        {
            var comps = go.GetComponents<Component>();
            var targetType = target.GetType();

            foreach (var c in comps)
            {
                if (c == null || c == target) continue;

                var reqAttrs = c.GetType().GetCustomAttributes(typeof(RequireComponent), true);
                foreach (RequireComponent req in reqAttrs)
                {
                    if ((req.m_Type0 != null && req.m_Type0.IsAssignableFrom(targetType)) ||
                        (req.m_Type1 != null && req.m_Type1.IsAssignableFrom(targetType)) ||
                        (req.m_Type2 != null && req.m_Type2.IsAssignableFrom(targetType)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void SetActive(bool active)
        {
            EnsureSetup();
            if (previewCamera != null)
            {
                previewCamera.enabled = active;
                if (active)
                {
                    previewCamera.Render();
                }
            }

            if (previewLight != null)
                previewLight.enabled = active;

            if (modelContainer != null)
                modelContainer.gameObject.SetActive(active);
        }

        public void SetPlayerColor(Color color)
        {
            if (_spawnedModel != null)
            {
                var appearance = _spawnedModel.GetComponentInChildren<ScaryParty.Player.CharacterAppearance>(true);
                if (appearance != null)
                {
                    appearance.ApplyPrimaryColor(color);
                    if (previewCamera != null && previewCamera.enabled)
                    {
                        previewCamera.Render();
                    }
                    return;
                }
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                if (_spawnedModel != null)
                    _renderers = _spawnedModel.GetComponentsInChildren<Renderer>();
            }

            if (_renderers == null) return;

            var mpb = new MaterialPropertyBlock();
            foreach (var r in _renderers)
            {
                if (r == null || r.sharedMaterials == null) continue;
                if (r.sharedMaterials.Length == 4)
                {
                    r.GetPropertyBlock(mpb, 3);
                    mpb.SetColor("_BaseColor", color);
                    mpb.SetColor("_Color", color);
                    mpb.SetColor("_MainColor", color);
                    r.SetPropertyBlock(mpb, 3);
                }
                else
                {
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        r.GetPropertyBlock(mpb, i);
                        mpb.SetColor("_BaseColor", color);
                        mpb.SetColor("_Color", color);
                        mpb.SetColor("_MainColor", color);
                        r.SetPropertyBlock(mpb, i);
                    }
                }
            }

            if (previewCamera != null && previewCamera.enabled)
            {
                previewCamera.Render();
            }
        }

        public void BindDragEvents(VisualElement previewElement)
        {
            if (previewElement == null) return;

            previewElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                _isDragging = true;
                _lastMousePos = evt.position;
                previewElement.CapturePointer(evt.pointerId);
            });

            previewElement.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!_isDragging || modelContainer == null) return;
                float deltaX = evt.position.x - _lastMousePos.x;
                _lastMousePos = evt.position;
                _currentRotationY -= deltaX * 0.9f;
                modelContainer.localRotation = Quaternion.Euler(0, _currentRotationY, 0);

                if (previewCamera != null && previewCamera.enabled)
                {
                    previewCamera.Render();
                }
            });

            previewElement.RegisterCallback<PointerUpEvent>(evt =>
            {
                _isDragging = false;
                previewElement.ReleasePointer(evt.pointerId);
            });
        }
    }
}
