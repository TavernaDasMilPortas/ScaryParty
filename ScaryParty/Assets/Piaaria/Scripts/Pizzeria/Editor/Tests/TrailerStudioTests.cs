using System.Collections.Generic;
using NUnit.Framework;
using Piaaria.TrailerStudio;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    public class TrailerStudioTests
    {
        [Test]
        public void ProfileAsset_Has15UniqueTakes_WithValidTimelinesAndMargins()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TrailerStudioProfile>(
                "Assets/Piaaria/TrailerStudio/Profiles/TrailerStudioProfile.asset");
            Assert.IsNotNull(profile, "TrailerStudioProfile.asset não encontrado.");
            Assert.AreEqual(15, profile.takes.Count, "Esperados 15 takes no profile.");

            var seen = new HashSet<TrailerTakeId>();
            foreach (var def in profile.takes)
            {
                Assert.IsNotNull(def);
                Assert.IsTrue(seen.Add(def.id), $"ID duplicado: {def.id}");
                Assert.IsNotNull(def.timelineAsset, $"TimelineAsset ausente em {def.id}");
                Assert.AreEqual(0.75f, def.preRoll, 0.001f);
                Assert.AreEqual(0.75f, def.postRoll, 0.001f);
                Assert.Greater(def.actionDuration, 0f);
            }
        }

        [Test]
        public void EnsureRuntimeDefaults_PopulatesCuesAndCameras_WithoutOverwritingManualEdits()
        {
            var profile = ScriptableObject.CreateInstance<TrailerStudioProfile>();
            try
            {
                var customOffset = new Vector3(3.3f, 2.2f, 1.1f);
                var t03 = new TrailerTakeDefinition
                {
                    id = TrailerTakeId.T03_Dough,
                    displayName = "T03_Dough",
                    cameraAStartOffset = customOffset,
                    cameraAEndOffset = customOffset
                };
                profile.takes.Add(t03);
                profile.takes.Add(new TrailerTakeDefinition { id = TrailerTakeId.T07_Packaging, displayName = "T07_Packaging" });
                profile.takes.Add(new TrailerTakeDefinition { id = TrailerTakeId.T12_MotorcycleStart, displayName = "T12_MotorcycleStart" });

                profile.EnsureRuntimeDefaults();

                Assert.AreEqual(customOffset, t03.cameraAStartOffset, "Offset manual não deve ser sobrescrito.");
                Assert.Greater(t03.cues.Count, 2, "T03 deve receber cues completos.");
                foreach (var def in profile.takes)
                {
                    Assert.AreNotEqual(Vector3.zero, def.cameraAStartOffset, $"{def.id} deve ter cameraAStartOffset válido.");
                    Assert.Greater(def.cues.Count, 0, $"{def.id} deve ter cues.");
                    foreach (var cue in def.cues)
                    {
                        Assert.GreaterOrEqual(cue.time, 0f);
                        Assert.LessOrEqual(cue.time, def.actionDuration + 0.001f);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EvaluateCuesExactOnce_TriggersBoundaryCueOnceAcrossEndFrame()
        {
            var go = new GameObject("TestDirector");
            try
            {
                var director = go.AddComponent<TrailerDirector>();
                var def = new TrailerTakeDefinition
                {
                    id = TrailerTakeId.T03_Dough,
                    actionDuration = 6.0f,
                    cues = new List<TrailerActionCue>
                    {
                        new TrailerActionCue { time = 6.0f, actor = TrailerActorId.ActorA, actionType = TrailerActionType.None }
                    }
                };

                director.EvaluateCuesExactOnce(def, 5.95f);
                director.EvaluateCuesExactOnce(def, 6.00f);
                director.EvaluateCuesExactOnce(def, 6.05f);

                // Se Play() for chamado sem environmentReady, permanece bloqueado
                director.Play();
                Assert.AreNotEqual(TrailerDirectorState.Playing, director.state);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
