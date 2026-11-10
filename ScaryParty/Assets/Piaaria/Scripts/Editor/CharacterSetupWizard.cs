#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using ScaryParty.Player;
using ScaryParty.UI.Piaria.Editor;
using StarterAssets;
using Debug = UnityEngine.Debug;

namespace ScaryParty.EditorScripts
{
    /// <summary>
    /// Wizard de exportação, configuração e validação do Personagem Canônico (Fase 1 + Fase 7).
    /// Permite:
    /// 1) Re-exportar os .fbx a partir dos .blend usando o Blender local (export_character_fbx.py + character_export_manifest.json);
    /// 2) Configurar os ModelImporters (Generic Rig, Avatar canônico, Loop Time, Root Motion off);
    /// 3) Gerar os materiais toon/URP de 4 slots (Corpo/Avental, Pupilas, Esclera, Pele);
    /// 4) Gerar a AvatarMask de tronco/braços e o CharacterAnimator.controller (Locomotion BlendTree, Jump/FreeFall, SitDown/SitIdle/StandUp, Prepare);
    /// 5) Gerar o CharacterVisual.prefab compartilhado;
    /// 6) Integrar o CharacterVisual.prefab em PlayerArmature.prefab e regenerar PlayerArmature_Preview.prefab;
    /// 7) Validar todas as referências, ossos, clips e prefabs.
    /// </summary>
    [InitializeOnLoad]
    public class CharacterSetupWizard : EditorWindow
    {
        public const string GeneratedFbxFolder = "Assets/Piaaria/Art/Characters/Generated";
        public const string CharacterMaterialsFolder = "Assets/Piaaria/Art/Characters/Materials";
        public const string AnimatorFolder = "Assets/Piaaria/Animations/Characters";
        public const string CharacterVisualPrefabPath = "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab";
        public const string PlayerArmaturePrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        public const string PlayerPreviewPrefabPath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature_Preview.prefab";
        public const string AnimatorControllerPath = "Assets/Piaaria/Animations/Characters/CharacterAnimator.controller";
        public const string UpperBodyMaskPath = "Assets/Piaaria/Animations/Characters/CharacterUpperBody.mask";

        static CharacterSetupWizard()
        {
            EditorApplication.delayCall += EnsureSetupAutoAppliedOnce;
        }

        private static void EnsureSetupAutoAppliedOnce()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string flagPath = "Temp/CharacterSetupApplied_v2.flag";
            if (!File.Exists(flagPath))
            {
                File.WriteAllText(flagPath, DateTime.UtcNow.ToString());
                bool ok = ApplyFullCharacterSetup(out string report);
                Debug.Log($"[CharacterSetupWizard] Auto-Setup Result (ok={ok}):\n{report}");
            }
        }

        private string _blenderExecutablePath = @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe";
        private Vector2 _scrollPos;
        private string _report = "Pronto para configurar e validar o Personagem.";
        private MessageType _reportType = MessageType.Info;

        [MenuItem("Tools/Scary Party/Pizzeria/Personagens (Character Setup Wizard)", false, 15)]
        public static void ShowWindow()
        {
            var win = GetWindow<CharacterSetupWizard>("Character Setup Wizard");
            win.minSize = new Vector2(560, 620);
            win.Show();
        }

        private void OnEnable()
        {
            _blenderExecutablePath = EditorPrefs.GetString("ScaryParty_BlenderExePath", ResolveDefaultBlenderExecutable());
        }

        public static string ResolveDefaultBlenderExecutable()
        {
            string[] candidates = new[]
            {
                @"C:\Program Files\Blender Foundation\Blender 4.5\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.4\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.3\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender 4.2\blender.exe",
                @"C:\Program Files\Blender Foundation\Blender\blender.exe"
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }
            return candidates[0];
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(8);
            GUILayout.Label("Scary Party — Wizard de Personagens (FBX, Visual Compartilhado & Animator)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Exporta os arquivos .blend para FBX, configura rig Generic canônico, materiais toon de 4 slots, " +
                "AnimatorController (Idle/Walk/Run, Jump/FreeFall, SitDown/SitIdle/StandUp, Prepare), " +
                "gera CharacterVisual.prefab e integra em PlayerArmature e PlayerArmature_Preview.",
                MessageType.None);

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("1. Exportação Blender (.blend -> .fbx)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            _blenderExecutablePath = EditorGUILayout.TextField("Executável do Blender", _blenderExecutablePath);
            if (GUILayout.Button("Procurar...", GUILayout.Width(90)))
            {
                string selected = EditorUtility.OpenFilePanel("Selecionar blender.exe", @"C:\Program Files\Blender Foundation", "exe");
                if (!string.IsNullOrEmpty(selected))
                {
                    _blenderExecutablePath = selected;
                    EditorPrefs.SetString("ScaryParty_BlenderExePath", _blenderExecutablePath);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Re-exportar FBXs via Blender Headless", GUILayout.Height(28)))
            {
                EditorPrefs.SetString("ScaryParty_BlenderExePath", _blenderExecutablePath);
                bool ok = RunBlenderExport(_blenderExecutablePath, out _report);
                _reportType = ok ? MessageType.Info : MessageType.Error;
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("2. Pipeline de Configuração e Validação Unity", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Executar Setup Completo (FBX + Animator + Prefabs)", GUILayout.Height(32)))
            {
                if (EditorApplication.isPlaying)
                {
                    EditorUtility.DisplayDialog("Aviso", "Saia do Play Mode antes de aplicar o setup.", "OK");
                    return;
                }
                bool ok = ApplyFullCharacterSetup(out _report);
                _reportType = ok ? MessageType.Info : MessageType.Error;
            }

            if (GUILayout.Button("Validar Setup", GUILayout.Height(32), GUILayout.Width(160)))
            {
                bool ok = ValidateCharacterSetup(out _report);
                _reportType = ok ? MessageType.Info : MessageType.Error;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(8);
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.HelpBox(_report, _reportType);
            EditorGUILayout.EndScrollView();
        }

        public static bool RunBlenderExport(string blenderExePath, out string report)
        {
            var sb = new StringBuilder();
            if (string.IsNullOrWhiteSpace(blenderExePath) || !File.Exists(blenderExePath))
            {
                report = $"[ERRO] Executável do Blender não encontrado em: '{blenderExePath}'. Indique um caminho válido para blender.exe.";
                Debug.LogError(report);
                return false;
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptPath = Path.Combine(projectRoot, "Tools", "Blender", "export_character_fbx.py");
            string manifestPath = Path.Combine(projectRoot, "Tools", "Blender", "character_export_manifest.json");

            if (!File.Exists(scriptPath) || !File.Exists(manifestPath))
            {
                report = $"[ERRO] Script ou manifesto ausente:\n- {scriptPath}\n- {manifestPath}";
                Debug.LogError(report);
                return false;
            }

            var psi = new ProcessStartInfo
            {
                FileName = blenderExePath,
                Arguments = $"-b --python \"{scriptPath}\" -- --manifest \"{manifestPath}\"",
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(psi))
            {
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit(60000);

                sb.AppendLine(stdout);
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    sb.AppendLine("--- STDERR ---");
                    sb.AppendLine(stderr);
                }

                bool completed = stdout.Contains("=== ALL_CHARACTER_FBX_EXPORTS_COMPLETED ===");
                AssetDatabase.Refresh();
                report = sb.ToString();
                return completed;
            }
        }

        [MenuItem("Tools/Scary Party/Pizzeria/Aplicar Setup de Personagens Agora", false, 16)]
        public static void RunFullSetupMenu()
        {
            ApplyFullCharacterSetup(out string report);
            Debug.Log(report);
        }

        public static bool ApplyFullCharacterSetup(out string report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== APLICANDO SETUP DO PERSONAGEM CANÔNICO ===");

            try
            {
                EnsureFolders();
                AssetDatabase.Refresh();

                // 1. Configurar FBX Importers
                if (!ConfigureFbxImporters(sb))
                {
                    report = sb.ToString();
                    return false;
                }

                // 2. Criar materiais de 4 slots (Corpo/Avental, Pupilas, Esclera, Pele)
                Material[] charMats = EnsureCharacterMaterials(sb);

                // 3. Criar AvatarMask de UpperBody e CharacterAnimator.controller
                AvatarMask upperBodyMask = EnsureUpperBodyAvatarMask(sb);
                AnimatorController controller = EnsureAnimatorController(upperBodyMask, sb);

                // 4. Criar CharacterVisual.prefab
                GameObject visualPrefab = EnsureCharacterVisualPrefab(charMats, controller, sb);
                if (visualPrefab == null)
                {
                    sb.AppendLine("[ERRO] Falha ao criar CharacterVisual.prefab.");
                    report = sb.ToString();
                    return false;
                }

                // 5. Integrar no PlayerArmature.prefab e atualizar PlayerArmature_Preview.prefab
                if (!IntegrateIntoPlayerArmature(visualPrefab, sb))
                {
                    report = sb.ToString();
                    return false;
                }

                PlayerPreviewPrefabGenerator.GeneratePreviewPrefab(true);
                sb.AppendLine("[OK] PlayerArmature_Preview.prefab regenerado com CharacterVisual.");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                bool valid = ValidateCharacterSetup(out string valReport);
                sb.AppendLine();
                sb.AppendLine(valReport);
                report = sb.ToString();
                return valid;
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[EXCEÇÃO] {ex}");
                Debug.LogException(ex);
                report = sb.ToString();
                return false;
            }
        }

        private static void EnsureFolders()
        {
            string[] dirs = new[]
            {
                GeneratedFbxFolder,
                CharacterMaterialsFolder,
                AnimatorFolder,
                "Assets/Piaaria/Prefabs/Characters"
            };

            foreach (var d in dirs)
            {
                if (!Directory.Exists(d))
                    Directory.CreateDirectory(d);
            }
        }

        private static bool ConfigureFbxImporters(StringBuilder sb)
        {
            string canonicalPath = $"{GeneratedFbxFolder}/Character.fbx";
            if (!File.Exists(canonicalPath))
            {
                sb.AppendLine($"[ERRO] Arquivo canônico não encontrado: {canonicalPath}. Execute a exportação do Blender primeiro.");
                return false;
            }

            // 1. Configurar Character.fbx (cria o Avatar Generic canônico)
            var canonicalImporter = AssetImporter.GetAtPath(canonicalPath) as ModelImporter;
            if (canonicalImporter == null)
            {
                AssetDatabase.ImportAsset(canonicalPath, ImportAssetOptions.ForceUpdate);
                canonicalImporter = AssetImporter.GetAtPath(canonicalPath) as ModelImporter;
            }

            if (canonicalImporter == null)
            {
                sb.AppendLine($"[ERRO] ModelImporter nulo para {canonicalPath}.");
                return false;
            }

            bool canonicalDirty = false;
            if (!canonicalImporter.bakeAxisConversion)
            {
                canonicalImporter.bakeAxisConversion = true;
                canonicalDirty = true;
            }
            if (Mathf.Abs(canonicalImporter.globalScale - 1f) > 0.001f)
            {
                canonicalImporter.globalScale = 1f;
                canonicalDirty = true;
            }
            if (!canonicalImporter.useFileScale)
            {
                canonicalImporter.useFileScale = true;
                canonicalDirty = true;
            }
            if (canonicalImporter.animationType != ModelImporterAnimationType.Generic)
            {
                canonicalImporter.animationType = ModelImporterAnimationType.Generic;
                canonicalDirty = true;
            }
            if (canonicalImporter.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                canonicalImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                canonicalDirty = true;
            }
            if (canonicalImporter.importAnimation)
            {
                canonicalImporter.importAnimation = false;
                canonicalDirty = true;
            }
            if (canonicalImporter.importCameras || canonicalImporter.importLights)
            {
                canonicalImporter.importCameras = false;
                canonicalImporter.importLights = false;
                canonicalDirty = true;
            }

            if (canonicalDirty)
            {
                canonicalImporter.SaveAndReimport();
            }

            Avatar canonicalAvatar = null;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(canonicalPath);
            foreach (var a in allAssets)
            {
                if (a is Avatar av)
                {
                    canonicalAvatar = av;
                    break;
                }
            }

            if (canonicalAvatar == null)
            {
                canonicalImporter.SaveAndReimport();
                allAssets = AssetDatabase.LoadAllAssetsAtPath(canonicalPath);
                foreach (var a in allAssets)
                {
                    if (a is Avatar av)
                    {
                        canonicalAvatar = av;
                        break;
                    }
                }
            }

            sb.AppendLine($"[OK] Character.fbx importado (Avatar: {(canonicalAvatar != null ? canonicalAvatar.name : "Generic Default")}).");

            // 2. Configurar os 7 arquivos de animação
            var clipSpecs = new (string fileName, string clipName, bool loop)[]
            {
                ("Character_Idle.fbx", "Idle", true),
                ("Character_Walk.fbx", "Walk", true),
                ("Character_Run.fbx", "Run", true),
                ("Character_Prepare.fbx", "Prepare", true),
                ("Character_SitIdle.fbx", "SitIdle", true),
                ("Character_SitDown.fbx", "SitDown", false),
                ("Character_StandUp.fbx", "StandUp", false)
            };

            foreach (var spec in clipSpecs)
            {
                string path = $"{GeneratedFbxFolder}/{spec.fileName}";
                if (!File.Exists(path))
                {
                    sb.AppendLine($"[ERRO] FBX de animação ausente: {path}");
                    return false;
                }

                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                    imp = AssetImporter.GetAtPath(path) as ModelImporter;
                }
                if (imp == null) continue;

                imp.bakeAxisConversion = true;
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.animationType = ModelImporterAnimationType.Generic;
                if (canonicalAvatar != null)
                {
                    imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    imp.sourceAvatar = canonicalAvatar;
                }
                else
                {
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }

                imp.importAnimation = true;
                imp.importCameras = false;
                imp.importLights = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;

                var defaultClips = imp.defaultClipAnimations;
                if (defaultClips != null && defaultClips.Length > 0)
                {
                    var clip = defaultClips[0];
                    clip.name = spec.clipName;
                    clip.loopTime = spec.loop;
                    clip.loopPose = spec.loop;
                    clip.lockRootRotation = true;
                    clip.lockRootHeightY = true;
                    clip.lockRootPositionXZ = true;
                    imp.clipAnimations = new[] { clip };
                }

                imp.SaveAndReimport();
                sb.AppendLine($"[OK] Clip '{spec.clipName}' configurado em {spec.fileName} (loop={spec.loop}).");
            }

            return true;
        }

        private static Material[] EnsureCharacterMaterials(StringBuilder sb)
        {
            Shader shader = Shader.Find("Distant Lands/Illustrate/Opaque")
                         ?? Shader.Find("Distant Lands/Illustrate/Simple")
                         ?? Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            var specs = new (string assetName, Color color)[]
            {
                ("Mat_Character_Apron.mat", new Color(0.95f, 0.95f, 0.95f, 1f)),       // Slot 0: M_Apron (Avental branco limpo)
                ("Mat_Character_Pupils.mat", new Color(0.05f, 0.05f, 0.06f, 1f)),      // Slot 1: M_EyePupil (Pupilas pretas)
                ("Mat_Character_EyesWhite.mat", new Color(0.96f, 0.96f, 0.96f, 1f)),   // Slot 2: M_EyeWhite (Esclera branca)
                ("Mat_Character_BodyPrimary.mat", new Color(0.85f, 0.55f, 0.25f, 1f))  // Slot 3: M_Body (Corpo do personagem - customizável)
            };

            Material[] result = new Material[specs.Length];
            for (int i = 0; i < specs.Length; i++)
            {
                string matPath = $"{CharacterMaterialsFolder}/{specs[i].assetName}";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(shader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                else if (shader != null && mat.shader != shader)
                {
                    mat.shader = shader;
                }

                Color c = specs[i].color;
                if (mat.HasProperty("_MainColor")) mat.SetColor("_MainColor", c);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
                if (mat.HasProperty("_UseWaves")) mat.SetFloat("_UseWaves", 0f);
                if (mat.HasProperty("_VaryColor")) mat.SetFloat("_VaryColor", 0f);

                EditorUtility.SetDirty(mat);
                result[i] = mat;
            }

            sb.AppendLine($"[OK] 4 materiais do personagem configurados com shader '{(shader != null ? shader.name : "null")}'.");
            return result;
        }

        private static AvatarMask EnsureUpperBodyAvatarMask(StringBuilder sb)
        {
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, UpperBodyMaskPath);
            }

            // Caminhos relativos ao Animator root (CharacterVisual)
            string[] allPaths = new[]
            {
                "",
                "Armature",
                "Armature/central",
                "Armature/central/chest",
                "Armature/central/chest/neck",
                "Armature/central/chest/neck/head",
                "Armature/central/chest/shoulder.l",
                "Armature/central/chest/shoulder.l/arm1.l",
                "Armature/central/chest/shoulder.l/arm1.l/arm2.l",
                "Armature/central/chest/shoulder.r",
                "Armature/central/chest/shoulder.r/arm1.r",
                "Armature/central/chest/shoulder.r/arm1.r/arm2.r",
                "Armature/central/hip.l",
                "Armature/central/hip.l/leg1.l",
                "Armature/central/hip.l/leg1.l/leg2.l",
                "Armature/central/hip.r",
                "Armature/central/hip.r/leg1.r",
                "Armature/central/hip.r/leg1.r/leg2.r",
                "Cylinder.002"
            };

            mask.transformCount = allPaths.Length;
            for (int i = 0; i < allPaths.Length; i++)
            {
                string p = allPaths[i];
                bool isUpper = p.Contains("chest") || p.Contains("neck") || p.Contains("head") || p.Contains("shoulder") || p.Contains("arm");
                mask.SetTransformPath(i, p);
                mask.SetTransformActive(i, isUpper);
            }

            EditorUtility.SetDirty(mask);
            sb.AppendLine($"[OK] AvatarMask de tronco/braços salva em {UpperBodyMaskPath}.");
            return mask;
        }

        private static AnimationClip LoadClipFromFbx(string fbxFileName, string expectedClipName)
        {
            string path = $"{GeneratedFbxFolder}/{fbxFileName}";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            AnimationClip fallback = null;
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.Equals(clip.name, expectedClipName, StringComparison.OrdinalIgnoreCase))
                        return clip;
                    fallback = clip;
                }
            }
            return fallback;
        }

        private static AnimatorController EnsureAnimatorController(AvatarMask upperBodyMask, StringBuilder sb)
        {
            AnimationClip idleClip = LoadClipFromFbx("Character_Idle.fbx", "Idle");
            AnimationClip walkClip = LoadClipFromFbx("Character_Walk.fbx", "Walk");
            AnimationClip runClip = LoadClipFromFbx("Character_Run.fbx", "Run");
            AnimationClip prepareClip = LoadClipFromFbx("Character_Prepare.fbx", "Prepare");
            AnimationClip sitIdleClip = LoadClipFromFbx("Character_SitIdle.fbx", "SitIdle");
            AnimationClip sitDownClip = LoadClipFromFbx("Character_SitDown.fbx", "SitDown");
            AnimationClip standUpClip = LoadClipFromFbx("Character_StandUp.fbx", "StandUp");

            if (File.Exists(AnimatorControllerPath))
            {
                AssetDatabase.DeleteAsset(AnimatorControllerPath);
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);

            // Parâmetros compatíveis com ThirdPersonController + extensões de preparo/assento/armas
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("MotionSpeed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Jump", AnimatorControllerParameterType.Bool);
            controller.AddParameter("FreeFall", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsPreparing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSitting", AnimatorControllerParameterType.Bool);
            controller.AddParameter("SitDown", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("StandUp", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("WeaponMode", AnimatorControllerParameterType.Int);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("IsMoving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsSprinting", AnimatorControllerParameterType.Bool);

            // Definir Grounded = true por padrão
            var parameters = controller.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].name == "Grounded") parameters[i].defaultBool = true;
                if (parameters[i].name == "MotionSpeed") parameters[i].defaultFloat = 1f;
            }
            controller.parameters = parameters;

            // --- BASE LAYER ---
            var rootSm = controller.layers[0].stateMachine;

            // 1. BlendTree Locomotion (Idle / Walk / Run)
            AnimatorState locomotionState = controller.CreateBlendTreeInController("Idle Walk Run Blend", out BlendTree blendTree, 0);
            locomotionState.speedParameterActive = true;
            locomotionState.speedParameter = "MotionSpeed";
            rootSm.defaultState = locomotionState;

            blendTree.blendType = BlendTreeType.Simple1D;
            blendTree.blendParameter = "Speed";
            blendTree.useAutomaticThresholds = false;
            blendTree.AddChild(idleClip, 0f);
            blendTree.AddChild(walkClip, 2.0f);
            blendTree.AddChild(runClip, 5.335f);

            // 2. Estados de Salto / Queda Livre (pose neutra segura com retorno suave)
            var jumpState = rootSm.AddState("JumpStart", new Vector3(320, -100, 0));
            jumpState.motion = idleClip;
            var fallState = rootSm.AddState("InAir", new Vector3(540, -100, 0));
            fallState.motion = idleClip;

            var tLocToJump = locomotionState.AddTransition(jumpState);
            tLocToJump.AddCondition(AnimatorConditionMode.If, 0, "Jump");
            tLocToJump.hasExitTime = false;
            tLocToJump.duration = 0.1f;

            var tLocToFall = locomotionState.AddTransition(fallState);
            tLocToFall.AddCondition(AnimatorConditionMode.If, 0, "FreeFall");
            tLocToFall.hasExitTime = false;
            tLocToFall.duration = 0.15f;

            var tJumpToFall = jumpState.AddTransition(fallState);
            tJumpToFall.hasExitTime = true;
            tJumpToFall.exitTime = 0.4f;
            tJumpToFall.duration = 0.15f;

            var tFallToLoc = fallState.AddTransition(locomotionState);
            tFallToLoc.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            tFallToLoc.hasExitTime = false;
            tFallToLoc.duration = 0.12f;

            var tJumpToLoc = jumpState.AddTransition(locomotionState);
            tJumpToLoc.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            tJumpToLoc.AddCondition(AnimatorConditionMode.IfNot, 0, "Jump");
            tJumpToLoc.hasExitTime = false;
            tJumpToLoc.duration = 0.12f;

            // 3. Estados de Sentar / Levantar (NPCs nas cadeiras das mesas)
            var sitDownState = rootSm.AddState("SitDown", new Vector3(320, 140, 0));
            sitDownState.motion = sitDownClip;

            var sitIdleState = rootSm.AddState("SitIdle", new Vector3(560, 140, 0));
            sitIdleState.motion = sitIdleClip;

            var standUpState = rootSm.AddState("StandUp", new Vector3(320, 240, 0));
            standUpState.motion = standUpClip;

            var tLocToSitDown = locomotionState.AddTransition(sitDownState);
            tLocToSitDown.AddCondition(AnimatorConditionMode.If, 0, "IsSitting");
            tLocToSitDown.hasExitTime = false;
            tLocToSitDown.duration = 0.15f;

            var tSitDownToSitIdle = sitDownState.AddTransition(sitIdleState);
            tSitDownToSitIdle.hasExitTime = true;
            tSitDownToSitIdle.exitTime = 0.9f;
            tSitDownToSitIdle.duration = 0.1f;

            var tSitIdleToStandUp = sitIdleState.AddTransition(standUpState);
            tSitIdleToStandUp.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSitting");
            tSitIdleToStandUp.hasExitTime = false;
            tSitIdleToStandUp.duration = 0.15f;

            var tSitDownAbort = sitDownState.AddTransition(standUpState);
            tSitDownAbort.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSitting");
            tSitDownAbort.hasExitTime = false;
            tSitDownAbort.duration = 0.15f;

            var tStandUpToLoc = standUpState.AddTransition(locomotionState);
            tStandUpToLoc.hasExitTime = true;
            tStandUpToLoc.exitTime = 0.9f;
            tStandUpToLoc.duration = 0.12f;

            // --- LAYER 1: UPPER BODY PREPARE ---
            var prepareLayer = new AnimatorControllerLayer
            {
                name = "PrepareUpperBody",
                defaultWeight = 0f,
                avatarMask = upperBodyMask,
                blendingMode = AnimatorLayerBlendingMode.Override,
                stateMachine = new AnimatorStateMachine { name = "PrepareUpperBody", hideFlags = HideFlags.HideInHierarchy }
            };
            AssetDatabase.AddObjectToAsset(prepareLayer.stateMachine, controller);
            controller.AddLayer(prepareLayer);

            var prepSm = prepareLayer.stateMachine;
            var emptyUpper = prepSm.AddState("UpperIdle", new Vector3(260, 60, 0));
            emptyUpper.motion = null;
            prepSm.defaultState = emptyUpper;

            var prepActive = prepSm.AddState("Preparing", new Vector3(520, 60, 0));
            prepActive.motion = prepareClip;

            var tToPrep = emptyUpper.AddTransition(prepActive);
            tToPrep.AddCondition(AnimatorConditionMode.If, 0, "IsPreparing");
            tToPrep.hasExitTime = false;
            tToPrep.duration = 0.12f;

            var tFromPrep = prepActive.AddTransition(emptyUpper);
            tFromPrep.AddCondition(AnimatorConditionMode.IfNot, 0, "IsPreparing");
            tFromPrep.hasExitTime = false;
            tFromPrep.duration = 0.12f;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            sb.AppendLine($"[OK] CharacterAnimator.controller gerado em {AnimatorControllerPath} com BlendTree Idle/Walk/Run, Jump/FreeFall, SitDown/SitIdle/StandUp e camada PrepareUpperBody.");
            return controller;
        }

        private static GameObject EnsureCharacterVisualPrefab(Material[] charMats, AnimatorController controller, StringBuilder sb)
        {
            string canonicalPath = $"{GeneratedFbxFolder}/Character.fbx";
            var fbxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(canonicalPath);
            if (fbxAsset == null) return null;

            Avatar canonicalAvatar = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(canonicalPath))
            {
                if (a is Avatar av) { canonicalAvatar = av; break; }
            }

            var rootGo = new GameObject("CharacterVisual");
            try
            {
                var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(fbxAsset);
                PrefabUtility.UnpackPrefabInstance(modelInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                // Transferir filhos do FBX (Armature e Cylinder.002) diretamente para CharacterVisual para casar com os caminhos de animação ("Armature/central/...")
                var childrenToMove = new List<Transform>();
                for (int i = 0; i < modelInstance.transform.childCount; i++)
                {
                    childrenToMove.Add(modelInstance.transform.GetChild(i));
                }
                foreach (var ch in childrenToMove)
                {
                    ch.SetParent(rootGo.transform, false);
                    ch.localPosition = Vector3.zero;
                    ch.localRotation = Quaternion.identity;
                    ch.localScale = Vector3.one;
                }
                DestroyImmediate(modelInstance);

                // Normaliza o osso central para rotacao ereta (elimina pitch de -90 graus herdado do FBX)
                var centralBone = rootGo.transform.Find("Armature/central");
                if (centralBone != null)
                {
                    centralBone.localPosition = new Vector3(0f, 0.46774173f, 0f);
                    centralBone.localRotation = Quaternion.identity;
                }

                // Garantir apenas um Animator ativo na raiz do CharacterVisual
                var childAnimators = rootGo.GetComponentsInChildren<Animator>(true);
                foreach (var ca in childAnimators)
                {
                    if (ca != null && ca.gameObject != rootGo)
                        DestroyImmediate(ca);
                }

                var animator = rootGo.GetComponent<Animator>();
                if (animator == null) animator = rootGo.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.avatar = canonicalAvatar;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var smr = rootGo.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr != null && charMats != null && charMats.Length == 4)
                {
                    smr.sharedMaterials = charMats;
                    smr.updateWhenOffscreen = true;
                }

                var appearance = rootGo.AddComponent<CharacterAppearance>();
                appearance.SetPrimaryRenderer(smr, 3, true);

                var visualCtrl = rootGo.AddComponent<CharacterVisualController>();
                visualCtrl.ResolveReferences();

                var savedPrefab = PrefabUtility.SaveAsPrefabAsset(rootGo, CharacterVisualPrefabPath);
                string resourcesPath = "Assets/Piaaria/Resources/CharacterVisual.prefab";
                Directory.CreateDirectory(Path.GetDirectoryName(resourcesPath));
                PrefabUtility.SaveAsPrefabAsset(rootGo, resourcesPath);
                sb.AppendLine($"[OK] CharacterVisual.prefab salvo em {CharacterVisualPrefabPath} e {resourcesPath}.");
                return savedPrefab;
            }
            finally
            {
                DestroyImmediate(rootGo);
            }
        }

        private static bool IntegrateIntoPlayerArmature(GameObject visualPrefab, StringBuilder sb)
        {
            if (!File.Exists(PlayerArmaturePrefabPath))
            {
                sb.AppendLine($"[ERRO] PlayerArmature.prefab não encontrado em {PlayerArmaturePrefabPath}.");
                return false;
            }

            var contents = PrefabUtility.LoadPrefabContents(PlayerArmaturePrefabPath);
            try
            {
                // 1. Remover geometria/esqueleto antigos (Geometry, Skeleton, ou CharacterVisual anterior)
                for (int i = contents.transform.childCount - 1; i >= 0; i--)
                {
                    var child = contents.transform.GetChild(i);
                    string n = child.name;
                    if (string.Equals(n, "Geometry", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "Skeleton", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "CharacterVisual", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "Armature", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(n, "Cylinder.002", StringComparison.OrdinalIgnoreCase))
                    {
                        DestroyImmediate(child.gameObject);
                    }
                }

                // 2. Remover Animator antigo da raiz se não houver RequireComponent exigindo
                var rootAnimator = contents.GetComponent<Animator>();
                if (rootAnimator != null)
                {
                    DestroyImmediate(rootAnimator);
                }

                // 3. Instanciar CharacterVisual.prefab como filho na origem
                var visualInst = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab, contents.transform);
                visualInst.name = "CharacterVisual";
                visualInst.transform.localPosition = Vector3.zero;
                visualInst.transform.localRotation = Quaternion.identity;
                visualInst.transform.localScale = Vector3.one;

                var visualCtrl = visualInst.GetComponent<CharacterVisualController>();
                var childAnimator = visualInst.GetComponent<Animator>();

                // 4. Atualizar ThirdPersonController com referência explícita ao Animator e CharacterVisualController
                var tpc = contents.GetComponent<ThirdPersonController>();
                if (tpc != null)
                {
                    tpc.SetExplicitAnimator(childAnimator, visualCtrl);
                    var soTpc = new SerializedObject(tpc);
                    var expProp = soTpc.FindProperty("_explicitAnimator");
                    if (expProp != null) expProp.objectReferenceValue = childAnimator;
                    var vcProp = soTpc.FindProperty("_visualController");
                    if (vcProp != null) vcProp.objectReferenceValue = visualCtrl;
                    soTpc.ApplyModifiedPropertiesWithoutUndo();
                }

                // 5. Atualizar OwnerNetworkAnimator para apontar para o Animator do CharacterVisual
                var ownerNetAnim = contents.GetComponent<OwnerNetworkAnimator>();
                if (ownerNetAnim != null && childAnimator != null)
                {
                    ownerNetAnim.Animator = childAnimator;
                    var soNetAnim = new SerializedObject(ownerNetAnim);
                    var animProp = soNetAnim.FindProperty("m_Animator");
                    if (animProp != null) animProp.objectReferenceValue = childAnimator;
                    soNetAnim.ApplyModifiedPropertiesWithoutUndo();
                }

                // 6. Atualizar WeaponController
                var weaponCtrl = contents.GetComponent<WeaponController>();
                if (weaponCtrl != null)
                {
                    weaponCtrl.animator = childAnimator;
                    if (visualCtrl != null && visualCtrl.RightHandBone != null)
                        weaponCtrl.rightHandAttachment = visualCtrl.RightHandBone;
                }

                // 7. Atualizar BackpackVisualizer
                var bpVis = contents.GetComponent<ScaryParty.Pizzeria.Presentation.BackpackVisualizer>();
                if (bpVis != null && visualCtrl != null && visualCtrl.ChestBone != null)
                {
                    var soBp = new SerializedObject(bpVis);
                    var mountProp = soBp.FindProperty("backpackMount");
                    if (mountProp != null) mountProp.objectReferenceValue = visualCtrl.ChestBone;
                    soBp.ApplyModifiedPropertiesWithoutUndo();
                }

                // 8. Garantir ajuste de CharacterController compatível com 1.60m de altura mantendo jogabilidade
                var cc = contents.GetComponent<CharacterController>();
                if (cc != null)
                {
                    cc.height = 1.65f;
                    cc.center = new Vector3(0f, 0.85f, 0f);
                    cc.radius = 0.28f;
                }

                PrefabUtility.SaveAsPrefabAsset(contents, PlayerArmaturePrefabPath);
                sb.AppendLine($"[OK] PlayerArmature.prefab atualizado com CharacterVisual.prefab e bindings explícitos.");
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        public static bool ValidateCharacterSetup(out string report)
        {
            bool ok = true;
            var sb = new StringBuilder();
            sb.AppendLine("=== VALIDAÇÃO DO SETUP DE PERSONAGENS ===");

            // 1. Verificar FBXs
            string[] fbxs = new[]
            {
                "Character.fbx",
                "Character_Idle.fbx",
                "Character_Walk.fbx",
                "Character_Run.fbx",
                "Character_Prepare.fbx",
                "Character_SitIdle.fbx",
                "Character_SitDown.fbx",
                "Character_StandUp.fbx"
            };

            foreach (var f in fbxs)
            {
                string p = $"{GeneratedFbxFolder}/{f}";
                if (!File.Exists(p))
                {
                    sb.AppendLine($"[ERRO] FBX ausente: {p}");
                    ok = false;
                }
            }

            // 2. Verificar CharacterVisual.prefab
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterVisualPrefabPath);
            if (visualPrefab == null)
            {
                sb.AppendLine($"[ERRO] CharacterVisual.prefab ausente em {CharacterVisualPrefabPath}.");
                ok = false;
            }
            else
            {
                var vc = visualPrefab.GetComponent<CharacterVisualController>();
                var app = visualPrefab.GetComponent<CharacterAppearance>();
                var anim = visualPrefab.GetComponent<Animator>();
                if (vc == null || app == null || anim == null || anim.runtimeAnimatorController == null)
                {
                    sb.AppendLine("[ERRO] CharacterVisual.prefab está incompleto (faltando Controller, Appearance ou AnimatorController).");
                    ok = false;
                }
                else
                {
                    sb.AppendLine($"[OK] CharacterVisual.prefab válido (RootBone={vc.RootBone?.name}, Chest={vc.ChestBone?.name}, LHand={vc.LeftHandBone?.name}, RHand={vc.RightHandBone?.name}).");

                    var smr = visualPrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh != null)
                    {
                        var b = smr.sharedMesh.bounds;
                        float h = b.size.y;
                        if (h < 1.3f || h > 1.9f)
                        {
                            sb.AppendLine($"[AVISO] Altura da malha: {h:F2}m (fora do intervalo canônico 1.4-1.8m).");
                        }
                        else
                        {
                            sb.AppendLine($"[OK] Altura canônica validada: {h:F2}m (pivot nos pés).");
                        }
                    }
                    if (app != null)
                    {
                        sb.AppendLine($"[OK] CharacterAppearance configurado (Slot primário={app.PrimaryColorMaterialSlot} - Corpo).");
                    }
                }
            }

            // 3. Verificar PlayerArmature.prefab
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerArmaturePrefabPath);
            if (playerPrefab == null)
            {
                sb.AppendLine($"[ERRO] PlayerArmature.prefab ausente.");
                ok = false;
            }
            else
            {
                var anims = playerPrefab.GetComponentsInChildren<Animator>(true);
                if (anims.Length != 1)
                {
                    sb.AppendLine($"[ERRO] PlayerArmature deve possuir exatamente 1 Animator ativo, mas possui {anims.Length}.");
                    ok = false;
                }
                var ownerNetAnim = playerPrefab.GetComponent<OwnerNetworkAnimator>();
                if (ownerNetAnim == null || ownerNetAnim.Animator == null)
                {
                    sb.AppendLine("[ERRO] OwnerNetworkAnimator no PlayerArmature está sem referência de Animator.");
                    ok = false;
                }
                else
                {
                    sb.AppendLine($"[OK] PlayerArmature.prefab possui exatamente 1 Animator ({ownerNetAnim.Animator.name}) vinculado ao OwnerNetworkAnimator.");
                }
            }

            // 4. Verificar PlayerArmature_Preview.prefab
            var previewPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPreviewPrefabPath);
            if (previewPrefab == null)
            {
                sb.AppendLine("[ERRO] PlayerArmature_Preview.prefab ausente.");
                ok = false;
            }
            else
            {
                var smr = previewPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (smr == null)
                {
                    sb.AppendLine("[ERRO] PlayerArmature_Preview.prefab não contém SkinnedMeshRenderer.");
                    ok = false;
                }
                else
                {
                    sb.AppendLine($"[OK] PlayerArmature_Preview.prefab válido (Mesh={smr.sharedMesh?.name}).");
                }
            }

            report = sb.ToString();
            return ok;
        }
    }
}
#endif
