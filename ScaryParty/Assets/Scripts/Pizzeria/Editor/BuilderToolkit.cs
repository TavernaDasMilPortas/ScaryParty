#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ScaryParty.Pizzeria.Integration;

namespace ScaryParty.Pizzeria.Editor
{
    public static class BuilderToolkit
    {
        public static GameObject EnsureChild(GameObject parent, string childName, Func<GameObject> factory = null)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));

            var childTransform = parent.transform.Find(childName);
            if (childTransform != null)
            {
                return childTransform.gameObject;
            }

            GameObject childObj = factory != null ? factory() : new GameObject(childName);
            childObj.name = childName;
            childObj.transform.SetParent(parent.transform, false);
            return childObj;
        }

        public static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            var comp = target.GetComponent<T>();
            if (comp == null)
            {
                comp = target.AddComponent<T>();
            }
            return comp;
        }

        public static GeneratedMarker MarkGenerated(GameObject target, string builderName, string key, string configHash = "")
        {
            var marker = GetOrAddComponent<GeneratedMarker>(target);
            marker.Mark(builderName, key, configHash);
            EditorUtility.SetDirty(target);
            return marker;
        }

        public static bool RegisterNetworkPrefab(GameObject prefab)
        {
            if (prefab == null) return false;

            const string listPath = "Assets/DefaultNetworkPrefabs.asset";
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(listPath);
            if (list == null)
            {
                Debug.LogWarning($"[BuilderToolkit] NetworkPrefabsList não encontrado em {listPath}");
                return false;
            }

            if (list.Contains(prefab))
                return false;

            list.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(list);
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuilderToolkit] Prefab {prefab.name} registrado com sucesso no NetworkPrefabsList.");
            return true;
        }

        public static string BackupScene(Scene scene)
        {
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return null;

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string backupDir = Path.Combine(projectRoot, "Backups");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string backupFileName = $"{scene.name}_{timestamp}.unity";
            string backupPath = Path.Combine(backupDir, backupFileName);

            File.Copy(scene.path, backupPath, true);
            Debug.Log($"[BuilderToolkit] Backup da cena salvo em: {backupPath}");
            return backupPath;
        }

        public static string BackupCurrentScene()
        {
            return BackupScene(SceneManager.GetActiveScene());
        }
    }
}
#endif
