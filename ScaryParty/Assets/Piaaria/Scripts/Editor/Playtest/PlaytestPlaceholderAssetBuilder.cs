using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScaryParty.Combat.Monsters;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Editor.Playtest
{
    /// <summary>
    /// Construtor idempotente dos placeholders 3D persistentes da Fase P5:
    /// materiais URP, catálogo de monstros (reusando FBXs existentes sem labels de espécie),
    /// funcionários, moto com âncoras explícitas, terminal de suprimentos, área de recebimento,
    /// quadro de gestão, ingrediente Alho (id=10) + pacote de suprimento e slot extra de embalagem.
    /// </summary>
    public static class PlaytestPlaceholderAssetBuilder
    {
        public const string PlaytestRoot = "Assets/Piaaria/Playtest";
        public const string MaterialsDir = PlaytestRoot + "/Materials";
        public const string PrefabsDir = PlaytestRoot + "/Prefabs";
        public const string ScriptableObjectsDir = PlaytestRoot + "/ScriptableObjects";
        public const string PresetsDir = PlaytestRoot + "/Presets";

        public const string MonsterCatalogPath = ScriptableObjectsDir + "/MonsterVisualCatalog.asset";
        public const string GarlicIngredientAssetPath = ScriptableObjectsDir + "/IngredientDef_10_Garlic.asset";
        public const string GarlicSupplyAssetPath = ScriptableObjectsDir + "/SupplyItem_5_Garlic.asset";

        public const string VampFbxPath = "Assets/Piaaria/Art/Models/vamp.fbx";
        public const string LobiFbxPath = "Assets/Piaaria/Art/Models/lobi.fbx";
        public const string GhostFbxPath = "Assets/Piaaria/Art/Models/fanstasmi.fbx";
        public const string ZombieFbxPath = "Assets/Piaaria/Art/Models/zombi.fbx";

        public static int EnsureAllPlaceholders(PlaytestReferenceManifest manifest, StringBuilder sb)
        {
            int changes = 0;
            EnsureFolders();

            // 1. Materiais URP persistentes
            var matEyes = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Eyes.mat", Color.white, new Color(0.25f, 0.25f, 0.25f), ref changes, sb);
            var matCashierCloth = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Cashier.mat", new Color(0.18f, 0.48f, 0.82f), Color.black, ref changes, sb);
            var matCashierAcc = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_CashierVisor.mat", new Color(0.10f, 0.26f, 0.56f), Color.black, ref changes, sb);
            var matPizzaioloCloth = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Pizzaiolo.mat", new Color(0.92f, 0.89f, 0.80f), Color.black, ref changes, sb);
            var matPizzaioloHat = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_ChefHat.mat", Color.white, Color.black, ref changes, sb);
            var matMotoBody = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_MotoBody.mat", new Color(0.84f, 0.14f, 0.14f), Color.black, ref changes, sb);
            var matMotoMetal = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_MotoMetal.mat", new Color(0.72f, 0.76f, 0.80f), Color.black, ref changes, sb);
            var matMotoTire = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_MotoTire.mat", new Color(0.10f, 0.10f, 0.12f), Color.black, ref changes, sb);
            var matWood = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_CrateWood.mat", new Color(0.60f, 0.40f, 0.20f), Color.black, ref changes, sb);
            var matTerminal = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Terminal.mat", new Color(0.18f, 0.20f, 0.24f), Color.black, ref changes, sb);
            var matScreen = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_ScreenGlow.mat", new Color(0.14f, 0.72f, 0.86f), new Color(0.10f, 0.48f, 0.60f), ref changes, sb);
            var matReceivingPad = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_ReceivingFloor.mat", new Color(0.88f, 0.66f, 0.16f), Color.black, ref changes, sb);
            var matBoardFrame = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_BoardFrame.mat", new Color(0.32f, 0.20f, 0.12f), Color.black, ref changes, sb);
            var matGarlicBulb = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Garlic.mat", new Color(0.92f, 0.94f, 0.78f), Color.black, ref changes, sb);
            var matGarlicStem = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_GarlicStem.mat", new Color(0.36f, 0.68f, 0.26f), Color.black, ref changes, sb);
            var matMushroomCap = EnsureUrpMaterial(MaterialsDir + "/Mat_Playtest_Mushroom.mat", new Color(0.76f, 0.62f, 0.48f), Color.black, ref changes, sb);

            // 2. Monstros + Catálogo
            var vampPrefab = EnsureMonsterPrefab(PrefabsDir + "/Visual_Monster_Vampire.prefab", VampFbxPath, NpcSpecies.Vampire, matEyes, ref changes, sb);
            var lobiPrefab = EnsureMonsterPrefab(PrefabsDir + "/Visual_Monster_Werewolf.prefab", LobiFbxPath, NpcSpecies.Werewolf, matEyes, ref changes, sb);
            var ghostPrefab = EnsureMonsterPrefab(PrefabsDir + "/Visual_Monster_Ghost.prefab", GhostFbxPath, NpcSpecies.Ghost, matEyes, ref changes, sb);
            var zombiePrefab = EnsureMonsterPrefab(PrefabsDir + "/Visual_Monster_Zombie.prefab", ZombieFbxPath, NpcSpecies.Zombie, matEyes, ref changes, sb);

            var monsterCatalog = EnsureMonsterCatalog(
                vampPrefab, lobiPrefab, ghostPrefab, zombiePrefab,
                ref changes, sb);

            // 3. Funcionários (Caixa vs Pizzaiolo distinguíveis)
            var cashierPrefab = EnsureEmployeePrefab(PrefabsDir + "/Visual_Employee_Cashier.prefab", EmployeeRole.Cashier, matCashierCloth, matCashierAcc, ref changes, sb);
            var pizzaioloPrefab = EnsureEmployeePrefab(PrefabsDir + "/Visual_Employee_Pizzaiolo.prefab", EmployeeRole.Pizzaiolo, matPizzaioloCloth, matPizzaioloHat, ref changes, sb);

            // 4. Moto com âncoras explícitas
            var motoPrefab = EnsureMotorcyclePrefab(PrefabsDir + "/Visual_DeliveryMotorcycle.prefab", matMotoBody, matMotoMetal, matMotoTire, matWood, matScreen, ref changes, sb);

            // 5. Estações & Suprimentos
            var supplierVisualPrefab = EnsureSupplierTerminalPrefab(PrefabsDir + "/Visual_SupplierTerminal.prefab", matTerminal, matScreen, matMotoMetal, ref changes, sb);
            var receivingVisualPrefab = EnsureReceivingZonePrefab(PrefabsDir + "/Visual_ReceivingZone.prefab", matReceivingPad, matWood, ref changes, sb);
            var boardVisualPrefab = EnsureManagementBoardPrefab(PrefabsDir + "/Visual_ManagementBoard.prefab", matBoardFrame, matPizzaioloHat, matScreen, ref changes, sb);

            // 6. Ingredientes (Alho id=10 + Cogumelo fallback)
            var garlicPrefab = EnsureGarlicPrefab(PrefabsDir + "/Visual_Ingredient_Garlic.prefab", matGarlicBulb, matGarlicStem, ref changes, sb);
            var mushroomPrefab = EnsureMushroomPrefab(PrefabsDir + "/Visual_Ingredient_Mushroom.prefab", matMushroomCap, matPizzaioloCloth, ref changes, sb);

            changes += EnsureGarlicAuthoringAndConfig(garlicPrefab, mushroomPrefab, sb);
            changes += EnsurePizzeriaPrefabWiring(supplierVisualPrefab, receivingVisualPrefab, boardVisualPrefab, sb);

            // 7. Atualizar Manifesto Persistente
            if (manifest != null)
            {
                bool mDirty = false;
                if (manifest.monsterVisualCatalog != monsterCatalog) { manifest.monsterVisualCatalog = monsterCatalog; mDirty = true; }
                if (manifest.cashierVisualPrefab != cashierPrefab) { manifest.cashierVisualPrefab = cashierPrefab; mDirty = true; }
                if (manifest.pizzaioloVisualPrefab != pizzaioloPrefab) { manifest.pizzaioloVisualPrefab = pizzaioloPrefab; mDirty = true; }
                if (manifest.motorcycleVisualPrefab != motoPrefab) { manifest.motorcycleVisualPrefab = motoPrefab; mDirty = true; }
                if (manifest.supplierTerminalVisualPrefab != supplierVisualPrefab) { manifest.supplierTerminalVisualPrefab = supplierVisualPrefab; mDirty = true; }
                if (manifest.receivingZoneVisualPrefab != receivingVisualPrefab) { manifest.receivingZoneVisualPrefab = receivingVisualPrefab; mDirty = true; }
                if (manifest.managementBoardVisualPrefab != boardVisualPrefab) { manifest.managementBoardVisualPrefab = boardVisualPrefab; mDirty = true; }

                var desiredRecords = new List<PlaytestPlaceholderRecord>
                {
                    MakeRecord("monster_vampire", "Monstro", AssetDatabase.GetAssetPath(vampPrefab), "Substituir apenas se trocar o FBX/animação mantendo MonsterVisualCatalog + EntityVisualAnchorRig.", vampPrefab),
                    MakeRecord("monster_werewolf", "Monstro", AssetDatabase.GetAssetPath(lobiPrefab), "Substituir apenas se trocar o FBX/animação mantendo MonsterVisualCatalog + EntityVisualAnchorRig.", lobiPrefab),
                    MakeRecord("monster_ghost", "Monstro", AssetDatabase.GetAssetPath(ghostPrefab), "Substituir apenas se trocar o FBX/animação mantendo MonsterVisualCatalog + EntityVisualAnchorRig.", ghostPrefab),
                    MakeRecord("monster_zombie", "Monstro", AssetDatabase.GetAssetPath(zombiePrefab), "Substituir apenas se trocar o FBX/animação mantendo MonsterVisualCatalog + EntityVisualAnchorRig.", zombiePrefab),
                    MakeRecord("employee_cashier", "Funcionário", AssetDatabase.GetAssetPath(cashierPrefab), "Substituir por modelo humanóide final do Caixa preservando EntityVisualAnchorRig.", cashierPrefab),
                    MakeRecord("employee_pizzaiolo", "Funcionário", AssetDatabase.GetAssetPath(pizzaioloPrefab), "Substituir por modelo humanóide final do Pizzaiolo preservando EntityVisualAnchorRig.", pizzaioloPrefab),
                    MakeRecord("motorcycle", "Veículo", AssetDatabase.GetAssetPath(motoPrefab), "Substituir por mesh final da moto preservando DeliveryMotorcycleController e EntityVisualAnchorRig.", motoPrefab),
                    MakeRecord("supplier_terminal", "Cozinha/Suprimentos", AssetDatabase.GetAssetPath(supplierVisualPrefab), "Substituir por modelo final do computador/terminal sob Station_SupplierTerminal.", supplierVisualPrefab),
                    MakeRecord("receiving_zone", "Cozinha/Suprimentos", AssetDatabase.GetAssetPath(receivingVisualPrefab), "Substituir por palete/doca final sob Station_Receiving mantendo slotAnchor.", receivingVisualPrefab),
                    MakeRecord("management_board", "Cozinha/Operações", AssetDatabase.GetAssetPath(boardVisualPrefab), "Substituir por mural 3D final sob ManagementBoard_Playtest mantendo ManagementBoardStation.", boardVisualPrefab),
                    MakeRecord("ingredient_garlic", "Ingrediente", AssetDatabase.GetAssetPath(garlicPrefab), "Substituir por malha final de Alho em IngredientDef_10_Garlic.placeholderPrefab.", garlicPrefab),
                    MakeRecord("ingredient_mushroom", "Ingrediente", AssetDatabase.GetAssetPath(mushroomPrefab), "Substituir por malha final de Cogumelo nos IngredientDefs 8/9.", mushroomPrefab)
                };

                if (!RecordsMatch(manifest.placeholders, desiredRecords))
                {
                    manifest.placeholders = desiredRecords;
                    mDirty = true;
                }

                if (mDirty)
                {
                    EditorUtility.SetDirty(manifest);
                    sb.AppendLine("[P5] Updated PlaytestReferenceManifest with persistent 3D placeholders.");
                    changes++;
                }
            }

            return changes;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Piaaria", "Playtest");
            EnsureFolder(PlaytestRoot, "Materials");
            EnsureFolder(PlaytestRoot, "Prefabs");
            EnsureFolder(PlaytestRoot, "ScriptableObjects");
            EnsureFolder(PlaytestRoot, "Presets");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
                AssetDatabase.CreateFolder(parent, child);
        }

        private static Material EnsureUrpMaterial(string path, Color color, Color emission, ref int changes, StringBuilder sb)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(urpLit);
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (emission != Color.black && mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission);
                }
                AssetDatabase.CreateAsset(mat, path);
                changes++;
                sb.AppendLine($"[P5 Material] Created {path}");
                return mat;
            }

            bool dirty = false;
            if (urpLit != null && mat.shader != urpLit) { mat.shader = urpLit; dirty = true; }
            Color cur = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.color;
            if (ColorDiff(cur, color) > 0.02f)
            {
                mat.color = color;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                dirty = true;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(mat);
                changes++;
            }
            return mat;
        }

        private static float ColorDiff(Color a, Color b)
            => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);

        private static GameObject EnsureMonsterPrefab(
            string path,
            string fbxPath,
            NpcSpecies species,
            Material eyesMat,
            ref int changes,
            StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    bool ok = loaded.GetComponent<EntityVisualAnchorRig>() != null && loaded.transform.Find("Eyes") != null;
                    if (ok) return existing;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(loaded);
                }
            }

            var root = new GameObject($"Visual_Monster_{species}");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.Monster;
                rig.placeholderId = $"monster_{species}";

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (fbx != null)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                    inst.name = "Model";
                    foreach (var c in inst.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);

                    Bounds b = new Bounds(Vector3.zero, new Vector3(1f, 1.8f, 1f));
                    bool hasBounds = false;
                    foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!hasBounds) { b = r.bounds; hasBounds = true; }
                        else b.Encapsulate(r.bounds);
                    }
                    float h = Mathf.Max(0.25f, b.size.y);
                    float scale = 1.85f / h;
                    inst.transform.localScale = Vector3.one * scale;
                    inst.transform.localPosition = new Vector3(-b.center.x * scale, -b.min.y * scale, -b.center.z * scale);
                }
                else
                {
                    var body = CreatePart(root.transform, "BodyFallback", PrimitiveType.Capsule, new Vector3(0f, 0.9f, 0f), new Vector3(0.75f, 0.9f, 0.75f), Vector3.zero, eyesMat);
                    body.transform.localScale = new Vector3(0.75f, 0.9f, 0.75f);
                }

                var eyes = CreatePart(root.transform, "Eyes", PrimitiveType.Cube, new Vector3(0f, 1.54f, 0.22f), new Vector3(0.32f, 0.08f, 0.12f), Vector3.zero, eyesMat);
                rig.ResolveAnchor(ref rig.headAccessoryAnchor, "HeadAnchor", new Vector3(0f, 1.65f, 0f), Vector3.zero);
                if (eyes != null) eyes.transform.SetParent(root.transform, true);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static MonsterVisualCatalog EnsureMonsterCatalog(
            GameObject vamp,
            GameObject lobi,
            GameObject ghost,
            GameObject zombie,
            ref int changes,
            StringBuilder sb)
        {
            var cat = AssetDatabase.LoadAssetAtPath<MonsterVisualCatalog>(MonsterCatalogPath);
            if (cat == null)
            {
                cat = ScriptableObject.CreateInstance<MonsterVisualCatalog>();
                AssetDatabase.CreateAsset(cat, MonsterCatalogPath);
                changes++;
                sb.AppendLine($"[P5 Catalog] Created {MonsterCatalogPath}");
            }

            var desired = new List<MonsterSpeciesVisualEntry>
            {
                new MonsterSpeciesVisualEntry { species = NpcSpecies.Vampire, visualPrefab = vamp, baseTint = new Color(0.85f, 0.80f, 0.84f), aggressiveTint = Color.red, visualLocalScale = Vector3.one, colliderHeight = 1.85f, colliderRadius = 0.38f },
                new MonsterSpeciesVisualEntry { species = NpcSpecies.Werewolf, visualPrefab = lobi, baseTint = new Color(0.86f, 0.82f, 0.76f), aggressiveTint = new Color(1f, 0.45f, 0.15f), visualLocalScale = Vector3.one, colliderHeight = 1.90f, colliderRadius = 0.42f },
                new MonsterSpeciesVisualEntry { species = NpcSpecies.Ghost, visualPrefab = ghost, baseTint = new Color(0.82f, 0.94f, 1.0f), aggressiveTint = new Color(0.45f, 0.95f, 1f), visualLocalScale = Vector3.one, colliderHeight = 1.80f, colliderRadius = 0.35f },
                new MonsterSpeciesVisualEntry { species = NpcSpecies.Zombie, visualPrefab = zombie, baseTint = new Color(0.74f, 0.88f, 0.72f), aggressiveTint = new Color(0.85f, 1f, 0.35f), visualLocalScale = Vector3.one, colliderHeight = 1.82f, colliderRadius = 0.38f }
            };

            bool dirty = cat.entries == null || cat.entries.Count != desired.Count;
            if (!dirty)
            {
                for (int i = 0; i < desired.Count; i++)
                {
                    if (cat.entries[i].species != desired[i].species || cat.entries[i].visualPrefab != desired[i].visualPrefab)
                    {
                        dirty = true;
                        break;
                    }
                }
            }

            if (dirty)
            {
                cat.entries = desired;
                EditorUtility.SetDirty(cat);
                changes++;
                sb.AppendLine("[P5 Catalog] Updated MonsterVisualCatalog entries.");
            }
            return cat;
        }

        private static GameObject EnsureEmployeePrefab(string path, EmployeeRole role, Material clothMat, Material accMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (loaded.GetComponent<EntityVisualAnchorRig>() != null) return existing;
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }

            var root = new GameObject($"Visual_Employee_{role}");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.CharacterAccessories;
                rig.placeholderId = $"employee_{role}";

                CreatePart(root.transform, "Torso", PrimitiveType.Capsule, new Vector3(0f, 0.90f, 0f), new Vector3(0.58f, 0.75f, 0.45f), Vector3.zero, clothMat);
                CreatePart(root.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 1.54f, 0f), new Vector3(0.36f, 0.36f, 0.36f), Vector3.zero, clothMat);

                var headAccAnchor = rig.ResolveAnchor(ref rig.headAccessoryAnchor, "HeadAccessoryAnchor", new Vector3(0f, 1.72f, 0f), Vector3.zero);
                var apronAnchor = rig.ResolveAnchor(ref rig.apronAnchor, "ApronAnchor", new Vector3(0f, 0.82f, 0.24f), Vector3.zero);
                rig.ResolveAnchor(ref rig.backpackBackAnchor, "BackpackBackAnchor", new Vector3(0f, 1.15f, -0.24f), Vector3.zero);

                if (role == EmployeeRole.Cashier)
                {
                    CreatePart(headAccAnchor, "CashierCapCrown", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.34f, 0.08f, 0.34f), Vector3.zero, accMat);
                    CreatePart(headAccAnchor, "CashierVisor", PrimitiveType.Cube, new Vector3(0f, -0.02f, 0.17f), new Vector3(0.32f, 0.03f, 0.18f), Vector3.zero, accMat);
                    CreatePart(apronAnchor, "CashierNameBadge", PrimitiveType.Cube, new Vector3(0.10f, 0.22f, 0.01f), new Vector3(0.14f, 0.08f, 0.02f), Vector3.zero, accMat);
                }
                else
                {
                    CreatePart(headAccAnchor, "ChefHatBase", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(0.30f, 0.14f, 0.30f), Vector3.zero, accMat);
                    CreatePart(headAccAnchor, "ChefHatPuff", PrimitiveType.Sphere, new Vector3(0f, 0.20f, 0f), new Vector3(0.38f, 0.22f, 0.38f), Vector3.zero, accMat);
                    CreatePart(apronAnchor, "ChefApron", PrimitiveType.Cube, Vector3.zero, new Vector3(0.42f, 0.52f, 0.04f), Vector3.zero, accMat);
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureMotorcyclePrefab(
            string path, Material bodyMat, Material metalMat, Material tireMat, Material cargoMat, Material lightMat,
            ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
            {
                var loaded = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var rigChk = loaded.GetComponent<EntityVisualAnchorRig>();
                    if (rigChk != null && rigChk.seatAnchor != null && rigChk.cargoAnchor != null) return existing;
                }
                finally { PrefabUtility.UnloadPrefabContents(loaded); }
            }

            var root = new GameObject("Visual_DeliveryMotorcycle");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.Motorcycle;
                rig.placeholderId = "delivery_motorcycle";

                CreatePart(root.transform, "Chassis", PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f), new Vector3(0.42f, 0.40f, 1.65f), Vector3.zero, bodyMat);
                CreatePart(root.transform, "SeatCushion", PrimitiveType.Cube, new Vector3(0f, 0.78f, -0.14f), new Vector3(0.34f, 0.12f, 0.56f), Vector3.zero, tireMat);
                CreatePart(root.transform, "WheelFront", PrimitiveType.Cylinder, new Vector3(0f, 0.32f, 0.72f), new Vector3(0.62f, 0.09f, 0.62f), new Vector3(0f, 0f, 90f), tireMat);
                CreatePart(root.transform, "WheelBack", PrimitiveType.Cylinder, new Vector3(0f, 0.32f, -0.72f), new Vector3(0.62f, 0.11f, 0.62f), new Vector3(0f, 0f, 90f), tireMat);
                CreatePart(root.transform, "Handlebar", PrimitiveType.Cube, new Vector3(0f, 1.02f, 0.56f), new Vector3(0.75f, 0.08f, 0.10f), Vector3.zero, metalMat);
                CreatePart(root.transform, "Headlight", PrimitiveType.Cylinder, new Vector3(0f, 0.92f, 0.76f), new Vector3(0.18f, 0.08f, 0.18f), new Vector3(90f, 0f, 0f), lightMat);
                CreatePart(root.transform, "DeliveryCargoBox", PrimitiveType.Cube, new Vector3(0f, 0.96f, -0.74f), new Vector3(0.48f, 0.36f, 0.46f), Vector3.zero, cargoMat);

                rig.ResolveAnchor(ref rig.seatAnchor, "RiderSeatAnchor", new Vector3(0f, 0.80f, -0.10f), Vector3.zero);
                rig.ResolveAnchor(ref rig.handlebarLeftAnchor, "HandlebarLeftAnchor", new Vector3(-0.32f, 1.02f, 0.52f), Vector3.zero);
                rig.ResolveAnchor(ref rig.handlebarRightAnchor, "HandlebarRightAnchor", new Vector3(0.32f, 1.02f, 0.52f), Vector3.zero);
                rig.ResolveAnchor(ref rig.leftDismountAnchor, "LeftExitAnchor", new Vector3(-1.35f, 0.05f, 0f), Vector3.zero);
                rig.ResolveAnchor(ref rig.rightDismountAnchor, "RightExitAnchor", new Vector3(1.35f, 0.05f, 0f), Vector3.zero);
                rig.ResolveAnchor(ref rig.cargoAnchor, "CargoRackAnchor", new Vector3(0f, 1.15f, -0.74f), Vector3.zero);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureSupplierTerminalPrefab(string path, Material bodyMat, Material screenMat, Material trimMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Visual_SupplierTerminal");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.Station;
                rig.placeholderId = "supplier_terminal";

                CreatePart(root.transform, "DeskPedestal", PrimitiveType.Cube, new Vector3(0f, 0.45f, 0f), new Vector3(0.85f, 0.90f, 0.58f), Vector3.zero, bodyMat);
                CreatePart(root.transform, "MonitorFrame", PrimitiveType.Cube, new Vector3(0f, 1.15f, -0.05f), new Vector3(0.72f, 0.48f, 0.08f), new Vector3(-12f, 0f, 0f), trimMat);
                CreatePart(root.transform, "MonitorScreen", PrimitiveType.Cube, new Vector3(0f, 1.15f, 0f), new Vector3(0.64f, 0.40f, 0.04f), new Vector3(-12f, 0f, 0f), screenMat);
                CreatePart(root.transform, "KeyboardTray", PrimitiveType.Cube, new Vector3(0f, 0.91f, 0.14f), new Vector3(0.46f, 0.04f, 0.18f), Vector3.zero, trimMat);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureReceivingZonePrefab(string path, Material padMat, Material crateMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Visual_ReceivingZone");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.Station;
                rig.placeholderId = "receiving_zone";

                CreatePart(root.transform, "DockPad", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(1.75f, 0.06f, 1.55f), Vector3.zero, padMat);
                CreatePart(root.transform, "CrateMarker_L", PrimitiveType.Cube, new Vector3(-0.55f, 0.25f, -0.45f), new Vector3(0.42f, 0.38f, 0.42f), Vector3.zero, crateMat);
                CreatePart(root.transform, "CrateMarker_R", PrimitiveType.Cube, new Vector3(0.55f, 0.25f, -0.45f), new Vector3(0.42f, 0.38f, 0.42f), Vector3.zero, crateMat);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureManagementBoardPrefab(string path, Material frameMat, Material paperMat, Material accentMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Visual_ManagementBoard");
            try
            {
                var rig = root.AddComponent<EntityVisualAnchorRig>();
                rig.rigRole = EntityVisualAnchorRig.RigRole.Station;
                rig.placeholderId = "management_board";

                CreatePart(root.transform, "StandPost", PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(0.14f, 1.30f, 0.14f), Vector3.zero, frameMat);
                CreatePart(root.transform, "BoardPanel", PrimitiveType.Cube, new Vector3(0f, 1.35f, 0f), new Vector3(1.20f, 0.85f, 0.08f), Vector3.zero, frameMat);
                CreatePart(root.transform, "SheetLeft", PrimitiveType.Cube, new Vector3(-0.30f, 1.38f, 0.05f), new Vector3(0.36f, 0.46f, 0.02f), Vector3.zero, paperMat);
                CreatePart(root.transform, "SheetRight", PrimitiveType.Cube, new Vector3(0.28f, 1.34f, 0.05f), new Vector3(0.40f, 0.40f, 0.02f), Vector3.zero, accentMat);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureGarlicPrefab(string path, Material bulbMat, Material stemMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Visual_Ingredient_Garlic");
            try
            {
                CreatePart(root.transform, "BulbCenter", PrimitiveType.Sphere, new Vector3(0f, 0.07f, 0f), new Vector3(0.16f, 0.13f, 0.16f), Vector3.zero, bulbMat);
                CreatePart(root.transform, "CloveL", PrimitiveType.Sphere, new Vector3(-0.04f, 0.06f, 0.03f), new Vector3(0.09f, 0.10f, 0.09f), Vector3.zero, bulbMat);
                CreatePart(root.transform, "CloveR", PrimitiveType.Sphere, new Vector3(0.04f, 0.06f, -0.02f), new Vector3(0.09f, 0.10f, 0.09f), Vector3.zero, bulbMat);
                CreatePart(root.transform, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0f), new Vector3(0.03f, 0.05f, 0.03f), Vector3.zero, stemMat);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject EnsureMushroomPrefab(string path, Material capMat, Material stemMat, ref int changes, StringBuilder sb)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;

            var root = new GameObject("Visual_Ingredient_Mushroom");
            try
            {
                CreatePart(root.transform, "Stem", PrimitiveType.Cylinder, new Vector3(0f, 0.06f, 0f), new Vector3(0.07f, 0.06f, 0.07f), Vector3.zero, stemMat);
                CreatePart(root.transform, "Cap", PrimitiveType.Sphere, new Vector3(0f, 0.12f, 0f), new Vector3(0.18f, 0.10f, 0.18f), Vector3.zero, capMat);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, path);
                changes++;
                sb.AppendLine($"[P5 Prefab] Saved {path}");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static int EnsureGarlicAuthoringAndConfig(GameObject garlicPrefab, GameObject mushroomPrefab, StringBuilder sb)
        {
            int changes = 0;

            var garlicDef = AssetDatabase.LoadAssetAtPath<IngredientDef>(GarlicIngredientAssetPath);
            if (garlicDef == null)
            {
                garlicDef = ScriptableObject.CreateInstance<IngredientDef>();
                garlicDef.id = 10;
                garlicDef.ingredientName = "Alho";
                garlicDef.category = ItemCategory.Topping;
                garlicDef.requiresPrep = false;
                garlicDef.placeholderColor = new Color(0.92f, 0.94f, 0.76f);
                garlicDef.placeholderPrefab = garlicPrefab;
                AssetDatabase.CreateAsset(garlicDef, GarlicIngredientAssetPath);
                changes++;
                sb.AppendLine($"[P5 Config] Created {GarlicIngredientAssetPath}");
            }
            else if (garlicDef.placeholderPrefab != garlicPrefab || garlicDef.id != 10)
            {
                garlicDef.id = 10;
                garlicDef.ingredientName = "Alho";
                garlicDef.category = ItemCategory.Topping;
                garlicDef.requiresPrep = false;
                garlicDef.placeholderColor = new Color(0.92f, 0.94f, 0.76f);
                garlicDef.placeholderPrefab = garlicPrefab;
                EditorUtility.SetDirty(garlicDef);
                changes++;
            }

            var garlicSupply = AssetDatabase.LoadAssetAtPath<SupplyItemDef>(GarlicSupplyAssetPath);
            if (garlicSupply == null)
            {
                garlicSupply = ScriptableObject.CreateInstance<SupplyItemDef>();
                garlicSupply.id = 5;
                garlicSupply.itemName = "Pacote Alho (5x)";
                garlicSupply.ingredientId = 10;
                garlicSupply.unitsPerPack = 5;
                garlicSupply.packPrice = 14;
                garlicSupply.baseETA = 18f;
                AssetDatabase.CreateAsset(garlicSupply, GarlicSupplyAssetPath);
                changes++;
                sb.AppendLine($"[P5 Config] Created {GarlicSupplyAssetPath}");
            }

            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PlaytestInstallerAndValidator.PizzeriaConfigPath);
            if (cfg != null)
            {
                bool cfgDirty = false;
                bool hasGarlicIng = false;
                foreach (var ing in cfg.ingredients)
                {
                    if (ing == null) continue;
                    if (ing.id == 10) hasGarlicIng = true;
                    else if ((ing.id == 8 || ing.id == 9) && ing.placeholderPrefab == null && mushroomPrefab != null)
                    {
                        ing.placeholderPrefab = mushroomPrefab;
                        EditorUtility.SetDirty(ing);
                        changes++;
                    }
                }
                if (!hasGarlicIng)
                {
                    cfg.ingredients.Add(garlicDef);
                    cfgDirty = true;
                }

                bool hasGarlicSup = false;
                foreach (var s in cfg.supplyItems)
                {
                    if (s != null && s.id == 5) { hasGarlicSup = true; break; }
                }
                if (!hasGarlicSup)
                {
                    cfg.supplyItems.Add(garlicSupply);
                    cfgDirty = true;
                }

                bool hasGarlicStock = false;
                foreach (var slot in cfg.initialStorageSlots)
                {
                    if (slot != null && slot.EffectiveIngredientId == 10) { hasGarlicStock = true; break; }
                }
                if (!hasGarlicStock)
                {
                    cfg.initialStorageSlots.Add(new StorageSlotConfig
                    {
                        storageId = 5,
                        ingredient = garlicDef,
                        ingredientId = 10,
                        initialStock = 10,
                        capacity = 100
                    });
                    cfgDirty = true;
                }

                if (cfgDirty)
                {
                    EditorUtility.SetDirty(cfg);
                    changes++;
                    sb.AppendLine("[P5 Config] Registered Alho (id=10) + SupplyItem_5_Garlic in PizzeriaConfig.");
                }
            }

            return changes;
        }

        private static int EnsurePizzeriaPrefabWiring(
            GameObject supplierVisualPrefab,
            GameObject receivingVisualPrefab,
            GameObject boardVisualPrefab,
            StringBuilder sb)
        {
            string prefabPath = PlaytestInstallerAndValidator.PizzeriaPrefabPath;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null) return 0;

            int changes = 0;
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                bool dirty = false;

                // 1. Station_Receiving (id=14): anexar visual + garantir slotAnchor
                foreach (var sv in root.GetComponentsInChildren<StationView>(true))
                {
                    if (sv.stationId == 14)
                    {
                        if (sv.GetComponentsInChildren<Renderer>(true).Length == 0 && receivingVisualPrefab != null)
                        {
                            var inst = (GameObject)PrefabUtility.InstantiatePrefab(receivingVisualPrefab, sv.transform);
                            inst.name = "Visual_ReceivingZone";
                            inst.transform.localPosition = Vector3.zero;
                            inst.transform.localRotation = Quaternion.identity;
                            dirty = true;
                            changes++;
                            sb.AppendLine("[P5 Prefab] Attached Visual_ReceivingZone to Station_Receiving (id=14).");
                        }
                    }
                    else if (sv.stationId == 20)
                    {
                        if (sv.GetComponentsInChildren<Renderer>(true).Length == 0 && supplierVisualPrefab != null)
                        {
                            var inst = (GameObject)PrefabUtility.InstantiatePrefab(supplierVisualPrefab, sv.transform);
                            inst.name = "Visual_SupplierTerminal";
                            inst.transform.localPosition = Vector3.zero;
                            inst.transform.localRotation = Quaternion.identity;
                            dirty = true;
                            changes++;
                            sb.AppendLine("[P5 Prefab] Attached Visual_SupplierTerminal to Station_SupplierTerminal (id=20).");
                        }
                        if (sv.slotAnchors == null || sv.slotAnchors.Length == 0)
                        {
                            var anchorGo = new GameObject("TerminalAnchor");
                            anchorGo.transform.SetParent(sv.transform, false);
                            anchorGo.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                            sv.slotAnchors = new[] { anchorGo.transform };
                            dirty = true;
                            changes++;
                        }
                        var boxCol = sv.GetComponent<BoxCollider>();
                        if (boxCol != null && boxCol.size.y < 0.9f)
                        {
                            boxCol.center = new Vector3(0f, 0.7f, 0f);
                            boxCol.size = new Vector3(1.1f, 1.4f, 0.9f);
                            dirty = true;
                            changes++;
                        }
                    }
                    else if (sv.stationId == 9 && sv is PackagingStation)
                    {
                        if (sv.slotAnchors == null || sv.slotAnchors.Length < 4)
                        {
                            Transform anchorParent = sv.slotAnchors != null && sv.slotAnchors.Length > 0 && sv.slotAnchors[0] != null
                                ? sv.slotAnchors[0].parent
                                : sv.transform;

                            var list = new List<Transform>();
                            if (sv.slotAnchors != null)
                            {
                                foreach (var t in sv.slotAnchors)
                                    if (t != null) list.Add(t);
                            }

                            int nextIdx = list.Count;
                            while (list.Count < 4)
                            {
                                var extra = new GameObject($"SlotAnchor_{nextIdx}");
                                extra.transform.SetParent(anchorParent, false);
                                extra.transform.localPosition = new Vector3(0f, 0.50f, 2.75f);
                                list.Add(extra.transform);
                                nextIdx++;
                            }

                            sv.slotAnchors = list.ToArray();
                            EditorUtility.SetDirty(sv);
                            dirty = true;
                            changes++;
                            sb.AppendLine("[P5 Prefab] Added 4th slotAnchor to PackagingStation (id=9).");
                        }
                    }
                }

                // 2. Quadro de Gestão / Operações físico dentro da pizzaria
                var existingBoard = root.GetComponentInChildren<ManagementBoardStation>(true);
                if (existingBoard == null && boardVisualPrefab != null)
                {
                    var boardGo = new GameObject("ManagementBoard_Playtest");
                    boardGo.transform.SetParent(root.transform, false);
                    boardGo.transform.localPosition = new Vector3(19.2f, -1.40f, -21.2f);
                    boardGo.transform.localRotation = Quaternion.identity;

                    var col = boardGo.AddComponent<BoxCollider>();
                    col.center = new Vector3(0f, 1.15f, 0f);
                    col.size = new Vector3(1.35f, 1.45f, 0.45f);

                    var station = boardGo.AddComponent<ManagementBoardStation>();
                    station.defaultTab = "orders";

                    var vis = (GameObject)PrefabUtility.InstantiatePrefab(boardVisualPrefab, boardGo.transform);
                    vis.name = "Visual_ManagementBoard";
                    vis.transform.localPosition = Vector3.zero;
                    vis.transform.localRotation = Quaternion.identity;

                    dirty = true;
                    changes++;
                    sb.AppendLine("[P5 Prefab] Installed ManagementBoard_Playtest inside PizzeriaModular_Manual.prefab.");
                }

                if (dirty)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return changes;
        }

        private static GameObject CreatePart(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Vector3 localPos,
            Vector3 localScale,
            Vector3 localEuler,
            Material mat)
        {
            var go = GameObject.CreatePrimitive(primitive);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;
            var rend = go.GetComponent<Renderer>();
            if (rend != null && mat != null)
                rend.sharedMaterial = mat;
            return go;
        }

        private static PlaytestPlaceholderRecord MakeRecord(string id, string cat, string path, string note, UnityEngine.Object asset)
            => new PlaytestPlaceholderRecord { id = id, category = cat, assetPath = path, replaceWithNote = note, asset = asset };

        private static bool RecordsMatch(List<PlaytestPlaceholderRecord> current, List<PlaytestPlaceholderRecord> desired)
        {
            if (current == null || current.Count != desired.Count) return false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i] == null || current[i].id != desired[i].id || current[i].asset != desired[i].asset || current[i].assetPath != desired[i].assetPath)
                    return false;
            }
            return true;
        }
    }
}
