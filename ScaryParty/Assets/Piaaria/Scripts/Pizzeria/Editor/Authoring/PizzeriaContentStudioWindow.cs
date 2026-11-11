using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Janela Independente do Estúdio Visual de Conteúdo da Pizzaria.
    /// Permite manter a bancada aberta em uma janela (ex: com tomate no 3º slot e faca posicionada)
    /// enquanto o desenvolvedor edita qualquer ingrediente, utensílio ou preset em outra janela.
    /// </summary>
    public class PizzeriaContentStudioWindow : EditorWindow
    {
        private PizzeriaSharedViewport3D _viewport;
        private bool _isPlayingSimulation = false;
        private double _lastSimTime;

        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Estúdio Visual 3D", false, 11)]
        public static void ShowWindow()
        {
            var win = GetWindow<PizzeriaContentStudioWindow>("Estúdio Pizzaria 3D");
            win.minSize = new Vector2(850, 600);
            win.Show();
        }

        private void OnEnable()
        {
            if (_viewport == null)
            {
                _viewport = new PizzeriaSharedViewport3D();
            }
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            if (_viewport != null)
            {
                _viewport.Cleanup();
                _viewport = null;
            }
        }

        private void OnUndoRedo()
        {
            _viewport?.RebuildPreviewScene();
            Repaint();
        }

        private void OnEditorUpdate()
        {
            if (_isPlayingSimulation)
            {
                double now = EditorApplication.timeSinceStartup;
                double delta = now - _lastSimTime;
                _lastSimTime = now;

                float newProg = PizzeriaContentStudioSession.PinnedState.processProgress + (float)(delta * 0.45);
                if (newProg > 1f) newProg = 0f;
                PizzeriaContentStudioSession.SetProcessProgress(newProg);
                _viewport?.RebuildPreviewScene();
                Repaint();
            }
        }

        private void OnGUI()
        {
            var session = PizzeriaContentStudioSession.PinnedState;

            // 1. Barra de Ferramentas Superior
            DrawTopToolbar();

            // 2. Layout Dividido: Controles à esquerda / Viewport 3D à direita
            EditorGUILayout.BeginHorizontal();

            // Painel Lateral Esquerdo (Controles e Ficha de Montagem)
            DrawSidebarControls(session);

            // Viewport 3D à Direita
            Rect viewportRect = EditorGUILayout.GetControlRect(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_viewport != null && viewportRect.width > 20 && viewportRect.height > 20)
            {
                _viewport.DrawViewportGUI(viewportRect);
            }

            EditorGUILayout.EndHorizontal();

            // 3. Barra Inferior de Simulação & Diagnóstico
            DrawBottomStatusBar(session);
        }

        private void DrawTopToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            // Botão Fixar Montagem (PIN)
            bool isPinned = PizzeriaContentStudioSession.IsPinned;
            GUI.backgroundColor = isPinned ? new Color(1.0f, 0.65f, 0.25f) : Color.white;
            string pinLabel = isPinned ? "📌 Fixado (Segurando Montagem)" : "👀 Seguir Seleção";
            if (GUILayout.Button(pinLabel, EditorStyles.toolbarButton, GUILayout.Width(180)))
            {
                PizzeriaContentStudioSession.SetPinned(!isPinned);
            }
            GUI.backgroundColor = Color.white;

            // Modo de Exibição
            PizzeriaContentStudioSession.DisplayMode = (ViewportDisplayMode)EditorGUILayout.EnumPopup(
                PizzeriaContentStudioSession.DisplayMode,
                EditorStyles.toolbarPopup,
                GUILayout.Width(140));

            GUILayout.Space(10);

            // Câmera
            if (GUILayout.Button("Enquadrar [F]", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                _viewport?.FrameSelection();
            }
            if (GUILayout.Button("Vista Superior", EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                _viewport?.SetTopView();
            }
            if (GUILayout.Button("Vista Frontal", EditorStyles.toolbarButton, GUILayout.Width(85)))
            {
                _viewport?.SetFrontView();
            }

            GUILayout.FlexibleSpace();

            // Toggles de Overlay
            if (_viewport != null)
            {
                _viewport.ShowGrid = GUILayout.Toggle(_viewport.ShowGrid, "Grade", EditorStyles.toolbarButton, GUILayout.Width(50));
                _viewport.ShowMetricScale = GUILayout.Toggle(_viewport.ShowMetricScale, "Métrica", EditorStyles.toolbarButton, GUILayout.Width(55));
                _viewport.ShowCuttingContact = GUILayout.Toggle(_viewport.ShowCuttingContact, "Corte", EditorStyles.toolbarButton, GUILayout.Width(50));
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSidebarControls(PinnedAssemblyState session)
        {
            EditorGUILayout.BeginVertical("box", GUILayout.Width(280), GUILayout.ExpandHeight(true));

            EditorGUILayout.LabelField("Configuração da Montagem", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Esta bancada e seus itens permanecem visíveis aqui mesmo que você troque de aba ou selecione outro asset.", MessageType.None);

            EditorGUILayout.Space(4);

            // 1. Bancada Selecionada
            EditorGUILayout.LabelField("1. Bancada / Estação Física", EditorStyles.miniBoldLabel);
            var prevStation = session.pinnedStation;
            session.pinnedStation = (StationView)EditorGUILayout.ObjectField(session.pinnedStation, typeof(StationView), true);
            if (session.pinnedStation != prevStation)
            {
                _viewport?.RebuildPreviewScene();
            }

            var prevDef = session.pinnedStationDef;
            session.pinnedStationDef = (StationDef)EditorGUILayout.ObjectField(session.pinnedStationDef, typeof(StationDef), false);
            if (session.pinnedStationDef != prevDef)
            {
                _viewport?.RebuildPreviewScene();
            }

            // Seleção de Slot Ativo
            int slotCount = 4;
            if (session.pinnedStation != null && session.pinnedStation.slotAnchors != null)
                slotCount = Mathf.Max(1, session.pinnedStation.slotAnchors.Length);
            else if (session.pinnedStationDef != null && session.pinnedStationDef.defaultSlotLayout != null)
                slotCount = Mathf.Max(1, session.pinnedStationDef.defaultSlotLayout.SlotCount);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("2. Escolha o Slot de Teste:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            for (int s = 0; s < Mathf.Min(6, slotCount); s++)
            {
                bool isCur = session.pinnedSlotIndex == s;
                GUI.backgroundColor = isCur ? new Color(0.35f, 0.85f, 0.45f) : Color.white;
                if (GUILayout.Button($"Slot {s}", GUILayout.Height(24)))
                {
                    session.pinnedSlotIndex = s;
                    _viewport?.RebuildPreviewScene();
                }
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6);

            // 3. Ingrediente Apoiado
            EditorGUILayout.LabelField("3. Ingrediente no Slot", EditorStyles.miniBoldLabel);
            var prevIng = session.pinnedIngredient;
            session.pinnedIngredient = (IngredientDef)EditorGUILayout.ObjectField(session.pinnedIngredient, typeof(IngredientDef), false);
            if (session.pinnedIngredient != prevIng)
            {
                _viewport?.RebuildPreviewScene();
            }

            // 4. Utensílio
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("4. Utensílio / Faca", EditorStyles.miniBoldLabel);
            var prevTool = session.pinnedTool;
            session.pinnedTool = (ToolDef)EditorGUILayout.ObjectField(session.pinnedTool, typeof(ToolDef), false);
            if (session.pinnedTool != prevTool)
            {
                _viewport?.RebuildPreviewScene();
            }

            // 5. Processo
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("5. Processo Culinário", EditorStyles.miniBoldLabel);
            session.pinnedProcess = (ProcessDef)EditorGUILayout.ObjectField(session.pinnedProcess, typeof(ProcessDef), false);

            EditorGUILayout.Space(8);

            // Ações Rápidas de Ajuste
            EditorGUILayout.LabelField("Ações Rápidas de Teste:", EditorStyles.miniBoldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apoiar Tomate"))
            {
                string[] guids = AssetDatabase.FindAssets("Ingredient_");
                foreach (var g in guids)
                {
                    var ing = AssetDatabase.LoadAssetAtPath<IngredientDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (ing != null && (ing.name.Contains("Tomate") || ing.name.Contains("Calabresa") || ing.name.Contains("Massa")))
                    {
                        session.pinnedIngredient = ing;
                        break;
                    }
                }
                _viewport?.RebuildPreviewScene();
            }
            if (GUILayout.Button("Apoiar Faca"))
            {
                string[] guids = AssetDatabase.FindAssets("Tool_");
                foreach (var g in guids)
                {
                    var t = AssetDatabase.LoadAssetAtPath<ToolDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (t != null && t.name.Contains("Faca"))
                    {
                        session.pinnedTool = t;
                        break;
                    }
                }
                _viewport?.RebuildPreviewScene();
            }
            EditorGUILayout.EndHorizontal();

            GUILayout.FlexibleSpace();

            // Botão de Aplicação ao Prefab
            if (session.pinnedStation != null)
            {
                GUI.backgroundColor = new Color(0.4f, 0.85f, 0.55f);
                if (GUILayout.Button("Aplicar Poses à Estação no Prefab", GUILayout.Height(30)))
                {
                    EditorUtility.SetDirty(session.pinnedStation);
                    if (session.pinnedStation.slotLayoutAsset != null)
                    {
                        session.pinnedStation.slotLayoutAsset.ApplyToStation(session.pinnedStation);
                    }
                }
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawBottomStatusBar(PinnedAssemblyState session)
        {
            EditorGUILayout.BeginHorizontal("box", GUILayout.Height(36));

            // Scrubber de Progresso de Processamento
            EditorGUILayout.LabelField("⏱️ Simulação:", GUILayout.Width(80));

            EditorGUI.BeginChangeCheck();
            float prog = EditorGUILayout.Slider(session.processProgress, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                PizzeriaContentStudioSession.SetProcessProgress(prog);
                _viewport?.RebuildPreviewScene();
            }

            // Botões Play / Pause / Reset
            string playBtnLabel = _isPlayingSimulation ? "⏸️ Pausar" : "▶️ Play";
            if (GUILayout.Button(playBtnLabel, GUILayout.Width(75), GUILayout.Height(22)))
            {
                _isPlayingSimulation = !_isPlayingSimulation;
                _lastSimTime = EditorApplication.timeSinceStartup;
            }
            if (GUILayout.Button("⏮️ Reset", GUILayout.Width(65), GUILayout.Height(22)))
            {
                _isPlayingSimulation = false;
                PizzeriaContentStudioSession.SetProcessProgress(0f);
                _viewport?.RebuildPreviewScene();
            }

            GUILayout.Space(15);

            // Relatório de Altura de Corte
            if (_viewport != null)
            {
                EditorGUILayout.LabelField(_viewport.CuttingContactReport, EditorStyles.boldLabel, GUILayout.Width(350));
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
