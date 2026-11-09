using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector.Editor;

namespace ProceduralGraphicSystem.Editor
{
    [CustomEditor(typeof(ProceduralGraphic))]
    public class ProceduralGraphicEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            ProceduralGraphic pg = (ProceduralGraphic)target;

            if (GUILayout.Button("Edit Preset", GUILayout.Height(30)))
            {
                if (pg.Preset != null)
                {
                    ShapePresetEditorWindow.OpenWindow(pg.Preset);
                }
                else
                {
                    Debug.LogWarning("Nenhum Shape Preset atribuído para editar.");
                }
            }
            
            EditorGUILayout.Space();
            base.OnInspectorGUI();
        }
    }
}
