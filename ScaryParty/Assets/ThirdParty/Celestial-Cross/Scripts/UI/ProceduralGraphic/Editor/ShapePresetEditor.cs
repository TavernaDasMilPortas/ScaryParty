using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

namespace ProceduralGraphicSystem.Editor
{
    [CustomEditor(typeof(ShapePreset))]
    public class ShapePresetEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open in Shape Editor", GUILayout.Height(30)))
            {
                ShapePresetEditorWindow.OpenWindow((ShapePreset)target);
            }
            
            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
