#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor.Tests
{
    public class VisualStylePhase4Tests
    {
        private const string TempFixtureFolder = "Assets/Piaaria/Tests/_TempVisualStyleFixtures";
        private VisualStyleProfile _profile;
        private VisualConversionRuleSet _ruleSet;

        [SetUp]
        public void SetUp()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            VisualStylePresetSeeder.EnsureFolder(TempFixtureFolder);
            VisualStyleImportWatcher.ClearQueue();

            _ruleSet = ScriptableObject.CreateInstance<VisualConversionRuleSet>();
            VisualConversionRuleSet.PopulateDefaults(_ruleSet);

            _profile = ScriptableObject.CreateInstance<VisualStyleProfile>();
            _profile.ApplyPresetDefaults(VisualStylePresetType.SoftToonVibrant);
            _profile.styleId = "test_phase4_idempotency";
            _profile.displayName = "Test Phase4";
            _profile.discovery.conversionRuleSet = _ruleSet;
        }

        [TearDown]
        public void TearDown()
        {
            VisualStyleImportWatcher.ClearQueue();

            string genFolder = VisualStyleMaterialFactory.GetStyleGeneratedFolder("test_phase4_idempotency");
            if (AssetDatabase.IsValidFolder(genFolder))
            {
                AssetDatabase.DeleteAsset(genFolder);
            }
            if (AssetDatabase.IsValidFolder(TempFixtureFolder))
            {
                AssetDatabase.DeleteAsset(TempFixtureFolder);
            }

            if (_profile != null) Object.DestroyImmediate(_profile);
            if (_ruleSet != null) Object.DestroyImmediate(_ruleSet);
            AssetDatabase.Refresh();
        }

        [Test]
        public void GenerateOrUpdateLibrary_SecondRunIsIdempotent_AndPreservesGuidsAndTextures()
        {
            // Arrange temporary texture + textured alpha-clipped material + prefab
            var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            string texPath = $"{TempFixtureFolder}/Tex_TestPizza.png";
            File.WriteAllBytes(texPath, png);
            AssetDatabase.ImportAsset(texPath);
            Texture2D importedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var srcMat = new Material(urpLit) { name = "Mat_PizzaSauceTest" };
            if (srcMat.HasProperty("_BaseMap")) srcMat.SetTexture("_BaseMap", importedTex);
            if (srcMat.HasProperty("_BaseColor")) srcMat.SetColor("_BaseColor", new Color(0.8f, 0.2f, 0.1f, 1f));
            srcMat.SetTextureScale(srcMat.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex", new Vector2(2f, 3f));

            string srcMatPath = $"{TempFixtureFolder}/Mat_PizzaSauceTest.mat";
            AssetDatabase.CreateAsset(srcMat, srcMatPath);
            AssetDatabase.SaveAssets();

            string srcMatGuidBefore = AssetDatabase.AssetPathToGUID(srcMatPath);
            var scan1 = VisualStyleAssetDiscoveryService.Scan(_profile, _ruleSet, null, VisualDiscoveryScope.SelectedFolders, new[] { TempFixtureFolder });

            // Act 1
            var rep1 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_profile, _ruleSet, scan1);
            Assert.AreEqual(0, rep1.errorCount, string.Join(";", rep1.errors));
            Assert.Greater(rep1.createdCount, 0);

            var lib1 = VisualStyleMaterialFactory.LoadLibrary(_profile.styleId);
            Assert.IsNotNull(lib1);
            Assert.IsTrue(lib1.TryGetMappedMaterial(srcMat, out MappedMaterialEntry mapped1));
            Assert.IsNotNull(mapped1.targetMaterial);
            Assert.AreEqual(VisualStyleShaderUtility.ShaderOpaque, mapped1.targetMaterial.shader.name);
            Assert.AreEqual(importedTex, mapped1.targetMaterial.GetTexture("_Texture"));
            Assert.AreEqual(new Vector2(2f, 3f), mapped1.targetMaterial.GetTextureScale("_Texture"));
            string genGuidRun1 = mapped1.targetMaterialGuid;

            // Act 2: Re-scan and re-run with NO changes
            var manifest1 = VisualStyleMaterialFactory.LoadManifest(_profile.styleId);
            var scan2 = VisualStyleAssetDiscoveryService.Scan(_profile, _ruleSet, manifest1, VisualDiscoveryScope.SelectedFolders, new[] { TempFixtureFolder });
            var rep2 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_profile, _ruleSet, scan2, onlyDirtyOrNew: true);

            // Assert idempotency
            Assert.AreEqual(0, rep2.createdCount, "Second run must not create extra materials.");
            Assert.AreEqual(0, rep2.updatedCount, "Second run with no changes must not overwrite existing materials.");
            Assert.AreEqual(srcMatGuidBefore, AssetDatabase.AssetPathToGUID(srcMatPath), "Original material GUID must never change.");

            var lib2 = VisualStyleMaterialFactory.LoadLibrary(_profile.styleId);
            Assert.IsTrue(lib2.TryGetMappedMaterial(srcMat, out MappedMaterialEntry mapped2));
            Assert.AreEqual(genGuidRun1, mapped2.targetMaterialGuid, "Generated material GUID must remain identical across runs.");
        }

        [Test]
        public void IncrementalRebuilder_DetectsSourceModification_UnchangedRemainSkipped_AndOrphansTracked()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var matA = new Material(urpLit) { name = "Mat_PizzaItemA" };
            var matB = new Material(urpLit) { name = "Mat_PizzaItemB" };
            string pathA = $"{TempFixtureFolder}/Mat_PizzaItemA.mat";
            string pathB = $"{TempFixtureFolder}/Mat_PizzaItemB.mat";
            AssetDatabase.CreateAsset(matA, pathA);
            AssetDatabase.CreateAsset(matB, pathB);
            AssetDatabase.SaveAssets();

            var scan1 = VisualStyleAssetDiscoveryService.Scan(_profile, _ruleSet, null, VisualDiscoveryScope.SelectedFolders, new[] { TempFixtureFolder });
            VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_profile, _ruleSet, scan1);

            // Mutate only Material A
            matA.SetColor("_BaseColor", new Color(0.1f, 0.9f, 0.2f, 1f));
            EditorUtility.SetDirty(matA);
            AssetDatabase.SaveAssets();

            var manifest = VisualStyleMaterialFactory.LoadManifest(_profile.styleId);
            var scan2 = VisualStyleAssetDiscoveryService.Scan(_profile, _ruleSet, manifest, VisualDiscoveryScope.SelectedFolders, new[] { TempFixtureFolder });

            int modCount = scan2.CountByStatus(VisualAssetSyncStatus.SourceModified);
            Assert.AreEqual(1, modCount, "Only the modified source material should enter SourceModified.");

            var rep2 = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_profile, _ruleSet, scan2, onlyDirtyOrNew: true);
            Assert.AreEqual(1, rep2.updatedCount, "Only the single dirty dependency should rebuild.");

            // Import watcher auto-tracked dirty assets and sync clears queue cleanly
            Assert.GreaterOrEqual(VisualStyleImportWatcher.GetPendingCount(), 1, "Import watcher should auto-record newly created/modified source assets.");
            VisualStyleImportWatcher.ClearQueue();
            VisualStyleImportWatcher.RecordPendingPath(pathB, "ImportedOrModified");
            Assert.AreEqual(1, VisualStyleImportWatcher.GetPendingCount());
            VisualStyleIncrementalRebuilder.SyncPendingAndChangedAssets(_profile, _ruleSet, VisualDiscoveryScope.SelectedFolders);
            Assert.AreEqual(0, VisualStyleImportWatcher.GetPendingCount(), "Sync should clear handled pending imports.");

            // Delete source Material B and scan Full/Selected with orphan detection (via Scan with CurrentSelection or SelectedFolders won't delete generated mat)
            var libBeforeDelete = VisualStyleMaterialFactory.LoadLibrary(_profile.styleId);
            Assert.IsTrue(libBeforeDelete.TryGetMappedMaterial(matB, out var entryB));
            string genPathB = AssetDatabase.GetAssetPath(entryB.targetMaterial);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(genPathB));

            AssetDatabase.DeleteAsset(pathB);
            var scanOrphan = VisualStyleAssetDiscoveryService.Scan(_profile, _ruleSet, manifest, VisualDiscoveryScope.GameplayReachable);
            Assert.IsTrue(scanOrphan.orphanedEntries.Exists(e => e.sourceMaterialName == "Mat_PizzaItemB"), "Deleted asset must be flagged as Orphaned.");
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Material>(genPathB), "Orphan detection must NOT auto-delete generated materials.");
        }
    }
}
#endif
