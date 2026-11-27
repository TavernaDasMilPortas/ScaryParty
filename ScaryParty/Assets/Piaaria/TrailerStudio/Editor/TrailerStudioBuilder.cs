using System;
using System.Collections.Generic;
using System.IO;
using Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace Piaaria.TrailerStudio.Editor
{
    public static class TrailerStudioBuilder
    {
        private const string RootFolder = "Assets/Piaaria/TrailerStudio";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string ProfilesFolder = RootFolder + "/Profiles";
        private const string TimelinesFolder = RootFolder + "/Timelines";
        private const string GeneratedFolder = RootFolder + "/Generated";
        private const string ScenePath = ScenesFolder + "/TrailerStudio.unity";
        private const string ProfilePath = ProfilesFolder + "/TrailerStudioProfile.asset";
        private const string CityConfigCopyPath = ProfilesFolder + "/TrailerCityConfig.asset";

        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/Construir ou Atualizar Cena", false, 192)]
        public static void BuildOrUpdateScene()
        {
            EnsureFolders();

            TrailerStudioProfile profile = LoadOrCreateProfile();
            PopulateDefaultAssets(profile);
            PopulateTakeDefinitions(profile);
            CreateOrUpdateTimelines(profile);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            Scene scene = OpenOrCreateScene();
            BuildSceneHierarchy(scene, profile);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[TrailerStudio] Cena, Profile e 15 Timelines construídos/atualizados com sucesso em " + ScenePath);
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/Piaaria", "TrailerStudio");
            CreateFolderIfMissing(RootFolder, "Scenes");
            CreateFolderIfMissing(RootFolder, "Profiles");
            CreateFolderIfMissing(RootFolder, "Timelines");
            CreateFolderIfMissing(RootFolder, "Generated");
            CreateFolderIfMissing(RootFolder, "Tests");
        }

        private static void CreateFolderIfMissing(string parent, string folderName)
        {
            string full = parent + "/" + folderName;
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, folderName);
        }

        private static TrailerStudioProfile LoadOrCreateProfile()
        {
            TrailerStudioProfile profile = AssetDatabase.LoadAssetAtPath<TrailerStudioProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TrailerStudioProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            return profile;
        }

        private static void PopulateDefaultAssets(TrailerStudioProfile profile)
        {
            if (profile.cityConfig == null)
            {
                CityConfig existingCopy = AssetDatabase.LoadAssetAtPath<CityConfig>(CityConfigCopyPath);
                if (existingCopy == null)
                {
                    CityConfig orig = AssetDatabase.LoadAssetAtPath<CityConfig>("Assets/Piaaria/ScriptableObjects/DefaultCityConfig.asset");
                    if (orig != null)
                    {
                        existingCopy = UnityEngine.Object.Instantiate(orig);
                        AssetDatabase.CreateAsset(existingCopy, CityConfigCopyPath);
                    }
                }
                profile.cityConfig = existingCopy;
            }

            if (profile.pizzeriaPrefab == null)
                profile.pizzeriaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab");

            if (profile.characterVisualPrefab == null)
                profile.characterVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab");

            if (profile.motorcyclePrefab == null)
                profile.motorcyclePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Playtest/Prefabs/Visual_DeliveryMotorcycle.prefab");

            if (profile.doughPrefab == null)
                profile.doughPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Ingredient_Dough.prefab");
            if (profile.pizzaWipPrefab == null)
                profile.pizzaWipPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/PizzaWIP.prefab");
            if (profile.pizzaBoxPrefab == null)
                profile.pizzaBoxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/PizzaBox.prefab");
            if (profile.knifePrefab == null)
                profile.knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Tool_Knife.prefab");
            if (profile.calabresaPrefab == null)
                profile.calabresaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Ingredient_Calabresa.prefab");
            if (profile.cheesePrefab == null)
                profile.cheesePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Ingredient_Cheese.prefab");
            if (profile.itemCatalog == null)
                profile.itemCatalog = AssetDatabase.LoadAssetAtPath<ScaryParty.Pizzeria.Presentation.ItemPresentationCatalog>("Assets/Piaaria/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
            if (profile.openDoughPrefab == null)
                profile.openDoughPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_MassaAberta.prefab");
            if (profile.saucePrefab == null)
                profile.saucePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_Sauce.prefab");
            if (profile.gratedCheesePrefab == null)
                profile.gratedCheesePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_GratedCheese.prefab");
            if (profile.slicedCalabresaPrefab == null)
                profile.slicedCalabresaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_Ingredient_SlicedPepperoni.prefab");
            if (profile.deliveryBagVisualPrefab == null)
                profile.deliveryBagVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Piaaria/Prefabs/Pizzeria/Visuals/Visual_DeliveryBag.prefab");
        }

        private static void PopulateTakeDefinitions(TrailerStudioProfile profile)
        {
            var names = new (TrailerTakeId id, string name)[]
            {
                (TrailerTakeId.T01_CityReveal, "T01_CityReveal"),
                (TrailerTakeId.T02_KitchenTeam, "T02_KitchenTeam"),
                (TrailerTakeId.T03_Dough, "T03_Dough"),
                (TrailerTakeId.T04_Ingredients, "T04_Ingredients"),
                (TrailerTakeId.T05_Assembly, "T05_Assembly"),
                (TrailerTakeId.T06_Oven, "T06_Oven"),
                (TrailerTakeId.T07_Packaging, "T07_Packaging"),
                (TrailerTakeId.T08_Tickets, "T08_Tickets"),
                (TrailerTakeId.T09_KitchenChaos, "T09_KitchenChaos"),
                (TrailerTakeId.T10_BurntPizza, "T10_BurntPizza"),
                (TrailerTakeId.T11_Backpack, "T11_Backpack"),
                (TrailerTakeId.T12_MotorcycleStart, "T12_MotorcycleStart"),
                (TrailerTakeId.T13_CityRide, "T13_CityRide"),
                (TrailerTakeId.T14_Delivery, "T14_Delivery"),
                (TrailerTakeId.T15_FinalGag, "T15_FinalGag"),
            };

            foreach (var s in names)
            {
                var def = profile.GetTake(s.id);
                if (def == null)
                {
                    def = new TrailerTakeDefinition { id = s.id };
                    profile.takes.Add(def);
                }
                def.displayName = s.name;
                TrailerStudioProfile.ApplyTakeDefaultsIfMissing(def);
            }
        }

        private static void CreateOrUpdateTimelines(TrailerStudioProfile profile)
        {
            foreach (var def in profile.takes)
            {
                string path = $"{TimelinesFolder}/{def.displayName}.playable";
                TimelineAsset tl = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
                if (tl == null)
                {
                    tl = ScriptableObject.CreateInstance<TimelineAsset>();
                    tl.editorSettings.frameRate = 60.0;
                    AssetDatabase.CreateAsset(tl, path);
                }
                def.timelineAsset = tl;
                EditorUtility.SetDirty(tl);
            }
        }

        private static Scene OpenOrCreateScene()
        {
            if (File.Exists(ScenePath))
                return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return newScene;
        }

        private static void BuildSceneHierarchy(Scene scene, TrailerStudioProfile profile)
        {
            GameObject root = GameObject.Find("TrailerStudio") ?? new GameObject("TrailerStudio");
            Transform env = EnsureChild(root.transform, "Environment");
            Transform anchors = EnsureChild(root.transform, "Anchors");
            Transform cast = EnsureChild(root.transform, "Cast");
            Transform props = EnsureChild(root.transform, "Props");
            Transform vehicles = EnsureChild(root.transform, "Vehicles");
            Transform cameras = EnsureChild(root.transform, "Cameras");
            Transform directorGo = EnsureChild(root.transform, "Director");

            // Main Camera + CinemachineBrain
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                camGo.transform.SetParent(cameras, false);
                mainCam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            CinemachineBrain brain = mainCam.GetComponent<CinemachineBrain>();
            if (brain == null) brain = mainCam.gameObject.AddComponent<CinemachineBrain>();
            brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);

            // vCams
            foreach (var def in profile.takes)
            {
                EnsureVCam(cameras, $"{def.id}_A", def.cameraAFov);
                if (def.hasCameraB)
                    EnsureVCam(cameras, $"{def.id}_B", def.cameraBFov);
            }

            // CityGenerator na Environment
            CityGenerator cg = env.GetComponentInChildren<CityGenerator>();
            if (cg == null)
            {
                GameObject cgGo = new GameObject("CityGenerator");
                cgGo.transform.SetParent(env, false);
                cg = cgGo.AddComponent<CityGenerator>();
            }
            cg.config = profile.cityConfig;
            CopyCityGeneratorFromGameplayScene(cg);

            // Directional Light na Environment
            Light dirLight = env.GetComponentInChildren<Light>();
            if (dirLight == null)
            {
                GameObject lightGo = new GameObject("Directional Light");
                lightGo.transform.SetParent(env, false);
                lightGo.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                dirLight = lightGo.AddComponent<Light>();
                dirLight.type = LightType.Directional;
                dirLight.intensity = 1.15f;
                dirLight.shadows = LightShadows.Soft;
            }

            // Director
            PlayableDirector pd = directorGo.GetComponent<PlayableDirector>();
            if (pd == null) pd = directorGo.gameObject.AddComponent<PlayableDirector>();
            pd.playOnAwake = false;
            pd.timeUpdateMode = DirectorUpdateMode.Manual;

            AudioSource audio = directorGo.GetComponent<AudioSource>();
            if (audio == null) audio = directorGo.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;

            TrailerDirector td = directorGo.GetComponent<TrailerDirector>();
            if (td == null) td = directorGo.gameObject.AddComponent<TrailerDirector>();
            td.profile = profile;
            td.playableDirector = pd;
            td.cinemachineBrain = brain;
            td.audioSource = audio;
            td.cityGenerator = cg;
            td.castRoot = cast;
            td.propsRoot = props;
            td.vehiclesRoot = vehicles;
            td.anchorsRoot = anchors;
            td.camerasRoot = cameras;
        }

        private static void CopyCityGeneratorFromGameplayScene(CityGenerator target)
        {
            const string gameplayScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
            if (!File.Exists(gameplayScenePath)) return;

            Scene gameplayScene = EditorSceneManager.OpenScene(gameplayScenePath, OpenSceneMode.Additive);
            try
            {
                foreach (GameObject rootGo in gameplayScene.GetRootGameObjects())
                {
                    CityGenerator source = rootGo.GetComponentInChildren<CityGenerator>(true);
                    if (source != null)
                    {
                        target.pizzeriaPrefab = source.pizzeriaPrefab;
                        target.streetMaterial = source.streetMaterial;
                        target.sidewalkMaterial = source.sidewalkMaterial;
                        target.intersectionMaterial = source.intersectionMaterial;
                        target.buildingMaterials = source.buildingMaterials != null ? (Material[])source.buildingMaterials.Clone() : null;
                        target.roadMarkingYellowMaterial = source.roadMarkingYellowMaterial;
                        target.roadMarkingWhiteMaterial = source.roadMarkingWhiteMaterial;
                        target.crosswalkMaterial = source.crosswalkMaterial;
                        EditorUtility.SetDirty(target);
                        break;
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(gameplayScene, true);
            }
        }

        private static Transform EnsureChild(Transform parent, string name)
        {
            Transform t = parent.Find(name);
            if (t == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(parent, false);
                t = go.transform;
            }
            return t;
        }

        private static void EnsureVCam(Transform camerasRoot, string vcamName, float fov)
        {
            Transform t = camerasRoot.Find(vcamName);
            if (t == null)
            {
                GameObject go = new GameObject(vcamName);
                go.transform.SetParent(camerasRoot, false);
                t = go.transform;
            }
            var vcam = t.GetComponent<CinemachineVirtualCamera>();
            if (vcam == null) vcam = t.gameObject.AddComponent<CinemachineVirtualCamera>();
            vcam.m_Lens.FieldOfView = fov;
            vcam.Priority = 0;
        }
    }
}
