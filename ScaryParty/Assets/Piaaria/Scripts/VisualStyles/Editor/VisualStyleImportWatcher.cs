#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    [Serializable]
    public class VisualPendingImportRecord
    {
        public string assetPath;
        public string assetGuid;
        public string changeType; // Imported, Modified, Deleted
        public string detectedUtc;
    }

    [Serializable]
    internal class VisualPendingImportQueueData
    {
        public List<VisualPendingImportRecord> items = new List<VisualPendingImportRecord>();
    }

    /// <summary>
    /// Lightweight AssetPostprocessor that records new or changed visual assets as pending
    /// without converting assets or mutating source files during import.
    /// </summary>
    public class VisualStyleImportWatcher : AssetPostprocessor
    {
        private const string QueueFilePath = "Library/VisualStylePendingImports.json";
        private static readonly string[] IgnoredPrefixes =
        {
            "Assets/Piaaria/Art/VisualStyles/",
            "Assets/Piaaria/Prefabs/VisualStyles/",
            "Assets/Piaaria/Scenes/GameplayVariants/",
            "Assets/Piaaria/Scenes/VisualTests/",
            "Assets/ThirdParty/Distant Lands/",
            "Assets/TextMesh Pro/",
            "Packages/",
            "ProjectSettings/",
            "Library/"
        };

        private static readonly string[] WatchedExtensions =
        {
            ".fbx", ".obj", ".dae", ".blend", ".prefab", ".mat",
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".exr", ".shader"
        };

        public static event Action OnPendingQueueChanged;

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            bool changed = false;

            if (importedAssets != null)
            {
                for (int i = 0; i < importedAssets.Length; i++)
                {
                    if (ShouldTrackPath(importedAssets[i]))
                    {
                        changed |= EnqueueInternal(importedAssets[i], "ImportedOrModified");
                    }
                }
            }

            if (movedAssets != null)
            {
                for (int i = 0; i < movedAssets.Length; i++)
                {
                    if (ShouldTrackPath(movedAssets[i]))
                    {
                        changed |= EnqueueInternal(movedAssets[i], "Moved");
                    }
                }
            }

            if (deletedAssets != null)
            {
                for (int i = 0; i < deletedAssets.Length; i++)
                {
                    if (ShouldTrackPath(deletedAssets[i]))
                    {
                        changed |= EnqueueInternal(deletedAssets[i], "Deleted");
                    }
                }
            }

            if (changed)
            {
                OnPendingQueueChanged?.Invoke();
            }
        }

        public static bool ShouldTrackPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string normalized = assetPath.Replace('\\', '/');
            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) return false;

            for (int i = 0; i < IgnoredPrefixes.Length; i++)
            {
                if (normalized.StartsWith(IgnoredPrefixes[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            string ext = Path.GetExtension(normalized).ToLowerInvariant();
            for (int i = 0; i < WatchedExtensions.Length; i++)
            {
                if (ext == WatchedExtensions[i]) return true;
            }
            return false;
        }

        public static List<VisualPendingImportRecord> GetPendingRecords()
        {
            var data = LoadQueue();
            return data.items ?? new List<VisualPendingImportRecord>();
        }

        public static int GetPendingCount()
        {
            var data = LoadQueue();
            return data.items != null ? data.items.Count : 0;
        }

        public static void RecordPendingPath(string assetPath, string changeType = "ImportedOrModified")
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (EnqueueInternal(assetPath, changeType))
            {
                OnPendingQueueChanged?.Invoke();
            }
        }

        public static void ClearQueue()
        {
            SaveQueue(new VisualPendingImportQueueData());
            OnPendingQueueChanged?.Invoke();
        }

        public static void RemovePaths(IEnumerable<string> processedPaths)
        {
            if (processedPaths == null) return;
            var set = new HashSet<string>(processedPaths, StringComparer.OrdinalIgnoreCase);
            var data = LoadQueue();
            int removed = data.items.RemoveAll(r => r != null && set.Contains(r.assetPath));
            if (removed > 0)
            {
                SaveQueue(data);
                OnPendingQueueChanged?.Invoke();
            }
        }

        private static bool EnqueueInternal(string assetPath, string changeType)
        {
            string normalized = assetPath.Replace('\\', '/');
            string guid = AssetDatabase.AssetPathToGUID(normalized);
            var data = LoadQueue();

            for (int i = 0; i < data.items.Count; i++)
            {
                if (string.Equals(data.items[i].assetPath, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    data.items[i].changeType = changeType;
                    data.items[i].assetGuid = guid;
                    data.items[i].detectedUtc = DateTime.UtcNow.ToString("O");
                    SaveQueue(data);
                    return true;
                }
            }

            data.items.Add(new VisualPendingImportRecord
            {
                assetPath = normalized,
                assetGuid = guid,
                changeType = changeType,
                detectedUtc = DateTime.UtcNow.ToString("O")
            });
            SaveQueue(data);
            return true;
        }

        private static VisualPendingImportQueueData LoadQueue()
        {
            try
            {
                if (File.Exists(QueueFilePath))
                {
                    string json = File.ReadAllText(QueueFilePath);
                    var parsed = JsonUtility.FromJson<VisualPendingImportQueueData>(json);
                    if (parsed != null && parsed.items != null) return parsed;
                }
            }
            catch
            {
                // Ignore transient corrupt queue and reset cleanly
            }
            return new VisualPendingImportQueueData();
        }

        private static void SaveQueue(VisualPendingImportQueueData data)
        {
            try
            {
                Directory.CreateDirectory("Library");
                File.WriteAllText(QueueFilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VisualStyleImportWatcher] Failed writing pending queue: {ex.Message}");
            }
        }
    }
}
#endif
