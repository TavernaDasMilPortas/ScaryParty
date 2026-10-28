#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.Migration;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor
{
    [InitializeOnLoad]
    public static class IngredientVisualSetup
    {
        private const string VisualsFolder = "Assets/Prefabs/Pizzeria/Visuals";
        private const string MaterialsFolder = "Assets/Prefabs/Pizzeria/Materials";
        private const string ProfilesFolder = "Assets/ScriptableObjects/Pizzeria/Profiles";
        private const string CatalogPath = "Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset";
        private const string ResourcesCatalogFolder = "Assets/Resources";
        private const string ResourcesCatalogPath = "Assets/Resources/ItemPresentationCatalog.asset";
        private const string PizzeriaPrefabPath = "Assets/Prefabs/Pizzeria/PizzeriaPrefab.prefab";
        private const string GameScenePath = "Assets/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        private const string ImportProfilePath = "Assets/ScriptableObjects/Pizzeria/Import/PizzeriaFbxImportProfile.asset";

        static IngredientVisualSetup()
        {
            EditorApplication.delayCall += () =>
            {
                // Run once on load if needed
                RunFullIngredientSetup(silent: true);
            };
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Configurar Modelos 3D dos Ingredientes e Mesas", false, 1)]
        public static void RunFullIngredientSetupMenu()
        {
            RunFullIngredientSetup(silent: false);
        }

        public static void RunFullIngredientSetup(bool silent = false)
        {
            try
            {
                EnsureDirectories();
                var materials = EnsureMaterials();
                var prefabs = BuildIngredientVisualPrefabs(materials);
                UpdateIngredientProfiles(prefabs);
                EnsureResourcesCatalog();
                FixPizzeriaPrefabSlotsAndCatalog();
                FixGameSceneCatalog();
                FixImportProfileSlotOffsets();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (!silent)
                {
                    EditorUtility.DisplayDialog(
                        "Configuração Concluída",
                        "Todos os modelos visuais 3D dos ingredientes foram gerados, associados aos perfis, " +
                        "e as âncoras das bancadas de preparo/montagem foram calibradas para a superfície das mesas!",
                        "OK"
                    );
                }
                Debug.Log("[IngredientVisualSetup] ✅ Modelos 3D dos ingredientes e âncoras das bancadas configurados com sucesso!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[IngredientVisualSetup] ❌ Erro ao configurar modelos 3D dos ingredientes: {ex}");
            }
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(VisualsFolder)) Directory.CreateDirectory(VisualsFolder);
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(ProfilesFolder)) Directory.CreateDirectory(ProfilesFolder);
            if (!Directory.Exists(ResourcesCatalogFolder)) Directory.CreateDirectory(ResourcesCatalogFolder);
        }

        private static Dictionary<string, Material> EnsureMaterials()
        {
            var dict = new Dictionary<string, Material>();

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            dict["Dough"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_Dough.mat");
            if (dict["Dough"] == null)
            {
                dict["Dough"] = new Material(urpLit) { color = new Color(0.92f, 0.85f, 0.70f) };
                AssetDatabase.CreateAsset(dict["Dough"], $"{MaterialsFolder}/Mat_Dough.mat");
            }

            dict["Sauce"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_Sauce.mat");
            if (dict["Sauce"] == null)
            {
                dict["Sauce"] = new Material(urpLit) { color = new Color(0.80f, 0.15f, 0.10f) };
                AssetDatabase.CreateAsset(dict["Sauce"], $"{MaterialsFolder}/Mat_Sauce.mat");
            }

            dict["Cheese"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_Cheese.mat");
            if (dict["Cheese"] == null)
            {
                dict["Cheese"] = new Material(urpLit) { color = new Color(0.98f, 0.88f, 0.40f) };
                AssetDatabase.CreateAsset(dict["Cheese"], $"{MaterialsFolder}/Mat_Cheese.mat");
            }

            dict["GratedCheese"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_CheeseRalado 1.mat");
            if (dict["GratedCheese"] == null)
            {
                dict["GratedCheese"] = new Material(urpLit) { color = new Color(1.0f, 0.85f, 0.25f) };
                AssetDatabase.CreateAsset(dict["GratedCheese"], $"{MaterialsFolder}/Mat_CheeseRalado 1.mat");
            }

            dict["Calabresa"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_Calabresa.mat");
            if (dict["Calabresa"] == null)
            {
                dict["Calabresa"] = new Material(urpLit) { color = new Color(0.65f, 0.15f, 0.15f) };
                AssetDatabase.CreateAsset(dict["Calabresa"], $"{MaterialsFolder}/Mat_Calabresa.mat");
            }

            dict["Mushroom"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_Mushroom.mat");
            if (dict["Mushroom"] == null)
            {
                dict["Mushroom"] = new Material(urpLit) { color = new Color(0.82f, 0.76f, 0.66f) };
                AssetDatabase.CreateAsset(dict["Mushroom"], $"{MaterialsFolder}/Mat_Mushroom.mat");
            }

            dict["Container"] = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsFolder}/Mat_IngredientContainer.mat");
            if (dict["Container"] == null)
            {
                var mat = new Material(urpLit)
                {
                    color = new Color(0.85f, 0.85f, 0.88f)
                };
                if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.6f);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.7f);
                dict["Container"] = mat;
                AssetDatabase.CreateAsset(dict["Container"], $"{MaterialsFolder}/Mat_IngredientContainer.mat");
            }

            return dict;
        }

        private static Dictionary<string, GameObject> BuildIngredientVisualPrefabs(Dictionary<string, Material> mats)
        {
            var result = new Dictionary<string, GameObject>();

            // 1. Visual_Ingredient_Dough (Bola de massa arredondada)
            result["Dough"] = CreateOrUpdatePrefab("Visual_Ingredient_Dough", root =>
            {
                var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                body.name = "DoughMesh";
                body.transform.SetParent(root.transform, false);
                body.transform.localScale = new Vector3(0.22f, 0.15f, 0.22f);
                body.transform.localPosition = Vector3.zero;
                body.GetComponent<Renderer>().sharedMaterial = mats["Dough"];
                UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", Vector3.zero);
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.075f, 0f));
            });

            // 2. Visual_Ingredient_MassaAberta (Disco de massa aberta / base crua)
            result["MassaAberta"] = CreateOrUpdatePrefab("Visual_Ingredient_MassaAberta", root =>
            {
                var disk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                disk.name = "BaseDisk";
                disk.transform.SetParent(root.transform, false);
                disk.transform.localScale = new Vector3(0.36f, 0.008f, 0.36f);
                disk.transform.localPosition = Vector3.zero;
                disk.GetComponent<Renderer>().sharedMaterial = mats["Dough"];
                UnityEngine.Object.DestroyImmediate(disk.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", Vector3.zero);
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.008f, 0f));
                CreateAnchor(root.transform, "IngredientLayerAnchor", new Vector3(0f, 0.010f, 0f));
            });

            // 3. Visual_Ingredient_Sauce (Frasco / Dosador de molho de tomate)
            result["Sauce"] = CreateOrUpdatePrefab("Visual_Ingredient_Sauce", root =>
            {
                var jar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                jar.name = "JarBody";
                jar.transform.SetParent(root.transform, false);
                jar.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
                jar.transform.localPosition = Vector3.zero;
                jar.GetComponent<Renderer>().sharedMaterial = mats["Sauce"];
                UnityEngine.Object.DestroyImmediate(jar.GetComponent<Collider>());

                var lid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                lid.name = "JarLid";
                lid.transform.SetParent(root.transform, false);
                lid.transform.localScale = new Vector3(0.06f, 0.03f, 0.06f);
                lid.transform.localPosition = new Vector3(0f, 0.13f, 0f);
                lid.GetComponent<Renderer>().sharedMaterial = mats["Container"];
                UnityEngine.Object.DestroyImmediate(lid.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", new Vector3(0f, 0.05f, 0f));
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.12f, 0f));
            });

            // 4. Visual_Ingredient_Cheese (Bloco de Queijo Muçarela)
            result["Cheese"] = CreateOrUpdatePrefab("Visual_Ingredient_Cheese", root =>
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = "CheeseBlock";
                block.transform.SetParent(root.transform, false);
                block.transform.localScale = new Vector3(0.18f, 0.10f, 0.12f);
                block.transform.localPosition = Vector3.zero;
                block.GetComponent<Renderer>().sharedMaterial = mats["Cheese"];
                UnityEngine.Object.DestroyImmediate(block.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", Vector3.zero);
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.05f, 0f));
            });

            // 5. Visual_Ingredient_GratedCheese (Bowl de Queijo Ralado)
            result["GratedCheese"] = CreateOrUpdatePrefab("Visual_Ingredient_GratedCheese", root =>
            {
                var bowl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bowl.name = "Bowl";
                bowl.transform.SetParent(root.transform, false);
                bowl.transform.localScale = new Vector3(0.18f, 0.035f, 0.18f);
                bowl.transform.localPosition = Vector3.zero;
                bowl.GetComponent<Renderer>().sharedMaterial = mats["Container"];
                UnityEngine.Object.DestroyImmediate(bowl.GetComponent<Collider>());

                var mound = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                mound.name = "CheesePile";
                mound.transform.SetParent(root.transform, false);
                mound.transform.localScale = new Vector3(0.16f, 0.05f, 0.16f);
                mound.transform.localPosition = new Vector3(0f, 0.02f, 0f);
                mound.GetComponent<Renderer>().sharedMaterial = mats["GratedCheese"];
                UnityEngine.Object.DestroyImmediate(mound.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", new Vector3(0f, 0.02f, 0f));
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.035f, 0f));
            });

            // 6. Visual_Ingredient_Pepperoni (Calabresa Inteira)
            result["Pepperoni"] = CreateOrUpdatePrefab("Visual_Ingredient_Pepperoni", root =>
            {
                var sausage = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                sausage.name = "PepperoniStick";
                sausage.transform.SetParent(root.transform, false);
                sausage.transform.localScale = new Vector3(0.06f, 0.10f, 0.06f);
                sausage.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                sausage.transform.localPosition = Vector3.zero;
                sausage.GetComponent<Renderer>().sharedMaterial = mats["Calabresa"];
                UnityEngine.Object.DestroyImmediate(sausage.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", Vector3.zero);
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.03f, 0f));
            });

            // 7. Visual_Ingredient_SlicedPepperoni (Prato com Fatias de Calabresa)
            result["SlicedPepperoni"] = CreateOrUpdatePrefab("Visual_Ingredient_SlicedPepperoni", root =>
            {
                var dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dish.name = "Dish";
                dish.transform.SetParent(root.transform, false);
                dish.transform.localScale = new Vector3(0.18f, 0.015f, 0.18f);
                dish.transform.localPosition = Vector3.zero;
                dish.GetComponent<Renderer>().sharedMaterial = mats["Container"];
                UnityEngine.Object.DestroyImmediate(dish.GetComponent<Collider>());

                // 4 fatias circulares organizadas
                for (int i = 0; i < 4; i++)
                {
                    var slice = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    slice.name = $"Slice_{i}";
                    slice.transform.SetParent(root.transform, false);
                    slice.transform.localScale = new Vector3(0.065f, 0.004f, 0.065f);
                    float ang = i * Mathf.PI * 0.5f;
                    slice.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.035f, 0.018f, Mathf.Sin(ang) * 0.035f);
                    slice.GetComponent<Renderer>().sharedMaterial = mats["Calabresa"];
                    UnityEngine.Object.DestroyImmediate(slice.GetComponent<Collider>());
                }

                CreateAnchor(root.transform, "GripPoint", new Vector3(0f, 0.015f, 0f));
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.015f, 0f));
            });

            // 8. Visual_Ingredient_Mushroom (Cogumelo Inteiro)
            result["Mushroom"] = CreateOrUpdatePrefab("Visual_Ingredient_Mushroom", root =>
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cap.name = "Cap";
                cap.transform.SetParent(root.transform, false);
                cap.transform.localScale = new Vector3(0.13f, 0.07f, 0.13f);
                cap.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                cap.GetComponent<Renderer>().sharedMaterial = mats["Mushroom"];
                UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());

                var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.name = "Stem";
                stem.transform.SetParent(root.transform, false);
                stem.transform.localScale = new Vector3(0.05f, 0.04f, 0.05f);
                stem.transform.localPosition = new Vector3(0f, -0.02f, 0f);
                stem.GetComponent<Renderer>().sharedMaterial = mats["Mushroom"];
                UnityEngine.Object.DestroyImmediate(stem.GetComponent<Collider>());

                CreateAnchor(root.transform, "GripPoint", Vector3.zero);
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.06f, 0f));
            });

            // 9. Visual_Ingredient_SlicedMushroom (Prato com Cogumelo Fatiado)
            result["SlicedMushroom"] = CreateOrUpdatePrefab("Visual_Ingredient_SlicedMushroom", root =>
            {
                var dish = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                dish.name = "Dish";
                dish.transform.SetParent(root.transform, false);
                dish.transform.localScale = new Vector3(0.18f, 0.015f, 0.18f);
                dish.transform.localPosition = Vector3.zero;
                dish.GetComponent<Renderer>().sharedMaterial = mats["Container"];
                UnityEngine.Object.DestroyImmediate(dish.GetComponent<Collider>());

                for (int i = 0; i < 4; i++)
                {
                    var slice = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    slice.name = $"MushroomSlice_{i}";
                    slice.transform.SetParent(root.transform, false);
                    slice.transform.localScale = new Vector3(0.05f, 0.005f, 0.035f);
                    float ang = i * Mathf.PI * 0.5f;
                    slice.transform.localPosition = new Vector3(Mathf.Cos(ang) * 0.035f, 0.018f, Mathf.Sin(ang) * 0.035f);
                    slice.transform.localRotation = Quaternion.Euler(0f, i * 45f, 0f);
                    slice.GetComponent<Renderer>().sharedMaterial = mats["Mushroom"];
                    UnityEngine.Object.DestroyImmediate(slice.GetComponent<Collider>());
                }

                CreateAnchor(root.transform, "GripPoint", new Vector3(0f, 0.015f, 0f));
                CreateAnchor(root.transform, "SupportPoint", new Vector3(0f, -0.015f, 0f));
            });

            return result;
        }

        private static GameObject CreateOrUpdatePrefab(string prefabName, Action<GameObject> buildAction)
        {
            string prefabPath = $"{VisualsFolder}/{prefabName}.prefab";
            var tempGo = new GameObject(prefabName);

            try
            {
                buildAction(tempGo);
                var saved = PrefabUtility.SaveAsPrefabAsset(tempGo, prefabPath);
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempGo);
            }
        }

        private static Transform CreateAnchor(Transform parent, string name, Vector3 localPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static void UpdateIngredientProfiles(Dictionary<string, GameObject> prefabs)
        {
            ConfigureProfile("Profile_Ingredient_Dough.asset", prefabs["Dough"], ItemCategory.Dough, 1);
            ConfigureProfile("Profile_Ingredient_MassaAberta.asset", prefabs["MassaAberta"], ItemCategory.Dough, 2);
            ConfigureProfile("Profile_Ingredient_Sauce.asset", prefabs["Sauce"], ItemCategory.Sauce, 3);
            ConfigureProfile("Profile_Ingredient_Cheese.asset", prefabs["Cheese"], ItemCategory.Cheese, 4);
            ConfigureProfile("Profile_Ingredient_GratedCheese.asset", prefabs["GratedCheese"], ItemCategory.Cheese, 5);
            ConfigureProfile("Profile_Ingredient_Pepperoni.asset", prefabs["Pepperoni"], ItemCategory.Topping, 6);
            ConfigureProfile("Profile_Ingredient_SlicedPepperoni.asset", prefabs["SlicedPepperoni"], ItemCategory.Topping, 7);
            ConfigureProfile("Profile_Ingredient_Mushroom.asset", prefabs["Mushroom"], ItemCategory.Topping, 8);
            ConfigureProfile("Profile_Ingredient_SlicedMushroom.asset", prefabs["SlicedMushroom"], ItemCategory.Topping, 9);
        }

        private static void ConfigureProfile(string assetName, GameObject prefab, ItemCategory category, int defId)
        {
            string path = $"{ProfilesFolder}/{assetName}";
            var profile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<ItemPresentationProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            profile.visualPrefab = prefab;
            profile.category = category;
            profile.definitionId = defId;
            profile.isTool = false;
            profile.isPlaceholder = false;
            profile.gripPointName = "GripPoint";
            profile.supportPointName = "SupportPoint";

            profile.stationSlotPose = new ItemPoseOffset
            {
                position = Vector3.zero,
                rotationEuler = Vector3.zero,
                scale = Vector3.one
            };

            profile.fpRightHandPose = new ItemPoseOffset
            {
                position = new Vector3(0.04f, -0.04f, 0.22f),
                rotationEuler = Vector3.zero,
                scale = Vector3.one
            };

            profile.fpLeftHandPose = new ItemPoseOffset
            {
                position = new Vector3(-0.04f, -0.04f, 0.22f),
                rotationEuler = Vector3.zero,
                scale = Vector3.one
            };

            EditorUtility.SetDirty(profile);
        }

        private static void EnsureResourcesCatalog()
        {
            var mainCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
            if (mainCatalog != null)
            {
                // Garante que todos os 14 perfis estejam na lista
                string[] profileGuids = AssetDatabase.FindAssets("t:ItemPresentationProfile", new[] { ProfilesFolder });
                mainCatalog.profiles.Clear();
                foreach (var g in profileGuids)
                {
                    var p = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(AssetDatabase.GUIDToAssetPath(g));
                    if (p != null) mainCatalog.profiles.Add(p);
                }
                EditorUtility.SetDirty(mainCatalog);

                // Copia ou cria referência em Resources/ItemPresentationCatalog.asset
                if (!File.Exists(ResourcesCatalogPath))
                {
                    AssetDatabase.CopyAsset(CatalogPath, ResourcesCatalogPath);
                }
                else
                {
                    var resCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(ResourcesCatalogPath);
                    if (resCatalog != null)
                    {
                        resCatalog.profiles = new List<ItemPresentationProfile>(mainCatalog.profiles);
                        EditorUtility.SetDirty(resCatalog);
                    }
                }
            }
        }

        private static void FixPizzeriaPrefabSlotsAndCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);

            var prefabRoot = PrefabUtility.LoadPrefabContents(PizzeriaPrefabPath);
            if (prefabRoot == null) return;

            try
            {
                // 1. Atribui catálogo ao PizzeriaItemVisualizer
                var visualizer = prefabRoot.GetComponentInChildren<PizzeriaItemVisualizer>(true);
                if (visualizer != null)
                {
                    visualizer.catalog = catalog;
                    EditorUtility.SetDirty(visualizer);
                }

                // 2. Corrige âncoras de slots em todas as bancadas e mesas
                var stations = prefabRoot.GetComponentsInChildren<StationView>(true);
                foreach (var st in stations)
                {
                    CalibrateStationSlots(st);
                }

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PizzeriaPrefabPath);
                Debug.Log("[IngredientVisualSetup] ✅ Prefab canônico da pizzaria atualizado com catálogo e slots calibrados!");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        private static void FixGameSceneCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>(CatalogPath);
            var visualizers = UnityEngine.Object.FindObjectsByType<PizzeriaItemVisualizer>(FindObjectsSortMode.None);
            foreach (var vis in visualizers)
            {
                if (vis.catalog == null)
                {
                    vis.catalog = catalog;
                    EditorUtility.SetDirty(vis);
                    Debug.Log($"[IngredientVisualSetup] ✅ PizzeriaItemVisualizer em '{vis.gameObject.scene.name}' vinculado ao catálogo!");
                }
            }

            var stations = UnityEngine.Object.FindObjectsByType<StationView>(FindObjectsSortMode.None);
            foreach (var st in stations)
            {
                CalibrateStationSlots(st);
                EditorUtility.SetDirty(st);
            }
        }

        public static void CalibrateStationSlots(StationView st)
        {
            if (st == null) return;

            // Altura padrão do tampo das bancadas (mesa média = ~0.38m, balcão = ~0.38m)
            const float TableTopY = 0.38f;

            // Station 5 (Prep A) e Station 6 (Prep B)
            if (st.stationId == 5 || st.stationId == 6 || st is PrepStation)
            {
                EnsureTwoSlotAnchors(st, new Vector3(-0.35f, TableTopY, 0f), new Vector3(0.35f, TableTopY, 0f));
            }
            // Station 7 (Assembly)
            else if (st.stationId == 7 || st is AssemblyStation)
            {
                EnsureTwoSlotAnchors(st, new Vector3(-0.35f, TableTopY, 0f), new Vector3(0.35f, TableTopY, 0f));
            }
            // Station 4 (Balcão / Counter)
            else if (st.stationId == 4 || st is CounterStation)
            {
                EnsureTwoSlotAnchors(st, new Vector3(0f, TableTopY, -0.4f), new Vector3(0f, TableTopY, 0.4f));
            }
            // Station 9 (Packaging)
            else if (st.stationId == 9 || st is PackagingStation)
            {
                EnsureTwoSlotAnchors(st, new Vector3(-0.35f, TableTopY, 0f), new Vector3(0.35f, TableTopY, 0f));
            }
        }

        private static void EnsureTwoSlotAnchors(StationView st, Vector3 slot0Pos, Vector3 slot1Pos)
        {
            var slotsParent = st.transform.Find("Slots");
            if (slotsParent == null)
            {
                var go = new GameObject("Slots");
                go.transform.SetParent(st.transform, false);
                slotsParent = go.transform;
            }

            var s0 = slotsParent.Find("SlotAnchor_0") ?? slotsParent.Find("Slot_0");
            if (s0 == null)
            {
                var go = new GameObject("SlotAnchor_0");
                go.transform.SetParent(slotsParent, false);
                s0 = go.transform;
            }
            s0.localPosition = slot0Pos;
            s0.localRotation = Quaternion.identity;

            var s1 = slotsParent.Find("SlotAnchor_1") ?? slotsParent.Find("Slot_1");
            if (s1 == null)
            {
                var go = new GameObject("SlotAnchor_1");
                go.transform.SetParent(slotsParent, false);
                s1 = go.transform;
            }
            s1.localPosition = slot1Pos;
            s1.localRotation = Quaternion.identity;

            st.slotAnchors = new Transform[] { s0, s1 };
            EditorUtility.SetDirty(st);
        }

        private static void FixImportProfileSlotOffsets()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PizzeriaFbxImportProfile>(ImportProfilePath);
            if (profile == null) return;

            const float TableTopY = 0.38f;

            foreach (var m in profile.stationMappings)
            {
                if (m.stationId == 5 || m.stationId == 6 || m.stationId == 7 || m.stationId == 9)
                {
                    m.slotOffsets = new List<Vector3>
                    {
                        new Vector3(-0.35f, TableTopY, 0f),
                        new Vector3(0.35f, TableTopY, 0f)
                    };
                }
                else if (m.stationId == 4)
                {
                    m.slotOffsets = new List<Vector3>
                    {
                        new Vector3(0f, TableTopY, -0.4f),
                        new Vector3(0f, TableTopY, 0.4f)
                    };
                }
            }

            EditorUtility.SetDirty(profile);
        }
    }
}
#endif
