using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class PizzaModularComposerTests
    {
        [Test]
        public void NetItemDto_Supports8DistinctIngredientsWithStageAndCount()
        {
            var dto = new NetItemDto();
            dto.DistinctIngredientCount = 8;

            for (int i = 0; i < 8; i++)
            {
                dto.SetIngredient(i, new NetIngredientDto
                {
                    DefinitionId = 100 + i,
                    Stage = (byte)(i % 4),
                    Count = (byte)(i + 1)
                });
            }

            Assert.AreEqual(8, dto.DistinctIngredientCount);
            for (int i = 0; i < 8; i++)
            {
                var ing = dto.GetIngredient(i);
                Assert.AreEqual(100 + i, ing.DefinitionId);
                Assert.AreEqual(i % 4, ing.Stage);
                Assert.AreEqual(i + 1, ing.Count);
            }

            // Test backwards compatibility properties
            Assert.AreEqual(100, dto.IngredientId_0);
            Assert.AreEqual(101, dto.IngredientId_1);
            Assert.AreEqual(102, dto.IngredientId_2);
            Assert.AreEqual(103, dto.IngredientId_3);
        }

        [Test]
        public void PizzaLayoutGenerator_IsDeterministicForSameSeedAndComposition()
        {
            var ingredients = new List<NetIngredientDto>
            {
                new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 }, // Molho
                new NetIngredientDto { DefinitionId = 5, Stage = (byte)ProcessingStage.Prepared, Count = 1 }, // Queijo
                new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 }, // Calabresa
                new NetIngredientDto { DefinitionId = 9, Stage = (byte)ProcessingStage.Prepared, Count = 1 }  // Cogumelo
            };

            var compA = new PizzaVisualComposition(12345UL, 2, CookingStage.Uncooked, 0f, 0f, ingredients, 0.36f);
            var compB = new PizzaVisualComposition(12345UL, 2, CookingStage.Uncooked, 0f, 0f, ingredients, 0.36f);

            var resultA = PizzaLayoutGenerator.Generate(compA, PizzaVisualCatalog.Instance);
            var resultB = PizzaLayoutGenerator.Generate(compB, PizzaVisualCatalog.Instance);

            Assert.AreEqual(resultA.Placements.Count, resultB.Placements.Count);
            for (int i = 0; i < resultA.Placements.Count; i++)
            {
                Assert.AreEqual(resultA.Placements[i].IngredientId, resultB.Placements[i].IngredientId);
                Assert.AreEqual(resultA.Placements[i].Mode, resultB.Placements[i].Mode);
                Assert.That(Vector3.Distance(resultA.Placements[i].LocalPosition, resultB.Placements[i].LocalPosition), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(resultA.Placements[i].LocalRotation, resultB.Placements[i].LocalRotation), Is.LessThan(0.01f));
            }
        }

        [Test]
        public void PizzaLayoutGenerator_PreservesSpecificModePosesExactly()
        {
            var catalog = ScriptableObject.CreateInstance<PizzaVisualCatalog>();
            var specProfile = ScriptableObject.CreateInstance<PizzaIngredientVisualProfile>();
            specProfile.ingredientId = 99;
            specProfile.displayName = "Ingrediente Autoral";
            specProfile.placementMode = PizzaPlacementMode.Specific;
            specProfile.specificPoses = new List<SpecificPlacementPose>
            {
                new SpecificPlacementPose { localPosition = new Vector3(0.12f, 0.035f, 0.15f), localRotationEuler = new Vector3(0f, 45f, 0f), localScale = Vector3.one },
                new SpecificPlacementPose { localPosition = new Vector3(-0.10f, 0.035f, -0.08f), localRotationEuler = new Vector3(0f, 90f, 0f), localScale = Vector3.one * 1.2f }
            };
            catalog.profiles.Add(specProfile);

            var ingredients = new List<NetIngredientDto>
            {
                new NetIngredientDto { DefinitionId = 99, Stage = (byte)ProcessingStage.Prepared, Count = 1 }
            };

            var comp = new PizzaVisualComposition(999UL, 2, CookingStage.Uncooked, 0f, 0f, ingredients, 0.36f);
            var result = PizzaLayoutGenerator.Generate(comp, catalog);

            Assert.AreEqual(2, result.Placements.Count);
            Assert.AreEqual(new Vector3(0.12f, 0.035f, 0.15f), result.Placements[0].LocalPosition);
            Assert.AreEqual(Quaternion.Euler(0f, 45f, 0f), result.Placements[0].LocalRotation);
            Assert.AreEqual(new Vector3(-0.10f, 0.035f, -0.08f), result.Placements[1].LocalPosition);
            Assert.AreEqual(Vector3.one * 1.2f, result.Placements[1].LocalScale);
        }

        [Test]
        public void PizzaLayoutGenerator_TotalModeAppliesAsLayerWithoutOccupying2DPlane()
        {
            var catalog = ScriptableObject.CreateInstance<PizzaVisualCatalog>();
            var sauce = ScriptableObject.CreateInstance<PizzaIngredientVisualProfile>();
            sauce.ingredientId = 3;
            sauce.placementMode = PizzaPlacementMode.Total;
            sauce.coatingScale = 0.38f;
            sauce.thickness = 0.005f;
            catalog.profiles.Add(sauce);

            var pepperoni = ScriptableObject.CreateInstance<PizzaIngredientVisualProfile>();
            pepperoni.ingredientId = 7;
            pepperoni.placementMode = PizzaPlacementMode.Sliced;
            pepperoni.piecesPerPortion = 4;
            pepperoni.occupancyRadius = 0.04f;
            pepperoni.minDistanceBetweenPieces = 0.06f;
            catalog.profiles.Add(pepperoni);

            var ingredients = new List<NetIngredientDto>
            {
                new NetIngredientDto { DefinitionId = 3, Stage = (byte)ProcessingStage.Prepared, Count = 1 },
                new NetIngredientDto { DefinitionId = 7, Stage = (byte)ProcessingStage.Prepared, Count = 1 }
            };

            var comp = new PizzaVisualComposition(777UL, 2, CookingStage.Uncooked, 0f, 0f, ingredients, 0.36f);
            var result = PizzaLayoutGenerator.Generate(comp, catalog);

            // Total mode generates 1 coating layer, Sliced generates 4 pieces -> total 5 placements
            Assert.AreEqual(5, result.Placements.Count);
            Assert.IsTrue(result.Placements[0].IsCoating);
            Assert.IsFalse(result.Placements[1].IsCoating);
        }
    }
}
