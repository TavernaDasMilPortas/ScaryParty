#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ScaryParty.City;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Editor;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class PizzeriaPlacementAndMaterialsTests
    {
        private GameObject _tempPizzeriaGo;
        private PizzeriaFootprint _tempFootprint;
        private PizzeriaAppearanceProfile _tempProfile;

        [SetUp]
        public void SetUp()
        {
            _tempPizzeriaGo = new GameObject("Test_Pizzeria");
            _tempFootprint = _tempPizzeriaGo.AddComponent<PizzeriaFootprint>();
            _tempFootprint.floorSurfaceLocalY = -1.6f;
            _tempFootprint.diningHallRect = Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f);
            _tempFootprint.kitchenRect = Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f);
            _tempFootprint.structuralRegions = new Rect[] { _tempFootprint.diningHallRect, _tempFootprint.kitchenRect };
            _tempFootprint.entranceDirectionLocal = new Vector2(-1f, 0f);

            _tempProfile = ScriptableObject.CreateInstance<PizzeriaAppearanceProfile>();
            _tempProfile.InitializeDefaultRules();
        }

        [TearDown]
        public void TearDown()
        {
            if (_tempPizzeriaGo != null)
                Object.DestroyImmediate(_tempPizzeriaGo);
            if (_tempProfile != null)
                Object.DestroyImmediate(_tempProfile);
        }

        [Test]
        public void Test_FloorSurfaceElevation_CalculatesAccurately()
        {
            // Caso atual: piso em -1.6 e apoio da calçada em 0.15 resultam em raiz em 1.75
            float sidewalkHeight = 0.15f;
            float rootY = sidewalkHeight - _tempFootprint.floorSurfaceLocalY;

            Assert.AreEqual(1.75f, rootY, 0.0001f);

            _tempPizzeriaGo.transform.position = new Vector3(10f, rootY, 20f);
            float worldSurfaceY = _tempFootprint.GetFloorSurfaceWorldY();

            // A superfície final do piso deve ser exatamente igual à altura da calçada
            Assert.AreEqual(sidewalkHeight, worldSurfaceY, 0.0001f);
        }

        [Test]
        public void Test_WorldBounds_Rotations_0_45_90_Degrees()
        {
            Bounds localB = new Bounds(Vector3.zero, new Vector3(20f, 4f, 10f)); // extents = (10, 2, 5)

            // 1. Rotação 0°
            Matrix4x4 m0 = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);
            Bounds b0 = PizzeriaFootprint.TransformBounds(localB, m0);
            Assert.AreEqual(20f, b0.size.x, 0.001f);
            Assert.AreEqual(4f, b0.size.y, 0.001f);
            Assert.AreEqual(10f, b0.size.z, 0.001f);

            // 2. Rotação 90° em torno de Y
            Matrix4x4 m90 = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 90f, 0), Vector3.one);
            Bounds b90 = PizzeriaFootprint.TransformBounds(localB, m90);
            Assert.AreEqual(10f, b90.size.x, 0.001f, "Em 90°, a largura X deve se tornar a profundidade Z original (10)");
            Assert.AreEqual(20f, b90.size.z, 0.001f, "Em 90°, a profundidade Z deve se tornar a largura X original (20)");

            // 3. Rotação 45° em torno de Y
            // Para extents (10, 5), na rotação de 45°:
            // extents.x_world = 10*cos(45) + 5*sin(45) = 15 * sqrt(2)/2 ≈ 10.6066 -> size = 21.2132
            Matrix4x4 m45 = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 45f, 0), Vector3.one);
            Bounds b45 = PizzeriaFootprint.TransformBounds(localB, m45);
            float expectedExtentsXZ = (10f + 5f) * Mathf.Cos(45f * Mathf.Deg2Rad);
            Assert.AreEqual(expectedExtentsXZ * 2f, b45.size.x, 0.01f);
            Assert.AreEqual(expectedExtentsXZ * 2f, b45.size.z, 0.01f);

            // Confirma que todos os 8 cantos transformados estão dentro de b45
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = localB.center + Vector3.Scale(localB.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 worldCorner = m45.MultiplyPoint3x4(corner);
                Assert.IsTrue(b45.Contains(worldCorner), $"Canto {i} deve estar contido no AABB transformado");
            }
        }

        [Test]
        public void Test_PlacementSolver_Rejects_BlockTooSmall()
        {
            var config = ScriptableObject.CreateInstance<CityConfig>();
            config.sidewalkHeight = 0.15f;

            // Quadra pequena de 10m x 10m (área 100 m² < 600 m² da pizzaria)
            List<BlockInfo> blocks = new List<BlockInfo>
            {
                new BlockInfo
                {
                    worldCenter = new Vector3(0, 0, 0),
                    size = new Vector3(10f, 0f, 10f),
                    area = 100f,
                    zoneType = ZoneType.Commercial,
                    polygon = new Vector3[]
                    {
                        new Vector3(0, 0, 0),
                        new Vector3(10, 0, 0),
                        new Vector3(10, 0, 10),
                        new Vector3(0, 0, 10)
                    }
                }
            };

            PlacementResult result = PizzeriaPlacementSolver.Solve(blocks, _tempPizzeriaGo, config, new System.Random(42));

            Assert.IsFalse(result.success, "Quadra menor que a pizzaria deve ser rejeitada imediatamente");
            StringAssert.Contains("Nenhuma quadra", result.failureReason);
        }

        [Test]
        public void Test_PlacementSolver_Succeeds_And_FullyContains_L_Footprint()
        {
            var config = ScriptableObject.CreateInstance<CityConfig>();
            config.sidewalkHeight = 0.15f;

            // Quadra ampla e viável de 70m x 70m
            List<BlockInfo> blocks = new List<BlockInfo>
            {
                new BlockInfo
                {
                    worldCenter = new Vector3(35, 0, 35),
                    size = new Vector3(70f, 0f, 70f),
                    area = 4900f,
                    zoneType = ZoneType.Commercial,
                    polygon = new Vector3[]
                    {
                        new Vector3(0, 0, 0),
                        new Vector3(70, 0, 0),
                        new Vector3(70, 0, 70),
                        new Vector3(0, 0, 70)
                    }
                }
            };

            PlacementResult result = PizzeriaPlacementSolver.Solve(blocks, _tempPizzeriaGo, config, new System.Random(42));

            Assert.IsTrue(result.success, $"Deveria posicionar com sucesso na quadra ampla. Motivo: {result.failureReason}");
            Assert.AreEqual(0, result.blockIndex);

            // Altura do piso deve bater exatamente com a calçada
            float expectedRootY = config.sidewalkHeight - _tempFootprint.floorSurfaceLocalY;
            Assert.AreEqual(expectedRootY, result.position.y, 0.001f);

            // Verifica que todos os vértices de ambos os braços do L estão 100% contidos na quadra
            Rect[] regions = _tempFootprint.GetStructuralRegions();
            for (int r = 0; r < regions.Length; r++)
            {
                Vector3[] cornersLocal = PizzeriaPlacementSolver.GetRectCornersLocal(regions[r]);
                for (int c = 0; c < 4; c++)
                {
                    Vector3 worldCorner = result.position + result.rotation * cornersLocal[c];
                    bool inside = PizzeriaPlacementSolver.IsPointInsidePolygonXZ(worldCorner, blocks[0].polygon);
                    Assert.IsTrue(inside, $"Vértice {c} da região {r} deve estar estritamente dentro da quadra!");
                }
            }

            // Verifica que foram geradas OBBs reservadas para o BlockFiller
            Assert.AreEqual(2, result.reservedOBBs.Count, "Deve gerar 2 OBBs reservadas correspondentes aos dois retângulos do L");
        }

        [Test]
        public void Test_PlacementSolver_Rejects_Concave_Intersecting_Block()
        {
            var config = ScriptableObject.CreateInstance<CityConfig>();
            config.sidewalkHeight = 0.15f;

            // Cria um polígono com um recorte que intersectaria a profundidade da pizzaria
            // U-shape onde o miolo tem um buraco/rua
            Vector3[] uPoly = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(60, 0, 0),
                new Vector3(60, 0, 60),
                new Vector3(45, 0, 60),
                new Vector3(45, 0, 10), // Recorte profundo
                new Vector3(15, 0, 10),
                new Vector3(15, 0, 60),
                new Vector3(0, 0, 60)
            };

            // Teste de intersecção direto
            Vector3 segA = new Vector3(10, 0, 5);
            Vector3 segB = new Vector3(50, 0, 5);
            Vector3 wallA = new Vector3(45, 0, 0);
            Vector3 wallB = new Vector3(45, 0, 60);

            bool intersects = PizzeriaPlacementSolver.SegmentsIntersectXZ(segA, segB, wallA, wallB);
            Assert.IsTrue(intersects, "Detecção de intersecção em quadras côncavas deve acusar colisão");
        }

        [Test]
        public void Test_BlockFiller_OBBOverlap_DetectsCollisionWithReservedPizzeria()
        {
            // Reserva da pizzaria: retangulo centrado em (20, 20), tamanho 30 x 20 (halfExtents = 15, 10)
            BlockFiller.OBB2D pizzeriaOBB = new BlockFiller.OBB2D(
                new Vector2(20f, 20f),
                new Vector2(15f, 10f),
                Vector2.right,
                Vector2.up);

            // Prédio candidato que tenta nascer dentro da área da pizzaria em (22, 22), tamanho 8 x 8 (halfExtents = 4, 4)
            BlockFiller.OBB2D candidateInside = new BlockFiller.OBB2D(
                new Vector2(22f, 22f),
                new Vector2(4f, 4f),
                Vector2.right,
                Vector2.up);

            bool overlaps = BlockFiller.OBBOverlap(candidateInside, pizzeriaOBB, out _, out _);
            Assert.IsTrue(overlaps, "Prédio candidato sobre a reserva da pizzaria deve ser detectado e bloqueado!");

            // Prédio candidato fora da área da pizzaria em (50, 50)
            BlockFiller.OBB2D candidateOutside = new BlockFiller.OBB2D(
                new Vector2(50f, 50f),
                new Vector2(4f, 4f),
                Vector2.right,
                Vector2.up);

            bool overlapsOutside = BlockFiller.OBBOverlap(candidateOutside, pizzeriaOBB, out _, out _);
            Assert.IsFalse(overlapsOutside, "Prédio candidato distante da pizzaria deve ser aceito normalmente.");
        }

        [Test]
        public void Test_MaterialApplicator_Analysis_And_RoleSuggestion()
        {
            // Cria hierarquia de teste com nomes representativos
            GameObject wallGo = new GameObject("paredecomum.001");
            wallGo.transform.SetParent(_tempPizzeriaGo.transform);
            var wallRend = wallGo.AddComponent<MeshRenderer>();
            wallRend.sharedMaterials = new Material[1];

            GameObject ovenGo = new GameObject("forno_industrial");
            ovenGo.transform.SetParent(_tempPizzeriaGo.transform);
            var ovenRend = ovenGo.AddComponent<MeshRenderer>();
            ovenRend.sharedMaterials = new Material[1];

            GameObject salaoFloorGo = new GameObject("Floor_Salao");
            salaoFloorGo.transform.SetParent(_tempPizzeriaGo.transform);
            var salaoRend = salaoFloorGo.AddComponent<MeshRenderer>();
            salaoRend.sharedMaterials = new Material[1];

            var report = PizzeriaMaterialApplicator.AnalyzePrefab(_tempPizzeriaGo, _tempProfile);

            Assert.GreaterOrEqual(report.totalRenderers, 3);
            Assert.AreEqual(3, report.totalSlots);
            Assert.AreEqual(3, report.nullSlots, "Slots inicializados como null devem ser contados");

            // Testa as sugestões de papel
            Assert.AreEqual(MaterialPaletteRole.ParedeClara, _tempProfile.SuggestRole("paredecomum.001", ""));
            Assert.AreEqual(MaterialPaletteRole.FornoLenha, _tempProfile.SuggestRole("forno.001", ""));
            Assert.AreEqual(MaterialPaletteRole.PortaForno, _tempProfile.SuggestRole("portaforno.001", ""));
            Assert.AreEqual(MaterialPaletteRole.PisoSalao, _tempProfile.SuggestRole("Floor_Salao", ""));
            Assert.AreEqual(MaterialPaletteRole.PisoCozinha, _tempProfile.SuggestRole("Floor_Cozinha", ""));
            Assert.AreEqual(MaterialPaletteRole.Mesa, _tempProfile.SuggestRole("mesa comum.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Escuro, _tempProfile.SuggestRole("puxador_porta", ""));
            Assert.AreEqual(MaterialPaletteRole.DestaqueSecundario, _tempProfile.SuggestRole("letreiro_pizzaria", ""));
        }

        [Test]
        public void Test_SpecializedKitchenAndFurnitureRoles_Suggestion()
        {
            // Forno a lenha e porta
            Assert.AreEqual(MaterialPaletteRole.FornoLenha, _tempProfile.SuggestRole("forno.002", ""));
            Assert.AreEqual(MaterialPaletteRole.PortaForno, _tempProfile.SuggestRole("portaforno.003", ""));

            // Geladeiras industriais
            Assert.AreEqual(MaterialPaletteRole.Geladeira, _tempProfile.SuggestRole("Geladeira.002", ""));
            Assert.AreEqual(MaterialPaletteRole.Geladeira, _tempProfile.SuggestRole("portamaiorgeldeira.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Geladeira, _tempProfile.SuggestRole("portapequenageldeira.002", ""));

            // Armários e prateleiras
            Assert.AreEqual(MaterialPaletteRole.Armario, _tempProfile.SuggestRole("armario.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Armario, _tempProfile.SuggestRole("prateleira.001", ""));

            // Facas e faqueiros
            Assert.AreEqual(MaterialPaletteRole.Faca, _tempProfile.SuggestRole("faca.004", ""));
            Assert.AreEqual(MaterialPaletteRole.Faqueiro, _tempProfile.SuggestRole("suportefaca.002", ""));

            // Utensílios e ferramentas
            Assert.AreEqual(MaterialPaletteRole.TabuaCorte, _tempProfile.SuggestRole("tabua de cortar.002", ""));
            Assert.AreEqual(MaterialPaletteRole.CortadorPizza, _tempProfile.SuggestRole("cortador de pizza.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Espatula, _tempProfile.SuggestRole("espatula sla.001", ""));
            Assert.AreEqual(MaterialPaletteRole.FormaPizza, _tempProfile.SuggestRole("prato/forma.003", ""));
            Assert.AreEqual(MaterialPaletteRole.PanelaConcha, _tempProfile.SuggestRole("panela e concha.001", ""));
            Assert.AreEqual(MaterialPaletteRole.RecipienteMolho, _tempProfile.SuggestRole("molhos.005", ""));
            Assert.AreEqual(MaterialPaletteRole.PizzaPronta, _tempProfile.SuggestRole("PizzasProntas", ""));

            // Mobiliário
            Assert.AreEqual(MaterialPaletteRole.Mesa, _tempProfile.SuggestRole("mesa comum.005", ""));
            Assert.AreEqual(MaterialPaletteRole.Mesa, _tempProfile.SuggestRole("mesaempacotamento.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Cadeira, _tempProfile.SuggestRole("cadeira.012", ""));
            Assert.AreEqual(MaterialPaletteRole.Balcao, _tempProfile.SuggestRole("balcao.008", ""));
            Assert.AreEqual(MaterialPaletteRole.Balcao, _tempProfile.SuggestRole("quinabalcao.001", ""));

            // Embalagens, operação e descarte
            Assert.AreEqual(MaterialPaletteRole.CaixaPizza, _tempProfile.SuggestRole("caixa.003", ""));
            Assert.AreEqual(MaterialPaletteRole.DeliveryBag, _tempProfile.SuggestRole("deliverybag.002", ""));
            Assert.AreEqual(MaterialPaletteRole.CaixaRegistradora, _tempProfile.SuggestRole("caixaregistradora.001", ""));
            Assert.AreEqual(MaterialPaletteRole.CaixaRegistradora, _tempProfile.SuggestRole("gavetacaixa", ""));
            Assert.AreEqual(MaterialPaletteRole.CardapioPrancheta, _tempProfile.SuggestRole("cardapio.001", ""));
            Assert.AreEqual(MaterialPaletteRole.CardapioPrancheta, _tempProfile.SuggestRole("prancheta.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Lixeira, _tempProfile.SuggestRole("lata de lixo.001", ""));
            Assert.AreEqual(MaterialPaletteRole.Lixeira, _tempProfile.SuggestRole("tampa do lixo.002", ""));

            // Preservação de textos/comandas
            Assert.AreEqual(MaterialPaletteRole.Preserve, _tempProfile.SuggestRole("SheetText", ""));
        }
    }
}
#endif
