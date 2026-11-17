using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ScaryParty.Cutscenes.Editor
{
    public sealed class CutsceneBuilderWindow : EditorWindow
    {
        private enum Tab
        {
            Origem,
            Modelos,
            Materiais,
            CamerasECortes,
            CenaEVisual,
            Reproducao,
            Validacao
        }

        private Tab _activeTab = Tab.Origem;
        private CutsceneBindingProfile _profile;
        private CutsceneDefinition _definition;
        private TextAsset _manifestAsset;
        private CutsceneManifest _manifest;
        private string _lastError;
        private string _lastReport;
        private Vector2 _scroll;

        [MenuItem("Tools/Scary Party/Wizards/Cutscene Builder", false, 120)]
        public static void Open()
        {
            var win = GetWindow<CutsceneBuilderWindow>("Cutscene Builder");
            win.minSize = new Vector2(720f, 480f);
        }

        private void OnGUI()
        {
            DrawToolbar();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            switch (_activeTab)
            {
                case Tab.Origem: DrawOrigemTab(); break;
                case Tab.Modelos: DrawModelosTab(); break;
                case Tab.Materiais: DrawMateriaisTab(); break;
                case Tab.CamerasECortes: DrawCamerasTab(); break;
                case Tab.CenaEVisual: DrawCenaTab(); break;
                case Tab.Reproducao: DrawReproducaoTab(); break;
                case Tab.Validacao: DrawValidacaoTab(); break;
            }

            EditorGUILayout.EndScrollView();
            DrawFooterActions();
        }

        private void DrawToolbar()
        {
            string[] names = { "Origem", "Modelos", "Materiais", "Câmeras e cortes", "Cena e visual", "Reprodução", "Validação" };
            _activeTab = (Tab)GUILayout.Toolbar((int)_activeTab, names);
            EditorGUILayout.Space(6);
        }

        private void DrawOrigemTab()
        {
            _profile = (CutsceneBindingProfile)EditorGUILayout.ObjectField("Binding Profile", _profile, typeof(CutsceneBindingProfile), false);
            _definition = (CutsceneDefinition)EditorGUILayout.ObjectField("Cutscene Definition", _definition, typeof(CutsceneDefinition), false);
            _manifestAsset = (TextAsset)EditorGUILayout.ObjectField("Manifest JSON", _manifestAsset, typeof(TextAsset), false);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto-Detectar Assets"))
            {
                AutoDetectAssets();
            }
            if (GUILayout.Button("Carregar Manifesto") && _manifestAsset != null)
            {
                if (CutsceneManifestParser.TryParse(_manifestAsset.text, out _manifest, out _lastError))
                {
                    SyncProfileFromManifest();
                    _lastReport = $"Manifesto '{_manifest.sequence.id}' carregado (schema {_manifest.schemaVersion}, {_manifest.objects.Count} objetos, {_manifest.shots.Count} shots).";
                }
            }
            EditorGUILayout.EndHorizontal();

            if (_manifest != null)
            {
                EditorGUILayout.HelpBox(
                    $"ID: {_manifest.sequence.id}\nSchema: {_manifest.schemaVersion}\n" +
                    $"FPS Efetivo: {_manifest.sequence.effectiveFps:F3} | Frames: {_manifest.sequence.frameStart}..{_manifest.sequence.frameEnd} ({_manifest.sequence.duration:F2}s)\n" +
                    $"Objetos: {_manifest.objects.Count} | Câmeras: {_manifest.cameras.Count} | Shots: {_manifest.shots.Count}",
                    MessageType.Info);
            }
        }

        private void DrawModelosTab()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Selecione um CutsceneBindingProfile na aba Origem.", MessageType.Warning);
                return;
            }

            foreach (var entry in _profile.objectBindings)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{entry.sourceName} ({entry.sourceType}) [{entry.sourceId}]", EditorStyles.boldLabel);
                entry.targetPrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Atual", entry.targetPrefab, typeof(GameObject), false);
                entry.useExportedFallback = EditorGUILayout.Toggle("Usar Fallback Exportado", entry.useExportedFallback);
                EditorGUILayout.LabelField("Compatibilidade", $"{entry.compatibility}: {entry.compatibilityReason}");
                EditorGUILayout.EndVertical();
            }
        }

        private void DrawMateriaisTab()
        {
            if (_profile == null)
            {
                EditorGUILayout.HelpBox("Selecione um CutsceneBindingProfile na aba Origem.", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("Aplicar Materiais do Preset Atual"))
            {
                CutsceneMaterialCatalog.ApplyActivePresetToProfile(_profile, out _lastReport);
            }
            EditorGUILayout.Space(4);

            foreach (var mat in _profile.materialBindings)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField($"{mat.sourceObjectId} [Slot {mat.slotIndex}: {mat.sourceMaterialName}]", GUILayout.Width(280));
                mat.targetMaterial = (Material)EditorGUILayout.ObjectField(mat.targetMaterial, typeof(Material), false);
                mat.isConfirmed = EditorGUILayout.Toggle("Confirmado", mat.isConfirmed, GUILayout.Width(90));
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawCamerasTab()
        {
            if (_manifest == null)
            {
                EditorGUILayout.HelpBox("Carregue um Manifesto na aba Origem.", MessageType.Info);
                return;
            }

            foreach (var shot in _manifest.shots)
            {
                EditorGUILayout.LabelField($"Frame {shot.startFrame} ({shot.startTime:F3}s) → {shot.cameraName} [{shot.cameraId}]");
            }
        }

        private void DrawCenaTab()
        {
            if (_definition == null)
            {
                EditorGUILayout.HelpBox("Selecione uma CutsceneDefinition na aba Origem.", MessageType.Warning);
                return;
            }

            _definition.targetScenePath = EditorGUILayout.TextField("Cena de Destino (.unity)", _definition.targetScenePath);
            _definition.timelineAsset = (UnityEngine.Timeline.TimelineAsset)EditorGUILayout.ObjectField("Timeline Asset", _definition.timelineAsset, typeof(UnityEngine.Timeline.TimelineAsset), false);
        }

        private void DrawReproducaoTab()
        {
            if (_definition == null)
            {
                EditorGUILayout.HelpBox("Selecione uma CutsceneDefinition na aba Origem.", MessageType.Warning);
                return;
            }

            _definition.qualityPreset = (CutsceneQualityPreset)EditorGUILayout.EnumPopup("Quality Preset", _definition.qualityPreset);
            _definition.allowSkip = EditorGUILayout.Toggle("Permitir Skip", _definition.allowSkip);
            _definition.fadeInDuration = EditorGUILayout.FloatField("Fade In (s)", _definition.fadeInDuration);
            _definition.fadeOutDuration = EditorGUILayout.FloatField("Fade Out (s)", _definition.fadeOutDuration);
            _definition.disableGameplay3DRenderingWhenOpaque = EditorGUILayout.Toggle("Desativar 3D Gameplay quando Opaco", _definition.disableGameplay3DRenderingWhenOpaque);
        }

        private void DrawValidacaoTab()
        {
            if (!string.IsNullOrEmpty(_lastError))
                EditorGUILayout.HelpBox(_lastError, MessageType.Error);
            if (!string.IsNullOrEmpty(_lastReport))
                EditorGUILayout.HelpBox(_lastReport, MessageType.Info);
        }

        private void DrawFooterActions()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Validar"))
            {
                if (_profile != null)
                {
                    foreach (var obj in _profile.objectBindings)
                        CutsceneCompatibilityValidator.ValidateObjectBinding(obj, null);
                    _lastReport = "Validação de compatibilidade concluída.";
                }
            }

            if (GUILayout.Button("Montar / Atualizar"))
            {
                if (_profile != null && _manifest != null && (string.IsNullOrEmpty(_profile.cutsceneId) || _profile.objectBindings.Count == 0))
                    SyncProfileFromManifest();

                if (CutsceneSceneAssembler.BuildOrUpdateScene(_manifest, _profile, _definition, out var rep))
                {
                    _lastError = string.Empty;
                    _lastReport = rep;
                }
                else
                {
                    _lastError = rep;
                }
                GUIUtility.ExitGUI();
            }

            if (GUILayout.Button("Abrir Cena") && _definition != null && File.Exists(_definition.targetScenePath))
            {
                EditorSceneManager.OpenScene(_definition.targetScenePath, OpenSceneMode.Single);
                GUIUtility.ExitGUI();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void AutoDetectAssets()
        {
            if (_profile == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:CutsceneBindingProfile");
                if (guids.Length > 0)
                    _profile = AssetDatabase.LoadAssetAtPath<CutsceneBindingProfile>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_definition == null)
            {
                string[] guids = AssetDatabase.FindAssets("t:CutsceneDefinition");
                if (guids.Length > 0)
                    _definition = AssetDatabase.LoadAssetAtPath<CutsceneDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            if (_manifestAsset == null)
            {
                string[] guids = AssetDatabase.FindAssets("manifest t:TextAsset", new[] { "Assets/Piaaria/Cutscene" });
                if (guids.Length > 0)
                    _manifestAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (_manifestAsset != null && CutsceneManifestParser.TryParse(_manifestAsset.text, out _manifest, out _lastError))
            {
                SyncProfileFromManifest();
                _lastReport = $"Assets detectados e manifesto '{_manifest.sequence.id}' carregado.";
            }
        }

        private void SyncProfileFromManifest()
        {
            if (_profile == null || _manifest == null) return;

            string manifestPath = _manifestAsset != null ? AssetDatabase.GetAssetPath(_manifestAsset) : string.Empty;
            string manifestDir = !string.IsNullOrEmpty(manifestPath) ? Path.GetDirectoryName(manifestPath)?.Replace("\\", "/") : string.Empty;

            _profile.cutsceneId = _manifest.sequence.id;
            _profile.schemaVersion = _manifest.schemaVersion;
            _profile.manifestAssetPath = manifestPath;
            _profile.fbxAssetPath = !string.IsNullOrEmpty(manifestDir) && !string.IsNullOrEmpty(_manifest.fbxFile)
                ? $"{manifestDir}/{_manifest.fbxFile}"
                : _manifest.fbxFile;

            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(_manifestAsset != null ? _manifestAsset.text : _manifest.sequence.id);
                byte[] hash = sha.ComputeHash(bytes);
                _profile.manifestHash = System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 16);
            }

            // Preserve existing bindings by sourceId
            var existingObjs = new Dictionary<string, ObjectBindingEntry>();
            foreach (var b in _profile.objectBindings)
            {
                if (!string.IsNullOrEmpty(b.sourceId))
                    existingObjs[b.sourceId] = b;
            }

            var existingMats = new Dictionary<string, MaterialBindingEntry>();
            foreach (var m in _profile.materialBindings)
            {
                existingMats[$"{m.sourceObjectId}:{m.slotIndex}"] = m;
            }

            var newObjs = new List<ObjectBindingEntry>();
            var newMats = new List<MaterialBindingEntry>();

            foreach (var obj in _manifest.objects)
            {
                if (!existingObjs.TryGetValue(obj.id, out var entry))
                {
                    entry = new ObjectBindingEntry
                    {
                        sourceId = obj.id,
                        sourceName = obj.name,
                        sourceType = obj.type,
                        useExportedFallback = true
                    };
                }
                else
                {
                    entry.sourceName = obj.name;
                    entry.sourceType = obj.type;
                }
                newObjs.Add(entry);

                if (obj.materialSlots != null)
                {
                    for (int s = 0; s < obj.materialSlots.Count; s++)
                    {
                        string matName = obj.materialSlots[s];
                        if (string.IsNullOrEmpty(matName)) continue;

                        string key = $"{obj.id}:{s}";
                        if (!existingMats.TryGetValue(key, out var matEntry))
                        {
                            matEntry = new MaterialBindingEntry
                            {
                                sourceObjectId = obj.id,
                                rendererPath = obj.fbxNodePath,
                                slotIndex = s,
                                sourceMaterialName = matName
                            };
                        }
                        else
                        {
                            matEntry.rendererPath = obj.fbxNodePath;
                            matEntry.sourceMaterialName = matName;
                        }
                        newMats.Add(matEntry);
                    }
                }
            }

            _profile.objectBindings = newObjs;
            _profile.materialBindings = newMats;

            EditorUtility.SetDirty(_profile);
            AssetDatabase.SaveAssets();
        }
    }
}
