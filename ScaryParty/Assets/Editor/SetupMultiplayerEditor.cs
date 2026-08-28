using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

public class SetupMultiplayerEditor : EditorWindow
{
    [MenuItem("Tools/Setup Multiplayer (Passos 4 a 6)")]
    public static void SetupMultiplayer()
    {
        // 1. Open the GameScene scene
        string scenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // 2. Setup NetworkManager in the scene
        GameObject networkManagerGO = GameObject.Find("NetworkManager");
        if (networkManagerGO == null)
        {
            networkManagerGO = new GameObject("NetworkManager");
        }
        
        NetworkManager networkManager = networkManagerGO.GetComponent<NetworkManager>();
        if (networkManager == null)
        {
            networkManager = networkManagerGO.AddComponent<NetworkManager>();
        }

        UnityTransport transport = networkManagerGO.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = networkManagerGO.AddComponent<UnityTransport>();
        }
        
        // Setup transport link
        networkManager.NetworkConfig.NetworkTransport = transport;

        // 3. Find PlayerArmature in scene and delete it (so only environment remains)
        GameObject playerInScene = GameObject.Find("PlayerArmature");
        if (playerInScene != null)
        {
            DestroyImmediate(playerInScene);
        }

        // 4. Setup PlayerArmature Prefab
        string prefabPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        
        if (playerPrefab != null)
        {
            // We need to modify the prefab
            GameObject prefabContents = PrefabUtility.LoadPrefabContents(prefabPath);
            
            if (prefabContents.GetComponent<NetworkObject>() == null)
                prefabContents.AddComponent<NetworkObject>();

            NetworkTransform netTransform = prefabContents.GetComponent<NetworkTransform>();
            if (netTransform == null)
                netTransform = prefabContents.AddComponent<NetworkTransform>();
            
            // Uncheck scales and rotations using SerializedObject to ensure compatibility
            SerializedObject so = new SerializedObject(netTransform);
            try {
                if (so.FindProperty("SyncScaleX") != null) so.FindProperty("SyncScaleX").boolValue = false;
                if (so.FindProperty("SyncScaleY") != null) so.FindProperty("SyncScaleY").boolValue = false;
                if (so.FindProperty("SyncScaleZ") != null) so.FindProperty("SyncScaleZ").boolValue = false;
                if (so.FindProperty("SyncRotAngleX") != null) so.FindProperty("SyncRotAngleX").boolValue = false;
                if (so.FindProperty("SyncRotAngleZ") != null) so.FindProperty("SyncRotAngleZ").boolValue = false;
                so.ApplyModifiedProperties();
            } catch {
                Debug.LogWarning("Não foi possível desabilitar os eixos de rotação e escala automaticamente. Por favor faça isso manualmente no Inspector do NetworkTransform.");
            }

            NetworkAnimator netAnimator = prefabContents.GetComponent<NetworkAnimator>();
            if (netAnimator == null)
                netAnimator = prefabContents.AddComponent<NetworkAnimator>();
            
            Animator anim = prefabContents.GetComponent<Animator>();
            if (anim != null)
                netAnimator.Animator = anim;

            PrefabUtility.SaveAsPrefabAsset(prefabContents, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefabContents);

            // Assign prefab to NetworkManager
            playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            networkManager.NetworkConfig.PlayerPrefab = playerPrefab;
        }
        else
        {
            Debug.LogError("PlayerArmature prefab not found at " + prefabPath);
        }

        // Save scene
        EditorSceneManager.SaveScene(scene);

        // Add to build settings
        var originalScenes = EditorBuildSettings.scenes;
        bool sceneExists = false;
        foreach (var s in originalScenes)
        {
            if (s.path == scenePath) sceneExists = true;
        }
        if (!sceneExists)
        {
            var newScenes = new EditorBuildSettingsScene[originalScenes.Length + 1];
            System.Array.Copy(originalScenes, newScenes, originalScenes.Length);
            newScenes[newScenes.Length - 1] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = newScenes;
        }

        Debug.Log("Passos 4, 5 e 6 concluídos com sucesso! Abra a cena GameScene, selecione o NetworkManager e clique em Start Host no Play Mode.");
    }
}
