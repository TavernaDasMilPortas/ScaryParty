#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using ScaryParty.Lighting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ScaryParty.VisualStyles.Editor
{
    [Serializable]
    public class OriginalGameSceneRollbackManifest
    {
        public int schemaVersion = 1;
        public string appliedTimestampUtc;
        public string styleId;
        public string canonicalScenePath;
        public string backupSceneCopyPath;
        public string preApplySceneSha256;
        public string postApplySceneSha256;

        public bool createdBootstrapObject;
        public string managedObjectName = "VisualStyleManager";
        public bool addedTimeOfDayDirector;
        public string previousProfileGuid;
        public string previousLibraryGuid;

        public int previousGameplayRoute;
        public int previousVisualMode;
        public bool previousUseStyledGameplay;
        public string previousActiveStyleId;
        public string previousActiveGameplaySceneName;
    }

    /// <summary>
    /// Implementacao do Modo B: salva estilo como padrao da GameScene original usando apenas objetos minimos
    /// de bootstrap, backup verificavel e manifest de rollback seletivo que preserva edicoes de gameplay posteriores.
    /// </summary>
    public static class OriginalGameSceneStylePersistence
    {
        public const string BackupsFolder = "Assets/Piaaria/Art/VisualStyles/Backups";
        public const string BackupSceneCopyPath = BackupsFolder + "/GameScene_PreStyleBackup.unity";
        public const string RollbackManifestPath = BackupsFolder + "/OriginalGameScene_RollbackManifest.json";

        public static bool HasActiveManifest() => File.Exists(RollbackManifestPath);

        public static OriginalGameSceneRollbackManifest LoadManifest()
        {
            if (!File.Exists(RollbackManifestPath)) return null;
            try
            {
                return JsonUtility.FromJson<OriginalGameSceneRollbackManifest>(File.ReadAllText(RollbackManifestPath, Encoding.UTF8));
            }
            catch
            {
                return null;
            }
        }

        public static bool SaveStyleAsDefaultInOriginalGameScene(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            bool interactiveDialogs,
            out string report)
        {
            report = string.Empty;
            if (profile == null)
            {
                report = "Perfil nulo nao pode ser salvo como padrao da GameScene.";
                return false;
            }

            string canonicalScenePath = VisualStyleAuditService.CanonicalGameScenePath;
            if (!File.Exists(canonicalScenePath))
            {
                report = $"Cena canonica nao encontrada em '{canonicalScenePath}'.";
                return false;
            }

            // Garante bibliotecas e pipeline clonada antes de tocar na cena.
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();
            VisualMaterialLibrary library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            VisualStyleConversionManifest styleManifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            if (library == null || styleManifest == null ||
                !string.Equals(styleManifest.profileContentHash, profile.ComputeContentHash(), StringComparison.Ordinal))
            {
                var scan = VisualStyleAssetDiscoveryService.Scan(profile, ruleSet, styleManifest, VisualDiscoveryScope.GameplayReachable);
                VisualStyleMaterialFactory.GenerateOrUpdateLibrary(profile, ruleSet, scan);
                library = VisualStyleMaterialFactory.LoadLibrary(profile.styleId);
            }

            if (library == null)
            {
                report = $"Falha ao gerar/carregar VisualMaterialLibrary para '{profile.styleId}'.";
                return false;
            }

            var rpResult = VisualStyleUrpOutlineConfigurator.CreateOrUpdateVariantRenderPipeline(profile, library);
            if (rpResult == null || rpResult.rpAsset == null)
            {
                Debug.LogWarning($"[OriginalGameSceneStylePersistence] Aviso URP ao criar pipeline variante para '{profile.styleId}'.");
            }
            else
            {
                library.renderPipelineAsset = rpResult.rpAsset;
                library.renderPipelineRendererData = rpResult.rendererData;
                EditorUtility.SetDirty(library);
            }

            Scene currentlyOpenCanonical = SceneManager.GetSceneByPath(canonicalScenePath);
            if (currentlyOpenCanonical.IsValid() && currentlyOpenCanonical.isLoaded && currentlyOpenCanonical.isDirty)
            {
                if (interactiveDialogs && !Application.isBatchMode)
                {
                    if (!EditorSceneManager.SaveModifiedScenesIfUserWantsTo(new[] { currentlyOpenCanonical }))
                    {
                        report = "Operacao cancelada pelo usuario devido a alteracoes pendentes na GameScene.";
                        return false;
                    }
                }
            }

            EnsureBackupsFolder();

            OriginalGameSceneRollbackManifest manifest = LoadManifest();
            bool hadExistingManifest = manifest != null && !string.IsNullOrEmpty(manifest.preApplySceneSha256);

            if (!hadExistingManifest)
            {
                manifest = new OriginalGameSceneRollbackManifest
                {
                    appliedTimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"),
                    canonicalScenePath = canonicalScenePath,
                    backupSceneCopyPath = BackupSceneCopyPath,
                    preApplySceneSha256 = ComputeFileSha256(canonicalScenePath)
                };

                File.Copy(canonicalScenePath, BackupSceneCopyPath, overwrite: true);
                if (File.Exists(canonicalScenePath + ".meta") && !File.Exists(BackupSceneCopyPath + ".meta"))
                {
                    // Deixa Unity gerar um .meta novo proprio para o backup ao importar, nunca reutiliza GUID da cena canonica.
                }

                VisualStyleRuntimeConfig preCfg = VisualStylePresetSeeder.EnsureRuntimeConfig(null);
                if (preCfg != null)
                {
                    preCfg.EnsureMigrated();
                    manifest.previousGameplayRoute = (int)preCfg.gameplayRoute;
                    manifest.previousVisualMode = (int)preCfg.visualMode;
                    manifest.previousUseStyledGameplay = preCfg.useStyledGameplay;
                    manifest.previousActiveStyleId = preCfg.activeStyleId;
                    manifest.previousActiveGameplaySceneName = preCfg.activeGameplaySceneName;
                }
            }
            else
            {
                manifest.appliedTimestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            }

            manifest.styleId = profile.styleId;

            Scene prevActiveScene = SceneManager.GetActiveScene();
            bool openedAdditive = false;
            Scene targetScene = currentlyOpenCanonical;
            if (!targetScene.IsValid() || !targetScene.isLoaded)
            {
                targetScene = EditorSceneManager.OpenScene(canonicalScenePath, OpenSceneMode.Additive);
                openedAdditive = true;
            }

            try
            {
                VisualStyleManager manager = FindComponentInScene<VisualStyleManager>(targetScene);
                if (manager == null)
                {
                    var go = new GameObject(manifest.managedObjectName);
                    SceneManager.MoveGameObjectToScene(go, targetScene);
                    manager = go.AddComponent<VisualStyleManager>();
                    if (!hadExistingManifest)
                    {
                        manifest.createdBootstrapObject = true;
                    }
                }
                else if (!hadExistingManifest)
                {
                    manifest.previousProfileGuid = manager.activeProfile != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(manager.activeProfile))
                        : string.Empty;
                    manifest.previousLibraryGuid = manager.activeLibrary != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(manager.activeLibrary))
                        : string.Empty;
                }

                Undo.RecordObject(manager, "Save Visual Style as GameScene Default");
                manager.activeProfile = profile;
                manager.activeLibrary = library;
                manager.variantRendererData = rpResult?.rendererData;
                manager.variantRenderPipelineAsset = rpResult?.rpAsset;
                manager.applySceneLightingOnAwake = true;
                manager.applyToPreplacedRenderersOnAwake = true;
                EditorUtility.SetDirty(manager);

                TimeOfDayLightingDirector tod = FindComponentInScene<TimeOfDayLightingDirector>(targetScene);
                if (tod == null)
                {
                    tod = manager.gameObject.AddComponent<TimeOfDayLightingDirector>();
                    if (!hadExistingManifest)
                    {
                        manifest.addedTimeOfDayDirector = true;
                    }
                }
                EditorUtility.SetDirty(tod);

                EditorSceneManager.MarkSceneDirty(targetScene);
                if (!EditorSceneManager.SaveScene(targetScene))
                {
                    report = "Falha ao salvar a GameScene original.";
                    return false;
                }
            }
            finally
            {
                if (openedAdditive && targetScene.isLoaded)
                {
                    if (prevActiveScene.IsValid() && prevActiveScene.isLoaded)
                    {
                        SceneManager.SetActiveScene(prevActiveScene);
                    }
                    EditorSceneManager.CloseScene(targetScene, true);
                }
            }

            manifest.postApplySceneSha256 = ComputeFileSha256(canonicalScenePath);
            File.WriteAllText(RollbackManifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);

            VisualStyleRuntimeConfig cfg = VisualStylePresetSeeder.EnsureRuntimeConfig(profile);
            if (cfg != null)
            {
                Undo.RecordObject(cfg, "Set Persistent GameScene Style Config");
                cfg.gameplayRoute = GameplaySceneRoute.Original;
                cfg.visualMode = VisualApplicationMode.PersistentDefault;
                cfg.useStyledGameplay = true;
                cfg.activeStyleId = profile.styleId;
                cfg.activeGameplaySceneName = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
                cfg.activeProfile = profile;
                cfg.activeLibrary = library;
                EditorUtility.SetDirty(cfg);
                VisualStyleRuntimeConfig.ClearCache();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            report = $"Estilo '{profile.displayName}' salvo como padrao da GameScene original (bootstrap minimo + backup em '{BackupSceneCopyPath}').";
            return true;
        }

        public static bool DetectPostStyleConflicts(out string diffSummary)
        {
            diffSummary = string.Empty;
            OriginalGameSceneRollbackManifest manifest = LoadManifest();
            if (manifest == null) return false;

            string canonicalScenePath = VisualStyleAuditService.CanonicalGameScenePath;
            if (!File.Exists(canonicalScenePath)) return false;

            string currentSha = ComputeFileSha256(canonicalScenePath);
            if (string.Equals(currentSha, manifest.postApplySceneSha256, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            diffSummary =
                $"Conflito detectado em '{canonicalScenePath}':\n" +
                $"- SHA-256 apos aplicacao do estilo ({manifest.styleId}): {manifest.postApplySceneSha256}\n" +
                $"- SHA-256 atual no disco: {currentSha}\n\n" +
                "Edicoes legítimas de cena/gameplay foram feitas depois da gravacao do estilo.\n" +
                "A reversao seletiva remove apenas o bootstrap visual ('VisualStyleManager'/'TimeOfDayLightingDirector') preservando suas alteracoes posteriores.";
            return true;
        }

        public static bool RestoreOriginalGameSceneFromManifest(bool allowInteractiveDialog, out string report)
        {
            report = string.Empty;
            OriginalGameSceneRollbackManifest manifest = LoadManifest();
            if (manifest == null)
            {
                report = "Nenhum manifest de estilo persistido na GameScene original encontrado.";
                return true;
            }

            string canonicalScenePath = VisualStyleAuditService.CanonicalGameScenePath;
            if (!File.Exists(canonicalScenePath))
            {
                report = $"Cena canonica ausente: '{canonicalScenePath}'.";
                return false;
            }

            bool fullFileRollback = false;
            if (DetectPostStyleConflicts(out string diffSummary))
            {
                if (allowInteractiveDialog && !Application.isBatchMode)
                {
                    int choice = EditorUtility.DisplayDialogComplex(
                        "Edicoes posteriores detectadas na GameScene",
                        diffSummary,
                        "Reverter Apenas Estilo (Preservar Edicoes)",
                        "Cancelar",
                        "Restaurar Backup Completo (Sobrescrever)");
                    if (choice == 1)
                    {
                        report = "Reversao cancelada pelo usuario para resolucao manual de conflito.";
                        return false;
                    }
                    fullFileRollback = choice == 2;
                }
            }

            if (fullFileRollback && File.Exists(manifest.backupSceneCopyPath))
            {
                File.Copy(manifest.backupSceneCopyPath, canonicalScenePath, overwrite: true);
            }
            else
            {
                Scene prevActiveScene = SceneManager.GetActiveScene();
                Scene openCanonical = SceneManager.GetSceneByPath(canonicalScenePath);
                bool openedTemp = false;
                Scene targetScene = openCanonical;
                if (!targetScene.IsValid() || !targetScene.isLoaded)
                {
                    targetScene = EditorSceneManager.OpenScene(canonicalScenePath, OpenSceneMode.Additive);
                    openedTemp = true;
                }

                try
                {
                    VisualStyleManager manager = FindComponentInScene<VisualStyleManager>(targetScene);
                    if (manager != null)
                    {
                        if (manifest.createdBootstrapObject)
                        {
                            Undo.DestroyObjectImmediate(manager.gameObject);
                        }
                        else
                        {
                            Undo.RecordObject(manager, "Revert VisualStyleManager in GameScene");
                            manager.activeProfile = !string.IsNullOrEmpty(manifest.previousProfileGuid)
                                ? AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(AssetDatabase.GUIDToAssetPath(manifest.previousProfileGuid))
                                : null;
                            manager.activeLibrary = !string.IsNullOrEmpty(manifest.previousLibraryGuid)
                                ? AssetDatabase.LoadAssetAtPath<VisualMaterialLibrary>(AssetDatabase.GUIDToAssetPath(manifest.previousLibraryGuid))
                                : null;
                            EditorUtility.SetDirty(manager);
                        }
                    }

                    if (manifest.addedTimeOfDayDirector)
                    {
                        TimeOfDayLightingDirector tod = FindComponentInScene<TimeOfDayLightingDirector>(targetScene);
                        if (tod != null)
                        {
                            Undo.DestroyObjectImmediate(tod);
                        }
                    }

                    EditorSceneManager.MarkSceneDirty(targetScene);
                    EditorSceneManager.SaveScene(targetScene);
                }
                finally
                {
                    if (openedTemp && targetScene.isLoaded)
                    {
                        if (prevActiveScene.IsValid() && prevActiveScene.isLoaded)
                        {
                            SceneManager.SetActiveScene(prevActiveScene);
                        }
                        EditorSceneManager.CloseScene(targetScene, true);
                    }
                }
            }

            VisualStyleRuntimeConfig cfg = VisualStylePresetSeeder.EnsureRuntimeConfig(null);
            if (cfg != null)
            {
                cfg.gameplayRoute = (GameplaySceneRoute)manifest.previousGameplayRoute;
                cfg.visualMode = (VisualApplicationMode)manifest.previousVisualMode;
                cfg.useStyledGameplay = false;
                cfg.activeStyleId = string.Empty;
                cfg.activeGameplaySceneName = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
                cfg.activeProfile = null;
                cfg.activeLibrary = null;
                EditorUtility.SetDirty(cfg);
                VisualStyleRuntimeConfig.ClearCache();
            }

            if (File.Exists(RollbackManifestPath))
            {
                File.Delete(RollbackManifestPath);
                if (File.Exists(RollbackManifestPath + ".meta"))
                {
                    File.Delete(RollbackManifestPath + ".meta");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report = "GameScene original revertida seletivamente sem perder alteracoes posteriores de gameplay.";
            return true;
        }

        private static T FindComponentInScene<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                T found = roots[i].GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureBackupsFolder()
        {
            if (!Directory.Exists(BackupsFolder))
            {
                Directory.CreateDirectory(BackupsFolder);
            }
        }

        private static string ComputeFileSha256(string path)
        {
            using var sha = SHA256.Create();
            using FileStream fs = File.OpenRead(path);
            byte[] hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }
    }
}
#endif
