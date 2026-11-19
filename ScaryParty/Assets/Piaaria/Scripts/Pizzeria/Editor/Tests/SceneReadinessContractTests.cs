using System.Collections.Generic;
using NUnit.Framework;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class SceneReadinessContractTests
    {
        [Test]
        public void Evaluate_FailsWhenRequiredMissing_PassesWhenOnlyOptionalMissing()
        {
            bool hasCamera = true;
            bool hasHud = false;
            bool hasOptionalLighting = false;

            var contract = new SceneReadinessContract("GameScene")
                .Add(new DelegateReadinessRequirement("LocalCamera", true, () => hasCamera, "Camera.main nula"))
                .Add(new DelegateReadinessRequirement("MainHUD", true, () => hasHud, "UIDocument inativo"))
                .Add(new DelegateReadinessRequirement("Lighting", false, () => hasOptionalLighting, "Luz opcional pendente"));

            var reqMissing = new List<string>();
            var optMissing = new List<string>();

            Assert.IsFalse(contract.Evaluate(reqMissing, optMissing));
            Assert.AreEqual(1, reqMissing.Count);
            Assert.AreEqual(1, optMissing.Count);
            StringAssert.Contains("MainHUD", contract.BuildDiagnosticSummary());

            hasHud = true;
            Assert.IsTrue(contract.Evaluate(reqMissing, optMissing));
            Assert.AreEqual(0, reqMissing.Count);
            Assert.AreEqual(1, optMissing.Count);
        }

        [Test]
        public void StageTimingRecorder_RecordsSequentialStageDurations()
        {
            var recorder = new StageTimingRecorder();
            recorder.BeginStage("Connecting", 10.0f);
            recorder.BeginStage("LoadingScene", 10.25f);
            recorder.BeginStage("WaitingReadiness", 11.0f);
            recorder.EndActiveStage(11.5f);

            Assert.AreEqual(3, recorder.CompletedStages.Count);
            Assert.AreEqual("Connecting", recorder.CompletedStages[0].stage);
            Assert.AreEqual(250f, recorder.CompletedStages[0].durationMs, 0.1f);
            Assert.AreEqual(750f, recorder.CompletedStages[1].durationMs, 0.1f);
            Assert.AreEqual(500f, recorder.CompletedStages[2].durationMs, 0.1f);
        }
    }
}
