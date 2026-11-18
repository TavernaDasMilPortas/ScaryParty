using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Editor.Migration;
using ScaryParty.Pizzeria.Integration;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Presenters;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ScaryParty.Editor.Playtest
{
    /// <summary>
    /// Instalador e validador idempotente do ambiente de playtest (P0-P6).
    /// Configura cenas canônicas, manifesto persistente e valida referências sem alterar arquivos pré-existentes alheios.
    /// </summary>
    public class PlaytestInstallerAndValidator : EditorWindow
    {
        public const string LobbyScenePath = "Assets/Piaaria/Scenes/LobbyScene.unity";
        public const string ReadyScenePath = "Assets/Piaaria/Scenes/ReadyScene.unity";
        public const string CanonicalGameScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        public const string CelShadedGameScenePath = "Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity";

        public const string PizzeriaConfigPath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaConfig_PizzeriaModular_Manual.asset";
        public const string PizzeriaPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string PlayerPrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        public const string PanelSettingsPath = "Assets/Piaaria/UI/PanelSettings.asset";
        public const string LobbyTreePath = "Assets/Piaaria/UI/Piaria/UXML/LobbySceneContainer.uxml";
        public const string ReadyTreePath = "Assets/Piaaria/UI/Piaria/UXML/ReadyRoomScreen.uxml";
        public const string GameHudTreePath = "Assets/Piaaria/UI/Piaria/UXML/MainGameHUD.uxml";
        public const string ManifestAssetPath = "Assets/Piaaria/Resources/Playtest/PlaytestReferenceManifest.asset";
        public const string BuildInfoTextPath = "Assets/Piaaria/Resources/Playtest/BuildVersionInfo.txt";
        public const string AnimatorPath = "Assets/Piaaria/Animations/Characters/CharacterAnimator.controller";
        public const string NetworkPrefabsAssetPath = "Assets/Piaaria/DefaultNetworkPrefabs.asset";

        private Vector2 _scroll;
        private string _lastReport = string.Empty;

        [MenuItem("Tools/Scary Party/Wizards/UI/Playtest Installer & Validador", false, 95)]
        public static void ShowWindow()
        {
            var win = GetWindow<PlaytestInstallerAndValidator>("Playtest Installer");
            win.minSize = new Vector2(640f, 520f);
            win.Show();
        }

        [MenuItem("Tools/Scary Party/Uso Comum/UI/Preparar e Validar Playtest (P0-P6)", false, 188)]
        public static void RunPrepareAndValidateMenu()
        {
            string report = PrepareAndValidateAll(saveBuildInfoTimestamp: true);
            Debug.Log(report);
        }

        [MenuItem("Tools/Scary Party/Uso Comum/UI/Validar Cenas e Prefabs de Playtest", false, 189)]
        public static void RunValidateOnlyMenu()
        {
            string report = ValidateAll(out int errors, out int warnings);
            Debug.Log($"[PlaytestValidator] Errors={errors}, Warnings={warnings}\n{report}");
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("ScaryParty — Instalador e Validador de Playtest (P0-P6)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Execução idempotente: garante UI, manifesto persistente, cenas canônicas e valida Missing Scripts, referências, IDs, layers e Animator.", MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Preparar e Validar Tudo", GUILayout.Height(34)))
            {
                _lastReport = PrepareAndValidateAll(saveBuildInfoTimestamp: true);
            }
            if (GUILayout.Button("Somente Validar", GUILayout.Height(34)))
            {
                _lastReport = ValidateAll(out _, out _);
            }
            EditorGUILayout.EndHorizontal();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.TextArea(_lastReport, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
        }

        public static string PrepareAndValidateAll(bool saveBuildInfoTimestamp = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Exit Play Mode before running PlaytestInstallerAndValidator.");
            }

            var sb = new StringBuilder();
            int totalChanges = 0;

            EnsureDirectories();
            totalChanges += EnsureBuildVersionInfo(saveBuildInfoTimestamp, sb);
            totalChanges += EnsureManifest(sb);
            totalChanges += EnsureAllScenes(sb);

            if (totalChanges > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string validationReport = ValidateAll(out int errors, out int warnings);
            sb.AppendLine($"=== RESUMO INSTALAÇÃO === TotalChanges={totalChanges}, ValidationErrors={errors}, ValidationWarnings={warnings}");
            sb.Append(validationReport);
            return sb.ToString();
        }

        public static int PrepareAllOnly(out string summary)
        {
            var sb = new StringBuilder();
            int changes = 0;
            EnsureDirectories();
            changes += EnsureBuildVersionInfo(updateTimestamp: false, sb);
            changes += EnsureManifest(sb);
            changes += EnsureAllScenes(sb);
            if (changes > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            summary = sb.ToString();
            return changes;
        }

        private static void EnsureDirectories()
        {
            EnsureFolder("Assets/Piaaria", "Playtest");
            EnsureFolder("Assets/Piaaria/Playtest", "Presets");
            EnsureFolder("Assets/Piaaria/Playtest", "Prefabs");
            EnsureFolder("Assets/Piaaria/Playtest", "Materials");
            EnsureFolder("Assets/Piaaria/Resources", "Playtest");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string full = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(full))
            {
                AssetDatabase.CreateFolder(parent, child);
            }
        }

        private static int EnsureBuildVersionInfo(bool updateTimestamp, StringBuilder sb)
        {
            string branch = TryRunGit("rev-parse --abbrev-ref HEAD", "release/marcos-noturnos");
            string sha = TryRunGit("rev-parse --short HEAD", "bdffe509");
            string fullSha = TryRunGit("rev-parse HEAD", "bdffe5096163c0bac817a94a1c05e4d22fdf4652");
            string existing = File.Exists(BuildInfoTextPath) ? File.ReadAllText(BuildInfoTextPath, Encoding.UTF8) : string.Empty;

            var contentSb = new StringBuilder();
            contentSb.AppendLine($"branch={branch}");
            contentSb.AppendLine($"shortSha={sha}");
            contentSb.AppendLine($"fullSha={fullSha}");
            contentSb.AppendLine($"unity={Application.unityVersion}");
            if (updateTimestamp || string.IsNullOrEmpty(existing))
            {
                contentSb.AppendLine($"preparedUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z");
            }
            else
            {
                foreach (var line in existing.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.StartsWith("preparedUtc=", StringComparison.Ordinal))
                    {
                        contentSb.AppendLine(line);
                        break;
                    }
                }
            }

            string next = contentSb.ToString();
            if (string.Equals(existing, next, StringComparison.Ordinal))
            {
                return 0;
            }

            File.WriteAllText(BuildInfoTextPath, next, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            AssetDatabase.ImportAsset(BuildInfoTextPath);
            sb.AppendLine($"[Asset] Updated {BuildInfoTextPath}");
            return 1;
        }

        private static int EnsureManifest(StringBuilder sb)
        {
            int changes = 0;
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(ManifestAssetPath);
            if (manifest == null)
            {
                manifest = ScriptableObject.CreateInstance<PlaytestReferenceManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestAssetPath);
                sb.AppendLine($"[Manifest] Created {ManifestAssetPath}");
                changes++;
            }

            string branch = TryRunGit("rev-parse --abbrev-ref HEAD", manifest.branchName);
            string sha = TryRunGit("rev-parse HEAD", manifest.baseCommitSha);
            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PizzeriaConfigPath);
            var pizzeriaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);

            bool dirty = false;
            if (manifest.branchName != branch) { manifest.branchName = branch; dirty = true; }
            if (manifest.baseCommitSha != sha) { manifest.baseCommitSha = sha; dirty = true; }
            if (manifest.unityVersion != Application.unityVersion) { manifest.unityVersion = Application.unityVersion; dirty = true; }
            if (manifest.scenes == null) { manifest.scenes = new PlaytestScenePaths(); dirty = true; }
            if (manifest.scenes.lobbyScenePath != LobbyScenePath) { manifest.scenes.lobbyScenePath = LobbyScenePath; dirty = true; }
            if (manifest.scenes.readyScenePath != ReadyScenePath) { manifest.scenes.readyScenePath = ReadyScenePath; dirty = true; }
            if (manifest.scenes.canonicalGameScenePath != CanonicalGameScenePath) { manifest.scenes.canonicalGameScenePath = CanonicalGameScenePath; dirty = true; }
            if (manifest.scenes.celShadedVariantScenePath != CelShadedGameScenePath) { manifest.scenes.celShadedVariantScenePath = CelShadedGameScenePath; dirty = true; }
            if (manifest.activePizzeriaConfig != cfg) { manifest.activePizzeriaConfig = cfg; dirty = true; }
            if (manifest.activePizzeriaPrefab != pizzeriaPrefab) { manifest.activePizzeriaPrefab = pizzeriaPrefab; dirty = true; }
            if (manifest.playerPrefab != playerPrefab) { manifest.playerPrefab = playerPrefab; dirty = true; }

            if (dirty)
            {
                EditorUtility.SetDirty(manifest);
                sb.AppendLine("[Manifest] Updated references in PlaytestReferenceManifest.");
                changes++;
            }

            PlaytestReferenceManifest.ClearRuntimeCache();
            return changes;
        }

        private static int EnsureAllScenes(StringBuilder sb)
        {
            int changes = 0;
            var originalScene = SceneManager.GetActiveScene();
            string originalPath = originalScene.IsValid() ? originalScene.path : string.Empty;

            changes += ProcessScene(LobbyScenePath, EnsureLobbyScene, sb);
            changes += ProcessScene(ReadyScenePath, EnsureReadyScene, sb);
            changes += ProcessScene(CanonicalGameScenePath, EnsureGameplayScene, sb);
            changes += ProcessScene(CelShadedGameScenePath, EnsureGameplayScene, sb);

            if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid() || active.path != originalPath)
                {
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                }
            }

            return changes;
        }

        private static int ProcessScene(string scenePath, Func<Scene, StringBuilder, int> processor, StringBuilder sb)
        {
            if (!File.Exists(scenePath))
            {
                sb.AppendLine($"[ERROR] Scene not found: {scenePath}");
                return 0;
            }

            var opened = SceneManager.GetSceneByPath(scenePath);
            bool alreadyLoaded = opened.isLoaded;
            var scene = alreadyLoaded ? opened : EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int changes = processor(scene, sb);
            if (changes > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                sb.AppendLine($"[Scene] Saved {scenePath} ({changes} change(s)).");
            }
            return changes;
        }

        private static int EnsureLobbyScene(Scene scene, StringBuilder sb)
        {
            int changes = 0;
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LobbyTreePath);

            GameObject uiRoot = FindInScene(scene, "--- UI ---");
            if (uiRoot != null && panel != null && tree != null)
            {
                var doc = uiRoot.GetComponent<UIDocument>();
                if (doc != null && (doc.panelSettings != panel || doc.visualTreeAsset != tree))
                {
                    Undo.RecordObject(doc, "Fix Lobby UIDocument");
                    doc.panelSettings = panel;
                    doc.visualTreeAsset = tree;
                    EditorUtility.SetDirty(doc);
                    changes++;
                    sb.AppendLine("[LobbyScene] Fixed UIDocument panel/tree.");
                }

                if (uiRoot.GetComponent<UIFeedbackService>() == null)
                {
                    Undo.AddComponent<UIFeedbackService>(uiRoot);
                    changes++;
                    sb.AppendLine("[LobbyScene] Added UIFeedbackService.");
                }
            }

            return changes;
        }

        private static int EnsureReadyScene(Scene scene, StringBuilder sb)
        {
            int changes = 0;
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ReadyTreePath);

            GameObject uiRoot = FindInScene(scene, "--- UI ---");
            if (uiRoot == null)
            {
                uiRoot = new GameObject("--- UI ---");
                SceneManager.MoveGameObjectToScene(uiRoot, scene);
                Undo.RegisterCreatedObjectUndo(uiRoot, "Create ReadyScene UI root");
                changes++;
            }

            var doc = EnsureComponent<UIDocument>(uiRoot, ref changes, sb, "ReadyScene");
            if (panel != null && tree != null && (doc.panelSettings != panel || doc.visualTreeAsset != tree))
            {
                Undo.RecordObject(doc, "Assign ReadyScene UIDocument");
                doc.panelSettings = panel;
                doc.visualTreeAsset = tree;
                EditorUtility.SetDirty(doc);
                changes++;
                sb.AppendLine("[ReadyScene] Assigned PanelSettings and ReadyRoomScreen.uxml.");
            }

            if (uiRoot.GetComponent<UIFeedbackService>() == null)
            {
                Undo.AddComponent<UIFeedbackService>(uiRoot);
                changes++;
                sb.AppendLine("[ReadyScene] Added UIFeedbackService to --- UI ---.");
            }

            EnsureComponent<UIModalStack>(uiRoot, ref changes, sb, "ReadyScene");
            BindUIDocumentField(EnsureComponent<ModalPresenter>(uiRoot, ref changes, sb, "ReadyScene"), doc, ref changes);
            BindUIDocumentField(EnsureComponent<SettingsPresenter>(uiRoot, ref changes, sb, "ReadyScene"), doc, ref changes);
            BindUIDocumentField(EnsureComponent<ReadyRoomPresenter>(uiRoot, ref changes, sb, "ReadyScene"), doc, ref changes);
            BindUIDocumentField(EnsureComponent<ToastPresenter>(uiRoot, ref changes, sb, "ReadyScene"), doc, ref changes);

            return changes;
        }

        private static int EnsureGameplayScene(Scene scene, StringBuilder sb)
        {
            int changes = PiariaGameplayUIMigration.EnsureGameScene(scene);
            if (changes > 0)
            {
                sb.AppendLine($"[{scene.name}] PiariaGameplayUIMigration applied {changes} change(s).");
            }
            return changes;
        }

        public static string ValidateAll(out int errorCount, out int warningCount)
        {
            errorCount = 0;
            warningCount = 0;
            var sb = new StringBuilder();
            var originalScene = SceneManager.GetActiveScene();
            string originalPath = originalScene.IsValid() ? originalScene.path : string.Empty;

            // 1. Validar Layers
            string[] reqLayers = { "MinimapOnly", "LocalPlayerModel", "FirstPersonOnly" };
            foreach (var layer in reqLayers)
            {
                if (LayerMask.NameToLayer(layer) < 0)
                {
                    sb.AppendLine($"[ERROR] Missing Layer: {layer}");
                    errorCount++;
                }
            }

            // 2. Validar Animator Controller
            var animCtrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorPath);
            if (animCtrl == null)
            {
                sb.AppendLine($"[ERROR] Missing AnimatorController: {AnimatorPath}");
                errorCount++;
            }
            else
            {
                CheckAnimParam(animCtrl, "Speed", sb, ref warningCount);
                CheckAnimParam(animCtrl, "WeaponMode", sb, ref warningCount);
                CheckAnimParam(animCtrl, "IsRidingBike", sb, ref warningCount);
            }

            // 3. Validar Manifesto e Config
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(ManifestAssetPath);
            if (manifest == null)
            {
                sb.AppendLine($"[ERROR] Missing PlaytestReferenceManifest: {ManifestAssetPath}");
                errorCount++;
            }

            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PizzeriaConfigPath);
            if (cfg == null)
            {
                sb.AppendLine($"[ERROR] Missing PizzeriaConfig: {PizzeriaConfigPath}");
                errorCount++;
            }
            else
            {
                var seenStationIds = new HashSet<int>();
                foreach (var st in cfg.stationInstances)
                {
                    if (st == null) continue;
                    if (!seenStationIds.Add(st.instanceId))
                    {
                        sb.AppendLine($"[ERROR] Duplicate stationInstance id={st.instanceId} in PizzeriaConfig.");
                        errorCount++;
                    }
                    if (st.definition == null)
                    {
                        sb.AppendLine($"[ERROR] StationInstance id={st.instanceId} missing StationDef.");
                        errorCount++;
                    }
                }
            }

            // 4. Validar Prefab da Pizzaria Modular
            var pizzeriaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PizzeriaPrefabPath);
            if (pizzeriaPrefab == null)
            {
                sb.AppendLine($"[ERROR] Missing Pizzeria prefab: {PizzeriaPrefabPath}");
                errorCount++;
            }
            else
            {
                int missing = CountMissingScriptsRecursive(pizzeriaPrefab);
                if (missing > 0)
                {
                    sb.AppendLine($"[ERROR] {PizzeriaPrefabPath} has {missing} Missing Script(s).");
                    errorCount++;
                }

                var root = pizzeriaPrefab.GetComponent<PizzeriaRoot>();
                if (root == null)
                {
                    sb.AppendLine($"[ERROR] {PizzeriaPrefabPath} missing PizzeriaRoot.");
                    errorCount++;
                }

                var prefabStationIds = new HashSet<int>();
                foreach (var sv in pizzeriaPrefab.GetComponentsInChildren<StationView>(true))
                {
                    if (!prefabStationIds.Add(sv.stationId))
                    {
                        sb.AppendLine($"[ERROR] Duplicate StationView.stationId={sv.stationId} on {sv.name}.");
                        errorCount++;
                    }
                    if (sv.GetComponentsInChildren<Collider>(true).Length == 0)
                    {
                        sb.AppendLine($"[ERROR] StationView id={sv.stationId} ({sv.name}) has 0 Colliders.");
                        errorCount++;
                    }
                    if (sv.GetComponentsInChildren<Renderer>(true).Length == 0)
                    {
                        sb.AppendLine($"[WARN] StationView id={sv.stationId} ({sv.name}) has 0 Renderers (P5 target).");
                        warningCount++;
                    }
                }
            }

            // 5. Validar NetworkPrefabs (somente leitura)
            var npList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsAssetPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (npList == null)
            {
                sb.AppendLine($"[WARN] Could not load NetworkPrefabsList at {NetworkPrefabsAssetPath}");
                warningCount++;
            }
            else
            {
                if (playerPrefab != null && !npList.Contains(playerPrefab))
                {
                    sb.AppendLine($"[ERROR] PlayerPrefab not registered in {NetworkPrefabsAssetPath}.");
                    errorCount++;
                }
                if (pizzeriaPrefab != null && !npList.Contains(pizzeriaPrefab))
                {
                    sb.AppendLine($"[ERROR] PizzeriaModular_Manual not registered in {NetworkPrefabsAssetPath}.");
                    errorCount++;
                }
            }

            // 6. Validar Cenas
            ValidateScene(LobbyScenePath, isGameplay: false, isReady: false, sb, ref errorCount, ref warningCount);
            ValidateScene(ReadyScenePath, isGameplay: false, isReady: true, sb, ref errorCount, ref warningCount);
            ValidateScene(CanonicalGameScenePath, isGameplay: true, isReady: false, sb, ref errorCount, ref warningCount);
            ValidateScene(CelShadedGameScenePath, isGameplay: true, isReady: false, sb, ref errorCount, ref warningCount);

            if (!string.IsNullOrEmpty(originalPath) && File.Exists(originalPath))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid() || active.path != originalPath)
                {
                    EditorSceneManager.OpenScene(originalPath, OpenSceneMode.Single);
                }
            }

            sb.AppendLine($"Validation complete -> Errors: {errorCount}, Warnings: {warningCount}");
            return sb.ToString();
        }

        private static void ValidateScene(string scenePath, bool isGameplay, bool isReady, StringBuilder sb, ref int errors, ref int warnings)
        {
            if (!File.Exists(scenePath))
            {
                sb.AppendLine($"[ERROR] Scene missing: {scenePath}");
                errors++;
                return;
            }

            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.isLoaded) scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int missing = 0;
            UIDocument mainDoc = null;
            NetworkManager nm = null;
            CityGenerator cityGen = null;
            UIFeedbackService feedback = null;

            foreach (var root in scene.GetRootGameObjects())
            {
                missing += CountMissingScriptsRecursive(root);
                if (mainDoc == null)
                {
                    var docs = root.GetComponentsInChildren<UIDocument>(true);
                    foreach (var d in docs)
                    {
                        if (!isGameplay || d.gameObject.name == "MainGameUI")
                        {
                            mainDoc = d;
                            break;
                        }
                    }
                }
                if (nm == null) nm = root.GetComponentInChildren<NetworkManager>(true);
                if (cityGen == null) cityGen = root.GetComponentInChildren<CityGenerator>(true);
                if (feedback == null) feedback = root.GetComponentInChildren<UIFeedbackService>(true);
            }

            if (missing > 0)
            {
                sb.AppendLine($"[ERROR] {scenePath} has {missing} Missing Script(s).");
                errors++;
            }

            if (mainDoc == null)
            {
                sb.AppendLine($"[ERROR] {scenePath} missing expected UIDocument.");
                errors++;
            }
            else
            {
                if (mainDoc.panelSettings == null)
                {
                    sb.AppendLine($"[ERROR] {scenePath} UIDocument missing PanelSettings.");
                    errors++;
                }
                if (mainDoc.visualTreeAsset == null)
                {
                    sb.AppendLine($"[ERROR] {scenePath} UIDocument missing VisualTreeAsset.");
                    errors++;
                }
            }

            if (!isGameplay && feedback == null)
            {
                sb.AppendLine($"[ERROR] {scenePath} missing UIFeedbackService.");
                errors++;
            }

            if (isGameplay)
            {
                if (nm == null)
                {
                    sb.AppendLine($"[ERROR] {scenePath} missing NetworkManager.");
                    errors++;
                }
                else if (nm.NetworkConfig == null || nm.NetworkConfig.PlayerPrefab == null)
                {
                    sb.AppendLine($"[ERROR] {scenePath} NetworkManager missing PlayerPrefab.");
                    errors++;
                }

                if (cityGen == null)
                {
                    sb.AppendLine($"[ERROR] {scenePath} missing CityGenerator.");
                    errors++;
                }
                else
                {
                    if (cityGen.config == null)
                    {
                        sb.AppendLine($"[ERROR] {scenePath} CityGenerator missing CityConfig.");
                        errors++;
                    }
                    if (cityGen.pizzeriaPrefab == null)
                    {
                        sb.AppendLine($"[ERROR] {scenePath} CityGenerator missing pizzeriaPrefab.");
                        errors++;
                    }
                }
            }
        }

        private static void CheckAnimParam(AnimatorController ctrl, string paramName, StringBuilder sb, ref int warnings)
        {
            foreach (var p in ctrl.parameters)
            {
                if (p.name == paramName) return;
            }
            sb.AppendLine($"[WARN] AnimatorController missing parameter '{paramName}'.");
            warnings++;
        }

        private static int CountMissingScriptsRecursive(GameObject go)
        {
            int total = 0;
            foreach (var tr in go.GetComponentsInChildren<Transform>(true))
            {
                total += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject);
            }
            return total;
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                {
                    if (tr.gameObject.name == name) return tr.gameObject;
                }
            }
            return null;
        }

        private static T EnsureComponent<T>(GameObject obj, ref int changes, StringBuilder sb, string context) where T : Component
        {
            var existing = obj.GetComponent<T>();
            if (existing != null) return existing;
            changes++;
            sb.AppendLine($"[{context}] Added {typeof(T).Name} to {obj.name}.");
            return Undo.AddComponent<T>(obj);
        }

        private static void BindUIDocumentField(MonoBehaviour presenter, UIDocument doc, ref int changes)
        {
            if (presenter == null || doc == null) return;
            var field = presenter.GetType().GetField("uiDocument");
            if (field == null || Equals(field.GetValue(presenter), doc)) return;
            Undo.RecordObject(presenter, $"Bind uiDocument on {presenter.GetType().Name}");
            field.SetValue(presenter, doc);
            EditorUtility.SetDirty(presenter);
            changes++;
        }

        private static string TryRunGit(string args, string fallback)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("git", args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = System.Diagnostics.Process.Start(psi))
                {
                    string outStr = proc.StandardOutput.ReadToEnd()?.Trim();
                    proc.WaitForExit(3000);
                    if (proc.ExitCode == 0 && !string.IsNullOrEmpty(outStr))
                    {
                        return outStr;
                    }
                }
            }
            catch
            {
            }
            return fallback;
        }
    }
}
