using NUnit.Framework;
using ScaryParty.Editor.Playtest;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Models;
using ScaryParty.Pizzeria.Domain.Services;
using ScaryParty.Pizzeria.Network;
using UnityEditor;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class P6_PresetsAndDiagnosticsTests
    {
        [Test]
        public void RunPresets_EntregaPlaytestTutorial_ArePersistedAndWiredInConfigAndManifest()
        {
            var entrega = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.EntregaPresetPath);
            var playtest = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.PlaytestPresetPath);
            var tutorial = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.TutorialPresetPath);

            Assert.IsNotNull(entrega, "Entrega_3Noites.asset precisa existir.");
            Assert.IsNotNull(playtest, "Playtest_Integracao.asset precisa existir.");
            Assert.IsNotNull(tutorial, "Tutorial_Noite0.asset precisa existir.");

            Assert.AreEqual("Entrega_3Noites", entrega.presetId);
            Assert.AreEqual(3, entrega.totalNights);
            Assert.IsFalse(entrega.includeNight0Tutorial);
            Assert.IsFalse(entrega.allowDebugPanel, "Painel de diagnóstico deve estar desativado no preset de entrega.");

            float entregaTotalDuration = 0f;
            foreach (var n in entrega.nights) entregaTotalDuration += n.durationSeconds;
            Assert.GreaterOrEqual(entregaTotalDuration, 1800f, "Preset Entrega_3Noites deve preservar >= 30 min.");
            Assert.LessOrEqual(entregaTotalDuration, 2700f, "Preset Entrega_3Noites deve permanecer <= 45 min.");

            Assert.AreEqual("Playtest_Integracao", playtest.presetId);
            Assert.IsTrue(playtest.allowDebugPanel);
            Assert.IsFalse(playtest.includeNight0Tutorial);

            Assert.AreEqual("Tutorial_Noite0", tutorial.presetId);
            Assert.IsTrue(tutorial.includeNight0Tutorial);
            Assert.IsTrue(tutorial.night0NonLethalDefeat);
            Assert.IsTrue(tutorial.allowDebugPanel);

            var cfg = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>(PlaytestInstallerAndValidator.PizzeriaConfigPath);
            Assert.IsNotNull(cfg);
            Assert.AreSame(entrega, cfg.defaultPreset, "Entrega_3Noites deve ser o preset padrão em PizzeriaConfig.");
            Assert.GreaterOrEqual(cfg.availablePresets.Count, 3);
            Assert.Contains(entrega, cfg.availablePresets);
            Assert.Contains(playtest, cfg.availablePresets);
            Assert.Contains(tutorial, cfg.availablePresets);

            var manifest = AssetDatabase.LoadAssetAtPath<PlaytestReferenceManifest>(PlaytestInstallerAndValidator.ManifestAssetPath);
            Assert.IsNotNull(manifest);
            Assert.GreaterOrEqual(manifest.runPresets.Count, 3);
        }

        [Test]
        public void NightParameterResolver_ResolvesEntregaAndTutorialNoite0Deterministically()
        {
            var entrega = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.EntregaPresetPath);
            var tutorial = AssetDatabase.LoadAssetAtPath<PizzeriaRunPreset>(PlaytestPresetBuilder.TutorialPresetPath);
            Assert.IsNotNull(entrega);
            Assert.IsNotNull(tutorial);

            var n1OneHuman = NightParameterResolver.ResolveEffectiveParameters(1, 1, RunDifficulty.Normal, entrega.ToDomainData());
            var n1ThreeHumans = NightParameterResolver.ResolveEffectiveParameters(1, 3, RunDifficulty.Normal, entrega.ToDomainData());
            Assert.AreEqual(60, n1OneHuman.FinalGoal);
            Assert.AreEqual(129, n1ThreeHumans.FinalGoal);
            Assert.AreEqual(660f, n1OneHuman.DurationSeconds);
            Assert.IsFalse(n1OneHuman.NonLethalDefeat);

            var n0 = NightParameterResolver.ResolveEffectiveParameters(0, 2, RunDifficulty.Normal, tutorial.ToDomainData());
            Assert.AreEqual(0, n0.NightNumber);
            Assert.AreEqual(15, n0.FinalGoal);
            Assert.IsTrue(n0.NonLethalDefeat, "Noite 0 tutorial deve manter política não-letal.");
        }

        [Test]
        public void HostDebugMutationGuard_EnforcesHostOnlyAndAllowDebugPanel()
        {
            var state = new PizzeriaState { AllowDebugPanel = true };

            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(false, 0UL, 0UL, state), "Não-servidor não pode mutar.");
            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 2UL, 0UL, state), "Cliente remoto não pode executar comando debug do Host.");

            state.AllowDebugPanel = false;
            Assert.IsFalse(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 0UL, 0UL, state), "Preset com AllowDebugPanel=false deve bloquear mutações debug mesmo no Host.");

            state.AllowDebugPanel = true;
            Assert.IsTrue(PizzeriaCommandHandler.CanExecuteHostDebugMutation(true, 0UL, 0UL, state), "Host com AllowDebugPanel=true deve poder executar comandos de diagnóstico.");
        }
    }
}
