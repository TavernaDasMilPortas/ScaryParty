using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    [Serializable]
    public class ConversionManifestEntry
    {
        public string conversionKey;
        public string sourceAssetPath;
        public string sourceAssetGuid;
        public string sourceMaterialGuid;
        public long sourceLocalFileId;
        public string sourceMaterialName;
        public string sourceShaderName;
        public string sourceDependencyHash;
        public string sourceTextureGuid;

        public string targetMaterialAssetPath;
        public string targetMaterialGuid;
        public string targetShaderName;

        public string categoryId;
        public VisualSemanticRole semanticRole;
        public IllustrateShaderFamily shaderFamily;
        public VisualConversionAction action;
        public VisualAssetSyncStatus status;
        public float confidence;
        public string classificationReason;
        public string warnings;
        public bool isPromotedCustomMaterial;

        public List<string> referencingAssets = new List<string>();
    }

    [Serializable]
    public class VisualStyleRollbackFieldSnapshot
    {
        public string scenePath;
        public string objectHierarchyPath;
        public string componentTypeName;
        public string propertyPath;
        public string previousObjectAssetGuid;
        public long previousObjectLocalFileId;
    }

    [Serializable]
    public class VisualStyleRollbackSnapshot
    {
        public string capturedAtUtc;
        public string originalGameplayScenePath;
        public string variantGameplayScenePath;
        public string previousActiveSceneBootName = "GameScene";
        public bool hadVariantInBuildSettings;
        public List<VisualStyleRollbackFieldSnapshot> fieldSnapshots = new List<VisualStyleRollbackFieldSnapshot>();
    }

    /// <summary>
    /// Persistent generation and incremental dependency manifest linking source assets to generated
    /// Distant Lands materials, tracking hashes, orphan states, and gameplay rollback snapshots.
    /// </summary>
    [CreateAssetMenu(fileName = "GenerationManifest", menuName = "Scary Party/Visual Styles/Conversion Manifest")]
    public class VisualStyleConversionManifest : ScriptableObject
    {
        public int manifestVersion = 2;
        public string styleId;
        public string styleName;
        public string profileContentHash;
        public string ruleSetHash;
        public string unityVersion;
        public string baseGitSha;
        public string lastGeneratedUtc;
        public string lastAppliedUtc;
        public bool lastValidationPassed;

        public List<ConversionManifestEntry> entries = new List<ConversionManifestEntry>();
        public VisualStyleRollbackSnapshot rollbackSnapshot = new VisualStyleRollbackSnapshot();

        public ConversionManifestEntry FindByKey(string conversionKey)
        {
            if (string.IsNullOrEmpty(conversionKey) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].conversionKey == conversionKey)
                    return entries[i];
            }
            return null;
        }

        public ConversionManifestEntry FindBySourceMaterial(string sourceMaterialGuid, long localFileId)
        {
            if (string.IsNullOrEmpty(sourceMaterialGuid) || entries == null) return null;
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && e.sourceMaterialGuid == sourceMaterialGuid && e.sourceLocalFileId == localFileId)
                    return e;
            }
            return null;
        }

        public void UpsertEntry(ConversionManifestEntry entry)
        {
            if (entry == null) return;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].conversionKey == entry.conversionKey)
                {
                    entries[i] = entry;
                    return;
                }
            }
            entries.Add(entry);
        }
    }
}
