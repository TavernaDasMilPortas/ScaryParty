using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace ScaryParty.Cutscenes.Editor
{
    public static class CutsceneSceneAssembler
    {
        public static bool IsProtectedGameplayScene(string scenePath)
        {
            if (string.IsNullOrEmpty(scenePath)) return true;
            string fileName = Path.GetFileName(scenePath);
            return string.Equals(fileName, "GameScene.unity", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "GameScene_CelShaded.unity", System.StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, "LobbyScene.unity", System.StringComparison.OrdinalIgnoreCase);
        }

        public static bool BuildOrUpdateScene(
            CutsceneManifest manifest,
            CutsceneBindingProfile profile,
            CutsceneDefinition definition,
            out string report)
        {
            report = string.Empty;
            if (manifest == null || profile == null || definition == null)
            {
                report = "Manifest, Profile, or Definition is null.";
                return false;
            }

            if (IsProtectedGameplayScene(definition.targetScenePath))
            {
                report = $"Refusing destructive generation on protected gameplay scene: {definition.targetScenePath}";
                return false;
            }

            string normalizedPath = definition.targetScenePath.Replace("\\", "/").Trim();
            if (!normalizedPath.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = $"Assets/Piaaria/Scenes/Cutscenes/{normalizedPath}";
            }
            if (!normalizedPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath += ".unity";
            }
            definition.targetScenePath = normalizedPath;

            string sceneDir = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrEmpty(sceneDir) && !Directory.Exists(sceneDir))
            {
                Directory.CreateDirectory(sceneDir);
                AssetDatabase.Refresh();
            }

            Scene scene = File.Exists(definition.targetScenePath)
                ? EditorSceneManager.OpenScene(definition.targetScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CutsceneStage stage = Object.FindFirstObjectByType<CutsceneStage>();
            if (stage == null)
            {
                var stageGo = new GameObject("CutsceneStage");
                stage = stageGo.AddComponent<CutsceneStage>();
            }
            stage.EnsureRoots();

            // Clear only Generated children
            for (int i = stage.generatedRoot.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(stage.generatedRoot.GetChild(i).gameObject);
            }
            stage.spawnedActors.Clear();

            if (stage.director == null)
            {
                stage.director = stage.gameObject.GetComponent<PlayableDirector>();
                if (stage.director == null)
                {
                    stage.director = stage.gameObject.AddComponent<PlayableDirector>();
                }
            }

            if (stage.outputCamera == null)
            {
                var camGo = new GameObject("CutsceneOutputCamera");
                camGo.transform.SetParent(stage.generatedRoot, false);
                stage.outputCamera = camGo.AddComponent<Camera>();
            }

            // Ensure TimelineAsset exists and preserve its GUID
            TimelineAsset timeline = definition.timelineAsset;
            if (timeline == null)
            {
                string timelinePath = Path.ChangeExtension(definition.targetScenePath, ".playable");
                timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
                if (timeline == null)
                {
                    timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                    AssetDatabase.CreateAsset(timeline, timelinePath);
                }
                definition.timelineAsset = timeline;
            }

            // Rebuild CutsceneCameraTrack & generated AnimationTracks
            var existingTracks = new List<TrackAsset>(timeline.GetOutputTracks());
            foreach (var tr in existingTracks)
            {
                if (tr is CutsceneCameraTrack || (tr is AnimationTrack && tr.name.StartsWith("FBX_")))
                    timeline.DeleteTrack(tr);
            }

            // Ensure at least one Directional Light in scene
            if (Object.FindFirstObjectByType<Light>() == null)
            {
                var lightGo = new GameObject("CutsceneDirectionalLight");
                lightGo.transform.SetParent(stage.generatedRoot, false);
                lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
            }

            // Instantiate FBX & bind animations
            string fbxWarning = string.Empty;
            GameObject fbxAsset = !string.IsNullOrEmpty(profile.fbxAssetPath)
                ? AssetDatabase.LoadAssetAtPath<GameObject>(profile.fbxAssetPath)
                : null;

            if (fbxAsset != null)
            {
                var fbxInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset, stage.generatedRoot);
                fbxInstance.name = $"{fbxAsset.name}_StageRoot";
                fbxInstance.transform.localPosition = stage.stageSpatialOffset;
                stage.spawnedActors.Add(fbxInstance);

                // Apply material overrides from profile
                ApplyMaterialBindings(fbxInstance, profile);

                // Bind FBX AnimationClips to AnimationTrack
                var animator = fbxInstance.GetComponent<Animator>();
                if (animator == null)
                    animator = fbxInstance.AddComponent<Animator>();

                var clips = new List<AnimationClip>();
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(profile.fbxAssetPath))
                {
                    if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        clips.Add(clip);
                }

                if (clips.Count > 0)
                {
                    var animTrack = timeline.CreateTrack<AnimationTrack>(null, $"FBX_{fbxAsset.name}");
                    var animClip = animTrack.CreateClip(clips[0]);
                    animClip.start = 0.0;
                    stage.director.SetGenericBinding(animTrack, animator);
                }
            }
            else if (!string.IsNullOrEmpty(profile.fbxAssetPath))
            {
                fbxWarning = $" [AVISO: FBX não encontrado em '{profile.fbxAssetPath}' — objetos 3D não instanciados]";
            }

            // Instantiate bound prefabs (non-fallback)
            foreach (var objBinding in profile.objectBindings)
            {
                if (!objBinding.useExportedFallback && objBinding.targetPrefab != null)
                {
                    var prefabInst = (GameObject)PrefabUtility.InstantiatePrefab(objBinding.targetPrefab, stage.generatedRoot);
                    prefabInst.name = $"{objBinding.sourceName}_Bound";
                    prefabInst.transform.localPosition = stage.stageSpatialOffset + objBinding.pivotOffset;
                    prefabInst.transform.localScale = objBinding.scaleOverride;
                    stage.spawnedActors.Add(prefabInst);
                }
            }

            var camTrack = timeline.CreateTrack<CutsceneCameraTrack>(null, "Cutscene Camera");
            var camById = new Dictionary<string, ManifestCamera>();
            foreach (var c in manifest.cameras)
                camById[c.id] = c;

            float effFps = manifest.sequence.ComputeEffectiveFps();
            float totalDuration = manifest.sequence.ComputeDurationSeconds();

            for (int i = 0; i < manifest.shots.Count; i++)
            {
                var shot = manifest.shots[i];
                double start = shot.startTime;
                double end = (i + 1 < manifest.shots.Count)
                    ? manifest.shots[i + 1].startTime
                    : totalDuration;

                var clip = camTrack.CreateClip<CutsceneCameraClip>();
                clip.start = start;
                clip.duration = System.Math.Max(0.01, end - start);
                clip.displayName = string.IsNullOrEmpty(shot.cameraName) ? shot.cameraId : shot.cameraName;

                var camClip = clip.asset as CutsceneCameraClip;
                if (camClip != null && camById.TryGetValue(shot.cameraId, out var camData))
                {
                    camClip.template.cameraId = camData.id;
                    camClip.template.projection = camData.projection;
                    camClip.template.stageOriginOffset = stage.stageSpatialOffset;
                    camClip.template.samples = new List<CameraSample>(camData.samples);
                }
            }

            stage.director.playableAsset = timeline;
            stage.director.SetGenericBinding(camTrack, stage.outputCamera);

            definition.durationSeconds = totalDuration;
            definition.referenceWidth = manifest.sequence.resolutionX;
            definition.referenceHeight = manifest.sequence.resolutionY;
            definition.targetSceneName = Path.GetFileNameWithoutExtension(definition.targetScenePath);

            EditorUtility.SetDirty(timeline);
            EditorUtility.SetDirty(definition);
            EditorUtility.SetDirty(stage);
            EditorSceneManager.SaveScene(scene, definition.targetScenePath);
            EnsureSceneInBuildSettings(definition.targetScenePath);
            EnsureDefinitionInResources(definition);
            AssetDatabase.SaveAssets();

            report = $"Built/Updated '{definition.cutsceneId}' ({manifest.shots.Count} shots, {totalDuration:F2}s, {stage.spawnedActors.Count} actors) at {definition.targetScenePath}.{fbxWarning}";
            return true;
        }

        private static void EnsureSceneInBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var s in scenes)
            {
                if (string.Equals(s.path, scenePath, System.StringComparison.OrdinalIgnoreCase))
                {
                    if (!s.enabled)
                    {
                        s.enabled = true;
                        EditorBuildSettings.scenes = scenes.ToArray();
                    }
                    return;
                }
            }
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void EnsureDefinitionInResources(CutsceneDefinition definition)
        {
            string currentPath = AssetDatabase.GetAssetPath(definition);
            if (!string.IsNullOrEmpty(currentPath) && currentPath.Contains("/Resources/"))
                return;

            string resDir = "Assets/Piaaria/Resources/Cutscenes";
            if (!Directory.Exists(resDir))
            {
                Directory.CreateDirectory(resDir);
                AssetDatabase.Refresh();
            }

            string resPath = $"{resDir}/{definition.cutsceneId}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<CutsceneDefinition>(resPath);
            if (existing == null)
            {
                existing = ScriptableObject.CreateInstance<CutsceneDefinition>();
                AssetDatabase.CreateAsset(existing, resPath);
            }
            EditorUtility.CopySerialized(definition, existing);
            EditorUtility.SetDirty(existing);
        }

        private static void ApplyMaterialBindings(GameObject root, CutsceneBindingProfile profile)
        {
            if (root == null || profile == null || profile.materialBindings == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                foreach (var mb in profile.materialBindings)
                {
                    if (mb.targetMaterial == null) continue;
                    if (!string.Equals(r.name, mb.rendererPath, System.StringComparison.OrdinalIgnoreCase)) continue;
                    if (mb.slotIndex >= 0 && mb.slotIndex < mats.Length)
                    {
                        mats[mb.slotIndex] = mb.targetMaterial;
                        changed = true;
                    }
                }
                if (changed)
                {
                    r.sharedMaterials = mats;
                }
            }
        }
    }
}
