using NUnit.Framework;
using ScaryParty.Cutscenes.Editor;
using UnityEngine;

namespace ScaryParty.Cutscenes.Tests
{
    public sealed class CutsceneSystemTests
    {
        [Test]
        public void Parser_ComputesFractionalFpsAndNonZeroStartFrameDuration()
        {
            string json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""sequence"": {
                    ""id"": ""calib_01"",
                    ""fps"": 24,
                    ""fpsBase"": 1.001,
                    ""frameStart"": 10,
                    ""frameEnd"": 33
                },
                ""shots"": [
                    { ""startFrame"": 20, ""startTime"": 0.417, ""cameraId"": ""cam_b"", ""cameraName"": ""CamB"" },
                    { ""startFrame"": 10, ""startTime"": 0.0, ""cameraId"": ""cam_a"", ""cameraName"": ""CamA"" }
                ]
            }";

            bool ok = CutsceneManifestParser.TryParse(json, out var manifest, out var error);
            Assert.IsTrue(ok, error);
            Assert.AreEqual(24f / 1.001f, manifest.sequence.effectiveFps, 0.001f);
            Assert.AreEqual(24f / (24f / 1.001f), manifest.sequence.duration, 0.001f);
            Assert.AreEqual("cam_a", manifest.shots[0].cameraId);
            Assert.AreEqual("cam_b", manifest.shots[1].cameraId);
        }

        [Test]
        public void Parser_RejectsAmbiguousShotsOnSameFrame()
        {
            string json = @"{
                ""schemaVersion"": ""1.0.0"",
                ""sequence"": { ""id"": ""bad"", ""fps"": 24, ""fpsBase"": 1, ""frameStart"": 1, ""frameEnd"": 10 },
                ""shots"": [
                    { ""startFrame"": 5, ""cameraId"": ""cam_a"" },
                    { ""startFrame"": 5, ""cameraId"": ""cam_b"" }
                ]
            }";

            bool ok = CutsceneManifestParser.TryParse(json, out _, out var error);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void CoordinateConverter_ConvertsBlenderZUpToUnityYUp()
        {
            Vector3 unityPos = CutsceneCoordinateConverter.BlenderPositionToUnity(new[] { 1f, 2f, 3f });
            Assert.AreEqual(new Vector3(-1f, 3f, -2f), unityPos);
        }

        [Test]
        public void Assembler_ProtectsGameplayScenes()
        {
            Assert.IsTrue(CutsceneSceneAssembler.IsProtectedGameplayScene("Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity"));
            Assert.IsTrue(CutsceneSceneAssembler.IsProtectedGameplayScene("Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity"));
            Assert.IsFalse(CutsceneSceneAssembler.IsProtectedGameplayScene("Assets/Piaaria/Scenes/Cutscenes/Night01Intro.unity"));
        }
    }
}
