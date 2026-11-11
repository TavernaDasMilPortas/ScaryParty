using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Viewport 3D isolado e compartilhado para o Estúdio de Conteúdo da Pizzaria.
    /// Suporta prévia de bancadas com tampo e slots numerados, ingredientes apoiados,
    /// utensílios com avaliação de altura de corte e simulação de processos fora do Play Mode.
    /// </summary>
    public class PizzeriaSharedViewport3D
    {
        private PreviewRenderUtility _previewUtility;
        private GameObject _previewRoot;
        private GameObject _stationInstance;
        private GameObject _ingredientInstance;
        private GameObject _toolInstance;
        private GameObject _pizzaRoot;

        // Câmera & Navegação
        private float _cameraDistance = 1.4f;
        private float _cameraPitch = 38f;
        private float _cameraYaw = 30f;
        private Vector3 _cameraTarget = new Vector3(0f, 0.45f, 0f);
        private bool _isPanning;
        private bool _isOrbiting;

        // Opções de Visualização
        public bool ShowGrid = true;
        public bool ShowMetricScale = true;
        public bool ShowSlotLabels = true;
        public bool ShowVolumes = false;
        public bool ShowInteractionFront = true;
        public bool ShowCuttingContact = true;

        // Informações de Diagnóstico de Corte
        public float EvaluatedCuttingHeightCm { get; private set; } = 0f;
        public string CuttingContactReport { get; private set; } = "Nenhum corte ativo.";
        public bool HasCuttingContact { get; private set; } = false;

        public PizzeriaSharedViewport3D()
        {
            InitPreview();
            PizzeriaContentStudioSession.OnSessionChanged += RebuildPreviewScene;
        }

        public void Cleanup()
        {
            PizzeriaContentStudioSession.OnSessionChanged -= RebuildPreviewScene;
            DestroyPreviewObjects();
            if (_previewUtility != null)
            {
                _previewUtility.Cleanup();
                _previewUtility = null;
            }
        }

        private void InitPreview()
        {
            if (_previewUtility == null)
            {
                _previewUtility = new PreviewRenderUtility();
                _previewUtility.cameraFieldOfView = 34f;
                _previewUtility.camera.nearClipPlane = 0.01f;
                _previewUtility.camera.farClipPlane = 50f;
                _previewUtility.camera.clearFlags = CameraClearFlags.Color;
                _previewUtility.camera.backgroundColor = new Color(0.14f, 0.14f, 0.16f, 1f);
                _previewUtility.camera.cullingMask = ~0;
            }

            if (_previewRoot == null)
            {
                _previewRoot = new GameObject("PizzeriaSharedPreviewRoot");
                _previewRoot.hideFlags = HideFlags.HideAndDontSave;
                _previewUtility.AddSingleGO(_previewRoot);
            }
        }

        private void DestroyPreviewObjects()
        {
            if (_stationInstance != null) { UnityEngine.Object.DestroyImmediate(_stationInstance); _stationInstance = null; }
            if (_ingredientInstance != null) { UnityEngine.Object.DestroyImmediate(_ingredientInstance); _ingredientInstance = null; }
            if (_toolInstance != null) { UnityEngine.Object.DestroyImmediate(_toolInstance); _toolInstance = null; }
            if (_pizzaRoot != null) { UnityEngine.Object.DestroyImmediate(_pizzaRoot); _pizzaRoot = null; }
            if (_previewRoot != null) { UnityEngine.Object.DestroyImmediate(_previewRoot); _previewRoot = null; }
        }

        public void FrameSelection()
        {
            _cameraTarget = new Vector3(0f, 0.45f, 0f);
            _cameraDistance = 1.35f;
            _cameraPitch = 38f;
            _cameraYaw = 30f;
        }

        public void SetTopView()
        {
            _cameraPitch = 89f;
            _cameraYaw = 0f;
            _cameraDistance = 1.6f;
        }

        public void SetFrontView()
        {
            _cameraPitch = 0f;
            _cameraYaw = 0f;
            _cameraDistance = 1.6f;
        }

        public void RebuildPreviewScene()
        {
            InitPreview();

            if (_stationInstance != null) UnityEngine.Object.DestroyImmediate(_stationInstance);
            if (_ingredientInstance != null) UnityEngine.Object.DestroyImmediate(_ingredientInstance);
            if (_toolInstance != null) UnityEngine.Object.DestroyImmediate(_toolInstance);
            if (_pizzaRoot != null) UnityEngine.Object.DestroyImmediate(_pizzaRoot);

            var session = PizzeriaContentStudioSession.PinnedState;
            var displayMode = PizzeriaContentStudioSession.DisplayMode;

            Vector3 activeSlotWorldPos = new Vector3(0f, 0.45f, 0f);

            // 1. Instanciar Bancada / Estação
            _stationInstance = new GameObject("StationPreviewInstance");
            _stationInstance.transform.SetParent(_previewRoot.transform, false);

            if (session.pinnedStation != null)
            {
                // Clonar visual da bancada real
                CloneVisualHierarchy(session.pinnedStation.gameObject, _stationInstance);
            }
            else if (session.pinnedStationDef != null && session.pinnedStationDef.visualPrefab != null)
            {
                var clone = UnityEngine.Object.Instantiate(session.pinnedStationDef.visualPrefab, _stationInstance.transform);
                clone.transform.localPosition = Vector3.zero;
            }
            else
            {
                // Bancada procedual estética (Tampo e pés estilizados)
                BuildProceduralCounterModel(_stationInstance.transform);
            }

            // Descobrir posições de slots da bancada
            List<Vector3> slotPositions = ResolveSlotPositions(session, _stationInstance);

            int targetSlotIndex = Mathf.Clamp(session.pinnedSlotIndex, 0, Mathf.Max(0, slotPositions.Count - 1));
            if (slotPositions.Count > 0)
            {
                activeSlotWorldPos = slotPositions[targetSlotIndex];
            }

            // 2. Instanciar Ingrediente apoiado no Slot
            float itemTopY = activeSlotWorldPos.y;
            if (session.pinnedIngredient != null)
            {
                _ingredientInstance = new GameObject($"Ingredient_{session.pinnedIngredient.name}");
                _ingredientInstance.transform.SetParent(_previewRoot.transform, false);

                // Resolver modelo: Se simulação estiver no fim (>= 0.95), usar modelo processado
                GameObject prefabToUse = session.pinnedIngredient.placeholderPrefab;
                if (session.processProgress >= 0.85f && session.pinnedIngredient.preparedIngredient != null)
                {
                    if (session.pinnedIngredient.preparedIngredient.placeholderPrefab != null)
                        prefabToUse = session.pinnedIngredient.preparedIngredient.placeholderPrefab;
                }

                if (prefabToUse != null)
                {
                    var ingVisual = UnityEngine.Object.Instantiate(prefabToUse, _ingredientInstance.transform);
                    ingVisual.transform.localPosition = Vector3.zero;
                }
                else
                {
                    // Procedural fallback (ex: Tomate ou Queijo)
                    BuildProceduralIngredientModel(_ingredientInstance.transform, session.pinnedIngredient, session.processProgress);
                }

                // Aplicar pose de bancada do catálogo de apresentação se existir
                var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/Piaaria/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
                var profile = catalog != null ? catalog.FindProfile(session.pinnedIngredient.category, session.pinnedIngredient.id, false, false) : null;
                Vector3 poseOffset = profile != null ? profile.stationSlotPose.position : Vector3.zero;
                Vector3 poseRot = profile != null ? profile.stationSlotPose.rotationEuler : Vector3.zero;
                Vector3 poseScale = (profile != null && profile.stationSlotPose.scale != Vector3.zero) ? profile.stationSlotPose.scale : Vector3.one;

                _ingredientInstance.transform.position = activeSlotWorldPos + poseOffset;
                _ingredientInstance.transform.eulerAngles = poseRot;
                _ingredientInstance.transform.localScale = poseScale;

                // Calcular topo do item para o corte da faca
                Bounds itemBounds = ComputeHierarchyBounds(_ingredientInstance.transform);
                itemTopY = itemBounds.max.y;
            }

            // 3. Instanciar Utensílio (ex: Faca de corte)
            if (session.pinnedTool != null)
            {
                _toolInstance = new GameObject($"Tool_{session.pinnedTool.name}");
                _toolInstance.transform.SetParent(_previewRoot.transform, false);

                if (session.pinnedTool.placeholderPrefab != null)
                {
                    var toolVisual = UnityEngine.Object.Instantiate(session.pinnedTool.placeholderPrefab, _toolInstance.transform);
                    toolVisual.transform.localPosition = Vector3.zero;
                }
                else
                {
                    BuildProceduralToolModel(_toolInstance.transform, session.pinnedTool);
                }

                // Altura de corte: Faca desce em direção ao topo do ingrediente
                // Em repouso (progresso 0): +14cm acima do ingrediente
                // No corte (progresso 0.5): gume toca exatamente a metade da altura do ingrediente
                // No fim (progresso 1.0): faca sobe de volta
                float cuttingStroke = Mathf.Sin(session.processProgress * Mathf.PI); // 0 -> 1 -> 0
                float knifeRestHeight = itemTopY + 0.12f;
                float knifeContactHeight = Mathf.Max(activeSlotWorldPos.y + 0.015f, itemTopY - 0.025f);
                float currentKnifeY = Mathf.Lerp(knifeRestHeight, knifeContactHeight, cuttingStroke);

                _toolInstance.transform.position = new Vector3(activeSlotWorldPos.x, currentKnifeY, activeSlotWorldPos.z);
                _toolInstance.transform.rotation = Quaternion.Euler(0f, 45f + cuttingStroke * 15f, cuttingStroke * 10f);

                EvaluatedCuttingHeightCm = (currentKnifeY - activeSlotWorldPos.y) * 100f;
                HasCuttingContact = cuttingStroke > 0.4f;
                CuttingContactReport = HasCuttingContact
                    ? $"⚔️ Contato no corte: {EvaluatedCuttingHeightCm:F1} cm do tampo (Profundidade: {(itemTopY - currentKnifeY) * 100f:F1} cm)"
                    : $"🔪 Faca em repouso / aproximação: {EvaluatedCuttingHeightCm:F1} cm do tampo";
            }
            else
            {
                EvaluatedCuttingHeightCm = 0f;
                HasCuttingContact = false;
                CuttingContactReport = "Nenhum utensílio posicionado.";
            }

            // Aplicar layer de preview recursivamente
            SetPreviewLayerRecursively(_previewRoot, _previewUtility.camera.gameObject.layer);
            _previewUtility.AddSingleGO(_previewRoot);
        }

        private List<Vector3> ResolveSlotPositions(PinnedAssemblyState session, GameObject stationInstance)
        {
            var positions = new List<Vector3>();

            if (session.pinnedStation != null && session.pinnedStation.slotAnchors != null)
            {
                foreach (var anchor in session.pinnedStation.slotAnchors)
                {
                    if (anchor != null) positions.Add(anchor.position);
                }
            }

            if (positions.Count == 0 && session.pinnedStationDef != null && session.pinnedStationDef.defaultSlotLayout != null)
            {
                var layout = session.pinnedStationDef.defaultSlotLayout;
                for (int i = 0; i < layout.SlotCount; i++)
                {
                    positions.Add(layout.slots[i].localPosition + new Vector3(0f, 0.40f, 0f));
                }
            }

            if (positions.Count == 0)
            {
                // Default 2 slots
                positions.Add(new Vector3(-0.25f, 0.42f, 0f));
                positions.Add(new Vector3(0.25f, 0.42f, 0f));
            }

            return positions;
        }

        public void DrawViewportGUI(Rect rect)
        {
            if (rect.width <= 10 || rect.height <= 10) return;

            InitPreview();

            if (Event.current.type == EventType.Repaint && _previewUtility != null)
            {
                _previewUtility.BeginPreview(rect, GUIStyle.none);

                Quaternion camRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                Vector3 camPos = _cameraTarget + camRot * new Vector3(0f, 0f, -_cameraDistance);

                _previewUtility.camera.transform.position = camPos;
                _previewUtility.camera.transform.rotation = camRot;
                _previewUtility.camera.clearFlags = CameraClearFlags.Color;
                _previewUtility.camera.backgroundColor = new Color(0.13f, 0.13f, 0.15f, 1f);

                // Iluminação de estúdio profissional com luz principal e preenchimento
                _previewUtility.lights[0].intensity = 1.35f;
                _previewUtility.lights[0].color = new Color(1.0f, 0.97f, 0.92f);
                _previewUtility.lights[0].transform.rotation = Quaternion.Euler(45f, 35f, 0f);

                _previewUtility.lights[1].intensity = 0.75f;
                _previewUtility.lights[1].color = new Color(0.85f, 0.92f, 1.0f);
                _previewUtility.lights[1].transform.rotation = Quaternion.Euler(-25f, -140f, 0f);

                _previewUtility.Render(true);
                Texture resultTex = _previewUtility.EndPreview();
                GUI.DrawTexture(rect, resultTex, ScaleMode.StretchToFill, false);

                // Desenhar Overlays 2D sobre o viewport
                DrawOverlays(rect);
            }

            HandleInputEvents(rect);
        }

        private void DrawOverlays(Rect rect)
        {
            var session = PizzeriaContentStudioSession.PinnedState;

            // Overlay Top-Left: Status de Fixação e Modo
            Rect badgeRect = new Rect(rect.x + 10, rect.y + 10, 220, 24);
            EditorGUI.DrawRect(badgeRect, new Color(0.1f, 0.1f, 0.12f, 0.85f));
            string pinStatus = session.isPinned ? "📌 MONTAGEM FIXADA" : "👀 SEGUINDO SELEÇÃO";
            GUI.Label(badgeRect, $"  {pinStatus} [{PizzeriaContentStudioSession.DisplayMode}]", EditorStyles.boldLabel);

            // Overlay Bottom-Left: Medidor de Altura de Corte
            if (ShowCuttingContact && session.pinnedTool != null)
            {
                Rect cutRect = new Rect(rect.x + 10, rect.yMax - 34, 340, 24);
                EditorGUI.DrawRect(cutRect, HasCuttingContact ? new Color(0.6f, 0.15f, 0.15f, 0.85f) : new Color(0.1f, 0.1f, 0.12f, 0.85f));
                GUI.Label(cutRect, $"  {CuttingContactReport}", EditorStyles.miniBoldLabel);
            }

            // Overlay Top-Right: Régua Métrica
            if (ShowMetricScale)
            {
                Rect scaleRect = new Rect(rect.xMax - 130, rect.y + 10, 120, 24);
                EditorGUI.DrawRect(scaleRect, new Color(0.1f, 0.1f, 0.12f, 0.75f));
                GUI.Label(scaleRect, "  📏 Escala: 1.0 m", EditorStyles.miniLabel);
            }
        }

        private void HandleInputEvents(Rect rect)
        {
            int controlID = GUIUtility.GetControlID(FocusType.Passive);
            Event evt = Event.current;

            switch (evt.GetTypeForControl(controlID))
            {
                case EventType.MouseDown:
                    if (rect.Contains(evt.mousePosition))
                    {
                        GUIUtility.hotControl = controlID;
                        _isPanning = (evt.button == 2) || (evt.button == 0 && evt.shift) || (evt.button == 1 && evt.shift);
                        _isOrbiting = !_isPanning && (evt.button == 0 || evt.button == 1);
                        evt.Use();
                    }
                    break;

                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == controlID)
                    {
                        if (_isPanning)
                        {
                            Quaternion camRot = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);
                            Vector3 camRight = camRot * Vector3.right;
                            Vector3 camUp = camRot * Vector3.up;
                            _cameraTarget -= (camRight * evt.delta.x - camUp * evt.delta.y) * (_cameraDistance * 0.0018f);
                        }
                        else if (_isOrbiting)
                        {
                            _cameraYaw += evt.delta.x * 0.65f;
                            _cameraPitch = Mathf.Clamp(_cameraPitch + evt.delta.y * 0.65f, -10f, 89f);
                        }
                        evt.Use();
                    }
                    break;

                case EventType.MouseUp:
                    if (GUIUtility.hotControl == controlID)
                    {
                        GUIUtility.hotControl = 0;
                        _isPanning = false;
                        _isOrbiting = false;
                        evt.Use();
                    }
                    break;

                case EventType.ScrollWheel:
                    if (rect.Contains(evt.mousePosition))
                    {
                        _cameraDistance = Mathf.Clamp(_cameraDistance + evt.delta.y * 0.05f, 0.2f, 6.0f);
                        evt.Use();
                    }
                    break;

                case EventType.KeyDown:
                    if (evt.keyCode == KeyCode.F)
                    {
                        FrameSelection();
                        evt.Use();
                    }
                    break;
            }
        }

        #region Model Helpers & Procedural Fallbacks
        private static void CloneVisualHierarchy(GameObject source, GameObject destination)
        {
            var renderers = source.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;

                var child = new GameObject(r.name);
                child.transform.SetParent(destination.transform, false);
                child.transform.position = r.transform.position;
                child.transform.rotation = r.transform.rotation;
                child.transform.localScale = r.transform.lossyScale;

                var newMf = child.AddComponent<MeshFilter>();
                newMf.sharedMesh = mf.sharedMesh;
                var newMr = child.AddComponent<MeshRenderer>();
                newMr.sharedMaterials = r.sharedMaterials;
            }
        }

        private static void BuildProceduralCounterModel(Transform parent)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Tampo de aço escovado / bancada de pizzaria
            var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.transform.SetParent(parent, false);
            top.transform.localPosition = new Vector3(0f, 0.40f, 0f);
            top.transform.localScale = new Vector3(1.20f, 0.05f, 0.70f);
            var mr = top.GetComponent<Renderer>();
            if (mr != null)
            {
                mr.material = new Material(shader) { color = new Color(0.72f, 0.74f, 0.76f) };
            }

            // Pés metálicos
            Vector3[] legOffsets = new[]
            {
                new Vector3(-0.55f, 0.18f, -0.30f),
                new Vector3(0.55f, 0.18f, -0.30f),
                new Vector3(-0.55f, 0.18f, 0.30f),
                new Vector3(0.55f, 0.18f, 0.30f)
            };
            foreach (var off in legOffsets)
            {
                var leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                leg.transform.SetParent(parent, false);
                leg.transform.localPosition = off;
                leg.transform.localScale = new Vector3(0.04f, 0.18f, 0.04f);
                var lmr = leg.GetComponent<Renderer>();
                if (lmr != null) lmr.material = new Material(shader) { color = new Color(0.35f, 0.35f, 0.38f) };
            }
        }

        private static void BuildProceduralIngredientModel(Transform parent, IngredientDef ing, float progress)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;

            Color col = ing.placeholderColor != Color.clear ? ing.placeholderColor : new Color(0.85f, 0.2f, 0.15f);
            if (progress >= 0.85f)
            {
                // Unidade fatiada achatada
                go.transform.localScale = new Vector3(0.12f, 0.02f, 0.12f);
            }
            else
            {
                go.transform.localScale = Vector3.one * 0.11f;
            }

            var r = go.GetComponent<Renderer>();
            if (r != null) r.material = new Material(shader) { color = col };
        }

        private static void BuildProceduralToolModel(Transform parent, ToolDef tool)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            // Lâmina
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.transform.SetParent(parent, false);
            blade.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            blade.transform.localScale = new Vector3(0.01f, 0.06f, 0.22f);
            var bmr = blade.GetComponent<Renderer>();
            if (bmr != null) bmr.material = new Material(shader) { color = new Color(0.85f, 0.88f, 0.92f) };

            // Cabo de madeira
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            handle.transform.SetParent(parent, false);
            handle.transform.localPosition = new Vector3(0f, 0.04f, -0.16f);
            handle.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            handle.transform.localScale = new Vector3(0.025f, 0.06f, 0.025f);
            var hmr = handle.GetComponent<Renderer>();
            if (hmr != null) hmr.material = new Material(shader) { color = new Color(0.45f, 0.25f, 0.12f) };
        }

        private static Bounds ComputeHierarchyBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.position, Vector3.one * 0.1f);

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b;
        }

        private static void SetPreviewLayerRecursively(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                SetPreviewLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }
        #endregion
    }
}
