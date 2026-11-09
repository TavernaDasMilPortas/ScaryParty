#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.EditorScripts
{
    /// <summary>
    /// Configura automaticamente os ModelImporters para os FBXs do personagem gerados a partir do Blender:
    /// - Character.fbx: Rig Generic, gera Avatar canônico, sem animações, bakeAxisConversion ativo.
    /// - Character_*.fbx: Rig Generic, copia Avatar do Character.fbx, looping in-place sem root motion.
    /// </summary>
    public class CharacterModelPostprocessor : AssetPostprocessor
    {
        private const string GeneratedFolder = "Assets/Piaaria/Art/Characters/Generated";
        private const string CanonicalFbxPath = "Assets/Piaaria/Art/Characters/Generated/Character.fbx";

        private void OnPreprocessModel()
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            string normalizedPath = assetPath.Replace('\\', '/');
            if (!normalizedPath.StartsWith(GeneratedFolder, StringComparison.OrdinalIgnoreCase)) return;

            var modelImporter = (ModelImporter)assetImporter;
            if (modelImporter == null) return;

            modelImporter.globalScale = 1.0f;
            modelImporter.useFileScale = true;
            modelImporter.bakeAxisConversion = true;
            modelImporter.animationType = ModelImporterAnimationType.Generic;
            modelImporter.importCameras = false;
            modelImporter.importLights = false;

            bool isCanonical = normalizedPath.EndsWith("Character.fbx", StringComparison.OrdinalIgnoreCase);

            if (isCanonical)
            {
                modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                modelImporter.importAnimation = false;
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            }
            else
            {
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                modelImporter.importAnimation = true;

                var canonicalAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(CanonicalFbxPath);
                if (canonicalAvatar != null)
                {
                    modelImporter.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    modelImporter.sourceAvatar = canonicalAvatar;
                }
                else
                {
                    modelImporter.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }

                // Configura clipping e looping
                string fileName = Path.GetFileNameWithoutExtension(normalizedPath);
                string clipName = fileName.StartsWith("Character_") ? fileName.Substring("Character_".Length) : fileName;
                bool shouldLoop = !string.Equals(clipName, "SitDown", StringComparison.OrdinalIgnoreCase) &&
                                  !string.Equals(clipName, "StandUp", StringComparison.OrdinalIgnoreCase);

                var defaultClips = modelImporter.defaultClipAnimations;
                if (defaultClips != null && defaultClips.Length > 0)
                {
                    var clip = defaultClips[0];
                    clip.name = clipName;
                    clip.loopTime = shouldLoop;
                    clip.loopPose = shouldLoop;
                    clip.lockRootRotation = false;
                    clip.lockRootHeightY = false;
                    clip.lockRootPositionXZ = false;
                    clip.keepOriginalOrientation = true;
                    clip.keepOriginalPositionY = true;
                    clip.keepOriginalPositionXZ = true;
                    clip.maskType = ClipAnimationMaskType.CreateFromThisModel;
                    modelImporter.clipAnimations = new[] { clip };
                }
            }
        }
    }
}
#endif
