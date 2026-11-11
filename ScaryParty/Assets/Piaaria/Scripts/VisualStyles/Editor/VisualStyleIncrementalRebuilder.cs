#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    public sealed class VisualStyleValidationReport
    {
        public bool isValid;
        public int materialsToGenerate;
        public int materialsToUpdate;
        public int slotsPreserved;
        public int manualReviewCount;
        public int warningCount;
        public int errorCount;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    /// <summary>
    /// Orchestrates validation, staged diffing, incremental sync of newly imported assets,
    /// and user promotion of generated materials to custom ownership.
    /// </summary>
    public static class VisualStyleIncrementalRebuilder
    {
        public static VisualStyleValidationReport ValidateStyle(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            DiscoveryScanResult scan)
        {
            var report = new VisualStyleValidationReport();
            if (profile == null)
            {
                report.errors.Add("Nenhum VisualStyleProfile selecionado.");
            }
            if (ruleSet == null)
            {
                report.errors.Add("VisualConversionRuleSet ausente.");
            }

            string[] requiredShaders =
            {
                VisualStyleShaderUtility.ShaderOpaque,
                VisualStyleShaderUtility.ShaderSimple,
                VisualStyleShaderUtility.ShaderTransparent,
                VisualStyleShaderUtility.ShaderAlphaClipped,
                VisualStyleShaderUtility.ShaderOutline
            };
            for (int i = 0; i < requiredShaders.Length; i++)
            {
                if (Shader.Find(requiredShaders[i]) == null)
                {
                    report.errors.Add($"Shader Distant Lands ausente: {requiredShaders[i]}");
                }
            }

            if (profile != null && (profile.lighting.lightSteps < 1 || profile.lighting.lightSteps > 6))
            {
                report.warnings.Add($"LightSteps={profile.lighting.lightSteps} fora da faixa recomendada (1..6).");
            }

            if (scan != null)
            {
                for (int i = 0; i < scan.targets.Count; i++)
                {
                    var t = scan.targets[i];
                    if (t.sourceMaterial == null || t.sourceMaterial.shader == null ||
                        t.sourceMaterial.shader.name == "Hidden/InternalErrorShader")
                    {
                        report.errors.Add($"Material quebrado/erro em '{t.sourceAssetPath}' ({t.descriptor.materialName}).");
                        continue;
                    }

                    if (t.decision.action == VisualConversionAction.Preserve || t.decision.action == VisualConversionAction.Skip)
                    {
                        report.slotsPreserved += t.usageContexts.Count;
                    }
                    else if (t.decision.action == VisualConversionAction.ManualReview)
                    {
                        report.manualReviewCount++;
                        report.warnings.Add($"Pendente revisao manual: {t.descriptor.materialName} ({t.decision.reason})");
                    }
                    else if (t.incrementalStatus == VisualAssetSyncStatus.Unprocessed)
                    {
                        report.materialsToGenerate++;
                    }
                    else if (t.incrementalStatus == VisualAssetSyncStatus.SourceModified ||
                             t.incrementalStatus == VisualAssetSyncStatus.ProfileOutdated)
                    {
                        report.materialsToUpdate++;
                    }
                }

                if (scan.orphanedEntries.Count > 0)
                {
                    report.warnings.Add($"{scan.orphanedEntries.Count} entradas orfas no manifesto (nao serao apagadas automaticamente).");
                }
            }

            report.warningCount = report.warnings.Count;
            report.errorCount = report.errors.Count;
            report.isValid = report.errorCount == 0;
            return report;
        }

        public static MaterialGenerationReport SyncPendingAndChangedAssets(
            VisualStyleProfile profile,
            VisualConversionRuleSet ruleSet,
            VisualDiscoveryScope fallbackScope = VisualDiscoveryScope.GameplayReachable)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var manifest = VisualStyleMaterialFactory.LoadManifest(profile.styleId);
            var pending = VisualStyleImportWatcher.GetPendingRecords();

            DiscoveryScanResult scan;
            var processedPaths = new List<string>();

            if (pending.Count > 0)
            {
                var pendingObjects = new List<UnityEngine.Object>();
                for (int i = 0; i < pending.Count; i++)
                {
                    processedPaths.Add(pending[i].assetPath);
                    if (pending[i].changeType == "Deleted") continue;
                    var obj = AssetDatabase.LoadMainAssetAtPath(pending[i].assetPath);
                    if (obj != null) pendingObjects.Add(obj);
                }

                scan = VisualStyleAssetDiscoveryService.Scan(
                    profile, ruleSet, manifest,
                    VisualDiscoveryScope.CurrentSelection,
                    currentSelection: pendingObjects);
            }
            else
            {
                scan = VisualStyleAssetDiscoveryService.Scan(profile, ruleSet, manifest, fallbackScope);
            }

            var report = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(profile, ruleSet, scan, onlyDirtyOrNew: true);
            if (report.errorCount == 0 && processedPaths.Count > 0)
            {
                VisualStyleImportWatcher.RemovePaths(processedPaths);
            }
            return report;
        }

        public static bool PromoteToCustomMaterial(string styleId, string conversionKey)
        {
            var manifest = VisualStyleMaterialFactory.LoadManifest(styleId);
            if (manifest == null) return false;
            var entry = manifest.FindByKey(conversionKey);
            if (entry == null) return false;

            entry.isPromotedCustomMaterial = true;
            entry.status = VisualAssetSyncStatus.PromotedCustom;
            EditorUtility.SetDirty(manifest);
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
#endif
