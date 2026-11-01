using System;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Rig isolado de visualização 3D do personagem para a tela de Lobby.
    /// Renderiza o modelo do jogador com iluminação própria para uma RenderTexture exibida em UI Toolkit (Raw Image).
    /// Fica isolado no espaço distante (-3000, -3000, -3000) com scripts de gameplay e física desativados.
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

            previewCamera.transform.localPosition = new Vector3(0f, 1.05f, 2.1f);
            previewCamera.transform.localRotation = Quaternion.Euler(6f, 180f, 0f);
            previewCamera.targetTexture = targetTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.99f, 0.94f, 0.88f, 1f); // Creme papel suave
            previewCamera.fieldOfView = 34f;
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
                defaultModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab");
#endif
                if (defaultModelPrefab == null)
                {
                    defaultModelPrefab = Resources.Load<GameObject>("PlayerArmature");
                }
            }

            if (defaultModelPrefab != null)
            {
                _spawnedModel = Instantiate(defaultModelPrefab, modelContainer);
                _spawnedModel.transform.localPosition = Vector3.zero;
                _spawnedModel.transform.localRotation = Quaternion.identity;

                // Desativa completamente componentes de gameplay, física, câmera e áudio do clone de preview
                var behaviours = _spawnedModel.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var b in behaviours)
                {
                    if (b != null) b.enabled = false;
                }

                var colliders = _spawnedModel.GetComponentsInChildren<Collider>(true);
                foreach (var c in colliders)
                {
                    if (c != null) c.enabled = false;
                }

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
                            Destroy(cam.gameObject);
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
                            Destroy(l.gameObject);
                        }
                    }
                }

                _renderers = _spawnedModel.GetComponentsInChildren<Renderer>(true);
            }
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
            if (_renderers == null || _renderers.Length == 0)
            {
                if (_spawnedModel != null)
                    _renderers = _spawnedModel.GetComponentsInChildren<Renderer>();
            }

            if (_renderers == null) return;

            foreach (var r in _renderers)
            {
                if (r == null) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    var mat = new Material(mats[i]);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    if (mat.HasProperty("_MainColor")) mat.SetColor("_MainColor", color);
                    mats[i] = mat;
                }
                r.materials = mats;
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
