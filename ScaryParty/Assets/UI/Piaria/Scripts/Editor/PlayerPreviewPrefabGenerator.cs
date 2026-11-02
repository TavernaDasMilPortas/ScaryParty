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
        public const string SourcePrefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        public const string TargetPrefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature_Preview.prefab";

        static PlayerPreviewPrefabGenerator()
        {
            EditorApplication.delayCall += EnsurePreviewPrefabExists;
        }

        [MenuItem("Tools/Scary Party/UI/Gerar Prefab de Preview 3D (PlayerArmature_Preview)", false, 20)]
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

            // 1. Destrói primeiro ThirdPersonController para liberar CharacterController e PlayerInput
            var tpc = instance.GetComponent<StarterAssets.ThirdPersonController>();
            if (tpc != null) Object.DestroyImmediate(tpc, true);

            var bPush = instance.GetComponent("BasicRigidBodyPush");
            if (bPush != null) Object.DestroyImmediate(bPush, true);

            var sInputs = instance.GetComponent<StarterAssets.StarterAssetsInputs>();
            if (sInputs != null) Object.DestroyImmediate(sInputs, true);

            var pInput = instance.GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pInput != null) Object.DestroyImmediate(pInput, true);

            var cc = instance.GetComponent<CharacterController>();
            if (cc != null) Object.DestroyImmediate(cc, true);

            // 2. Destrói todos os componentes de gameplay restantes no root
            var allRemaining = instance.GetComponents<Component>();
            foreach (var comp in allRemaining)
            {
                if (comp is Transform || comp is Animator) continue;
                Object.DestroyImmediate(comp, true);
            }

            // 3. Remove câmeras e listeners filhos
            var cams = instance.GetComponentsInChildren<Camera>(true);
            foreach (var cam in cams)
            {
                if (cam != null && cam.gameObject != instance)
                {
                    Object.DestroyImmediate(cam.gameObject, true);
                }
            }

            var listeners = instance.GetComponentsInChildren<AudioListener>(true);
            foreach (var l in listeners)
            {
                if (l != null && l.gameObject != instance)
                {
                    Object.DestroyImmediate(l.gameObject, true);
                }
            }

            // 4. Remove colliders remanescentes nos filhos
            var colliders = instance.GetComponentsInChildren<Collider>(true);
            foreach (var col in colliders)
            {
                if (col != null) Object.DestroyImmediate(col, true);
            }

            // 5. Salva como prefab limpo
            PrefabUtility.SaveAsPrefabAsset(instance, TargetPrefabPath);
            Object.DestroyImmediate(instance);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PlayerPreviewPrefabGenerator] ✅ Prefab limpo de preview salvo em '{TargetPrefabPath}' com sucesso!");
        }
    }
}
