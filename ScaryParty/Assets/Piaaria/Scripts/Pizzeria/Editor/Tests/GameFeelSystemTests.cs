#if UNITY_EDITOR
using System;
using NUnit.Framework;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.UI.Piaria.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class GameFeelSystemTests
    {
        private bool _savedReduceMotion;
        private GameFeelPresetMode _savedPreset;

        [SetUp]
        public void SetUp()
        {
            GameFeelSettings.SetPersistenceEnabled(false);
            _savedReduceMotion = UIToolkitTweenService.ReduceMotion;
            _savedPreset = GameFeelSettings.Preset;
            UIToolkitTweenService.ReduceMotion = false;
            GameFeelSettings.Preset = GameFeelPresetMode.Cartoon;
        }

        [TearDown]
        public void TearDown()
        {
            UIToolkitTweenService.ReduceMotion = _savedReduceMotion;
            GameFeelSettings.Preset = _savedPreset;
            GameFeelSettings.SetPersistenceEnabled(true);
        }

        [Test]
        public void Deduplicator_SuppressesDuplicateWithinWindow_AndAcceptsNewRevisionOrEpoch()
        {
            var dedup = new GameFeelDeduplicator(64, 0.35d);
            var ctx = GameFeelContext.Create(GameFeelEventId.ItemPlaced, entityId: 42UL, stationId: 7, revision: 1u);

            Assert.IsTrue(dedup.TryAccept(in ctx, nowSeconds: 10.00d));
            Assert.IsFalse(dedup.TryAccept(in ctx, nowSeconds: 10.10d));

            // Nova revisão deve ser aceita imediatamente
            ctx.Revision = 2u;
            Assert.IsTrue(dedup.TryAccept(in ctx, nowSeconds: 10.12d));

            // Após expirar a janela, a mesma chave deve ser aceita novamente
            ctx.Revision = 1u;
            Assert.IsTrue(dedup.TryAccept(in ctx, nowSeconds: 10.40d));

            // Após reset de epoch, estado anterior é limpo
            Assert.IsFalse(dedup.TryAccept(in ctx, nowSeconds: 10.45d));
            dedup.ResetEpoch();
            Assert.IsTrue(dedup.TryAccept(in ctx, nowSeconds: 10.46d));
        }

        [Test]
        public void Settings_ReduceMotionSync_ZeroesMovementAndUiIntensity_AndEnforcesClamps()
        {
            GameFeelSettings.ReduceMotion = true;

            Assert.IsTrue(UIToolkitTweenService.ReduceMotion);
            Assert.AreEqual(GameFeelPresetMode.Reduzido, GameFeelSettings.Preset);
            Assert.AreEqual(0f, GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Movement), 0.0001f);
            Assert.AreEqual(0f, GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.UI), 0.0001f);
            Assert.IsFalse(GameFeelSettings.AllowFlashes);

            Vector3 clampedItem = GameFeelSettings.ClampItemLocalScale(new Vector3(0.2f, 2.5f, 1.0f));
            Assert.AreEqual(GameFeelSettings.MinItemScale, clampedItem.x, 0.0001f);
            Assert.AreEqual(GameFeelSettings.MaxItemScale, clampedItem.y, 0.0001f);

            Vector3 clampedChar = GameFeelSettings.ClampCharacterLocalScale(new Vector3(0.5f, 1.5f, 1.0f));
            Assert.AreEqual(GameFeelSettings.MinCharacterScale, clampedChar.x, 0.0001f);
            Assert.AreEqual(GameFeelSettings.MaxCharacterScale, clampedChar.y, 0.0001f);

            Assert.AreEqual(GameFeelSettings.MaxItemTiltDegrees, GameFeelSettings.ClampTiltDegrees(45f), 0.0001f);
            Assert.AreEqual(-GameFeelSettings.MaxItemTiltDegrees, GameFeelSettings.ClampTiltDegrees(-45f), 0.0001f);
        }

        [Test]
        public void MaterialClassifier_MapsCategoriesAndProcessesConsistently()
        {
            Assert.AreEqual(
                GameFeelMaterialFamily.CardboardPaper,
                GameFeelMaterialClassifier.ClassifyItem((byte)ItemCategory.Pizza, 2, isBoxed: true));
            Assert.AreEqual(
                GameFeelMaterialFamily.Dough,
                GameFeelMaterialClassifier.ClassifyItem((byte)ItemCategory.Dough, 1, isBoxed: false));
            Assert.AreEqual(
                GameFeelMaterialFamily.FluidGranular,
                GameFeelMaterialClassifier.ClassifyItem((byte)ItemCategory.Sauce, 3, isBoxed: false));
            Assert.AreEqual(
                GameFeelMaterialFamily.RigidMetal,
                GameFeelMaterialClassifier.ClassifyItem((byte)ItemCategory.Tool, 20, isBoxed: false));

            Assert.AreEqual(GameFeelMaterialFamily.Dough, GameFeelMaterialClassifier.ClassifyProcess(1));
            Assert.AreEqual(GameFeelMaterialFamily.FluidGranular, GameFeelMaterialClassifier.ClassifyProcess(2));
            Assert.AreEqual(GameFeelMaterialFamily.SmallBounceIngredient, GameFeelMaterialClassifier.ClassifyProcess(3));
            Assert.AreEqual(GameFeelMaterialFamily.SmallBounceIngredient, GameFeelMaterialClassifier.ClassifyProcess(4));
        }

        [Test]
        public void VisualPivot_PreservesBasePose_RespectsCharacterLocks_AndStopsCleanly()
        {
            var root = new GameObject("TestWrapperRoot");
            var pivotGo = new GameObject("CosmeticPivot");
            var meshGo = new GameObject("MeshChild");

            try
            {
                pivotGo.transform.SetParent(root.transform, false);
                meshGo.transform.SetParent(pivotGo.transform, false);
                meshGo.transform.localScale = new Vector3(0.37f, 0.37f, 0.37f);

                var pivot = pivotGo.AddComponent<GameFeelVisualPivot>();
                pivot.SetExplicitBasePose(new Vector3(0.1f, 0.2f, 0.3f), Quaternion.Euler(0f, 30f, 0f), new Vector3(1.2f, 1.2f, 1.2f));
                pivot.ClampAsCharacter = true;
                pivot.LockVerticalAndHipOffset = true;

                var recipe = GameFeelRecipe.CreateDefault(GameFeelEventId.NpcOrderGood, GameFeelMaterialFamily.CharacterBody);
                recipe.SquashScale = new Vector3(1.20f, 0.80f, 1.20f);
                recipe.ArcHeight = 0.08f;

                pivot.EvaluateRecipeForTest(in recipe, 0.33f, 1f, 3, out var posOff, out _, out var scaleFactor);

                Assert.AreEqual(0f, posOff.y, 0.0001f, "Personagem sentado não pode receber deslocamento vertical.");
                Assert.GreaterOrEqual(scaleFactor.y, GameFeelSettings.MinCharacterScale - 0.0001f);
                Assert.LessOrEqual(scaleFactor.x, GameFeelSettings.MaxCharacterScale + 0.0001f);

                pivot.SetContinuousOffset(new Vector3(0.05f, 0.1f, 0f), new Vector3(5f, 0f, 0f), new Vector3(1.03f, 0.97f, 1.03f));
                pivot.StopAndReset();

                Assert.AreEqual(new Vector3(0.1f, 0.2f, 0.3f), pivotGo.transform.localPosition);
                Assert.AreEqual(new Vector3(1.2f, 1.2f, 1.2f), pivotGo.transform.localScale);
                Assert.AreEqual(new Vector3(0.37f, 0.37f, 0.37f), meshGo.transform.localScale);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UIToolkitTweenService_ScaleToAndPulseScale_TrackAndKillTweensSafely()
        {
            var element = new VisualElement();
            try
            {
                int baselineCount = UIToolkitTweenService.ActiveTweenElementCount;

                UIToolkitTweenService.ScaleTo(element, 1.04f, 0.15f);
                Assert.GreaterOrEqual(UIToolkitTweenService.ActiveTweenElementCount, baselineCount + 1);

                UIToolkitTweenService.KillTweens(element);
                Assert.AreEqual(baselineCount, UIToolkitTweenService.ActiveTweenElementCount);

                // Com ReduceMotion ativo, PulseScale deve completar síncrono sem registrar tween pendente
                UIToolkitTweenService.ReduceMotion = true;
                bool completed = false;
                UIToolkitTweenService.PulseScale(element, 1.05f, 0.1f, 0.1f, () => completed = true);
                Assert.IsTrue(completed);
                Assert.AreEqual(baselineCount, UIToolkitTweenService.ActiveTweenElementCount);
            }
            finally
            {
                UIToolkitTweenService.KillTweens(element);
            }
        }

        [Test]
        public void Profile_RuntimeDefault_ProvidesSafeCalibratedRecipes()
        {
            var profile = GameFeelProfile.CreateRuntimeDefault();
            try
            {
                Assert.IsTrue(profile.TryGetRecipe(GameFeelEventId.ItemPlaced, GameFeelMaterialFamily.Dough, out var doughPlace));
                Assert.IsTrue(profile.TryGetRecipe(GameFeelEventId.SliceTomatoPepperoniStep, GameFeelMaterialFamily.SmallBounceIngredient, out var slice));
                Assert.IsTrue(profile.TryGetRecipe(GameFeelEventId.BoxPackaged, GameFeelMaterialFamily.CardboardPaper, out var box));
                Assert.IsTrue(profile.TryGetRecipe(GameFeelEventId.DeliverySuccess, GameFeelMaterialFamily.CharacterBody, out var delivery));

                Assert.Greater(doughPlace.Duration, 0.05f);
                Assert.LessOrEqual(slice.MaxTiltDegrees, GameFeelSettings.MaxItemTiltDegrees);
                Assert.LessOrEqual(box.MaxTiltDegrees, GameFeelSettings.MaxItemTiltDegrees);
                Assert.GreaterOrEqual(delivery.SquashScale.y, GameFeelSettings.MinCharacterScale);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
#endif
