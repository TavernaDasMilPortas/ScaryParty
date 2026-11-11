using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Editor.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class PizzeriaContentStudioTests
    {
        private GameObject _benchGo;
        private StationView _benchStation;
        private IngredientDef _tomatoDef;
        private IngredientDef _slicedTomatoDef;
        private ToolDef _knifeDef;
        private ProcessDef _sliceProcess;

        [SetUp]
        public void SetUp()
        {
            _benchGo = new GameObject("TestBench");
            _benchStation = _benchGo.AddComponent<StationView>();
            _benchStation.stationId = 55;

            _tomatoDef = ScriptableObject.CreateInstance<IngredientDef>();
            _tomatoDef.id = 70;
            _tomatoDef.ingredientName = "Tomate Fresco";
            _tomatoDef.category = ItemCategory.Topping;
            _tomatoDef.requiresPrep = true;

            _slicedTomatoDef = ScriptableObject.CreateInstance<IngredientDef>();
            _slicedTomatoDef.id = 71;
            _slicedTomatoDef.ingredientName = "Tomate Fatiado";
            _slicedTomatoDef.category = ItemCategory.Topping;
            _slicedTomatoDef.requiresPrep = false;

            _tomatoDef.preparedIngredient = _slicedTomatoDef;

            _knifeDef = ScriptableObject.CreateInstance<ToolDef>();
            _knifeDef.id = 80;
            _knifeDef.toolName = "Faca de Corte";
            _knifeDef.capabilities = ToolCapability.Slice;

            _sliceProcess = ScriptableObject.CreateInstance<ProcessDef>();
            _sliceProcess.id = 90;
            _sliceProcess.processName = "Fatiar Tomate";
            _sliceProcess.inputIngredient = _tomatoDef;
            _sliceProcess.outputIngredient = _slicedTomatoDef;
            _sliceProcess.requiredToolCapability = ToolCapability.Slice;
            _sliceProcess.baseDuration = 2.5f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_benchGo != null) Object.DestroyImmediate(_benchGo);
            if (_tomatoDef != null) Object.DestroyImmediate(_tomatoDef);
            if (_slicedTomatoDef != null) Object.DestroyImmediate(_slicedTomatoDef);
            if (_knifeDef != null) Object.DestroyImmediate(_knifeDef);
            if (_sliceProcess != null) Object.DestroyImmediate(_sliceProcess);

            PizzeriaContentStudioSession.SetPinned(false);
        }

        [Test]
        public void StudioSession_PinAssembly_LocksBenchWithTomatoAndKnifeAcrossInspections()
        {
            // 1. Fixar a montagem na bancada com Tomate no Slot 2 e Faca de corte
            PizzeriaContentStudioSession.PinAssembly(
                _benchStation,
                null,
                2,
                _tomatoDef,
                _knifeDef,
                _sliceProcess);

            Assert.IsTrue(PizzeriaContentStudioSession.IsPinned);
            Assert.AreEqual(_benchStation, PizzeriaContentStudioSession.PinnedState.pinnedStation);
            Assert.AreEqual(2, PizzeriaContentStudioSession.PinnedState.pinnedSlotIndex);
            Assert.AreEqual(_tomatoDef, PizzeriaContentStudioSession.PinnedState.pinnedIngredient);
            Assert.AreEqual(_knifeDef, PizzeriaContentStudioSession.PinnedState.pinnedTool);
            Assert.AreEqual(_sliceProcess, PizzeriaContentStudioSession.PinnedState.pinnedProcess);

            // 2. Simular seleção de outro asset no Project / Inspector (ex: Queijo ou Forno)
            var otherAsset = ScriptableObject.CreateInstance<IngredientDef>();
            otherAsset.id = 99;
            otherAsset.ingredientName = "Queijo Gorgonzola";

            PizzeriaContentStudioSession.ActiveInspectedObject = otherAsset;

            // A montagem fixada DEVE PERMANECER com Tomate, Faca e Bancada originais!
            Assert.IsTrue(PizzeriaContentStudioSession.IsPinned);
            Assert.AreEqual(_tomatoDef, PizzeriaContentStudioSession.PinnedState.pinnedIngredient);
            Assert.AreEqual(_knifeDef, PizzeriaContentStudioSession.PinnedState.pinnedTool);
            Assert.AreEqual(_benchStation, PizzeriaContentStudioSession.PinnedState.pinnedStation);

            Object.DestroyImmediate(otherAsset);
        }

        [Test]
        public void ProcessSimulation_ScrubberProgress_ControlsStagesAccurately()
        {
            PizzeriaContentStudioSession.PinAssembly(
                _benchStation,
                null,
                0,
                _tomatoDef,
                _knifeDef,
                _sliceProcess);

            // 1. Início (0%)
            PizzeriaContentStudioSession.SetProcessProgress(0f);
            Assert.AreEqual(0f, PizzeriaContentStudioSession.PinnedState.processProgress);

            // 2. Meio do corte (50%)
            PizzeriaContentStudioSession.SetProcessProgress(0.5f);
            Assert.AreEqual(0.5f, PizzeriaContentStudioSession.PinnedState.processProgress);

            // 3. Conclusão (100%)
            PizzeriaContentStudioSession.SetProcessProgress(1.0f);
            Assert.AreEqual(1.0f, PizzeriaContentStudioSession.PinnedState.processProgress);
        }

        [Test]
        public void ToolContactAndCuttingHeight_EvaluatesContactCorrectly()
        {
            var viewport = new PizzeriaSharedViewport3D();

            PizzeriaContentStudioSession.PinAssembly(
                _benchStation,
                null,
                0,
                _tomatoDef,
                _knifeDef,
                _sliceProcess);

            // Em repouso (progresso 0%): faca acima do ingrediente, sem contato
            PizzeriaContentStudioSession.SetProcessProgress(0f);
            viewport.RebuildPreviewScene();
            Assert.IsFalse(viewport.HasCuttingContact, "Progresso 0% não deve registrar contato no corte.");
            Assert.Greater(viewport.EvaluatedCuttingHeightCm, 5f, "Faca deve estar elevada em relação ao tampo.");

            // Durante o corte (progresso 50%): lâmina toca o ingrediente
            PizzeriaContentStudioSession.SetProcessProgress(0.5f);
            viewport.RebuildPreviewScene();
            Assert.IsTrue(viewport.HasCuttingContact, "Progresso 50% deve registrar contato no corte do ingrediente.");

            viewport.Cleanup();
        }
    }
}
