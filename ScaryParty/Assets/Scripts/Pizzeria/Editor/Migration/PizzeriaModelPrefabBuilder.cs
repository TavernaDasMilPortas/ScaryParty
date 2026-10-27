#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    public static class PizzeriaModelPrefabBuilder
    {
        public const string VisualsFolder = "Assets/Prefabs/Pizzeria/Visuals";
        public const string ProfilesFolder = "Assets/ScriptableObjects/Pizzeria/Profiles";

        public static void BuildVisualPrefabs(PizzeriaFbxImportProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));

            if (!Directory.Exists(VisualsFolder))
            {
                Directory.CreateDirectory(VisualsFolder);
                AssetDatabase.Refresh();
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(profile.fbxAssetPath);
            if (fbx == null)
            {
                throw new FileNotFoundException($"FBX da Pizzaria não encontrado em: {profile.fbxAssetPath}");
            }

            foreach (var rule in profile.itemScaleRules)
            {
                Transform sourceNode = FindNodeWithPrefix(fbx.transform, rule.nodePrefix);
                if (sourceNode == null)
                {
                    Debug.LogWarning($"[PizzeriaModelPrefabBuilder] Nó com prefixo '{rule.nodePrefix}' não encontrado no FBX!");
                    continue;
                }

                BuildSingleVisualPrefab(sourceNode, rule);
            }

            // Garante PizzaBox com BoxLabelAnchor
            BuildPizzaBoxVisualPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PizzeriaModelPrefabBuilder] ✅ Modelos visuais reutilizáveis extraídos e prefabs salvos com sucesso!");
        }

        private static void BuildSingleVisualPrefab(Transform sourceNode, ItemScaleRule rule)
        {
            var mf = sourceNode.GetComponent<MeshFilter>();
            var mr = sourceNode.GetComponent<MeshRenderer>();
            if (mf == null || mf.sharedMesh == null) return;

            string prefabPath = $"{VisualsFolder}/{rule.targetPrefabName}.prefab";
            var root = new GameObject(rule.targetPrefabName);

            try
            {
                Bounds meshBounds = mf.sharedMesh.bounds;
                float scale = rule.scaleFactor > 0.001f ? rule.scaleFactor : 1.0f;

                // Filho Visual com malha centrada
                var visualGo = new GameObject("VisualMesh");
                visualGo.transform.SetParent(root.transform, false);

                var newMf = visualGo.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;

                var newMr = visualGo.AddComponent<MeshRenderer>();
                newMr.sharedMaterials = mr != null ? mr.sharedMaterials : new Material[0];

                visualGo.transform.localScale = Vector3.one * scale;
                visualGo.transform.localEulerAngles = rule.visualRotationEuler;
                visualGo.transform.localPosition = -Vector3.Scale(meshBounds.center, visualGo.transform.localScale);

                // GripPoint
                var gripGo = new GameObject("GripPoint");
                gripGo.transform.SetParent(root.transform, false);
                gripGo.transform.localPosition = rule.gripPointOffset;

                // SupportPoint (apoio inferior)
                var supportGo = new GameObject("SupportPoint");
                supportGo.transform.SetParent(root.transform, false);
                Vector3 supportPos = rule.supportPointOffset;
                if (supportPos == Vector3.zero)
                {
                    supportPos = new Vector3(0f, -meshBounds.extents.y * scale, 0f);
                }
                supportGo.transform.localPosition = supportPos;

                // Âncoras especiais de pizza
                if (rule.targetPrefabName.Contains("PizzaPan") || rule.targetPrefabName.Contains("PizzaBase"))
                {
                    var ingAnchor = new GameObject("IngredientLayerAnchor");
                    ingAnchor.transform.SetParent(root.transform, false);
                    ingAnchor.transform.localPosition = new Vector3(0f, 0.02f, 0f);

                    var smokeAnchor = new GameObject("SmokeAnchor");
                    smokeAnchor.transform.SetParent(root.transform, false);
                    smokeAnchor.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                }

                // Salva prefab de forma limpa preservando GUID caso já exista
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PizzeriaModelPrefabBuilder] Prefab visual criado: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildPizzaBoxVisualPrefab()
        {
            string prefabPath = $"{VisualsFolder}/Visual_PizzaBox.prefab";
            var root = new GameObject("Visual_PizzaBox");

            try
            {
                // Corpo plano da caixa de pizza (42x42cm, 5cm de altura)
                var boxBody = GameObject.CreatePrimitive(PrimitiveType.Cube);
                boxBody.name = "BoxMesh";
                boxBody.transform.SetParent(root.transform, false);
                boxBody.transform.localScale = new Vector3(0.42f, 0.05f, 0.42f);
                boxBody.transform.localPosition = Vector3.zero;

                var col = boxBody.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    mat.color = new Color(0.72f, 0.52f, 0.35f); // Papelão
                    boxBody.GetComponent<Renderer>().sharedMaterial = mat;
                }

                // GripPoint
                var gripGo = new GameObject("GripPoint");
                gripGo.transform.SetParent(root.transform, false);
                gripGo.transform.localPosition = Vector3.zero;

                // SupportPoint
                var supportGo = new GameObject("SupportPoint");
                supportGo.transform.SetParent(root.transform, false);
                supportGo.transform.localPosition = new Vector3(0f, -0.025f, 0f);

                // BoxLabelAnchor no topo da caixa
                var labelAnchor = new GameObject("BoxLabelAnchor");
                labelAnchor.transform.SetParent(root.transform, false);
                labelAnchor.transform.localPosition = new Vector3(0f, 0.03f, 0f);
                labelAnchor.transform.localEulerAngles = new Vector3(90f, 0f, 0f);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[PizzeriaModelPrefabBuilder] Prefab visual de caixa criado: {prefabPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        public static void LinkVisualsToPresentationProfiles()
        {
            var knifeVisual = AssetDatabase.LoadAssetAtPath<GameObject>($"{VisualsFolder}/Visual_Knife.prefab");
            var cutterVisual = AssetDatabase.LoadAssetAtPath<GameObject>($"{VisualsFolder}/Visual_PizzaCutter.prefab");
            var panVisual = AssetDatabase.LoadAssetAtPath<GameObject>($"{VisualsFolder}/Visual_PizzaPan.prefab");
            var boxVisual = AssetDatabase.LoadAssetAtPath<GameObject>($"{VisualsFolder}/Visual_PizzaBox.prefab");

            // Profile_Tool_Knife
            var knifeProfile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{ProfilesFolder}/Profile_Tool_Knife.asset");
            if (knifeProfile != null && knifeVisual != null)
            {
                knifeProfile.visualPrefab = knifeVisual;
                knifeProfile.gripPointName = "GripPoint";
                knifeProfile.supportPointName = "SupportPoint";
                EditorUtility.SetDirty(knifeProfile);
            }

            // Profile_Pizza
            var pizzaProfile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{ProfilesFolder}/Profile_Pizza.asset");
            if (pizzaProfile != null && panVisual != null)
            {
                pizzaProfile.visualPrefab = panVisual;
                pizzaProfile.gripPointName = "GripPoint";
                pizzaProfile.supportPointName = "SupportPoint";
                EditorUtility.SetDirty(pizzaProfile);
            }

            // Profile_PizzaBase
            var pizzaBaseProfile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{ProfilesFolder}/Profile_PizzaBase.asset");
            if (pizzaBaseProfile != null && panVisual != null)
            {
                pizzaBaseProfile.visualPrefab = panVisual;
                pizzaBaseProfile.gripPointName = "GripPoint";
                pizzaBaseProfile.supportPointName = "SupportPoint";
                EditorUtility.SetDirty(pizzaBaseProfile);
            }

            // Profile_PizzaBox
            var pizzaBoxProfile = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>($"{ProfilesFolder}/Profile_PizzaBox.asset");
            if (pizzaBoxProfile != null && boxVisual != null)
            {
                pizzaBoxProfile.visualPrefab = boxVisual;
                pizzaBoxProfile.gripPointName = "GripPoint";
                pizzaBoxProfile.supportPointName = "SupportPoint";
                EditorUtility.SetDirty(pizzaBoxProfile);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[PizzeriaModelPrefabBuilder] ✅ ItemPresentationProfiles vinculados aos novos modelos visuais com sucesso!");
        }

        private static Transform FindNodeWithPrefix(Transform root, string prefix)
        {
            if (root == null || string.IsNullOrEmpty(prefix)) return null;
            foreach (Transform child in root)
            {
                if (child.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }
                var sub = FindNodeWithPrefix(child, prefix);
                if (sub != null) return sub;
            }
            return null;
        }
    }
}
#endif
