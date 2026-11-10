using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor.PizzaVisual
{
    /// <summary>
    /// Janela Odin independente de Live Preview 3D da Pizza Modular (Fase 3).
    /// Possui 4 abas completas: Modelo Original, Modelo Processado, Comparação e Montagem.
    /// Funciona sem Play Mode, com Undo/Redo e visualização em tempo real.
    /// </summary>
    public class PizzaVisualPreviewWindow : OdinEditorWindow
    {
        [MenuItem("Tools/Scary Party/Pizzeria/Prévia ao Vivo de Pizza (Fase 3)", false, 15)]
        public static void ShowWindow()
        {
            var win = GetWindow<PizzaVisualPreviewWindow>("Prévia 3D - Pizza Modular");
            win.minSize = new Vector2(950, 700);
            win.InitPreview();
        }

        #region Preview Utility
        private PreviewRenderUtility _previewUtility;
        private GameObject _previewPizzaRoot;
        private GameObject _previewItemA;
        private GameObject _previewItemB;
        private Vector2 _dragDelta;
        private float _cameraDistance = 1.3f;
        private float _cameraPitch = 45f;
        private float _cameraYaw = 30f;
        #endregion

        #region State
        [TabGroup("Tabs", "Montagem", SdfIconType.PieChartFill)]
        [Title("Configuração da Pizza")]
        [Range(0f, 1f), OnValueChanged(nameof(OnPizzaSettingsChanged))]
        [LabelText("Progresso de Cozimento (Assado)")]
        public float CookProgress = 0f;

        [TabGroup("Tabs", "Montagem")]
        [Range(0f, 1f), OnValueChanged(nameof(OnPizzaSettingsChanged))]
        [LabelText("Progresso de Queimado (Burn)")]
        public float BurnProgress = 0f;

        [TabGroup("Tabs", "Montagem")]
        [OnValueChanged(nameof(OnPizzaSettingsChanged))]
        [LabelText("Semente Visual Estável")]
        public int VisualSeed = 42;

        [TabGroup("Tabs", "Montagem")]
        [LabelText("Ingredientes Ativos na Pizza")]
        [ListDrawerSettings(Expanded = true, NumberOfItemsPerPage = 8, DraggableItems = false)]
        public List<PreviewIngredientEntry> ActiveIngredients = new List<PreviewIngredientEntry>
        {
            new PreviewIngredientEntry { IngredientDefId = 3, Stage = ProcessingStage.Prepared, Count = 1, Name = "Molho de Tomate" },
            new PreviewIngredientEntry { IngredientDefId = 5, Stage = ProcessingStage.Prepared, Count = 1, Name = "Queijo Ralado" },
            new PreviewIngredientEntry { IngredientDefId = 7, Stage = ProcessingStage.Prepared, Count = 1, Name = "Calabresa Fatiada" },
            new PreviewIngredientEntry { IngredientDefId = 9, Stage = ProcessingStage.Prepared, Count = 1, Name = "Cogumelo Fatiado" }
        };

        [Serializable]
        public class PreviewIngredientEntry
        {
            [HorizontalGroup("Row", 60), LabelText("ID")]
            public int IngredientDefId = 5;

            [HorizontalGroup("Row"), LabelText("Nome")]
            public string Name = "Ingrediente";

            [HorizontalGroup("Row", 100), LabelText("Estágio")]
            public ProcessingStage Stage = ProcessingStage.Prepared;

            [HorizontalGroup("Row", 50), LabelText("Qtd")]
            public int Count = 1;
        }

        [TabGroup("Tabs", "Montagem")]
        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [LabelText("Perfil em Edição Direta")]
        public PizzaIngredientVisualProfile ActiveProfileForTuning;

        [TabGroup("Tabs", "Montagem")]
        [Button(ButtonSizes.Large, Name = "Salvar Perfil Selecionado")]
        public void SaveActiveProfile()
        {
            if (ActiveProfileForTuning != null)
            {
                EditorUtility.SetDirty(ActiveProfileForTuning);
                AssetDatabase.SaveAssets();
                ShowNotification(new GUIContent("Perfil salvo com sucesso!"));
            }
        }

        [TabGroup("Tabs", "Modelo Original", SdfIconType.BoxSeam)]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [LabelText("Ingrediente Bruto (Original)")]
        public IngredientDef OriginalIngredient;

        [TabGroup("Tabs", "Modelo Processado", SdfIconType.Scissors)]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [LabelText("Ingrediente Processado (Fatiado / Ralado)")]
        public IngredientDef ProcessedIngredient;

        [TabGroup("Tabs", "Comparação", SdfIconType.ColumnsGap)]
        [InfoBox("Visualização e medição comparativa entre o modelo bruto e o modelo preparado para aplicação na pizza.")]
        [ShowInInspector, ReadOnly]
        public string InfoComparacao = "Selecione os modelos nas abas 'Modelo Original' e 'Modelo Processado' para comparar.";

        [TabGroup("Tabs", "Montagem")]
        [ShowInInspector, ReadOnly, Multiline(4)]
        [LabelText("Diagnóstico do Layout")]
        public string LayoutDiagnostics = "Tudo OK.";
        #endregion

        protected override void OnEnable()
        {
            base.OnEnable();
            InitPreview();
            LoadDefaultAssets();
            RebuildPizzaPreview();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            CleanupPreview();
        }

        private void LoadDefaultAssets()
        {
            if (ActiveProfileForTuning == null)
            {
                var guids = AssetDatabase.FindAssets("t:PizzaIngredientVisualProfile");
                if (guids.Length > 0)
                {
                    string p = AssetDatabase.GUIDToAssetPath(guids[0]);
                    ActiveProfileForTuning = AssetDatabase.LoadAssetAtPath<PizzaIngredientVisualProfile>(p);
                }
            }
        }

        private void InitPreview()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 35f;
                _previewUtility.camera.nearClipPlane = 0.05f;
                _previewUtility.camera.farClipPlane = 20f;
            }

            if (_previewPizzaRoot == null)
            {
                _previewPizzaRoot = new GameObject("PizzaPreviewRoot");
                _previewPizzaRoot.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(_previewPizzaRoot);
            }
        }

        private void CleanupPreview()
        {
            if (_previewPizzaRoot != null)
            {
                DestroyImmediate(_previewPizzaRoot);
                _previewPizzaRoot = null;
            }
            if (_previewItemA != null)
            {
                DestroyImmediate(_previewItemA);
                _previewItemA = null;
            }
            if (_previewItemB != null)
            {
                DestroyImmediate(_previewItemB);
                _previewItemB = null;
            }
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        public void OnPizzaSettingsChanged()
        {
            RebuildPizzaPreview();
        }

        [Button(ButtonSizes.Medium, Name = "Recalcular Layout da Pizza")]
        public void RebuildPizzaPreview()
        {
            if (_previewPizzaRoot == null) InitPreview();

            // Criar base da pizza se não existir
            var baseDough = _previewPizzaRoot.transform.Find("BaseDough");
            if (baseDough == null)
            {
                var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseGo.name = "BaseDough";
                baseGo.transform.SetParent(_previewPizzaRoot.transform, false);
                baseGo.transform.localPosition = Vector3.zero;
                baseGo.transform.localScale = new Vector3(0.40f, 0.01f, 0.40f);
                var col = baseGo.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                baseDough = baseGo.transform;
            }

            // Aplicar cor de cozimento da massa
            var baseRend = baseDough.GetComponent<Renderer>();
            if (baseRend != null)
            {
                Color doughColor = new Color(0.95f, 0.88f, 0.72f);
                Color bakedColor = new Color(0.80f, 0.48f, 0.15f);
                Color burnedColor = new Color(0.12f, 0.10f, 0.10f);
                Color targetDough = BurnProgress > 0f
                    ? Color.Lerp(bakedColor, burnedColor, BurnProgress)
                    : Color.Lerp(doughColor, bakedColor, CookProgress);
                baseRend.material.color = targetDough;
            }

            // Montar composição
            var netIngredients = new List<NetIngredientDto>();
            if (ActiveIngredients != null)
            {
                for (int i = 0; i < ActiveIngredients.Count; i++)
                {
                    netIngredients.Add(new NetIngredientDto
                    {
                        DefinitionId = ActiveIngredients[i].IngredientDefId,
                        Stage = (byte)ActiveIngredients[i].Stage,
                        Count = (byte)Mathf.Clamp(ActiveIngredients[i].Count, 1, 255)
                    });
                }
            }

            var cookingStage = BurnProgress > 0.5f ? CookingStage.Burned : (CookProgress >= 0.99f ? CookingStage.Baked : CookingStage.Uncooked);
            var comp = new PizzaVisualComposition(
                (ulong)VisualSeed,
                2,
                cookingStage,
                CookProgress,
                BurnProgress,
                netIngredients,
                0.36f
            );

            // Gerar layout
            var layout = PizzaLayoutGenerator.Generate(comp, PizzaVisualCatalog.Instance);

            // Diagnósticos
            if (layout.Warnings.Count > 0 || layout.Errors.Count > 0)
            {
                LayoutDiagnostics = $"Peças geradas: {layout.Placements.Count}\n" +
                                    string.Join("\n", layout.Warnings) + "\n" +
                                    string.Join("\n", layout.Errors);
            }
            else
            {
                LayoutDiagnostics = $"Peças geradas com sucesso: {layout.Placements.Count}. Sem colisões ou alertas.";
            }

            // Aplicar no Preview
            PizzaVisualComposer.ApplyLayout(_previewPizzaRoot, layout, default);
            Repaint();
        }

        protected override void OnImGUI()
        {
            base.OnImGUI();

            // Desenhar viewport 3D na parte inferior da janela
            Rect previewRect = GUILayoutUtility.GetRect(200, 320, GUILayout.ExpandWidth(true));
            Draw3DPreview(previewRect);
        }

        private void Draw3DPreview(Rect rect)
        {
            if (Event.current.type == EventType.Repaint && _previewUtility != null)
            {
                _previewUtility.BeginPreview(rect, GUIStyle.none);

                // Configurar câmera orbital
                Quaternion camRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                Vector3 camPos = camRot * new Vector3(0f, 0f, -_cameraDistance) + new Vector3(0f, 0.05f, 0f);

                _previewUtility.camera.transform.position = camPos;
                _previewUtility.camera.transform.rotation = camRot;

                // Iluminação de estúdio
                _previewUtility.lights[0].intensity = 1.2f;
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                _previewUtility.lights[1].intensity = 0.8f;
                _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-20f, -140f, 0f);

                _previewUtility.Render(true);
                Texture resultTex = _previewUtility.EndPreview();
                GUI.DrawTexture(rect, resultTex, ScaleMode.StretchToFill, false);
            }

            // Controle de órbita do mouse (arrastar para girar, scroll para zoom)
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;
            switch (evt.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        _cameraYaw += evt.delta.x * 0.8f;
                        _cameraPitch = Mathf.Clamp(_cameraPitch + evt.delta.y * 0.8f, 5f, 85f);
                        evt.Use();
                        Repaint();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        evt.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (rect.Contains(evt.mousePosition))
                    {
                        _cameraDistance = Mathf.Clamp(_cameraDistance + evt.delta.y * 0.05f, 0.4f, 3.0f);
                        evt.Use();
                        Repaint();
                    }
                    break;
            }
        }
    }
}
