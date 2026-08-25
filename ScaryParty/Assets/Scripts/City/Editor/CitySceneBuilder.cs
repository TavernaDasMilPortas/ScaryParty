using UnityEditor;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Editor window for setting up the complete game scene structure.
/// Access via: Tools > Scary Party > Scene Builder
/// </summary>
public class CitySceneBuilder : EditorWindow
{
    private CityConfig _currentConfig;
    private Vector2 _scrollPos;

    [MenuItem("Tools/Scary Party/Scene Builder")]
    public static void ShowWindow()
    {
        GetWindow<CitySceneBuilder>("Scary Party Scene Builder");
    }

    private void OnGUI()
    {
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        EditorGUILayout.LabelField("🍕 Scary Party — Scene Builder", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Use this tool to set up the default city, networking, and gameplay scene structure.\n" +
            "Click 'Full Setup' to create everything at once.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        _currentConfig = (CityConfig)EditorGUILayout.ObjectField(
            "City Config", _currentConfig, typeof(CityConfig), false);

        if (_currentConfig != null)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Test Settings (Grid Size)", EditorStyles.boldLabel);
            
            EditorGUI.BeginChangeCheck();
            int newWidth = EditorGUILayout.IntSlider("Width (Blocks)", _currentConfig.gridWidth, 1, 20);
            int newHeight = EditorGUILayout.IntSlider("Height (Blocks)", _currentConfig.gridHeight, 1, 20);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_currentConfig, "Change City Config Size");
                _currentConfig.gridWidth = newWidth;
                _currentConfig.gridHeight = newHeight;
                EditorUtility.SetDirty(_currentConfig);
            }
            
            if (GUILayout.Button("🏗️ Quick Rebuild City", GUILayout.Height(30)))
            {
                CityGenerator gen = FindObjectOfType<CityGenerator>();
                if (gen != null)
                {
                    gen.ClearCity();
                    gen.GenerateCity();
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                    Debug.Log($"[SceneBuilder] City rebuilt with size {newWidth}x{newHeight}!");
                }
                else
                {
                    Debug.LogWarning("No CityGenerator found in scene. Click 'Setup Base Scene' first.");
                }
            }
            EditorGUILayout.EndVertical();
        }

        EditorGUILayout.Space(10);

        if (GUILayout.Button("1. Setup Base Scene", GUILayout.Height(30)))
        {
            SetupBaseScene();
        }

        if (GUILayout.Button("2. Add Network Manager", GUILayout.Height(30)))
        {
            SetupNetworkManager();
        }

        if (GUILayout.Button("3. Generate Default City Config", GUILayout.Height(30)))
        {
            GenerateDefaultConfig();
        }

        if (GUILayout.Button("4. Setup Camera in Player Prefab", GUILayout.Height(30)))
        {
            SetupPlayerCameraPrefab();
        }

        if (GUILayout.Button("5. Setup In-Game Lobby UI", GUILayout.Height(30)))
        {
            SetupInGameLobbyUI();
        }

        EditorGUILayout.Space(10);
        GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);
        if (GUILayout.Button("🚀  Full Setup", GUILayout.Height(45)))
        {
            GenerateDefaultConfig();
            SetupBaseScene();
            SetupNetworkManager();
            SetupGameplaySystems();
            SetupInGameLobbyUI();
            SetupPlayerCameraPrefab();
        }
        GUI.backgroundColor = Color.white;
        
        DrawSceneStatus();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Lobby Setup", EditorStyles.boldLabel);
        GUI.backgroundColor = new Color(0.4f, 0.7f, 1f);
        if (GUILayout.Button("🎮 Setup Lobby Scene", GUILayout.Height(35)))
        {
            EditorApplication.delayCall += SetupLobbyScene;
        }
        GUI.backgroundColor = Color.white;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Playground Cleaner", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use this in the StarterAssets Playground scene to remove demo environment while keeping the Player and Network logic.",
            MessageType.Info);
        
        GUI.backgroundColor = new Color(1f, 0.6f, 0.2f);
        if (GUILayout.Button("🧹 Clean Playground Scene", GUILayout.Height(35)))
        {
            CleanPlaygroundScene();
        }
        
        EditorGUILayout.Space(5);
        GUI.backgroundColor = new Color(1f, 0.4f, 0.6f);
        if (GUILayout.Button("🛠️ Fix Playground Bugs (AI Diagnostics)", GUILayout.Height(35)))
        {
            FixPlaygroundBugs();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndScrollView();
    }

    private void FixPlaygroundBugs()
    {
        // 1. Ensure NetworkManager + PlayerPrefab
        SetupNetworkManager();

        // 2. Ensure NetworkObject on --- CITY --- (CRITICAL for OnNetworkSpawn)
        GameObject cityRoot = GameObject.Find("--- CITY ---");
        if (cityRoot != null && cityRoot.GetComponent<NetworkObject>() == null)
        {
            cityRoot.AddComponent<NetworkObject>();
            Debug.Log("[SceneBuilder] 🛠️ Added NetworkObject to --- CITY ---.");
        }

        // 3. Ensure MinimapRouteManager and all gameplay systems
        SetupGameplaySystems();

        // 4. Ensure pizza prefab is registered in NetworkPrefabs
        EnsurePizzaPrefabInNetworkPrefabs();

        // 5. Ensure MinimapOnly layer exists
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        bool layerExists = false;
        int emptyLayer = -1;
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);
            if (layerSP.stringValue == "MinimapOnly") { layerExists = true; break; }
            if (emptyLayer == -1 && string.IsNullOrEmpty(layerSP.stringValue)) emptyLayer = i;
        }
        if (!layerExists && emptyLayer != -1)
        {
            layers.GetArrayElementAtIndex(emptyLayer).stringValue = "MinimapOnly";
            tagManager.ApplyModifiedProperties();
            Debug.Log("[SceneBuilder] 🛠️ Created 'MinimapOnly' Layer.");
        }

        // 6. Regenerate city to fix all delivery point index issues
        CityGenerator gen = FindObjectOfType<CityGenerator>();
        if (gen != null)
        {
            gen.ClearCity();
            gen.GenerateCity();
            Debug.Log("[SceneBuilder] 🛠️ City regenerated — DeliveryPoint indices are now correct!");
        }
        else
        {
            Debug.LogWarning("[SceneBuilder] CityGenerator not found in scene. Run 'Setup Base Scene' first.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("🛠️ Playground Bugs Fixed",
            "✅ NetworkObject adicionado ao CITY\n" +
            "✅ MinimapRouteManager configurado\n" +
            "✅ Layer 'MinimapOnly' verificada\n" +
            "✅ Cidade regenerada (DeliveryPoints corretos)\n\n" +
            "Salve a cena (Ctrl+S) e dê Play!", "OK");
    }

    private void SetupLobbyScene()
    {
        // 1. Ask to save current scene or create new scene
        if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        // Create new empty scene
        UnityEngine.SceneManagement.Scene newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);

        // 2. Setup Hierarchy
        GameObject uiRoot = new GameObject("--- UI ---");
        GameObject networkRoot = new GameObject("--- NETWORK ---");
        GameObject lobbyRoot = new GameObject("--- LOBBY ---");

        // Camera
        GameObject camGo = new GameObject("Main Camera");
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.05f); // Dark background
        camGo.tag = "MainCamera";

        // 3. UI
        var uiDoc = uiRoot.AddComponent<UnityEngine.UIElements.UIDocument>();
        var visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/LobbyScreen.uxml");
        if (visualTree != null) uiDoc.visualTreeAsset = visualTree;
        
        var panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI/PanelSettings.asset");
        if (panelSettings != null) uiDoc.panelSettings = panelSettings;

        // Ensure EventSystem exists for UI Toolkit clicks
        GameObject eventSystemGo = new GameObject("EventSystem");
        eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
        
        // Add Input System UI Input Module (since project uses new input system)
        System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null)
        {
            eventSystemGo.AddComponent(inputModuleType);
        }
        else
        {
            eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 4. Network
        var netManager = networkRoot.AddComponent<NetworkManager>();
        var transport = networkRoot.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
        netManager.NetworkConfig = new Unity.Netcode.NetworkConfig();
        netManager.NetworkConfig.NetworkTransport = transport;
        
        // Ensure PlayerPrefab is assigned to NetworkManager
        string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
        if (playerPrefab != null)
        {
            netManager.NetworkConfig.PlayerPrefab = playerPrefab;
        }

        // Add NetworkPrefabs list to NetworkManager
        string listPath = "Assets/DefaultNetworkPrefabs.asset";
        var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
        if (prefabList != null)
        {
            netManager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
        }

        // 5. Lobby Logic
        var lobbyManager = lobbyRoot.AddComponent<LobbyManager>();
        var uiController = lobbyRoot.AddComponent<LobbyUIController>();

        uiController.LobbyManager = lobbyManager;

        // Create PlayerData if it doesn't exist
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
        {
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        string playerDataPath = "Assets/Resources/PlayerData.asset";
        PlayerData playerData = AssetDatabase.LoadAssetAtPath<PlayerData>(playerDataPath);
        if (playerData == null)
        {
            playerData = ScriptableObject.CreateInstance<PlayerData>();
            AssetDatabase.CreateAsset(playerData, playerDataPath);
            AssetDatabase.SaveAssets();
        }
        lobbyManager.PlayerData = playerData;

        // 6. Save Scene
        if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
        {
            AssetDatabase.CreateFolder("Assets", "Scenes");
        }
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, "Assets/Scenes/LobbyScene.unity");

        // 7. Add to Build Settings
        var original = EditorBuildSettings.scenes;
        bool hasLobby = false;
        bool hasSample = false;
        foreach (var s in original)
        {
            if (s.path == "Assets/Scenes/LobbyScene.unity") hasLobby = true;
            if (s.path == "Assets/Scenes/SampleScene.unity") hasSample = true;
        }

        if (!hasLobby)
        {
            var newScenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            newScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/LobbyScene.unity", true));
            foreach (var s in original)
            {
                newScenes.Add(s);
            }
            if (!hasSample)
            {
                newScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/SampleScene.unity", true));
            }
            EditorBuildSettings.scenes = newScenes.ToArray();
        }

        Debug.Log("[SceneBuilder] ✅ LobbyScene created and saved at Assets/Scenes/LobbyScene.unity");
    }

    private void EnsurePizzaPrefabInNetworkPrefabs()
    {
        string prefabPath = "Assets/Prefabs/PizzaBoxPlaceholder.prefab";
        GameObject pizzaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (pizzaPrefab == null)
        {
            // Try to generate it first
            pizzaPrefab = GeneratePizzaPrefab();
            if (pizzaPrefab == null)
            {
                Debug.LogWarning("[SceneBuilder] Pizza prefab not found and could not be created.");
                return;
            }
        }

        // Load the DefaultNetworkPrefabs list
        string listPath = "Assets/DefaultNetworkPrefabs.asset";
        var prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
        if (prefabList == null)
        {
            Debug.LogWarning($"[SceneBuilder] DefaultNetworkPrefabs.asset not found at {listPath}.");
            return;
        }

        // Check if already registered
        foreach (var entry in prefabList.PrefabList)
        {
            if (entry.Prefab == pizzaPrefab)
            {
                Debug.Log("[SceneBuilder] Pizza prefab already in NetworkPrefabs list.");
                // Also assign it to PizzariaManager if missing
                AssignPizzaPrefabToManager(pizzaPrefab);
                return;
            }
        }

        // Add it
        prefabList.Add(new NetworkPrefab { Prefab = pizzaPrefab });
        EditorUtility.SetDirty(prefabList);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneBuilder] ✅ Pizza prefab added to DefaultNetworkPrefabs.");
        AssignPizzaPrefabToManager(pizzaPrefab);
    }

    private void AssignPizzaPrefabToManager(GameObject pizzaPrefab)
    {
        PizzariaManager manager = FindObjectOfType<PizzariaManager>();
        if (manager != null && manager.pizzaBoxPrefab == null)
        {
            manager.pizzaBoxPrefab = pizzaPrefab;
            EditorUtility.SetDirty(manager);
            Debug.Log("[SceneBuilder] ✅ Assigned pizza prefab to PizzariaManager.");
        }
    }

    private void DrawSceneStatus()
    {
        EditorGUILayout.Space(15);
        EditorGUILayout.LabelField("Scene Status (Verifier)", EditorStyles.boldLabel);

        DrawStatusLine("City Root", GameObject.Find("--- CITY ---") != null);
        DrawStatusLine("Network Root", GameObject.Find("--- NETWORK ---") != null);
        DrawStatusLine("Gameplay Root", GameObject.Find("--- GAMEPLAY ---") != null);
        
        CityGenerator gen = FindObjectOfType<CityGenerator>();
        DrawStatusLine("CityGenerator", gen != null);
        if (gen != null)
        {
            bool hasMats = gen.buildingMaterials != null && gen.buildingMaterials.Length > 0;
            DrawStatusLine(" ↳ Building Mats Assigned", hasMats);

            // CRITICAL: NetworkObject is required for OnNetworkSpawn to fire
            bool hasNetObj = gen.GetComponent<NetworkObject>() != null;
            DrawStatusLine(" ↳ NetworkObject (OnNetworkSpawn)", hasNetObj);
        }

        DrawStatusLine("MinimapRouteManager", FindObjectOfType<MinimapRouteManager>() != null);
        DrawStatusLine("NetworkManager", FindObjectOfType<NetworkManager>() != null);
        
        PizzariaManager pManager = FindObjectOfType<PizzariaManager>();
        DrawStatusLine("PizzariaManager", pManager != null);
        if (pManager != null)
        {
            DrawStatusLine(" ↳ Pizza Prefab Assigned", pManager.pizzaBoxPrefab != null);
        }

        DrawStatusLine("UIManager", FindObjectOfType<UIManager>() != null);

        // Check if player has interaction
        NetworkManager netManager = FindObjectOfType<NetworkManager>();
        if (netManager != null)
        {
            DrawStatusLine(" ↳ Player Prefab Assigned", netManager.NetworkConfig.PlayerPrefab != null);
            if (netManager.NetworkConfig.PlayerPrefab != null)
            {
                bool hasInteraction = netManager.NetworkConfig.PlayerPrefab.GetComponent<PlayerInteraction>() != null;
                DrawStatusLine(" ↳ Player has Interaction", hasInteraction);
            }
        }
    }

    private void DrawStatusLine(string label, bool exists)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(150));
        GUI.color = exists ? Color.green : Color.red;
        EditorGUILayout.LabelField(exists ? "✅ Found" : "❌ Missing");
        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
    }

    private void SetupGameplaySystems()
    {
        GameObject gameplayRoot = CreateRootIfMissing("--- GAMEPLAY ---");

        // Generate Pizza Prefab if missing
        GameObject pizzaPrefab = GeneratePizzaPrefab();

        // UI Manager & Document
        GameObject uiObj = GameObject.Find("MainGameUI");
        UnityEngine.UIElements.UIDocument doc = null;
        if (uiObj == null)
        {
            uiObj = new GameObject("MainGameUI");
            uiObj.transform.SetParent(gameplayRoot.transform);
            
            doc = uiObj.AddComponent<UnityEngine.UIElements.UIDocument>();
            var visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/MainGameUI.uxml");
            if (visualTree != null) doc.visualTreeAsset = visualTree;

            UIManager uiManager = uiObj.AddComponent<UIManager>();
            uiManager.uiDocument = doc;
            
            Undo.RegisterCreatedObjectUndo(uiObj, "Create UI Manager");
        }
        else
        {
            doc = uiObj.GetComponent<UnityEngine.UIElements.UIDocument>();
        }

        if (doc != null)
        {
            // Ensure PanelSettings is created and assigned (otherwise UI is invisible)
            var panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI/PanelSettings.asset");
            if (panelSettings == null)
            {
                panelSettings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelSettings>();
                AssetDatabase.CreateAsset(panelSettings, "Assets/UI/PanelSettings.asset");
                AssetDatabase.SaveAssets();
            }
            doc.panelSettings = panelSettings;
        }

        // Pizzaria Manager
        PizzariaManager pizzariaManager = gameplayRoot.GetComponentInChildren<PizzariaManager>();
        if (pizzariaManager == null)
        {
            GameObject pizzariaObj = new GameObject("PizzariaManager");
            pizzariaObj.transform.SetParent(gameplayRoot.transform);
            
            pizzariaManager = pizzariaObj.AddComponent<PizzariaManager>();
            pizzariaObj.AddComponent<NetworkObject>();
            
            Undo.RegisterCreatedObjectUndo(pizzariaObj, "Create Pizzaria Manager");
        }

        // Assign prefab
        if (pizzariaManager.pizzaBoxPrefab == null && pizzaPrefab != null)
        {
            pizzariaManager.pizzaBoxPrefab = pizzaPrefab;
            EditorUtility.SetDirty(pizzariaManager);
        }

        // Setup Minimap Systems
        EnsureMinimapLayerExists();
        
        MinimapRouteManager routeManager = gameplayRoot.GetComponentInChildren<MinimapRouteManager>();
        if (routeManager == null)
        {
            GameObject routeObj = new GameObject("MinimapRouteManager");
            routeObj.transform.SetParent(gameplayRoot.transform);
            routeManager = routeObj.AddComponent<MinimapRouteManager>();
            Undo.RegisterCreatedObjectUndo(routeObj, "Create Route Manager");
        }

        // Setup Kamgam UGUIWorldImage Minimap
        GameObject minimapRig = GameObject.Find("Minimap_WorldImage_Rig");
        if (minimapRig == null)
        {
            minimapRig = new GameObject("Minimap_WorldImage_Rig");
            minimapRig.transform.SetParent(gameplayRoot.transform);
            
            // Requires Canvas for UGUIWorldImage
            Canvas canvas = minimapRig.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasGroup canvasGroup = minimapRig.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            
            GameObject worldImageObj = new GameObject("WorldImage_Minimap");
            worldImageObj.transform.SetParent(minimapRig.transform, false);
            
            Kamgam.UGUIWorldImage.WorldImage worldImage = worldImageObj.AddComponent<Kamgam.UGUIWorldImage.WorldImage>();
            worldImage.UseRenderTexture = true;
            worldImage.CameraOrthographic = true;
            worldImage.CameraOrthographicSize = 100f; // Cover the city
            worldImage.CameraOffset = new Vector3(0, 150f, 0); // High up looking down
            worldImage.CameraLookAtPosition = Vector3.zero;
            worldImage.ResolutionWidth = Kamgam.UGUIWorldImage.RenderTextureSize._512;
            worldImage.ResolutionHeight = Kamgam.UGUIWorldImage.RenderTextureSize._512;
            worldImage.CameraClearType = Kamgam.UGUIWorldImage.WorldObjectCamera.ClearType.Color;
            worldImage.CameraBackgroundColor = new Color(0.1f, 0.1f, 0.15f, 1f); // Dark background
            
            // Set culling mask: Default + MinimapOnly
            int defaultLayer = LayerMask.NameToLayer("Default");
            int minimapLayer = LayerMask.NameToLayer("MinimapOnly");
            worldImage.CameraCullingMask = (1 << defaultLayer) | (1 << minimapLayer);

            Undo.RegisterCreatedObjectUndo(minimapRig, "Create Minimap Rig");
        }

        Debug.Log("[SceneBuilder] ✅ Gameplay systems and Prefabs setup complete.");
    }

    private void EnsureMinimapLayerExists()
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        
        bool found = false;
        int emptySlot = -1;

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layerSP = layers.GetArrayElementAtIndex(i);
            if (layerSP.stringValue == "MinimapOnly")
            {
                found = true;
                break;
            }
            if (emptySlot == -1 && string.IsNullOrEmpty(layerSP.stringValue))
            {
                emptySlot = i;
            }
        }

        if (!found && emptySlot != -1)
        {
            layers.GetArrayElementAtIndex(emptySlot).stringValue = "MinimapOnly";
            tagManager.ApplyModifiedProperties();
            Debug.Log("[SceneBuilder] Created new Layer: MinimapOnly");
        }
    }

    private GameObject GeneratePizzaPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }

        string path = "Assets/Prefabs/PizzaBoxPlaceholder.prefab";
        GameObject existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existingPrefab != null) return existingPrefab;

        // Create temporary object
        GameObject pizzaObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pizzaObj.name = "PizzaBoxPlaceholder";
        pizzaObj.transform.localScale = new Vector3(0.5f, 0.1f, 0.5f);
        
        // Add components
        pizzaObj.AddComponent<NetworkObject>();
        BoxCollider col = pizzaObj.GetComponent<BoxCollider>();
        col.isTrigger = false;
        
        PizzaItem item = pizzaObj.AddComponent<PizzaItem>();
        item.pizzaType = "Placeholder Pizza";

        // Style it red
        Renderer renderer = pizzaObj.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
        mat.color = new Color(0.9f, 0.2f, 0.1f);
        AssetDatabase.CreateAsset(mat, "Assets/Prefabs/PizzaBoxMat.mat");
        renderer.sharedMaterial = mat;

        // Save prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(pizzaObj, path);
        DestroyImmediate(pizzaObj);

        // Add to NetworkManager NetworkPrefabs list so it can be spawned over the network!
        NetworkManager netManager = FindObjectOfType<NetworkManager>();
        if (netManager != null)
        {
            netManager.AddNetworkPrefab(prefab);
            EditorUtility.SetDirty(netManager);
        }

        return prefab;
    }

    private void SetupBaseScene()
    {
        // Create root hierarchy
        GameObject cityRoot = CreateRootIfMissing("--- CITY ---");
        CreateRootIfMissing("--- NETWORK ---");
        CreateRootIfMissing("--- GAMEPLAY ---");
        CreateRootIfMissing("--- UI ---");

        // Directional Light
        if (FindObjectOfType<Light>() == null)
        {
            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.85f);
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            Undo.RegisterCreatedObjectUndo(lightGo, "Create Light");
        }

        // Ground plane
        GameObject ground = GameObject.Find("GroundPlane");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GroundPlane";
            ground.transform.localScale = new Vector3(200, 1, 200);
            ground.transform.position = new Vector3(0, -0.15f, 0);
            ground.isStatic = true;
            Undo.RegisterCreatedObjectUndo(ground, "Create Ground");
        }

        // CityGenerator component
        CityGenerator generator = cityRoot.GetComponent<CityGenerator>();
        if (generator == null)
        {
            generator = Undo.AddComponent<CityGenerator>(cityRoot);
        }

        // NetworkObject is REQUIRED for CityGenerator.OnNetworkSpawn to fire
        if (cityRoot.GetComponent<NetworkObject>() == null)
        {
            Undo.AddComponent<NetworkObject>(cityRoot);
            Debug.Log("[SceneBuilder] ✅ Added NetworkObject to --- CITY --- (required for runtime city generation).");
        }

        // Assign config if available
        if (_currentConfig != null && generator.config == null)
        {
            generator.config = _currentConfig;
            EditorUtility.SetDirty(generator);
        }

        // Assign Distant Lands Materials
        AssignCartoonMaterials(generator);

        // CityGizmos component
        if (cityRoot.GetComponent<CityGizmos>() == null)
        {
            Undo.AddComponent<CityGizmos>(cityRoot);
        }

        Debug.Log("[SceneBuilder] ✅ Base scene setup complete.");
    }

    private void AssignCartoonMaterials(CityGenerator generator)
    {
        // Look for Distant Lands Toon Material
        string[] guids = AssetDatabase.FindAssets("Toon Preset t:Material");
        Material toonMat = null;
        if (guids.Length > 0)
        {
            toonMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        if (toonMat != null)
        {
            // Create specific instances for streets if they don't exist
            if (generator.streetMaterial == null)
            {
                generator.streetMaterial = new Material(toonMat);
                generator.streetMaterial.color = new Color(0.2f, 0.2f, 0.2f);
            }
            if (generator.sidewalkMaterial == null)
            {
                generator.sidewalkMaterial = new Material(toonMat);
                generator.sidewalkMaterial.color = new Color(0.6f, 0.6f, 0.6f);
            }
            if (generator.intersectionMaterial == null)
            {
                generator.intersectionMaterial = new Material(toonMat);
                generator.intersectionMaterial.color = new Color(0.18f, 0.18f, 0.18f);
            }

            if (generator.buildingMaterials == null || generator.buildingMaterials.Length == 0)
            {
                // Assign a few variations of the Toon material for buildings
                generator.buildingMaterials = new Material[] { toonMat };
            }
            
            EditorUtility.SetDirty(generator);
        }
    }

    private void SetupPlayerCameraPrefab()
    {
        string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        string mainCamPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/MainCamera.prefab";
        string followCamPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerFollowCamera.prefab";

        if (!System.IO.File.Exists(playerPath))
        {
            Debug.LogError("[SceneBuilder] PlayerArmature prefab not found at expected path!");
            return;
        }

        GameObject playerObj = PrefabUtility.LoadPrefabContents(playerPath);
        
        bool hasMainCam = playerObj.transform.Find("MainCamera") != null;
        bool hasFollowCam = playerObj.transform.Find("PlayerFollowCamera") != null;

        if (!hasMainCam)
        {
            GameObject mainCamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(mainCamPath);
            if (mainCamPrefab != null)
            {
                GameObject cam = (GameObject)PrefabUtility.InstantiatePrefab(mainCamPrefab, playerObj.transform);
                cam.name = "MainCamera";
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cam);
                
                // Try to add CinemachineBrain if missing
                if (cam.GetComponent("CinemachineBrain") == null && cam.GetComponent("Unity.Cinemachine.CinemachineBrain") == null)
                {
                    System.Type brainType = System.Type.GetType("Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine") 
                                         ?? System.Type.GetType("Cinemachine.CinemachineBrain, Cinemachine");
                    if (brainType != null) cam.AddComponent(brainType);
                }
            }
        }

        if (!hasFollowCam)
        {
            GameObject followCamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(followCamPath);
            if (followCamPrefab != null)
            {
                GameObject cam = (GameObject)PrefabUtility.InstantiatePrefab(followCamPrefab, playerObj.transform);
                cam.name = "PlayerFollowCamera";
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cam);
                
                // Clean child "cm" if it exists
                Transform cmChild = cam.transform.Find("cm");
                if (cmChild != null) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(cmChild.gameObject);
                
                // Attempt to link Cinemachine Follow Target using Reflection to avoid asmdef errors
                Transform camRoot = playerObj.transform.Find("PlayerCameraRoot");
                if (camRoot == null) camRoot = playerObj.transform;
                
                Component vcam = cam.GetComponent("CinemachineVirtualCamera");
                if (vcam == null) vcam = cam.GetComponent("CinemachineCamera"); // Unity 6
                
                // If it doesn't exist, try to add it
                if (vcam == null)
                {
                    System.Type vcamType = System.Type.GetType("Unity.Cinemachine.CinemachineCamera, Unity.Cinemachine") 
                                        ?? System.Type.GetType("Cinemachine.CinemachineVirtualCamera, Cinemachine");
                    if (vcamType != null) vcam = cam.AddComponent(vcamType);
                }
                
                if (vcam != null)
                {
                    var followProp = vcam.GetType().GetProperty("Follow");
                    if (followProp != null) followProp.SetValue(vcam, camRoot);
                }
            }
        }

        // Add Setup Script
        PlayerCameraSetup setupScript = playerObj.GetComponent<PlayerCameraSetup>();
        if (setupScript == null) setupScript = playerObj.AddComponent<PlayerCameraSetup>();
        
        Transform mainT = playerObj.transform.Find("MainCamera");
        Transform followT = playerObj.transform.Find("PlayerFollowCamera");
        
        if (mainT != null) setupScript.mainCamera = mainT.gameObject;
        if (followT != null) setupScript.followCamera = followT.gameObject;

        // Add MinimapController so the player registers with the minimap on spawn
        if (playerObj.GetComponent<MinimapController>() == null)
        {
            playerObj.AddComponent<MinimapController>();
            Debug.Log("[SceneBuilder] ✅ MinimapController added to PlayerArmature prefab.");
        }

        // Add PlayerInteraction if missing
        if (playerObj.GetComponent<PlayerInteraction>() == null)
        {
            playerObj.AddComponent<PlayerInteraction>();
            Debug.Log("[SceneBuilder] ✅ PlayerInteraction added to PlayerArmature prefab.");
        }

        // Add PlayerState for In-Game Lobby Customization
        if (playerObj.GetComponent<PlayerState>() == null)
        {
            playerObj.AddComponent<PlayerState>();
            Debug.Log("[SceneBuilder] ✅ PlayerState added to PlayerArmature prefab.");
        }

        PrefabUtility.SaveAsPrefabAsset(playerObj, playerPath);
        PrefabUtility.UnloadPrefabContents(playerObj);
        
        Debug.Log("[SceneBuilder] ✅ PlayerArmature prefab configured with in-prefab cameras!");
    }

    private void SetupNetworkManager()
    {
        // 1. Find all NetworkManagers in the scene
        NetworkManager[] allManagers = FindObjectsOfType<NetworkManager>();
        NetworkManager configuredManager = null;
        
        foreach (var nm in allManagers)
        {
            if (nm.NetworkConfig != null && nm.NetworkConfig.NetworkTransport != null)
            {
                configuredManager = nm;
                break;
            }
        }

        GameObject networkRoot = GameObject.Find("--- NETWORK ---");
        
        // 2. Resolve redundant objects
        if (configuredManager != null)
        {
            if (networkRoot != null && networkRoot != configuredManager.gameObject)
            {
                // We have an empty --- NETWORK --- root and a configured manager elsewhere.
                // Move any children over.
                while (networkRoot.transform.childCount > 0)
                {
                    networkRoot.transform.GetChild(0).SetParent(configuredManager.transform);
                }
                
                // Keep NetworkAutoStart if it exists
                if (networkRoot.GetComponent<NetworkAutoStart>() != null && configuredManager.GetComponent<NetworkAutoStart>() == null)
                {
                    Undo.AddComponent<NetworkAutoStart>(configuredManager.gameObject);
                }
                
                Undo.DestroyObjectImmediate(networkRoot);
            }
            
            networkRoot = configuredManager.gameObject;
            networkRoot.name = "--- NETWORK ---";
        }
        else
        {
            // No configured manager exists. Create/Use the root.
            if (networkRoot == null)
            {
                networkRoot = new GameObject("--- NETWORK ---");
                Undo.RegisterCreatedObjectUndo(networkRoot, "Create --- NETWORK ---");
            }
            
            if (networkRoot.GetComponent<NetworkManager>() == null)
            {
                Undo.AddComponent<NetworkManager>(networkRoot);
            }
            
            // Add UnityTransport if missing
            if (networkRoot.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>() == null)
            {
                var transport = Undo.AddComponent<Unity.Netcode.Transports.UTP.UnityTransport>(networkRoot);
                networkRoot.GetComponent<NetworkManager>().NetworkConfig.NetworkTransport = transport;
            }
        }

        NetworkManager netManager = networkRoot.GetComponent<NetworkManager>();

        if (netManager.NetworkConfig.PlayerPrefab == null)
        {
            string playerPath = "Assets/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(playerPath);
            if (playerPrefab != null)
            {
                netManager.NetworkConfig.PlayerPrefab = playerPrefab;
                EditorUtility.SetDirty(netManager);
                Debug.Log("[SceneBuilder] ✅ Assigned PlayerPrefab to NetworkManager.");
            }
        }

        // Add NetworkAutoStart so the game starts as Host automatically on Play
        if (networkRoot.GetComponent<NetworkAutoStart>() == null)
        {
            Undo.AddComponent<NetworkAutoStart>(networkRoot);
            Debug.Log("[SceneBuilder] ✅ NetworkAutoStart added — game will StartHost automatically on Play.");
        }
        
        // 3. Clean up any remaining duplicates
        NetworkManager[] remainingManagers = FindObjectsOfType<NetworkManager>();
        foreach (var nm in remainingManagers)
        {
            if (nm.gameObject != networkRoot)
            {
                Debug.Log($"[SceneBuilder] 🧹 Destroying redundant NetworkManager on object: {nm.gameObject.name}");
                Undo.DestroyObjectImmediate(nm.gameObject);
            }
        }
    }


    private void GenerateDefaultConfig()
    {
        if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
        {
            AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
        }

        string path = "Assets/ScriptableObjects/DefaultCityConfig.asset";

        // Check if already exists
        CityConfig existing = AssetDatabase.LoadAssetAtPath<CityConfig>(path);
        if (existing != null)
        {
            _currentConfig = existing;
            Debug.Log("[SceneBuilder] DefaultCityConfig already exists, using existing.");
            return;
        }

        CityConfig newConfig = ScriptableObject.CreateInstance<CityConfig>();
        AssetDatabase.CreateAsset(newConfig, path);
        AssetDatabase.SaveAssets();

        _currentConfig = newConfig;
        Debug.Log("[SceneBuilder] ✅ Created DefaultCityConfig at Assets/ScriptableObjects/");
    }

    private GameObject CreateRootIfMissing(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go == null)
        {
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }
        return go;
    }

    private void SetupInGameLobbyUI()
    {
        GameObject uiRoot = CreateRootIfMissing("--- UI ---");

        // Create EventSystem if it doesn't exist
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            
            System.Type inputModuleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null) eventSystemGo.AddComponent(inputModuleType);
            else eventSystemGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            
            eventSystemGo.transform.SetParent(uiRoot.transform);
        }

        // Create the UIDocument
        string docName = "InGameLobbyUI";
        Transform existingUI = uiRoot.transform.Find(docName);
        if (existingUI != null) DestroyImmediate(existingUI.gameObject);

        GameObject docGo = new GameObject(docName);
        docGo.transform.SetParent(uiRoot.transform);
        
        var uiDoc = docGo.AddComponent<UnityEngine.UIElements.UIDocument>();
        var visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/InGameLobbyScreen.uxml");
        if (visualTree != null) uiDoc.visualTreeAsset = visualTree;

        var panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/UI/PanelSettings.asset");
        if (panelSettings != null) uiDoc.panelSettings = panelSettings;

        // Add NetworkBehaviour controller
        docGo.AddComponent<InGameLobbyUIController>();
        
        // Add NetworkObject is REQUIRED for NetworkBehaviours!
        if (docGo.GetComponent<NetworkObject>() == null)
            docGo.AddComponent<NetworkObject>();

        Debug.Log("[SceneBuilder] ✅ In-Game Lobby UI Setup complete in active scene.");
    }

    private void CleanPlaygroundScene()
    {
        // Define names of root objects that we want to explicitly destroy from the Playground scene
        string[] objectsToRemove = new string[] 
        {
            "Environment",
            "Cubes",
            "Obstacles",
            "Arena",
            "Walls",
            "MainCamera",
            "PlayerFollowCamera"
        };

        int removedCount = 0;

        foreach (string objName in objectsToRemove)
        {
            GameObject go = GameObject.Find(objName);
            if (go != null)
            {
                Undo.DestroyObjectImmediate(go);
                removedCount++;
            }
        }

        // Also look for root objects that are just basic primitives (like floors or boxes) that might be lingering
        foreach (GameObject rootObj in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            // Skip our own setup
            if (rootObj.name.StartsWith("---") || rootObj.name == "GroundPlane" || rootObj.name == "Directional Light") continue;
            
            // Skip essential components
            if (rootObj.name.Contains("Player") || rootObj.name.Contains("Network") || rootObj.name.Contains("Camera") || 
                rootObj.name.Contains("EventSystem") || rootObj.name.Contains("Canvas") || rootObj.name.Contains("Volume")) continue;

            // If it has no scripts attached other than Transform/Mesh/Collider/Renderer, it's likely a dummy object
            var components = rootObj.GetComponents<Component>();
            bool hasLogic = false;
            foreach (var comp in components)
            {
                if (comp != null && !(comp is Transform || comp is MeshFilter || comp is MeshRenderer || comp is Collider))
                {
                    hasLogic = true;
                    break;
                }
            }

            if (!hasLogic && (rootObj.name.Contains("Cube") || rootObj.name.Contains("Plane") || rootObj.name.Contains("Floor")))
            {
                Undo.DestroyObjectImmediate(rootObj);
                removedCount++;
            }
        }

        Debug.Log($"[SceneBuilder] 🧹 Playground cleaned! Removed {removedCount} environment objects.");
    }
}
