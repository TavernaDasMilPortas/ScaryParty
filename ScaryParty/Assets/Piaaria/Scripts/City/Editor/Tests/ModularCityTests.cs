using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using ScaryParty.City.Modular;
using ScaryParty.City.Editor.Modular;

namespace ScaryParty.City.Editor.Tests
{
    [TestFixture]
    public class ModularCityTests
    {
        private CityModuleCatalog _catalog;

        [OneTimeSetUp]
        public void Setup()
        {
            // Garante que o kit completo esteja construído
            _catalog = ModularCityKitBuilder.BuildCompleteKit(false);
            Assert.IsNotNull(_catalog, "Catálogo de módulos não pôde ser carregado/construído.");
        }

        [Test]
        public void Test01_AllTwelveModulesPresentInCatalog()
        {
            Assert.AreEqual(12, Enum.GetValues(typeof(CityModuleId)).Length);
            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                CityModuleDefinition def = _catalog.GetDefinition(id);
                Assert.IsNotNull(def, $"Módulo {id} ausente no catálogo.");
                Assert.IsNotNull(def.prefab, $"Prefab do módulo {id} ausente.");
                Assert.AreEqual(id, def.id);
            }
        }

        [Test]
        public void Test02_CanonicalDimensionsVerification()
        {
            // A: 2 x 3 x 0.2
            var defA = _catalog.GetDefinition(CityModuleId.A);
            Assert.AreEqual(2.0f, defA.width, 0.01f);
            Assert.AreEqual(3.0f, defA.height, 0.01f);
            Assert.AreEqual(0.2f, defA.depth, 0.01f);
            Assert.AreEqual(1, defA.occupiedSlots);

            // E: 2 x 0.2 x 2
            var defE = _catalog.GetDefinition(CityModuleId.E);
            Assert.AreEqual(2.0f, defE.width, 0.01f);
            Assert.AreEqual(0.2f, defE.height, 0.01f);
            Assert.AreEqual(2.0f, defE.depth, 0.01f);
            Assert.IsTrue(defE.isRoof);

            // F: 2 x 0.4 x 0.2
            var defF = _catalog.GetDefinition(CityModuleId.F);
            Assert.AreEqual(2.0f, defF.width, 0.01f);
            Assert.AreEqual(0.4f, defF.height, 0.01f);
            Assert.AreEqual(0.2f, defF.depth, 0.01f);
            Assert.IsTrue(defF.isParapet);

            // I: 4 x 3 x 0.2, 2 slots
            var defI = _catalog.GetDefinition(CityModuleId.I);
            Assert.AreEqual(4.0f, defI.width, 0.01f);
            Assert.AreEqual(3.0f, defI.height, 0.01f);
            Assert.AreEqual(2, defI.occupiedSlots, "Garagem I DEVE ocupar exatamente 2 slots de 2m (4m).");
            Assert.IsTrue(defI.isGarage);

            // K: 2 x 0.4 x 0.8
            var defK = _catalog.GetDefinition(CityModuleId.K);
            Assert.AreEqual(2.0f, defK.width, 0.01f);
            Assert.AreEqual(0.4f, defK.height, 0.01f);
            Assert.AreEqual(0.8f, defK.depth, 0.01f);
            Assert.AreEqual(0.8f, defK.exteriorProtrusion, 0.01f);
            Assert.IsTrue(defK.isAwning);

            // L: 0.2 x 3 x 0.2
            var defL = _catalog.GetDefinition(CityModuleId.L);
            Assert.AreEqual(0.2f, defL.width, 0.01f);
            Assert.AreEqual(3.0f, defL.height, 0.01f);
            Assert.AreEqual(0.2f, defL.depth, 0.01f);
            Assert.AreEqual(0, defL.occupiedSlots, "Pilar L é acabamento de canto e não consome slots horizontais.");
            Assert.IsTrue(defL.isCornerPillar);
        }

        [Test]
        public void Test03_DoorPassageHasNoBlockingCollider()
        {
            // Valida que C e J possuem vão livre desobstruído
            var res = ModularCityValidator.ValidateKit(_catalog);
            Assert.IsTrue(res.isValid, $"Validação do kit falhou:\n{string.Join("\n", res.errors)}");

            // Validação explícita no prefab de C
            var defC = _catalog.GetDefinition(CityModuleId.C);
            Assert.IsTrue(defC.isEntrance);
            Transform movingPartsC = defC.prefab.transform.Find("MovingParts");
            BoxCollider[] colsC = defC.prefab.GetComponentsInChildren<BoxCollider>();
            foreach (var col in colsC)
            {
                if (col.isTrigger) continue;
                if (movingPartsC != null && col.transform.IsChildOf(movingPartsC)) continue;

                // Centro do vão (0, 1.0, -0.1) não pode estar contido no collider
                Vector3 passageCenter = new Vector3(0f, 1.0f, -0.1f);
                Vector3 localInCol = col.transform.InverseTransformPoint(defC.prefab.transform.TransformPoint(passageCenter));
                Bounds b = new Bounds(col.center, col.size);
                Assert.IsFalse(b.Contains(localInCol), "Collider de batente não pode invadir o centro do vão livre da porta C!");
            }

            // Validação explícita no prefab de J
            var defJ = _catalog.GetDefinition(CityModuleId.J);
            Assert.IsTrue(defJ.isEntrance);
            Transform movingPartsJ = defJ.prefab.transform.Find("MovingParts");
            BoxCollider[] colsJ = defJ.prefab.GetComponentsInChildren<BoxCollider>();
            foreach (var col in colsJ)
            {
                if (col.isTrigger) continue;
                if (movingPartsJ != null && col.transform.IsChildOf(movingPartsJ)) continue;

                Vector3 passageCenter = new Vector3(0f, 1.0f, -0.1f);
                Vector3 localInCol = col.transform.InverseTransformPoint(defJ.prefab.transform.TransformPoint(passageCenter));
                Bounds b = new Bounds(col.center, col.size);
                Assert.IsFalse(b.Contains(localInCol), "Collider de batente não pode invadir o centro do vão livre da porta dupla J!");
            }
        }

        [Test]
        public void Test04_FourTestRecipesAssembleCorrectly()
        {
            ModularBuildingRecipe[] recipes = new[]
            {
                ModularBuildingRecipe.HouseWithGarage,
                ModularBuildingRecipe.TwoStoryHouse,
                ModularBuildingRecipe.Shop,
                ModularBuildingRecipe.MixedBuilding
            };

            foreach (var r in recipes)
            {
                Vector3 testPos = new Vector3(10f, 0f, 20f);
                BuildingLayout layout = ModularBuildingAssembler.CreateRecipeLayout(r, testPos, Quaternion.identity, 42);
                Assert.IsNotNull(layout);
                Assert.GreaterOrEqual(layout.widthModules, 3);
                Assert.GreaterOrEqual(layout.depthModules, 3);
                Assert.GreaterOrEqual(layout.floorCount, 1);
                Assert.AreNotEqual(Vector3.zero, layout.entranceWorldPos, $"Receita {r} deve ter posição de entrada válida.");
                Assert.AreNotEqual(Vector3.zero, layout.approachWorldPos, $"Receita {r} deve ter posição de aproximação válida.");
                Assert.AreNotEqual(Vector3.zero, layout.deliveryWorldPos, $"Receita {r} deve ter posição de entrega válida.");
                Assert.AreNotEqual(layout.approachWorldPos, layout.interiorWorldPos, "Aproximação e interior devem estar em lados opostos.");

                // Instancia o prédio temporariamente para checagem
                GameObject go = ModularBuildingAssembler.AssembleBuilding(layout, _catalog, null);
                Assert.IsNotNull(go);

                CityBuilding cb = go.GetComponent<CityBuilding>();
                Assert.IsNotNull(cb);
                Assert.AreEqual(layout.WidthMeters, cb.footprint.x, 0.01f);
                Assert.AreEqual(layout.DepthMeters, cb.footprint.y, 0.01f);
                Assert.AreEqual(layout.floorCount, cb.floorCount);

                // Verifica fechamento: nós essenciais presentes
                Assert.IsNotNull(go.transform.Find("Facades"));
                Assert.IsNotNull(go.transform.Find("Roof"));
                Assert.IsNotNull(go.transform.Find("Parapet"));
                Assert.IsNotNull(go.transform.Find("Pillars"));

                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Test05_LotPlannerEnforcesPolygonContainment()
        {
            // Cria um polígono retangular de teste de 40m x 30m
            Vector3[] blockPoly = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(40, 0, 0),
                new Vector3(40, 0, 30),
                new Vector3(0, 0, 30)
            };

            BlockInfo block = new BlockInfo
            {
                worldCenter = new Vector3(20, 0, 15),
                size = new Vector3(40, 0, 30),
                zoneType = ZoneType.Commercial,
                polygon = blockPoly,
                area = 1200f
            };

            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            config.blockCornerMargin = 2f;
            config.seed = 9999;

            var layouts = ModularLotPlanner.PlanBlock(block, config, 0, 9999, null);
            Assert.Greater(layouts.Count, 0, "O planejador deve alocar prédios na quadra de teste.");

            foreach (var l in layouts)
            {
                // Todos os cantos do prédio devem estar dentro do polígono
                Vector3[] corners = l.GetBaseCornersWorld();
                foreach (var c in corners)
                {
                    // Pequena tolerância na borda
                    Assert.IsTrue(c.x >= -0.1f && c.x <= 40.1f && c.z >= -0.1f && c.z <= 30.1f,
                        $"Canto do prédio {l.buildingId} ({c}) extrapolou a quadra!");
                }

                // Largura e profundidade devem ser múltiplos exatos de 2m
                Assert.AreEqual(0f, l.WidthMeters % 2.0f, 0.001f);
                Assert.AreEqual(0f, l.DepthMeters % 2.0f, 0.001f);
            }

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Test06_DeterministicGenerationProducesIdenticalLayouts()
        {
            Vector3[] blockPoly = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(30, 0, 0),
                new Vector3(30, 0, 30),
                new Vector3(0, 0, 30)
            };

            BlockInfo block = new BlockInfo
            {
                worldCenter = new Vector3(15, 0, 15),
                size = new Vector3(30, 0, 30),
                zoneType = ZoneType.Residential,
                polygon = blockPoly,
                area = 900f
            };

            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            int testSeed = 55555;

            var run1 = ModularLotPlanner.PlanBlock(block, config, 1, testSeed, null);
            var run2 = ModularLotPlanner.PlanBlock(block, config, 1, testSeed, null);

            Assert.AreEqual(run1.Count, run2.Count, "Planejamento com mesma seed deve ter a mesma contagem de prédios.");
            for (int i = 0; i < run1.Count; i++)
            {
                Assert.AreEqual(run1[i].buildingId, run2[i].buildingId);
                Assert.AreEqual(run1[i].widthModules, run2[i].widthModules);
                Assert.AreEqual(run1[i].depthModules, run2[i].depthModules);
                Assert.AreEqual(run1[i].floorCount, run2[i].floorCount);
                Assert.AreEqual(run1[i].worldPosition, run2[i].worldPosition);
                Assert.AreEqual(run1[i].entranceWorldPos, run2[i].entranceWorldPos);
            }

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Test07_CatalogSignatureIsStable()
        {
            string sig1 = _catalog.GetCatalogSignature();
            string sig2 = _catalog.GetCatalogSignature();
            Assert.IsFalse(string.IsNullOrEmpty(sig1));
            Assert.AreEqual(sig1, sig2, "Assinatura do catálogo deve ser estável e determinística.");
        }

        [Test]
        public void Test08_ComplexPolygonsAndPizzeriaReservationHandling()
        {
            // Polígono côncavo em formato de "L"
            // (0,0) -> (60,0) -> (60,30) -> (30,30) -> (30,60) -> (0,60)
            Vector3[] lShapePoly = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(60, 0, 0),
                new Vector3(60, 0, 30),
                new Vector3(30, 0, 30),
                new Vector3(30, 0, 60),
                new Vector3(0, 0, 60)
            };

            BlockInfo block = new BlockInfo
            {
                worldCenter = new Vector3(25, 0, 25),
                size = new Vector3(60, 0, 60),
                zoneType = ZoneType.Commercial,
                polygon = lShapePoly,
                area = 2700f
            };

            // Simula reserva geométrica da Pizzaria no canto inferior esquerdo (15, 0, 15)
            Vector2 pCenter = new Vector2(15f, 15f);
            Vector2 pHalfExtents = new Vector2(8f, 8f);
            BlockFiller.OBB2D pizzeriaReservation = new BlockFiller.OBB2D(pCenter, pHalfExtents, Vector2.right, Vector2.up);
            List<BlockFiller.OBB2D> reservations = new List<BlockFiller.OBB2D> { pizzeriaReservation };

            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            config.blockCornerMargin = 2f;
            config.seed = 7777;

            var layouts = ModularLotPlanner.PlanBlock(block, config, 2, 7777, reservations);
            Assert.Greater(layouts.Count, 0, "Deve planejar edifícios no polígono em L respeitando a pizzaria.");

            foreach (var l in layouts)
            {
                // Nenhum prédio pode colidir com a reserva da pizzaria
                BlockFiller.OBB2D bldgObb = l.GetOBB2D();
                bool intersects = BlockFiller.OBBOverlap(bldgObb, pizzeriaReservation, out _, out _);
                Assert.IsFalse(intersects, $"Prédio {l.buildingId} invadiu a área reservada da Pizzaria!");

                // Nenhum canto pode estar fora do polígono em L
                Vector3[] corners = l.GetBaseCornersWorld();
                foreach (var c in corners)
                {
                    Assert.IsTrue(PolygonUtils.IsPointInPolygon(new Vector2(c.x, c.z), lShapePoly, 0.1f),
                        $"Canto ({c.x}, {c.z}) do prédio {l.buildingId} extrapolou o polígono côncavo em L!");
                }
            }

            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void Test09_FullModularProceduralAssemblyAndAnchors()
        {
            Vector3[] blockPoly = new[]
            {
                new Vector3(0, 0, 0),
                new Vector3(40, 0, 0),
                new Vector3(40, 0, 30),
                new Vector3(0, 0, 30)
            };

            BlockInfo block = new BlockInfo
            {
                worldCenter = new Vector3(20, 0, 15),
                size = new Vector3(40, 0, 30),
                zoneType = ZoneType.Residential,
                polygon = blockPoly,
                area = 1200f
            };

            CityConfig config = ScriptableObject.CreateInstance<CityConfig>();
            config.blockCornerMargin = 2f;
            config.sidewalkHeight = 0.20f;
            config.seed = 12345;
            config.deliveryBuildingDensity = 0.5f;

            var layouts = ModularLotPlanner.PlanBlock(block, config, 0, 12345, null);
            Assert.Greater(layouts.Count, 0);

            GameObject rootParent = new GameObject("Assembly_Test_Root");
            List<CityBuilding> assembledBuildings = new List<CityBuilding>();

            try
            {
                for (int i = 0; i < layouts.Count; i++)
                {
                    GameObject go = ModularBuildingAssembler.AssembleBuilding(layouts[i], _catalog, rootParent.transform);
                    CityBuilding cb = go.GetComponent<CityBuilding>();
                    Assert.IsNotNull(cb, "Todo prédio montado deve possuir CityBuilding anexado.");

                    // Footprint e pavimentos válidos
                    Assert.GreaterOrEqual(cb.footprint.x, 6f);
                    Assert.GreaterOrEqual(cb.footprint.y, 6f);
                    Assert.GreaterOrEqual(cb.floorCount, 1);

                    // Âncoras válidas
                    Assert.IsNotNull(cb.doorAnchor, "doorAnchor deve estar atribuído.");
                    Assert.IsNotNull(cb.approachAnchor, "approachAnchor deve estar atribuído.");
                    Assert.IsNotNull(cb.interiorAnchor, "interiorAnchor deve estar atribuído.");
                    Assert.IsNotNull(cb.deliveryAnchor, "deliveryAnchor deve estar atribuído.");

                    // Approach e Door devem estar na cota correta do chão/calçada
                    float expectedGroundY = 0.02f + config.sidewalkHeight;
                    Assert.AreEqual(expectedGroundY, cb.approachAnchor.position.y, 0.05f,
                        "Cota de aproximação deve respeitar a altura unificada da calçada.");

                    assembledBuildings.Add(cb);
                }

                // Testa a alocação de pontos de entrega
                GameObject deliveryParent = new GameObject("DeliveryPoints_Test_Root");
                deliveryParent.transform.SetParent(rootParent.transform);

                DeliveryPointPlacer placer = rootParent.AddComponent<DeliveryPointPlacer>();
                var deliveryPoints = placer.PlaceDeliveryPoints(
                    new[] { block }, assembledBuildings.ToArray(), config, new System.Random(12345), deliveryParent.transform);

                Assert.Greater(deliveryPoints.Count, 0, "Devem ser gerados pontos de entrega para os prédios elegíveis.");

                foreach (var dp in deliveryPoints)
                {
                    Assert.IsNotNull(dp);
                    // O marcador deve estar levemente acima da calçada (+0.05m de segurança para z-fighting)
                    float expectedDeliveryY = (0.02f + config.sidewalkHeight) + 0.05f;
                    Assert.AreEqual(expectedDeliveryY, dp.transform.position.y, 0.06f,
                        "Marcador de entrega não pode duplicar altura da calçada!");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootParent);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Test10_KitBuilderIdempotencyAndNoAssetDuplication()
        {
            // Executa o builder em modo seguro (não forçado)
            CityModuleCatalog catalogAgain = ModularCityKitBuilder.BuildCompleteKit(false);
            Assert.IsNotNull(catalogAgain);
            Assert.AreEqual(12, catalogAgain.definitions.Count);

            // A assinatura do catálogo deve permanecer idêntica
            Assert.AreEqual(_catalog.GetCatalogSignature(), catalogAgain.GetCatalogSignature(),
                "Reexecutar o builder deve ser estritamente idempotente.");
        }
    }
}
