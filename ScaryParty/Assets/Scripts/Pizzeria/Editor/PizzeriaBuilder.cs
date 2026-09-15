using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Editor
{
    public static class PizzeriaBuilder
    {
        private const string PrefabsFolder = "Assets/Prefabs/Pizzeria";
        private const string MaterialsFolder = "Assets/Prefabs/Pizzeria/Materials";
        private const string ConfigFolder = "Assets/ScriptableObjects/Pizzeria";
        private const string NetworkPrefabsAssetPath = "Assets/DefaultNetworkPrefabs.asset";

        [MenuItem("Tools/Scary Party/Pizzeria/Build or Update", false, 10)]
        public static void BuildOrUpdate()
        {
            EnsureDirectories();
            var materials = EnsureMaterials();
            var itemPrefabs = EnsureItemPrefabs(materials);
            var config = EnsureConfig();
            var pizzeriaPrefab = EnsurePizzeriaPrefab(materials, config);

            // Register in NetworkPrefabsList
            var prefabsToRegister = new List<GameObject>(itemPrefabs);
            if (pizzeriaPrefab != null) prefabsToRegister.Add(pizzeriaPrefab);
            RegisterInNetworkPrefabsList(prefabsToRegister);

            // Wire to CityGenerator in scene (if open)
            AssignPrefabToCityGenerator(pizzeriaPrefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PizzeriaBuilder] ✅ Pizzaria construída/atualizada com sucesso!");
            EditorUtility.DisplayDialog("Scary Party", "Módulo Pizzaria construído/atualizado com sucesso!", "OK");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PrefabsFolder)) Directory.CreateDirectory(PrefabsFolder);
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(ConfigFolder)) Directory.CreateDirectory(ConfigFolder);
        }

        private static Dictionary<string, Material> EnsureMaterials()
        {
            var dict = new Dictionary<string, Material>();
            dict["Wall"] = GetOrCreateMaterial("Mat_PizzeriaWall", new Color(0.85f, 0.82f, 0.78f));
            dict["Floor"] = GetOrCreateMaterial("Mat_PizzeriaFloor", new Color(0.35f, 0.35f, 0.38f));
            dict["Counter"] = GetOrCreateMaterial("Mat_PizzeriaCounter", new Color(0.45f, 0.25f, 0.15f));
            dict["Oven"] = GetOrCreateMaterial("Mat_PizzeriaOven", new Color(0.2f, 0.2f, 0.22f));
            dict["PizzaBox"] = GetOrCreateMaterial("Mat_PizzaBox", new Color(0.75f, 0.2f, 0.15f));
            dict["Dough"] = GetOrCreateMaterial("Mat_Dough", new Color(0.92f, 0.85f, 0.70f));
            dict["Sauce"] = GetOrCreateMaterial("Mat_Sauce", new Color(0.80f, 0.15f, 0.10f));
            dict["Cheese"] = GetOrCreateMaterial("Mat_Cheese", new Color(0.98f, 0.88f, 0.35f));
            dict["Calabresa"] = GetOrCreateMaterial("Mat_Calabresa", new Color(0.65f, 0.15f, 0.15f));
            dict["Tool"] = GetOrCreateMaterial("Mat_Tool", new Color(0.7f, 0.75f, 0.8f));
            return dict;
        }

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static List<GameObject> EnsureItemPrefabs(Dictionary<string, Material> materials)
        {
            var list = new List<GameObject>();
            list.Add(GetOrCreateItemPrefab("PizzaBox", PrimitiveType.Cube, new Vector3(0.5f, 0.15f, 0.5f), materials["PizzaBox"], ItemCategory.Box));
            list.Add(GetOrCreateItemPrefab("PizzaWIP", PrimitiveType.Cylinder, new Vector3(0.45f, 0.05f, 0.45f), materials["Dough"], ItemCategory.Pizza));
            list.Add(GetOrCreateItemPrefab("Ingredient_Dough", PrimitiveType.Sphere, new Vector3(0.25f, 0.25f, 0.25f), materials["Dough"], ItemCategory.Dough));
            list.Add(GetOrCreateItemPrefab("Ingredient_Cheese", PrimitiveType.Cube, new Vector3(0.25f, 0.25f, 0.25f), materials["Cheese"], ItemCategory.Cheese));
            list.Add(GetOrCreateItemPrefab("Ingredient_Calabresa", PrimitiveType.Cylinder, new Vector3(0.2f, 0.3f, 0.2f), materials["Calabresa"], ItemCategory.Topping));
            list.Add(GetOrCreateItemPrefab("Tool_Grater", PrimitiveType.Cube, new Vector3(0.2f, 0.35f, 0.15f), materials["Tool"], ItemCategory.Tool));
            list.Add(GetOrCreateItemPrefab("Tool_Knife", PrimitiveType.Cube, new Vector3(0.1f, 0.05f, 0.4f), materials["Tool"], ItemCategory.Tool));
            list.Add(GetOrCreateItemPrefab("SupplyCrate", PrimitiveType.Cube, new Vector3(0.8f, 0.6f, 0.8f), materials["Counter"], ItemCategory.Other));
            return list;
        }

        private static GameObject GetOrCreateItemPrefab(string name, PrimitiveType primType, Vector3 scale, Material mat, ItemCategory category)
        {
            string path = $"{PrefabsFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var go = GameObject.CreatePrimitive(primType);
            go.name = name;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            go.AddComponent<NetworkObject>();
            var phys = go.AddComponent<PhysicalItem>();
            phys.itemName = name;
            phys.category = category;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static PizzeriaConfig EnsureConfig()
        {
            string path = $"{ConfigFolder}/PizzeriaConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(path);
            if (config != null) return config;

            config = ScriptableObject.CreateInstance<PizzeriaConfig>();

            // Setup Default Ingredients
            var ingMassa = CreateIngredient(1, "Massa", ItemCategory.Dough, true, 2);
            var ingMassaAberta = CreateIngredient(2, "Massa Aberta", ItemCategory.Dough);
            var ingMolho = CreateIngredient(3, "Molho", ItemCategory.Sauce);
            var ingQueijo = CreateIngredient(4, "Queijo Inteiro", ItemCategory.Cheese, true, 5);
            var ingQueijoRalado = CreateIngredient(5, "Queijo Ralado", ItemCategory.Cheese);
            var ingCalabresa = CreateIngredient(6, "Calabresa Inteira", ItemCategory.Topping, true, 7);
            var ingCalabresaFatiada = CreateIngredient(7, "Calabresa Fatiada", ItemCategory.Topping);
            var ingCogumelo = CreateIngredient(8, "Cogumelo Inteiro", ItemCategory.Topping, true, 9);
            var ingCogumeloFatiado = CreateIngredient(9, "Cogumelo Fatiado", ItemCategory.Topping);

            config.ingredients.AddRange(new[] { ingMassa, ingMassaAberta, ingMolho, ingQueijo, ingQueijoRalado, ingCalabresa, ingCalabresaFatiada, ingCogumelo, ingCogumeloFatiado });

            // Setup Default Tools
            var toolRalador = CreateTool(1, "Ralador", ToolCapability.Grate, 1);
            var toolFaca = CreateTool(2, "Faca", ToolCapability.Slice, 1);
            config.tools.AddRange(new[] { toolRalador, toolFaca });

            // Setup Default Processes
            var procMassa = CreateProcess(1, "Abrir Massa", 1, ProcessingStage.Raw, 2, ProcessingStage.Prepared, ToolCapability.None, 3.0f);
            var procQueijo = CreateProcess(2, "Ralar Queijo", 4, ProcessingStage.Raw, 5, ProcessingStage.Prepared, ToolCapability.Grate, 3.0f);
            var procCalabresa = CreateProcess(3, "Fatiar Calabresa", 6, ProcessingStage.Raw, 7, ProcessingStage.Prepared, ToolCapability.Slice, 2.0f);
            config.processes.AddRange(new[] { procMassa, procQueijo, procCalabresa });

            // Setup Default Recipes
            var recMussarela = CreateRecipe(1, "Mussarela", 2, 15, 15f,
                new (int, ProcessingStage, int)[] { (3, ProcessingStage.Raw, 1), (5, ProcessingStage.Prepared, 1) });
            var recCalabresa = CreateRecipe(2, "Calabresa", 2, 20, 15f,
                new (int, ProcessingStage, int)[] { (3, ProcessingStage.Raw, 1), (5, ProcessingStage.Prepared, 1), (7, ProcessingStage.Prepared, 1) });
            config.recipes.AddRange(new[] { recMussarela, recCalabresa });

            // Setup Default Supplies
            config.supplyItems.Add(CreateSupplyItem(1, "Pacote Massa (5x)", 1, 5, 5, 20f));
            config.supplyItems.Add(CreateSupplyItem(2, "Pacote Molho (5x)", 3, 5, 5, 20f));
            config.supplyItems.Add(CreateSupplyItem(3, "Pacote Queijo (5x)", 4, 5, 10, 20f));
            config.supplyItems.Add(CreateSupplyItem(4, "Pacote Calabresa (5x)", 6, 5, 10, 20f));

            // Setup Default Upgrades
            config.upgrades.Add(CreateUpgrade(1, "FasterOven", UpgradeScope.Oven, "cookDuration", UpgradeOperation.PercentAdd, -0.20f));
            config.upgrades.Add(CreateUpgrade(2, "ExtraOvenSlot", UpgradeScope.Oven, "slotCount", UpgradeOperation.FlatAdd, 1f));
            config.upgrades.Add(CreateUpgrade(3, "LargerBackpack", UpgradeScope.Backpack, "slotCount", UpgradeOperation.FlatAdd, 1f));
            config.upgrades.Add(CreateUpgrade(4, "BackpackInsulation", UpgradeScope.Backpack, "decayMultiplier", UpgradeOperation.Multiply, 0.5f));
            config.upgrades.Add(CreateUpgrade(5, "UnlockMushroom", UpgradeScope.Recipe, "3", UpgradeOperation.BoolSet, 1f));
            config.upgrades.Add(CreateUpgrade(6, "FasterSupply", UpgradeScope.Restaurant, "supplyETA", UpgradeOperation.PercentAdd, -0.25f));

            AssetDatabase.CreateAsset(config, path);
            return config;
        }

        private static IngredientDef CreateIngredient(int id, string name, ItemCategory cat, bool reqPrep = false, int prepId = 0)
        {
            var def = ScriptableObject.CreateInstance<IngredientDef>();
            def.id = id;
            def.ingredientName = name;
            def.category = cat;
            def.requiresPrep = reqPrep;
            def.preparedIngredientId = prepId;
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/Ingredient_{id}_{name.Replace(" ", "")}.asset");
            return def;
        }

        private static ToolDef CreateTool(int id, string name, ToolCapability cap, int count)
        {
            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.id = id;
            def.toolName = name;
            def.capabilities = cap;
            def.initialSharedCount = count;
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/Tool_{id}_{name}.asset");
            return def;
        }

        private static ProcessDef CreateProcess(int id, string name, int inId, ProcessingStage inSt, int outId, ProcessingStage outSt, ToolCapability tool, float dur)
        {
            var def = ScriptableObject.CreateInstance<ProcessDef>();
            def.id = id;
            def.processName = name;
            def.inputIngredientId = inId;
            def.inputStage = inSt;
            def.outputIngredientId = outId;
            def.outputStage = outSt;
            def.requiredToolCapability = tool;
            def.baseDuration = dur;
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/Process_{id}_{name.Replace(" ", "")}.asset");
            return def;
        }

        private static RecipeDef CreateRecipe(int id, string name, int baseId, int price, float bakeDur, (int ingId, ProcessingStage stage, int qty)[] ings)
        {
            var def = ScriptableObject.CreateInstance<RecipeDef>();
            def.id = id;
            def.recipeName = name;
            def.baseIngredientId = baseId;
            def.basePrice = price;
            def.baseBakeDuration = bakeDur;
            foreach (var (ingId, stage, qty) in ings)
            {
                def.ingredients.Add(new IngredientRequirement { ingredientId = ingId, requiredStage = stage, quantity = qty });
            }
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/Recipe_{id}_{name}.asset");
            return def;
        }

        private static SupplyItemDef CreateSupplyItem(int id, string name, int ingId, int units, int price, float eta)
        {
            var def = ScriptableObject.CreateInstance<SupplyItemDef>();
            def.id = id;
            def.itemName = name;
            def.ingredientId = ingId;
            def.unitsPerPack = units;
            def.packPrice = price;
            def.baseETA = eta;
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/SupplyItem_{id}.asset");
            return def;
        }

        private static UpgradeDef CreateUpgrade(int id, string name, UpgradeScope scope, string stat, UpgradeOperation op, float val)
        {
            var def = ScriptableObject.CreateInstance<UpgradeDef>();
            def.id = id;
            def.upgradeName = name;
            def.scope = scope;
            def.statName = stat;
            def.operation = op;
            def.valuePerLevel = val;
            AssetDatabase.CreateAsset(def, $"{ConfigFolder}/Upgrade_{id}_{name}.asset");
            return def;
        }

        private static GameObject EnsurePizzeriaPrefab(Dictionary<string, Material> materials, PizzeriaConfig config)
        {
            string path = $"{PrefabsFolder}/PizzeriaPrefab.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) 
            {
                // To ensure a clean state without missing scripts or missing anchors,
                // we delete the old prefab and rebuild it from scratch.
                AssetDatabase.DeleteAsset(path);
            }

            var root = new GameObject("PizzeriaPrefab");
            root.transform.position = Vector3.zero;

            // Network Components
            root.AddComponent<NetworkObject>();
            var rootComp = root.AddComponent<PizzeriaRoot>();
            root.AddComponent<PizzeriaNetworkState>();
            root.AddComponent<PizzeriaCommandHandler>();
            root.AddComponent<ScaryParty.Pizzeria.Presentation.PizzeriaHudBuilder>();
            root.AddComponent<ScaryParty.Pizzeria.Presentation.PizzeriaItemVisualizer>();

            // Assign config field via SerializedObject
            var so = new SerializedObject(rootComp);
            so.FindProperty("_config").objectReferenceValue = config;

            // Building Architecture (12x12m)
            var bld = new GameObject("Building");
            bld.transform.SetParent(root.transform);

            // Floor (12x12m)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.SetParent(bld.transform);
            floor.transform.localPosition = new Vector3(0, -0.1f, 0);
            floor.transform.localScale = new Vector3(12f, 0.2f, 12f);
            floor.GetComponent<Renderer>().sharedMaterial = materials["Floor"];

            // Back Wall (Z = +6)
            CreateWall("BackWall", bld.transform, new Vector3(0, 2f, 6f), new Vector3(12f, 4f, 0.3f), materials["Wall"]);
            // Left Wall (X = -6)
            CreateWall("LeftWall", bld.transform, new Vector3(-6f, 2f, 0), new Vector3(0.3f, 4f, 12f), materials["Wall"]);
            // Right Wall (X = +6)
            CreateWall("RightWall", bld.transform, new Vector3(6f, 2f, 0), new Vector3(0.3f, 4f, 12f), materials["Wall"]);
            // Front Wall with Doorway (Z = -6): Left section, Right section, leaving 3m door opening in center
            CreateWall("FrontWall_Left", bld.transform, new Vector3(-3.75f, 2f, -6f), new Vector3(4.5f, 4f, 0.3f), materials["Wall"]);
            CreateWall("FrontWall_Right", bld.transform, new Vector3(3.75f, 2f, -6f), new Vector3(4.5f, 4f, 0.3f), materials["Wall"]);

            // Stations Group
            var stationsRoot = new GameObject("Stations");
            stationsRoot.transform.SetParent(root.transform);

            // 1. Storage Fridge (Left side)
            var fridge = CreateStation<StorageStation>("Storage_Fridge", 1, stationsRoot.transform, new Vector3(-5f, 1f, 4f), new Vector3(1.5f, 2f, 1f), materials["Counter"]);
            fridge.acceptedIngredientIds = new[] { 3, 4, 6, 8 };

            // 2. Storage Cupboard (Left side)
            var cupboard = CreateStation<StorageStation>("Storage_Cupboard", 2, stationsRoot.transform, new Vector3(-5f, 1f, 1.5f), new Vector3(1.5f, 2f, 1f), materials["Counter"]);
            cupboard.acceptedIngredientIds = new[] { 1 };

            // 3. Tool Rack (Shared tools)
            var toolRack = CreateStation<ToolRackStation>("Station_ToolRack", 3, stationsRoot.transform, new Vector3(-5f, 0.5f, -1f), new Vector3(1.2f, 1f, 1f), materials["Counter"]);

            // 4. Neutral Waiting Counter (Bancada de Espera)
            CreateStation<CounterStation>("Station_NeutralCounter", 4, stationsRoot.transform, new Vector3(-2.5f, 0.5f, 3f), new Vector3(2f, 1f, 1f), materials["Counter"]);

            // 5. Prep Counter A (Slot 0, 1)
            var prepA = CreateStation<PrepStation>("Station_Prep_A", 5, stationsRoot.transform, new Vector3(0f, 0.5f, 3f), new Vector3(2f, 1f, 1f), materials["Counter"]);
            prepA.defaultProcessId = 1;

            // 6. Prep Counter B (Slot 0, 1)
            var prepB = CreateStation<PrepStation>("Station_Prep_B", 6, stationsRoot.transform, new Vector3(2.5f, 0.5f, 3f), new Vector3(2f, 1f, 1f), materials["Counter"]);
            prepB.defaultProcessId = 2;

            // 7. Assembly Counter
            CreateStation<AssemblyStation>("Station_Assembly", 7, stationsRoot.transform, new Vector3(5f, 0.5f, 3f), new Vector3(1.5f, 1f, 1.5f), materials["Counter"]);

            // 8. Oven (Back wall)
            CreateStation<OvenStation>("Station_Oven", 8, stationsRoot.transform, new Vector3(0f, 0.75f, 5.2f), new Vector3(3f, 1.5f, 1.2f), materials["Oven"]);

            // 9. Packaging Station (Right wall)
            CreateStation<PackagingStation>("Station_Packaging", 9, stationsRoot.transform, new Vector3(5f, 0.5f, 0f), new Vector3(1.5f, 1f, 2f), materials["Counter"]);

            // 10. Staging Counter (Retirada de Caixas - near doorway)
            CreateStation<StagingStation>("Station_Staging", 10, stationsRoot.transform, new Vector3(3.5f, 0.5f, -4.5f), new Vector3(3f, 1f, 1f), materials["Counter"]);

            // 11. Supplier Terminal
            CreateStation<SupplierTerminal>("Terminal_Supplier", 11, stationsRoot.transform, new Vector3(-5f, 0.75f, -3.5f), new Vector3(1f, 1.5f, 0.8f), materials["Counter"]);

            // 12. Order Phone
            var phone = CreateStation<PhoneStation>("Station_Phone", 12, stationsRoot.transform, new Vector3(-3f, 0.6f, -4.5f), new Vector3(0.8f, 1.2f, 0.8f), materials["Counter"]);
            phone.isRinging = true;

            // 13. Receiving Area
            CreateStation<ReceivingStation>("Area_Receiving", 13, stationsRoot.transform, new Vector3(-3.5f, 0.05f, -7f), new Vector3(2f, 0.1f, 2f), materials["Floor"]);

            // 14. Trash Bin
            CreateStation<TrashStation>("Station_Trash", 14, stationsRoot.transform, new Vector3(5f, 0.5f, -3.5f), new Vector3(0.8f, 1f, 0.8f), materials["Oven"]);

            // Anchors Group
            var anchors = new GameObject("Anchors");
            anchors.transform.SetParent(root.transform);

            var spawnList = new List<Transform>();
            for (int i = 0; i < 4; i++)
            {
                var sp = new GameObject($"PlayerSpawn_{i}");
                sp.transform.SetParent(anchors.transform);
                sp.transform.localPosition = new Vector3(-1.5f + (i * 1.0f), 0.1f, -1.0f);
                sp.transform.localRotation = Quaternion.identity;
                spawnList.Add(sp.transform);
            }

            var recAnc = new GameObject("ReceivingAnchor");
            recAnc.transform.SetParent(anchors.transform);
            recAnc.transform.localPosition = new Vector3(-3.5f, 0.1f, -7f);

            var recovAnc = new GameObject("RecoveryAnchor");
            recovAnc.transform.SetParent(anchors.transform);
            recovAnc.transform.localPosition = new Vector3(-1f, 0.5f, 3f);

            var entAnc = new GameObject("EntranceAnchor");
            entAnc.transform.SetParent(anchors.transform);
            entAnc.transform.localPosition = new Vector3(0f, 0.1f, -6f);

            // Connect anchors to PizzeriaRoot
            rootComp.playerSpawnAnchors = spawnList.ToArray();
            rootComp.receivingAnchor = recAnc.transform;
            rootComp.recoveryAnchor = recovAnc.transform;
            rootComp.entranceAnchor = entAnc.transform;
            rootComp.toolRackAnchor = toolRack.transform;

            so.ApplyModifiedProperties();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateWall(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.SetParent(parent);
            wall.transform.localPosition = localPos;
            wall.transform.localScale = localScale;
            if (mat != null) wall.GetComponent<Renderer>().sharedMaterial = mat;
            return wall;
        }

        private static T CreateStation<T>(string name, int stationId, Transform parent, Vector3 localPos, Vector3 localScale, Material mat) where T : StationView
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;

            var station = go.AddComponent<T>();
            station.stationId = stationId;
            station.stationName = name;
            return station;
        }

        private static void RegisterInNetworkPrefabsList(List<GameObject> prefabs)
        {
            var listAsset = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsAssetPath);
            if (listAsset == null)
            {
                Debug.LogWarning($"[PizzeriaBuilder] NetworkPrefabsList não encontrado em {NetworkPrefabsAssetPath}");
                return;
            }

            bool changed = false;
            foreach (var p in prefabs)
            {
                if (p == null) continue;
                bool alreadyIn = false;
                foreach (var entry in listAsset.PrefabList)
                {
                    if (entry.Prefab == p)
                    {
                        alreadyIn = true;
                        break;
                    }
                }

                if (!alreadyIn)
                {
                    listAsset.Add(new NetworkPrefab { Prefab = p });
                    changed = true;
                    Debug.Log($"[PizzeriaBuilder] Registrado NetworkPrefab: {p.name}");
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(listAsset);
            }
        }
        private static void AssignPrefabToCityGenerator(GameObject pizzeriaPrefab)
        {
            if (pizzeriaPrefab == null) return;

            // Find CityGenerator in every open scene
            var generators = Object.FindObjectsByType<CityGenerator>(FindObjectsSortMode.None);
            foreach (var gen in generators)
            {
                if (gen.pizzeriaPrefab == null)
                {
                    gen.pizzeriaPrefab = pizzeriaPrefab;
                    EditorUtility.SetDirty(gen);
                    Debug.Log($"[PizzeriaBuilder] ✅ pizzeriaPrefab atribuído ao CityGenerator em '{gen.gameObject.scene.name}'.");
                }
            }
        }
    }
}
