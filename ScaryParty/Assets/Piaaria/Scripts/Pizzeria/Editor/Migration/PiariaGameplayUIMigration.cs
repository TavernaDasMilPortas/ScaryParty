using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Exceptions;
using ScaryParty.UI.Piaria.Presenters;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ScaryParty.Pizzeria.Editor.Migration
{
    /// <summary>Installs the single gameplay HUD without changing any pizzeria or network gameplay components.</summary>
    public static class PiariaGameplayUIMigration
    {
        private const string ScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        private const string HudPath = "Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml";
        private const string PanelPath = "Assets/Piaaria/UI/PanelSettings.asset";
        private static readonly string[] PrefabPaths =
        {
            "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab",
            "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab",
            "Assets/Piaaria/Prefabs/The_Pizzeria_Building.prefab"
        };

        [MenuItem("Tools/Scary Party/Setup e Uso Único/UI (P.I.A.R.I.A)/Migrar Gameplay UI para Piaria", false, 285)]
        public static void MigrateGameSceneAndPrefabs()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Piaria migration] Exit Play Mode before migrating gameplay UI.");
                return;
            }

            var originalScene = SceneManager.GetActiveScene();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                int changes = EnsureGameScene(scene);
                if (changes > 0) EditorSceneManager.SaveScene(scene);
                foreach (var path in PrefabPaths)
                    changes += RemoveLegacyHudFromPrefab(path);
                Debug.Log($"[Piaria migration] Applied {changes} changes. Re-run to verify idempotence.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
                if (originalScene.IsValid() && originalScene.isLoaded)
                    SceneManager.SetActiveScene(originalScene);
            }
        }

        // Batch-mode entry point: migrate and fail CI if a second pass would change the scene.
        public static void MigrateAndVerify()
        {
            MigrateGameSceneAndPrefabs();
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool openedHere = !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                if (EnsureGameScene(scene) != 0)
                    throw new InvalidOperationException("Piaria scene migration is not idempotent.");
                foreach (var path in PrefabPaths)
                    if (RemoveLegacyHudFromPrefab(path) != 0)
                        throw new InvalidOperationException($"Piaria prefab migration is not idempotent: {path}");
                Debug.Log("[Piaria migration] Scene and prefabs are idempotent.");
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        public static int EnsureGameScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("A loaded GameScene is required", nameof(scene));
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudPath);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelPath);
            if (tree == null || panel == null)
                throw new InvalidOperationException("Piaria MainGameHUD.uxml and PanelSettings.asset are required.");

            int changes = 0;
            GameObject hud = null;
            foreach (var obj in EnumerateScene(scene))
            {
                if (obj.name == "MainGameUI" && hud == null) hud = obj;
                else if (obj.name == "MainGameUI" && hud != obj)
                {
                    changes += Remove<UIDocument>(obj);
                    changes += Remove<UIManager>(obj);
                }
                if (obj.GetComponent<InGameLobbyUIController>() != null)
                {
                    // The obsolete lobby document also owns a network object; remove only its visual pieces.
                    changes += Remove<InGameLobbyUIController>(obj);
                    changes += Remove<UIDocument>(obj);
                }
                changes += Remove<PizzeriaHudBuilder>(obj);
                if (obj.name.StartsWith("deliverybag.", StringComparison.OrdinalIgnoreCase) &&
                    obj.GetComponentInParent<ScaryParty.Pizzeria.Composition.PizzeriaRoot>() != null)
                    changes += EnsureDeliveryBag(obj);
            }
            if (hud == null)
            {
                hud = new GameObject("MainGameUI");
                SceneManager.MoveGameObjectToScene(hud, scene);
                Undo.RegisterCreatedObjectUndo(hud, "Create Piaria gameplay HUD");
                changes++;
            }
            var doc = Ensure<UIDocument>(hud, ref changes);
            if (doc.visualTreeAsset != tree || doc.panelSettings != panel)
            {
                Undo.RecordObject(doc, "Assign Piaria gameplay HUD");
                doc.visualTreeAsset = tree;
                doc.panelSettings = panel;
                EditorUtility.SetDirty(doc);
                changes++;
            }
            var facade = Ensure<UIManager>(hud, ref changes);
            if (facade.uiDocument != doc)
            {
                Undo.RecordObject(facade, "Bind UI compatibility facade");
                facade.uiDocument = doc;
                EditorUtility.SetDirty(facade);
                changes++;
            }
            Ensure<UIModalStack>(hud, ref changes);
            Bind(Ensure<ModalPresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<SettingsPresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<HudPresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<ScoreboardPresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<StoragePresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<StagingPresenter>(hud, ref changes), doc, ref changes);
            Bind(Ensure<ToastPresenter>(hud, ref changes), doc, ref changes);
            Ensure<MinimapPresenter>(hud, ref changes);
            Ensure<OrderBoardPresenter>(hud, ref changes);
            Ensure<RecipeBookPresenter>(hud, ref changes);
            Ensure<BackpackPanelPresenter>(hud, ref changes);
            Ensure<DeliverySelectionPresenter>(hud, ref changes);
            Ensure<DeliveryResultPresenter>(hud, ref changes);
            Ensure<TicketModalPresenter>(hud, ref changes);
            // Keep the map service as a technical render rig, not a second HUD.
            if (changes > 0) EditorSceneManager.MarkSceneDirty(scene);
            return changes;
        }

        public static int RemoveLegacyHudFromPrefab(string path)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return 0;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int changes = 0;
                foreach (var builder in root.GetComponentsInChildren<PizzeriaHudBuilder>(true))
                {
                    UnityEngine.Object.DestroyImmediate(builder);
                    changes++;
                }
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (child.name.StartsWith("deliverybag.", StringComparison.OrdinalIgnoreCase) &&
                        child.GetComponentInParent<ScaryParty.Pizzeria.Composition.PizzeriaRoot>() != null)
                        changes += EnsureDeliveryBag(child.gameObject);
                if (changes > 0) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changes;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int EnsureDeliveryBag(GameObject obj)
        {
            const string prefix = "deliverybag.";
            if (!int.TryParse(obj.name.Substring(prefix.Length), out int id) || id <= 0)
                return 0;
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return 0;
            int changes = 0;
            var bag = Ensure<DeliveryBagAuthoring>(obj, ref changes);
            var collider = obj.GetComponent<Collider>();
            if (collider == null) collider = Ensure<BoxCollider>(obj, ref changes);
            var serialized = new SerializedObject(bag);
            var idField = serialized.FindProperty("backpackId");
            var rendererField = serialized.FindProperty("bagRenderer");
            var colliderField = serialized.FindProperty("interactionCollider");
            if (idField.longValue != id || rendererField.objectReferenceValue != renderer ||
                colliderField.objectReferenceValue != collider)
            {
                Undo.RecordObject(bag, "Bind delivery bag");
                idField.longValue = id;
                rendererField.objectReferenceValue = renderer;
                colliderField.objectReferenceValue = collider;
                serialized.ApplyModifiedProperties();
                changes++;
            }
            return changes;
        }

        private static IEnumerable<GameObject> EnumerateScene(Scene scene)
        {
            var objects = new List<GameObject>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    objects.Add(child.gameObject);
            return objects;
        }

        private static void Bind(MonoBehaviour presenter, UIDocument doc, ref int changes)
        {
            var field = presenter.GetType().GetField("uiDocument");
            if (field == null || field.GetValue(presenter) == doc) return;
            Undo.RecordObject(presenter, "Bind Piaria gameplay presenter");
            field.SetValue(presenter, doc);
            EditorUtility.SetDirty(presenter);
            changes++;
        }

        private static T Ensure<T>(GameObject obj, ref int changes) where T : Component
        {
            var component = obj.GetComponent<T>();
            if (component != null) return component;
            changes++;
            return Undo.AddComponent<T>(obj);
        }

        private static int Remove<T>(GameObject obj) where T : Component
        {
            var count = 0;
            foreach (var component in obj.GetComponents<T>())
            {
                Undo.DestroyObjectImmediate(component);
                count++;
            }
            return count;
        }
    }
}

