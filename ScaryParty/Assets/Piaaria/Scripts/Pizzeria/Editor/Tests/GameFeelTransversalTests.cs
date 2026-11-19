using NUnit.Framework;
using ScaryParty.Pizzeria.Presentation.Feedback;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class GameFeelTransversalTests
    {
        private static readonly GameFeelEventId[] MandatoryEvents = new[]
        {
            GameFeelEventId.VehicleMount,
            GameFeelEventId.VehicleDismount,
            GameFeelEventId.VehicleHorn,
            GameFeelEventId.VehicleImpact,
            GameFeelEventId.WeaponAim,
            GameFeelEventId.WeaponFire,
            GameFeelEventId.WeaponReload,
            GameFeelEventId.WeaponHit,
            GameFeelEventId.PlayerDamaged,
            GameFeelEventId.PlayerRescued,
            GameFeelEventId.EmployeeSpawned,
            GameFeelEventId.EmployeeWorkStart,
            GameFeelEventId.EmployeeWorkComplete,
            GameFeelEventId.EmployeeBlocked
        };

        [Test]
        public void All14MandatoryFeelEvents_ResolveValidRecipesInDefaultProfile()
        {
            var profile = GameFeelProfile.CreateRuntimeDefault();
            try
            {
                for (int i = 0; i < MandatoryEvents.Length; i++)
                {
                    var recipe = profile.GetRecipeOrFallback(MandatoryEvents[i], GameFeelMaterialFamily.Default);
                    Assert.Greater(recipe.Duration, 0f, $"Recipe inválida para {MandatoryEvents[i]}");
                    Assert.AreNotEqual(GameFeelChannel.None, recipe.Channels, $"Canais vazios para {MandatoryEvents[i]}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void GameFeelDeduplicator_RejectsDuplicateRevisionWithinWindow()
        {
            var dedup = new GameFeelDeduplicator();
            var ctx = GameFeelContext.Create(GameFeelEventId.WeaponFire, 1UL, 0, 42u, GameFeelMaterialFamily.RigidMetal);

            Assert.IsTrue(dedup.TryAccept(in ctx, 10.0));
            Assert.IsFalse(dedup.TryAccept(in ctx, 10.02));
            Assert.IsTrue(dedup.TryAccept(in ctx, 10.50));
        }
    }
}
