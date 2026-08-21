using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(CityGenerator))]
public class CityGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        CityGenerator gen = (CityGenerator)target;

        // Draw default inspector first (shows the config field and materials)
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        
        if (gen.config != null)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Map Size Override (Test Settings)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use these sliders to quickly change the map size for testing.", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            int newWidth = EditorGUILayout.IntSlider("Grid Width", gen.config.gridWidth, 1, 20);
            int newHeight = EditorGUILayout.IntSlider("Grid Height", gen.config.gridHeight, 1, 20);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(gen.config, "Change City Grid Size");
                gen.config.gridWidth = newWidth;
                gen.config.gridHeight = newHeight;
                EditorUtility.SetDirty(gen.config);
            }

            EditorGUILayout.Space(5);
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.3f);
            if (GUILayout.Button("🏗️ Quick Rebuild City", GUILayout.Height(35)))
            {
                gen.ClearCity();
                gen.GenerateCity();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log($"[CityGenerator] Rebuilt city with size {newWidth}x{newHeight}");
            }
            GUI.backgroundColor = Color.white;
            
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("🗑️ Clear City", GUILayout.Height(25)))
            {
                gen.ClearCity();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign a CityConfig to see generator tools.", MessageType.Warning);
        }
    }
}
