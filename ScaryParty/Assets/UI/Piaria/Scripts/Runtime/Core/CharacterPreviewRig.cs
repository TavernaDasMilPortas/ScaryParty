using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Rig isolado de visualização 3D do personagem para a tela de Lobby.
    /// Renderiza o modelo do jogador com iluminação própria para uma RenderTexture exibida em UI Toolkit.
    /// Gerencia ativação sob demanda para custo zero de performance em gameplay ou outras telas.
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
        private float _currentRotationY = 180f;
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

        private void EnsureSetup()
        {
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
                    camGo.transform.localPosition = new Vector3(0, 1.0f, 2.2f);
                    camGo.transform.localRotation = Quaternion.Euler(0, 180, 0);
                    previewCamera = camGo.AddComponent<Camera>();
                }
            }

            previewCamera.targetTexture = targetTexture;
            previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(0.92f, 0.85f, 0.76f, 0f); // Transparente
            previewCamera.fieldOfView = 36f;
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 20f;
            previewCamera.enabled = false; // Só liga quando a UI estiver aberta

            if (previewLight == null)
            {
                previewLight = GetComponentInChildren<Light>();
                if (previewLight == null)
                {
                    var lightGo = new GameObject("PreviewLight");
                    lightGo.transform.SetParent(transform, false);
                    lightGo.transform.localPosition = new Vector3(0.5f, 2f, 1.5f);
                    lightGo.transform.localRotation = Quaternion.Euler(35, -25, 0);
                    previewLight = lightGo.AddComponent<Light>();
                    previewLight.type = LightType.Directional;
                    previewLight.intensity = 1.3f;
                    previewLight.color = new Color(1f, 0.95f, 0.9f);
                }
            }

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
                // Tenta carregar o PlayerArmature do projeto
                defaultModelPrefab = Resources.Load<GameObject>("PlayerArmature");
                if (defaultModelPrefab == null)
                {
                    var all = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in all)
                    {
                        if (go.name == "PlayerArmature" && go.scene.name == null)
                        {
                            defaultModelPrefab = go;
                            break;
                        }
                    }
                }
            }

            if (defaultModelPrefab != null)
            {
                _spawnedModel = Instantiate(defaultModelPrefab, modelContainer);
                _spawnedModel.transform.localPosition = Vector3.zero;
                _spawnedModel.transform.localRotation = Quaternion.Euler(0, 180, 0);

                // Desativa scripts de gameplay do clone de preview
                var behaviours = _spawnedModel.GetComponentsInChildren<MonoBehaviour>();
                foreach (var b in behaviours)
                {
                    if (b != null) b.enabled = false;
                }

                var colliders = _spawnedModel.GetComponentsInChildren<Collider>();
                foreach (var c in colliders)
                {
                    if (c != null) c.enabled = false;
                }

                _renderers = _spawnedModel.GetComponentsInChildren<Renderer>();
            }
        }

        public void SetActive(bool active)
        {
            EnsureSetup();
            if (previewCamera != null)
                previewCamera.enabled = active;

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
                if (_isDragging && modelContainer != null)
                {
                    float deltaX = evt.position.x - _lastMousePos.x;
                    _lastMousePos = evt.position;
                    _currentRotationY -= deltaX * 0.7f;
                    modelContainer.localRotation = Quaternion.Euler(0, _currentRotationY, 0);
                }
            });

            previewElement.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (_isDragging)
                {
                    _isDragging = false;
                    previewElement.ReleasePointer(evt.pointerId);
                }
            });

            previewElement.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                _isDragging = false;
            });
        }

        private void OnDestroy()
        {
            if (targetTexture != null)
            {
                targetTexture.Release();
                Destroy(targetTexture);
            }
            if (Instance == this) Instance = null;
        }
    }
}
