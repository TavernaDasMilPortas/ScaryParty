using System.IO;
using UnityEngine;
using UnityEditor;

namespace ScaryParty.ArmatureMapper
{
    public static class JsonExporter
    {
        public static void Export(ArmatureMap map, string outputFolder)
        {
            if (map == null || map.prefabInfo == null)
            {
                Debug.LogError("ArmatureMapper: Map is null, cannot export.");
                return;
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string safeName = map.prefabInfo.name.Replace(" ", "_");
            string fileName = $"{safeName}_armature_map.json";
            string fullPath = Path.Combine(outputFolder, fileName);

            // Serialize to JSON
            // JsonUtility handles lists and basic types well, which is why we modeled it this way.
            string jsonString = JsonUtility.ToJson(map, true);

            File.WriteAllText(fullPath, jsonString);

            Debug.Log($"ArmatureMapper: Successfully exported JSON map to {fullPath}");

            // Refresh asset database so it shows up in Unity
            AssetDatabase.Refresh();

            // Ping the object in the editor
            Object asset = AssetDatabase.LoadAssetAtPath<Object>($"Assets/ArmatureMapper/Output/{fileName}");
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }
    }
}
