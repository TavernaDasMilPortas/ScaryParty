using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Garante que toda cena carregada possua uma Câmera ativa tagueada como 'MainCamera'
    /// e exatamente um AudioListener funcional, eliminando os erros:
    /// 'There are no audio listeners in the scene' e 'No cameras rendering'.
    /// Funciona de forma transparente tanto em Play Mode isolado quanto em transições Netcode.
    /// </summary>
    public class SceneCameraAudioBootstrap : MonoBehaviour
    {
        private static SceneCameraAudioBootstrap _instance;
        private static GameObject _fallbackCameraGo;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            EnsureBootstrapExists();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnAfterSceneLoad()
        {
            EnsureBootstrapExists();
            ValidateSceneCameraAndAudio();
        }

        private static void EnsureBootstrapExists()
        {
            if (_instance != null) return;

            var existing = UnityEngine.Object.FindFirstObjectByType<SceneCameraAudioBootstrap>();
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            var go = new GameObject("[Piaria_CameraAudioBootstrap]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<SceneCameraAudioBootstrap>();
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ValidateSceneCameraAndAudio();
        }

        private void LateUpdate()
        {
            // Se houver uma câmera de jogador ativa no mundo jogável, desativa a câmera de fallback
            if (_fallbackCameraGo != null && _fallbackCameraGo.activeSelf)
            {
                var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var cam in allCams)
                {
                    if (cam == null || cam.gameObject == _fallbackCameraGo) continue;
                    if (cam.enabled && cam.gameObject.activeInHierarchy && cam.targetTexture == null)
                    {
                        // Câmera de gameplay encontrada
                        _fallbackCameraGo.SetActive(false);
                        break;
                    }
                }
            }
            else if (_fallbackCameraGo != null && !_fallbackCameraGo.activeSelf)
            {
                // Se todas as câmeras de gameplay forem destruídas/desativadas, reativa a fallback
                if (Camera.main == null)
                {
                    ValidateSceneCameraAndAudio();
                }
            }
        }

        public static void ValidateSceneCameraAndAudio()
        {
            // 1. Garante Câmera Principal
            Camera mainCam = Camera.main;

            if (mainCam == null)
            {
                // Procura qualquer câmera ativa sem RenderTexture
                var allCams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                foreach (var c in allCams)
                {
                    if (c != null && c.enabled && c.gameObject.activeInHierarchy && c.targetTexture == null)
                    {
                        mainCam = c;
                        mainCam.tag = "MainCamera";
                        break;
                    }
                }
            }

            // Se nenhuma câmera de renderização direta existir, cria/ativa fallback
            if (mainCam == null)
            {
                if (_fallbackCameraGo == null)
                {
                    _fallbackCameraGo = new GameObject("Main Camera (Bootstrap Fallback)");
                    _fallbackCameraGo.tag = "MainCamera";
                    var camComp = _fallbackCameraGo.AddComponent<Camera>();
                    camComp.clearFlags = CameraClearFlags.SolidColor;
                    camComp.backgroundColor = new Color(0.97f, 0.94f, 0.90f, 1f); // Tom acolhedor P.I.A.R.I.A
                    camComp.fieldOfView = 60f;
                    camComp.nearClipPlane = 0.2f;
                    camComp.farClipPlane = 2000f;
                    _fallbackCameraGo.transform.position = new Vector3(0f, 2f, -5f);
                    _fallbackCameraGo.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
                }
                else
                {
                    _fallbackCameraGo.SetActive(true);
                }

                mainCam = _fallbackCameraGo.GetComponent<Camera>();
            }

            // 2. Garante AudioListener
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            bool hasActiveListener = false;

            if (listeners != null && listeners.Length > 0)
            {
                int activeCount = 0;
                foreach (var l in listeners)
                {
                    if (l != null && l.enabled && l.gameObject.activeInHierarchy)
                    {
                        activeCount++;
                        if (activeCount > 1)
                        {
                            // Unity só suporta 1 AudioListener ativo ao mesmo tempo
                            l.enabled = false;
                        }
                        else
                        {
                            hasActiveListener = true;
                        }
                    }
                }
            }

            if (!hasActiveListener && mainCam != null)
            {
                var existingListener = mainCam.GetComponent<AudioListener>();
                if (existingListener == null)
                {
                    existingListener = mainCam.gameObject.AddComponent<AudioListener>();
                }
                existingListener.enabled = true;
            }
        }
    }
}
