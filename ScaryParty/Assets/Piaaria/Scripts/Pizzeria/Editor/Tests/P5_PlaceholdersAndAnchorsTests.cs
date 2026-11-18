using NUnit.Framework;
using ScaryParty.City;
using ScaryParty.Combat.Monsters;
using ScaryParty.Editor.Playtest;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Stations;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class P5_PlaceholdersAndAnchorsTests
    {
        [Test]
        public void PlaytestReferenceManifest_HasAllPersistentPlaceholders()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(
                PlaytestInstallerAndValidator.ManifestAssetPath);

            Assert.IsNotNull(manifest, "PlaytestReferenceManifest.asset precisa existir.");
            Assert.IsNotNull(manifest.monsterVisualCatalog, "Manifesto deve apontar para MonsterVisualCatalog.");
            Assert.IsNotNull(manifest.cashierVisualPrefab, "Manifesto deve apontar para visual do Caixa.");
            Assert.IsNotNull(manifest.pizzaioloVisualPrefab, "Manifesto deve apontar para visual do Pizzaiolo.");
            Assert.IsNotNull(manifest.motorcycleVisualPrefab, "Manifesto deve apontar para visual da Moto.");
            Assert.IsNotNull(manifest.supplierTerminalVisualPrefab, "Manifesto deve apontar para visual do Terminal.");
            Assert.IsNotNull(manifest.receivingZoneVisualPrefab, "Manifesto deve apontar para visual de Recebimento.");
            Assert.IsNotNull(manifest.managementBoardVisualPrefab, "Manifesto deve apontar para visual do Quadro.");
            Assert.GreaterOrEqual(manifest.placeholders.Count, 12, "Manifesto deve catalogar os placeholders da P5.");
        }

        [Test]
        public void MonsterVisualCatalog_ResolvesAllSpecies_WithUrpMaterialsAndNoRevealingLabels()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(
                PlaytestInstallerAndValidator.ManifestAssetPath);
            Assert.IsNotNull(manifest);
            var catalog = manifest.monsterVisualCatalog as MonsterVisualCatalog;
            Assert.IsNotNull(catalog, "monsterVisualCatalog deve ser do tipo MonsterVisualCatalog.");

            var speciesList = new[]
            {
                NpcSpecies.Vampire,
                NpcSpecies.Werewolf,
                NpcSpecies.Ghost,
                NpcSpecies.Zombie
            };

            foreach (var species in speciesList)
            {
                Assert.IsTrue(
                    catalog.TryGetEntry(species, out var entry),
                    $"Catálogo deve conter entrada para {species}.");
                Assert.IsNotNull(entry.visualPrefab, $"Prefab visual ausente para {species}.");

                foreach (var rend in entry.visualPrefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var mat in rend.sharedMaterials)
                    {
                        if (mat == null) continue;
                        Assert.AreNotEqual(
                            "Standard",
                            mat.shader != null ? mat.shader.name : string.Empty,
                            $"Material em {entry.visualPrefab.name} não deve usar shader Standard em URP.");
                    }
                }

                foreach (var comp in entry.visualPrefab.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    bool isLabel = comp is TextMesh ||
                                   comp.GetType().Name.Contains("TextMeshPro") ||
                                   comp.GetType().Name.Contains("TMP_Text");
                    Assert.IsFalse(isLabel, $"{entry.visualPrefab.name} não deve conter label de texto revelando espécie no modo normal.");
                }
            }
        }

        [Test]
        public void MotorcycleVisualPrefab_HasExplicitEntityVisualAnchorRig()
        {
            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(
                PlaytestInstallerAndValidator.ManifestAssetPath);
            Assert.IsNotNull(manifest);
            Assert.IsNotNull(manifest.motorcycleVisualPrefab);

            var rig = manifest.motorcycleVisualPrefab.GetComponentInChildren<EntityVisualAnchorRig>(true);
            Assert.IsNotNull(rig, "Moto precisa de EntityVisualAnchorRig explícito.");
            Assert.IsNotNull(rig.seatAnchor, "Moto precisa de seatAnchor.");
            Assert.IsNotNull(rig.handlebarLeftAnchor, "Moto precisa de handlebarLeftAnchor.");
            Assert.IsNotNull(rig.handlebarRightAnchor, "Moto precisa de handlebarRightAnchor.");
            Assert.IsNotNull(rig.leftDismountAnchor, "Moto precisa de leftDismountAnchor.");
            Assert.IsNotNull(rig.rightDismountAnchor, "Moto precisa de rightDismountAnchor.");
            Assert.IsNotNull(rig.cargoAnchor, "Moto precisa de cargoAnchor.");
        }

        [Test]
        public void PizzeriaConfigAndPrefab_IncludeGarlicSupplyFourthSlotAndKitchenStations()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PlaytestInstallerAndValidator.PizzeriaConfigPath);
            Assert.IsNotNull(cfg, "PizzeriaConfig_PizzeriaModular_Manual.asset não encontrado.");

            IngredientDef garlic = null;
            foreach (var ing in cfg.ingredients)
            {
                if (ing != null && ing.id == 10)
                {
                    garlic = ing;
                    break;
                }
            }
            Assert.IsNotNull(garlic, "Ingrediente Alho (id=10) precisa estar registrado em PizzeriaConfig.");
            Assert.IsNotNull(garlic.placeholderPrefab, "Alho (id=10) precisa de placeholderPrefab persistente.");

            SupplyItemDef garlicSupply = null;
            foreach (var sup in cfg.supplyItems)
            {
                if (sup != null && sup.id == 5)
                {
                    garlicSupply = sup;
                    break;
                }
            }
            Assert.IsNotNull(garlicSupply, "Pacote de suprimento de Alho (id=5) precisa estar em PizzeriaConfig.");
            Assert.AreEqual(10, garlicSupply.ingredientId);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlaytestInstallerAndValidator.PizzeriaPrefabPath);
            Assert.IsNotNull(prefab, "PizzeriaModular_Manual.prefab não encontrado.");

            var board = prefab.GetComponentInChildren<ManagementBoardStation>(true);
            Assert.IsNotNull(board, "Quadro físico ManagementBoardStation deve estar instalado em PizzeriaModular_Manual.prefab.");

            PackagingStation packaging = null;
            bool foundReceiving = false;
            bool foundSupplier = false;

            foreach (var sv in prefab.GetComponentsInChildren<StationView>(true))
            {
                Assert.GreaterOrEqual(
                    sv.GetComponentsInChildren<Renderer>(true).Length,
                    1,
                    $"Estação {sv.name} (id={sv.stationId}) precisa ter pelo menos 1 Renderer.");

                if (sv.stationId == 9 && sv is PackagingStation pkg) packaging = pkg;
                if (sv.stationId == 14) foundReceiving = true;
                if (sv.stationId == 20) foundSupplier = true;
            }

            Assert.IsTrue(foundReceiving, "Station_Receiving (id=14) precisa existir no prefab.");
            Assert.IsTrue(foundSupplier, "Station_SupplierTerminal (id=20) precisa existir no prefab.");
            Assert.IsNotNull(packaging, "PackagingStation (id=9) precisa existir no prefab.");
            Assert.IsNotNull(packaging.slotAnchors);
            Assert.GreaterOrEqual(packaging.slotAnchors.Length, 4, "PackagingStation (id=9) precisa ter 4 slotAnchors.");
        }

        [Test]
        public void CityBuilding_EnsurePortal_CreatesRooftopPerchAndDropAnchors()
        {
            var go = new GameObject("TempCityBuilding_P5Test");
            try
            {
                var box = go.AddComponent<BoxCollider>();
                box.size = new Vector3(10f, 8f, 10f);
                box.center = new Vector3(0f, 4f, 0f);

                var building = go.AddComponent<CityBuilding>();
                building.EnsurePortal();

                Assert.IsNotNull(building.rooftopPerchAnchor, "rooftopPerchAnchor deve ser criado por EnsurePortal.");
                Assert.IsNotNull(building.rooftopDropAnchor, "rooftopDropAnchor deve ser criado por EnsurePortal.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
