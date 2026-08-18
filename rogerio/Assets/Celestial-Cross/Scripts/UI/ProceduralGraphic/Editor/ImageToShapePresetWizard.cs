using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;

namespace ProceduralGraphicSystem.Editor
{
    public class ImageToShapePresetWizard : OdinEditorWindow
    {
        [Title("Source")]
        [Required]
        [OnValueChanged("ExtractPreview")]
        public Texture2D sourceTexture;

        [Title("Settings")]
        [Range(4, 128)]
        [OnValueChanged("ExtractPreview")]
        public int targetPointCount = 32;

        [Range(0.01f, 1f)]
        [OnValueChanged("ExtractPreview")]
        public float alphaThreshold = 0.5f;

        [Tooltip("Preenche vãos vazios. 0 = Desativado.")]
        [Range(0, 50)]
        [OnValueChanged("ExtractPreview")]
        public int closeGapsRadius = 0;

        [OnValueChanged("ExtractPreview")]
        public bool markPointsAsSharp = false;

        private List<Vector2> _previewPoints = new List<Vector2>();

        [MenuItem("Celestial Cross/1. Editors/Image to Shape Preset")]
        private static void OpenWindow()
        {
            GetWindow<ImageToShapePresetWizard>("Image to Shape").Show();
        }

        private void ExtractPreview()
        {
            _previewPoints.Clear();
            if (sourceTexture == null) return;

            if (!sourceTexture.isReadable)
            {
                Debug.LogWarning("[ImageToShape] A textura precisa ter 'Read/Write' ativado nas configurações de importação.");
                return;
            }

            var contour = ContourExtractor.ExtractContour(sourceTexture, alphaThreshold, closeGapsRadius);
            if (contour.Count == 0) return;

            var simplified = ContourExtractor.SimplifyContour(contour, targetPointCount);
            _previewPoints = ContourExtractor.NormalizeContour(simplified, sourceTexture.width, sourceTexture.height);
        }

        [Button(ButtonSizes.Large), GUIColor(0.5f, 1f, 0.5f)]
        [EnableIf("@sourceTexture != null && _previewPoints.Count > 0")]
        public void GenerateShapePreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Save Shape Preset", sourceTexture.name + "_Shape", "asset", "Save the generated Shape Preset");
            if (string.IsNullOrEmpty(path)) return;

            ShapePreset preset = ScriptableObject.CreateInstance<ShapePreset>();
            
            foreach (var p in _previewPoints)
            {
                preset.Points.Add(new ShapePreset.ShapePoint 
                { 
                    position = p, 
                    isSharp = markPointsAsSharp 
                });
            }

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();

            Debug.Log($"[ImageToShape] Shape Preset criado em {path} com {_previewPoints.Count} pontos.");
            
            // Open the new preset in the editor
            ShapePresetEditorWindow.OpenWindow(preset);
        }

        [Button]
        [ShowIf("@sourceTexture != null && !sourceTexture.isReadable")]
        public void FixTextureReadWrite()
        {
            string path = AssetDatabase.GetAssetPath(sourceTexture);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                ExtractPreview();
            }
        }

        protected override void OnImGUI()
        {
            base.OnImGUI();

            if (_previewPoints != null && _previewPoints.Count > 0)
            {
                GUILayout.Space(20);
                GUILayout.Label($"Preview ({_previewPoints.Count} pontos)", EditorStyles.boldLabel);
                
                Rect rect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                GUI.Box(rect, "", GUI.skin.box);

                if (Event.current.type == EventType.Repaint)
                {
                    float size = Mathf.Min(rect.width, rect.height) * 0.8f;
                    Vector2 center = rect.center;
                    Rect drawArea = new Rect(center.x - size / 2, center.y - size / 2, size, size);

                    EditorGUI.DrawRect(drawArea, new Color(0.2f, 0.2f, 0.2f));

                    Handles.color = Color.green;
                    for (int i = 0; i < _previewPoints.Count; i++)
                    {
                        Vector2 p1 = NormalizedToScreen(_previewPoints[i], drawArea);
                        Vector2 p2 = NormalizedToScreen(_previewPoints[(i + 1) % _previewPoints.Count], drawArea);
                        Handles.DrawLine(p1, p2, 2f);

                        Rect handleRect = new Rect(p1.x - 3, p1.y - 3, 6, 6);
                        EditorGUI.DrawRect(handleRect, markPointsAsSharp ? Color.red : Color.cyan);
                    }
                }
            }
        }

        private Vector2 NormalizedToScreen(Vector2 normPos, Rect drawArea)
        {
            return new Vector2(
                Mathf.Lerp(drawArea.xMin, drawArea.xMax, normPos.x),
                Mathf.Lerp(drawArea.yMax, drawArea.yMin, normPos.y) // Invert Y visually
            );
        }
    }
}
