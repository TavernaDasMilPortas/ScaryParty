#if UNITY_EDITOR
using NUnit.Framework;
using ScaryParty.Pizzeria.Editor.Migration;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.UI.Piaria.Presenters;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class PiariaGameplayUIMigrationTests
    {
        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && _scene.isLoaded)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void InstallsSinglePiariaHudAndPreservesGameplayComponentsOnRepeatedRuns()
        {
            var gameplay = new GameObject("Pizzeria gameplay");
            gameplay.AddComponent<PizzeriaHudBuilder>();
            var collider = gameplay.AddComponent<BoxCollider>();
            SceneManager.MoveGameObjectToScene(gameplay, _scene);
            var legacyLobby = new GameObject("InGameLobbyUI");
            legacyLobby.AddComponent<InGameLobbyUIController>();
            legacyLobby.AddComponent<UIDocument>();
            SceneManager.MoveGameObjectToScene(legacyLobby, _scene);

            Assert.Greater(PiariaGameplayUIMigration.EnsureGameScene(_scene), 0);
            Assert.AreEqual(0, PiariaGameplayUIMigration.EnsureGameScene(_scene));
            Assert.IsNull(gameplay.GetComponent<PizzeriaHudBuilder>());
            Assert.IsNotNull(collider);
            Assert.IsNull(legacyLobby.GetComponent<UIDocument>());
            Assert.IsNull(legacyLobby.GetComponent<InGameLobbyUIController>());
            int hudCount = 0;
            foreach (var root in _scene.GetRootGameObjects())
            {
                foreach (var hud in root.GetComponentsInChildren<HudPresenter>(true))
                {
                    hudCount++;
                    Assert.IsNotNull(hud.GetComponent<RecipeBookPresenter>());
                    Assert.IsNotNull(hud.GetComponent<BackpackPanelPresenter>());
                    Assert.IsNotNull(hud.GetComponent<DeliverySelectionPresenter>());
                    Assert.IsNotNull(hud.GetComponent<DeliveryResultPresenter>());
                }
            }
            Assert.AreEqual(1, hudCount);
        }
    }
}
#endif
