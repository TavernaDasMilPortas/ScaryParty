using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using System.Linq;

namespace ProceduralGraphicSystem.Editor
{
    public class ShapePresetEditorWindow : OdinEditorWindow
    {
        [SerializeField, InlineEditor(Expanded = true)]
        private ShapePreset _targetPreset;

        public ShapePreset TargetPreset
        {
            get => _targetPreset;
            set => _targetPreset = value;
        }

        private float _zoom = 1f;
        private Vector2 _panOffset = Vector2.zero;
        
        private HashSet<int> _selectedPoints = new HashSet<int>();
        private bool _isBoxSelecting = false;
        private Vector2 _boxSelectStartPos;
        private Vector2 _boxSelectEndPos;
        private bool _isDraggingPoints = false;
        private Vector2 _sidebarScroll = Vector2.zero;
        
        [MenuItem("Celestial Cross/1. Editors/Shape Editor")]
        private static void OpenWindow()
        {
            var window = GetWindow<ShapePresetEditorWindow>();
            window.titleContent = new GUIContent("Shape Editor");
            window.wantsMouseMove = true;
            window.Show();
        }

        public static void OpenWindow(ShapePreset preset)
        {
            var window = GetWindow<ShapePresetEditorWindow>();
            window.titleContent = new GUIContent("Shape Editor");
            window.wantsMouseMove = true;
            window.TargetPreset = preset;
            window.Show();
        }

        protected override void OnImGUI()
        {
            // Toolbar
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            TargetPreset = (ShapePreset)EditorGUILayout.ObjectField(TargetPreset, typeof(ShapePreset), false, GUILayout.Width(200));
            if (GUILayout.Button("New", EditorStyles.toolbarButton))
            {
                CreateNewPreset();
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (TargetPreset == null)
            {
                GUILayout.Label("Selecione ou crie um Shape Preset para editar.", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Layout principal: Canvas (esquerda) + Sidebar (direita)
            GUILayout.BeginHorizontal();
            
            // --- Canvas ---
            Rect canvasRect = GUILayoutUtility.GetRect(200, 1000, 200, 1000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            DrawCanvas(canvasRect);

            // --- Sidebar com ScrollView ---
            GUILayout.BeginVertical(GUILayout.Width(350));
            _sidebarScroll = GUILayout.BeginScrollView(_sidebarScroll);
            
            // Desenha o inspector do preset (Odin InlineEditor)
            base.OnImGUI();
            
            // Separador visual
            EditorGUILayout.Space(15);
            EditorGUI.DrawRect(EditorGUILayout.GetControlRect(false, 2), new Color(0.4f, 0.4f, 0.4f));
            EditorGUILayout.Space(5);
            
            // Desenha a UI de keyframes manualmente (fora do Odin)
            DrawKeyframeUI();
            
            GUILayout.EndScrollView();
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            if (Event.current.type == EventType.ValidateCommand && Event.current.commandName == "UndoRedoPerformed")
            {
                Repaint();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  KEYFRAME UI — desenhada manualmente (sem atributos Odin)
        // ══════════════════════════════════════════════════════════
        
        private void DrawKeyframeUI()
        {
            if (TargetPreset == null) return;
            
            GUILayout.Label("🎬 Keyframes de Animação", EditorStyles.boldLabel);
            GUILayout.Space(5);
            
            // Botão de captura
            GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            if (GUILayout.Button("📸  Capturar Pose Atual como Keyframe", GUILayout.Height(32)))
            {
                Debug.Log("[ShapeEditor] Botão Capturar clicado.");
                CaptureKeyframe();
                GUIUtility.ExitGUI();
            }
            GUI.backgroundColor = Color.white;
            
            GUILayout.Space(5);
            
            // Lista de keyframes existentes
            if (TargetPreset.Keyframes == null || TargetPreset.Keyframes.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum keyframe salvo.\nEdite a forma no canvas e clique no botão acima.", MessageType.Info);
                return;
            }
            
            for (int i = 0; i < TargetPreset.Keyframes.Count; i++)
            {
                GUILayout.BeginVertical("box");
                var kf = TargetPreset.Keyframes[i];
                
                // Linha 1: Nome e Tempo
                GUILayout.BeginHorizontal();
                GUILayout.Label("Nome:", GUILayout.Width(45));
                kf.name = EditorGUILayout.TextField(kf.name);
                GUILayout.Label("Time:", GUILayout.Width(40));
                kf.time = EditorGUILayout.FloatField(kf.time, GUILayout.Width(50));
                GUILayout.EndHorizontal();

                // Linha 2: Ações
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Carregar no Canvas"))
                {
                    LoadKeyframe(i);
                    GUIUtility.ExitGUI();
                }
                if (GUILayout.Button("Sobrescrever"))
                {
                    OverwriteKeyframe(i);
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
                if (GUILayout.Button("X", GUILayout.Width(28)))
                {
                    Undo.RecordObject(TargetPreset, "Delete Keyframe");
                    TargetPreset.Keyframes.RemoveAt(i);
                    EditorUtility.SetDirty(TargetPreset);
                    GUI.backgroundColor = Color.white;
                    GUIUtility.ExitGUI();
                }
                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                
                TargetPreset.Keyframes[i] = kf;
                GUILayout.EndVertical();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  KEYFRAME ACTIONS
        // ══════════════════════════════════════════════════════════

        private void CaptureKeyframe()
        {
            Undo.RecordObject(TargetPreset, "Capture Keyframe");
            ShapePreset.ShapeKeyframe kf = new ShapePreset.ShapeKeyframe();
            kf.name = "Keyframe " + TargetPreset.Keyframes.Count;
            kf.time = TargetPreset.Keyframes.Count > 0 ? TargetPreset.Keyframes[TargetPreset.Keyframes.Count - 1].time + 0.5f : 0f;
            kf.positions = new List<Vector2>();
            for (int i = 0; i < TargetPreset.Points.Count; i++)
            {
                kf.positions.Add(TargetPreset.Points[i].position);
            }
            TargetPreset.Keyframes.Add(kf);
            EditorUtility.SetDirty(TargetPreset);
            TargetPreset.NotifyPresetChanged();
            Debug.Log($"[ShapeEditor] Keyframe '{kf.name}' capturado com {kf.positions.Count} pontos. Total: {TargetPreset.Keyframes.Count}");
        }

        private void LoadKeyframe(int index)
        {
            Undo.RecordObject(TargetPreset, "Load Keyframe");
            var kf = TargetPreset.Keyframes[index];
            for (int i = 0; i < kf.positions.Count && i < TargetPreset.Points.Count; i++)
            {
                var p = TargetPreset.Points[i];
                p.position = kf.positions[i];
                TargetPreset.Points[i] = p;
            }
            EditorUtility.SetDirty(TargetPreset);
            TargetPreset.NotifyPresetChanged();
            Debug.Log($"[ShapeEditor] Keyframe '{kf.name}' carregado no canvas.");
        }

        private void OverwriteKeyframe(int index)
        {
            Undo.RecordObject(TargetPreset, "Overwrite Keyframe");
            var kf = TargetPreset.Keyframes[index];
            kf.positions.Clear();
            for (int i = 0; i < TargetPreset.Points.Count; i++)
            {
                kf.positions.Add(TargetPreset.Points[i].position);
            }
            TargetPreset.Keyframes[index] = kf;
            EditorUtility.SetDirty(TargetPreset);
            Debug.Log($"[ShapeEditor] Keyframe '{kf.name}' sobrescrito com a pose atual.");
        }



        private void CreateNewPreset()
        {
            string path = EditorUtility.SaveFilePanelInProject("Salvar Shape Preset", "NewShapePreset", "asset", "Salvar novo shape preset");
            if (!string.IsNullOrEmpty(path))
            {
                ShapePreset newPreset = ScriptableObject.CreateInstance<ShapePreset>();
                
                newPreset.Points.Add(new ShapePreset.ShapePoint { position = new Vector2(0.2f, 0.2f), isSharp = false });
                newPreset.Points.Add(new ShapePreset.ShapePoint { position = new Vector2(0.2f, 0.8f), isSharp = false });
                newPreset.Points.Add(new ShapePreset.ShapePoint { position = new Vector2(0.8f, 0.8f), isSharp = false });
                newPreset.Points.Add(new ShapePreset.ShapePoint { position = new Vector2(0.8f, 0.2f), isSharp = false });

                AssetDatabase.CreateAsset(newPreset, path);
                AssetDatabase.SaveAssets();
                TargetPreset = newPreset;
                _selectedPoints.Clear();
                _panOffset = Vector2.zero;
                _zoom = 1f;
            }
        }

        private Vector2 ScreenToNormalized(Vector2 screenPos, Rect drawArea)
        {
            float nx = Mathf.InverseLerp(drawArea.xMin, drawArea.xMax, screenPos.x);
            float ny = Mathf.InverseLerp(drawArea.yMax, drawArea.yMin, screenPos.y); // Y is inverted visually
            return new Vector2(Mathf.Clamp01(nx), Mathf.Clamp01(ny));
        }

        private Vector2 NormalizedToScreen(Vector2 normPos, Rect drawArea)
        {
            return new Vector2(
                Mathf.Lerp(drawArea.xMin, drawArea.xMax, normPos.x),
                Mathf.Lerp(drawArea.yMax, drawArea.yMin, normPos.y)
            );
        }

        private Rect GetRectFromPoints(Vector2 p1, Vector2 p2)
        {
            return new Rect(
                Mathf.Min(p1.x, p2.x),
                Mathf.Min(p1.y, p2.y),
                Mathf.Abs(p1.x - p2.x),
                Mathf.Abs(p1.y - p2.y)
            );
        }

        private void DrawCanvas(Rect rect)
        {
            GUI.Box(rect, "", GUI.skin.box);

            if (TargetPreset == null || TargetPreset.Points == null) return;

            Event e = Event.current;
            int controlID = GUIUtility.GetControlID(FocusType.Passive);

            // 1. Zoom
            if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
            {
                _zoom -= e.delta.y * 0.05f;
                _zoom = Mathf.Clamp(_zoom, 0.1f, 5f);
                e.Use();
            }

            // 2. Pan
            if (e.type == EventType.MouseDrag && e.button == 2 && rect.Contains(e.mousePosition))
            {
                _panOffset += e.delta;
                e.Use();
            }

            // Handle keyboard delete
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace))
            {
                if (_selectedPoints.Count > 0)
                {
                    Undo.RecordObject(TargetPreset, "Delete Points");
                    List<int> toDelete = _selectedPoints.ToList();
                    toDelete.Sort((a, b) => b.CompareTo(a)); // Descending order
                    foreach (int idx in toDelete)
                    {
                        TargetPreset.Points.RemoveAt(idx);
                    }
                    _selectedPoints.Clear();
                    EditorUtility.SetDirty(TargetPreset);
                    TargetPreset.NotifyPresetChanged();
                    e.Use();
                }
            }

            Vector2 center = rect.center + _panOffset;
            float canvasSize = Mathf.Min(rect.width, rect.height) * 0.8f * _zoom;
            Rect drawArea = new Rect(center.x - canvasSize / 2, center.y - canvasSize / 2, canvasSize, canvasSize);

            EditorGUI.DrawRect(drawArea, new Color(0.2f, 0.2f, 0.2f));

            // Draw Spline
            IList<Vector2> evalPoints = _targetPreset.EvaluateSpline();
            if (evalPoints.Count >= 3)
            {
                Vector3[] screenPts = new Vector3[evalPoints.Count];
                for (int i = 0; i < evalPoints.Count; i++)
                {
                    screenPts[i] = NormalizedToScreen(evalPoints[i], drawArea);
                }
                
                Handles.color = new Color(0.5f, 0.8f, 1f, 0.8f);
                for (int i = 0; i < screenPts.Length; i++)
                {
                    if (TargetPreset.Loop || i < screenPts.Length - 1)
                    {
                        Handles.DrawLine(screenPts[i], screenPts[(i + 1) % screenPts.Length], 2f);
                    }
                }
            }

            // Determine Hovered Point
            int hoveredPoint = -1;
            for (int i = TargetPreset.Points.Count - 1; i >= 0; i--) // Iterate backwards so top-most visually is picked
            {
                Vector2 pos = NormalizedToScreen(TargetPreset.Points[i].position, drawArea);
                Rect handleRect = new Rect(pos.x - 7, pos.y - 7, 14, 14);
                if (handleRect.Contains(e.mousePosition))
                {
                    hoveredPoint = i;
                    break;
                }
            }

            // Mouse Down Logic — usar rect (não drawArea) para não roubar cliques da sidebar
            if (e.type == EventType.MouseDown && rect.Contains(e.mousePosition))
            {
                GUIUtility.hotControl = controlID;

                if (e.button == 0) // Left Click
                {
                    if (e.clickCount == 2)
                    {
                        // Double Click - Insert Point
                        Undo.RecordObject(TargetPreset, "Add Point");
                        Vector2 clickPos = ScreenToNormalized(e.mousePosition, drawArea);

                        if (TargetPreset.Points.Count < 2)
                        {
                            TargetPreset.Points.Add(new ShapePreset.ShapePoint { position = clickPos, isSharp = false });
                            _selectedPoints.Clear();
                            _selectedPoints.Add(TargetPreset.Points.Count - 1);
                        }
                        else
                        {
                            int bestInsertIndex = TargetPreset.Points.Count;
                            float minDistance = float.MaxValue;
                            int segments = TargetPreset.Loop ? TargetPreset.Points.Count : TargetPreset.Points.Count - 1;

                            for (int i = 0; i < segments; i++)
                            {
                                Vector2 a = TargetPreset.Points[i].position;
                                Vector2 b = TargetPreset.Points[(i + 1) % TargetPreset.Points.Count].position;

                                Vector2 ab = b - a;
                                Vector2 ap = clickPos - a;
                                float lenSqr = ab.sqrMagnitude;
                                float t = (lenSqr == 0) ? 0 : Mathf.Clamp01(Vector2.Dot(ap, ab) / lenSqr);
                                Vector2 proj = a + t * ab;
                                float dist = Vector2.Distance(clickPos, proj);

                                if (dist < minDistance)
                                {
                                    minDistance = dist;
                                    bestInsertIndex = i + 1;
                                }
                            }

                            if (bestInsertIndex >= TargetPreset.Points.Count)
                            {
                                TargetPreset.Points.Add(new ShapePreset.ShapePoint { position = clickPos, isSharp = false });
                                _selectedPoints.Clear();
                                _selectedPoints.Add(TargetPreset.Points.Count - 1);
                            }
                            else
                            {
                                TargetPreset.Points.Insert(bestInsertIndex, new ShapePreset.ShapePoint { position = clickPos, isSharp = false });
                                _selectedPoints.Clear();
                                _selectedPoints.Add(bestInsertIndex);
                            }
                        }

                        EditorUtility.SetDirty(TargetPreset);
                        TargetPreset.NotifyPresetChanged();
                        e.Use();
                    }
                    else // Single Click
                    {
                        if (hoveredPoint != -1)
                        {
                            if (e.shift)
                            {
                                if (_selectedPoints.Contains(hoveredPoint)) _selectedPoints.Remove(hoveredPoint);
                                else _selectedPoints.Add(hoveredPoint);
                            }
                            else
                            {
                                if (!_selectedPoints.Contains(hoveredPoint))
                                {
                                    _selectedPoints.Clear();
                                    _selectedPoints.Add(hoveredPoint);
                                }
                            }
                            _isDraggingPoints = true;
                            Undo.RecordObject(TargetPreset, "Move Points");
                        }
                        else
                        {
                            if (!e.shift) _selectedPoints.Clear();
                            _isBoxSelecting = true;
                            _boxSelectStartPos = e.mousePosition;
                            _boxSelectEndPos = e.mousePosition;
                        }
                        e.Use();
                    }
                }
                else if (e.button == 1) // Right Click
                {
                    if (hoveredPoint != -1)
                    {
                        if (!_selectedPoints.Contains(hoveredPoint))
                        {
                            _selectedPoints.Clear();
                            _selectedPoints.Add(hoveredPoint);
                        }

                        GenericMenu menu = new GenericMenu();
                        menu.AddItem(new GUIContent("Toggle Sharp"), false, () => {
                            Undo.RecordObject(TargetPreset, "Toggle Sharp");
                            foreach (int idx in _selectedPoints)
                            {
                                var p = TargetPreset.Points[idx];
                                p.isSharp = !p.isSharp;
                                TargetPreset.Points[idx] = p;
                            }
                            EditorUtility.SetDirty(TargetPreset);
                            TargetPreset.NotifyPresetChanged();
                        });
                        menu.AddItem(new GUIContent("Delete Points"), false, () => {
                            Undo.RecordObject(TargetPreset, "Delete Points");
                            List<int> toDelete = _selectedPoints.ToList();
                            toDelete.Sort((a, b) => b.CompareTo(a));
                            foreach (int idx in toDelete)
                            {
                                TargetPreset.Points.RemoveAt(idx);
                            }
                            _selectedPoints.Clear();
                            EditorUtility.SetDirty(TargetPreset);
                            TargetPreset.NotifyPresetChanged();
                        });
                        menu.ShowAsContext();
                        e.Use();
                    }
                }
            }

            // Mouse Drag Logic
            if (e.type == EventType.MouseDrag)
            {
                if (_isDraggingPoints && _selectedPoints.Count > 0)
                {
                    
                    Vector2 deltaScreen = e.delta;
                    Vector2 deltaNormalized = new Vector2(
                        deltaScreen.x / drawArea.width,
                        -deltaScreen.y / drawArea.height
                    );

                    for (int i = 0; i < TargetPreset.Points.Count; i++)
                    {
                        if (_selectedPoints.Contains(i))
                        {
                            var p = TargetPreset.Points[i];
                            Vector2 newPos = p.position + deltaNormalized;
                            p.position = new Vector2(Mathf.Clamp01(newPos.x), Mathf.Clamp01(newPos.y));
                            TargetPreset.Points[i] = p;
                        }
                    }
                    EditorUtility.SetDirty(TargetPreset);
                    TargetPreset.NotifyPresetChanged();
                    e.Use();
                }
                else if (_isBoxSelecting)
                {
                    _boxSelectEndPos = e.mousePosition;
                    e.Use();
                }
            }

            // Mouse Up Logic
            if (e.type == EventType.MouseUp)
            {
                if (GUIUtility.hotControl == controlID)
                {
                    GUIUtility.hotControl = 0;
                }
                
                if (_isDraggingPoints)
                {
                    _isDraggingPoints = false;
                }
                
                if (_isBoxSelecting)
                {
                    _isBoxSelecting = false;
                    Rect selectionRect = GetRectFromPoints(_boxSelectStartPos, e.mousePosition);
                    
                    for (int i = 0; i < TargetPreset.Points.Count; i++)
                    {
                        Vector2 pos = NormalizedToScreen(TargetPreset.Points[i].position, drawArea);
                        if (selectionRect.Contains(pos))
                        {
                            _selectedPoints.Add(i);
                        }
                    }
                    e.Use();
                }
            }

            // Drawing Points
            for (int i = 0; i < TargetPreset.Points.Count; i++)
            {
                var point = TargetPreset.Points[i];
                Vector2 pos = NormalizedToScreen(point.position, drawArea);
                Rect handleRect = new Rect(pos.x - 5, pos.y - 5, 10, 10);
                
                if (_selectedPoints.Contains(i))
                    EditorGUI.DrawRect(handleRect, Color.yellow);
                else
                    EditorGUI.DrawRect(handleRect, point.isSharp ? Color.red : Color.cyan);

                EditorGUIUtility.AddCursorRect(handleRect, MouseCursor.MoveArrow);
            }

            // Drawing Box Selection
            if (_isBoxSelecting)
            {
                Rect selectionRect = GetRectFromPoints(_boxSelectStartPos, _boxSelectEndPos);
                EditorGUI.DrawRect(selectionRect, new Color(0.2f, 0.5f, 1f, 0.3f));
            }

            if (e.type == EventType.MouseDrag || e.type == EventType.ScrollWheel || e.type == EventType.MouseMove || 
                e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.KeyDown)
            {
                Repaint();
            }
        }
    }
}
