using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector.Editor;

namespace ProceduralGraphicSystem.Editor
{
    [CustomEditor(typeof(PaperCutBorderGenerator))]
    public class PaperCutBorderEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            PaperCutBorderGenerator generator = (PaperCutBorderGenerator)target;

            Sprite sprite = generator.TargetSprite;
            
            if (sprite != null && sprite.texture != null)
            {
                Texture2D tex = sprite.texture;
                if (!tex.isReadable)
                {
                    EditorGUILayout.HelpBox("A textura do Sprite alvo não possui 'Read/Write' ativado. A borda não será gerada.", MessageType.Warning);
                    if (GUILayout.Button("Corrigir Texture Read/Write", GUILayout.Height(30)))
                    {
                        string path = AssetDatabase.GetAssetPath(tex);
                        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                        if (importer != null)
                        {
                            importer.isReadable = true;
                            importer.SaveAndReimport();
                            generator.GenerateBorder();
                        }
                    }
                    EditorGUILayout.Space();
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Atribua uma Target Image ou SpriteRenderer com um Sprite válido para que a borda possa ser extraída e desenhada.", MessageType.Info);
                EditorGUILayout.Space();
            }

            base.OnInspectorGUI();
        }
    }
}
