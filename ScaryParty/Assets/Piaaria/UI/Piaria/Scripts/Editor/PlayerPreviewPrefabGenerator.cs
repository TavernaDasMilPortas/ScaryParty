using System;
using UnityEngine;
using UnityEditor;

namespace ScaryParty.UI.Piaria.Editor
{
    /// <summary>
    /// Utilitário que gera uma cópia limpa do PlayerArmature apenas com as malhas, ossos e Animator,
    /// sem nenhum componente lógico, de física, rede ou câmeras.
    /// Usado com exclusividade pelo CharacterPreviewRig para eliminar erros de dependência (RequireComponent).
    /// </summary>
    [InitializeOnLoad]
    public static class PlayerPreviewPrefabGenerator
    {
        public const string SourcePrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        public const string TargetPrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature_Preview.prefab";

        static PlayerPreviewPrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePreviewPrefabExists;
        }

        [MenuItem("Tools/Scary Party/Setup e Uso Único/Personagens/Gerar Prefab de Preview 3D (PlayerArmature_Preview)", false, 226)]
        public static void GeneratePreviewPrefabMenu()
        {
            GeneratePreviewPrefab(true);
        }

        public static void EnsurePreviewPrefabExists()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath))
            {
                GeneratePreviewPrefab(false);
            }
        }

        public static void GeneratePreviewPrefab(bool force)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            if (source == null)
            {
                Debug.LogWarning($"[PlayerPreviewPrefabGenerator] Prefab fonte '{SourcePrefabPath}' não encontrado.");
                return;
            }

            if (!force && AssetDatabase.LoadAssetAtPath<GameObject>(TargetPrefabPath) != null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "PlayerArmature_Preview";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // 1. Remove componentes na ordem correta de dependência inversa
            // Primeiro os que dependem de outros (folhas)
            DestroyCompByName(instance, "WeaponInputHandler");
            DestroyCompByName(instance, "WeaponController");

            var tpc = instance.GetComponent<StarterAssets.ThirdPersonController>();
            if (tpc != null) UnityEngine.Object.DestroyImmediate(tpc, true);

            DestroyCompByName(instance, "BasicRigidBodyPush");

            var sInputs = instance.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (sInputs != null) UnityEngine.Object.DestroyImmediate(sInputs, true);

            var pInput = instance.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pInput != null) UnityEngine.Object.DestroyImmediate(pInput, true);

            var cc = instance.GetComponent<CharacterController>();
            if (cc != null) UnityEngine.Object.DestroyImmediate(cc, true);

            DestroyCompByName(instance, "PlayerCameraSetup");
            DestroyCompByName(instance, "FirstPersonCameraController");
            DestroyCompByName(instance, "PlayerInteraction");
            DestroyCompByName(instance, "MinimapController");
            DestroyCompByName(instance, "OutlineService");
            DestroyCompByName(instance, "PlayerState");
            DestroyCompByName(instance, "ClientNetworkTransform");

            var netObj = instance.GetComponent<Unity.Netcode.NetworkObject>();
            if (netObj != null) UnityEngine.Object.DestroyImmediate(netObj, true);

            // 2. Loop inteligente de varredura que remove qualquer componente remanescente respeitando [RequireComponent]
            StripRemainingComponentsSafely(instance);

            // 3. Remove objetos de câmera e pivots filhos desnecessários para a prévia
            for (int i = instance.transform.childCount - 1; i >= 0; i--)
            {
                var child = instance.transform.GetChild(i);
                var childName = child.gameObject.name;

                // Preserva CharacterVisual, Armature, Cylinder.002, Geometry e Skeleton
                if (childName.IndexOf("CharacterVisual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Armature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Cylinder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Geometry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    childName.IndexOf("Skeleton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    child.GetComponentInChildren<ScaryParty.Player.CharacterVisualController>(true) != null ||
                    child.GetComponentInChildren<Animator>(true) != null)
                {
                    continue;
                }

                // Se possuir Renderers (que não sejam câmeras/UI), mantém
                if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) != null ||
                    (child.GetComponentInChildren<Renderer>(true) != null && child.GetComponentInChildren<Camera>(true) == null))
                {
                    continue;
                }

                // Destrói PlayerCameraRoot, CameraPivot, etc.
                UnityEngine.Object.DestroyImmediate(child.gameObject, true);
            }

            // 4. Remove câmeras, listeners e colliders residuais em quaisquer filhos restantes
            var cams = instance.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                if (cam != null && cam.gameObject != instance)
                {
                    UnityEngine.Object.DestroyImmediate(cam.gameObject, true);
                }
            }

            var listeners = instance.GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners)
            {
                if (l != null && l.gameObject != instance)
                {
                    UnityEngine.Object.DestroyImmediate(l.gameObject, true);
                }
            }

            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null) UnityEngine.Object.DestroyImmediate(col, true);
            }

            // 5. Salva como prefab limpo
            PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerPreviewPrefabGenerator] ✅ Prefab limpo de preview salvo em '{TargetPrefabPath}' com sucesso!");
        }

        private static void DestroyCompByName(GameObject go, string componentTypeName)
        {
            var comp = go.GetComponent(componentTypeName);
            if (comp != null)
            {
                UnityEngine.Object.DestroyImmediate(comp, true);
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

                UnityEngine.Object.DestroyImmediate(compToRemove, true);
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
    }
}
