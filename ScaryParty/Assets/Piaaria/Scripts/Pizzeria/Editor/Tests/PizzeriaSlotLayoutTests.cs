using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor.Tests
{
    [TestFixture]
    public class PizzeriaSlotLayoutTests
    {
        private GameObject _counterGo1;
        private GameObject _counterGo2;
        private StationView _counterStation1;
        private StationView _counterStation2;
        private StationSlotLayoutAsset _sharedPreset;

        [SetUp]
        public void SetUp()
        {
            _counterGo1 = new GameObject("TestCounter1");
            _counterStation1 = _counterGo1.AddComponent<StationView>();
            _counterStation1.stationId = 101;
            _counterStation1.stationName = "Bancada 1";

            _counterGo2 = new GameObject("TestCounter2");
            _counterStation2 = _counterGo2.AddComponent<StationView>();
            _counterStation2.stationId = 102;
            _counterStation2.stationName = "Bancada 2 (Com Suporte Lateral)";

            _sharedPreset = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();
            _sharedPreset.layoutName = "TestSharedGrid2x2";
            _sharedPreset.presetType = StationPresetType.Grid;
            _sharedPreset.rows = 2;
            _sharedPreset.columns = 2;
            _sharedPreset.spacingX = 0.35f;
            _sharedPreset.spacingZ = 0.35f;
            _sharedPreset.originOffset = Vector3.zero;

            _sharedPreset.RecalculateFromPreset(out _, out _, out _, out _);
        }

        [TearDown]
        public void TearDown()
        {
            if (_counterGo1 != null) Object.DestroyImmediate(_counterGo1);
            if (_counterGo2 != null) Object.DestroyImmediate(_counterGo2);
            if (_sharedPreset != null) Object.DestroyImmediate(_sharedPreset);
        }

        /// <summary>
        /// TESTE DO CRITÉRIO DE ACEITE (Seção 3 do Plano):
        /// Duas bancadas usam a mesma grade 2 × 2. A segunda tem o slot traseiro direito deslocado
        /// para desviar de um suporte. Alterar o espaçamento do preset deve atualizar as duas
        /// e preservar o ajuste manual. Aplicar à bancada deve produzir exatamente quatro slots
        /// em cada uma, mantendo referências e ajustes.
        /// </summary>
        [Test]
        public void AcceptanceCriterion_TwoCountersShareGrid_OneWithManualShift_SpacingUpdatePreservesManualSlot()
        {
            // 1. Ambas as bancadas recebem a grade 2x2 inicial
            Assert.AreEqual(4, _sharedPreset.SlotCount);

            // Criar layout da Bancada 1 (baseado no preset compartilhado)
            var layoutCounter1 = ScriptableObject.Instantiate(_sharedPreset);
            // Criar layout da Bancada 2 (cópia com ajuste manual na ocorrência)
            var layoutCounter2 = ScriptableObject.Instantiate(_sharedPreset);

            // 2. Na Bancada 2, o slot traseiro direito (slot 3, r=1, c=1) é deslocado para desviar do suporte
            // e marcado como isManualOverride = true
            Vector3 customManualPos = new Vector3(0.32f, 0.05f, 0.28f);
            layoutCounter2.slots[3].localPosition = customManualPos;
            layoutCounter2.slots[3].isManualOverride = true;

            // 3. Alterar o espaçamento do preset de 0.35m para 0.50m em X e 0.45m em Z
            float newSpacingX = 0.50f;
            float newSpacingZ = 0.45f;

            layoutCounter1.spacingX = newSpacingX;
            layoutCounter1.spacingZ = newSpacingZ;
            layoutCounter1.RecalculateFromPreset(out int m1, out int mov1, out int c1, out int r1);

            layoutCounter2.spacingX = newSpacingX;
            layoutCounter2.spacingZ = newSpacingZ;
            layoutCounter2.RecalculateFromPreset(out int m2, out int mov2, out int c2, out int r2);

            // Verificação 1: Bancada 1 teve todos os 4 slots movidos para as novas posições
            Assert.AreEqual(4, layoutCounter1.SlotCount);
            Assert.AreEqual(4, mov1);
            Assert.AreEqual(0, m1);

            Vector3 expectedRearRightPure = layoutCounter1.GetCalculatedPresetPosition(1, 1);
            Assert.AreEqual(expectedRearRightPure.x, layoutCounter1.slots[3].localPosition.x, 0.001f);
            Assert.AreEqual(expectedRearRightPure.z, layoutCounter1.slots[3].localPosition.z, 0.001f);

            // Verificação 2: Bancada 2 teve 3 slots atualizados para o novo espaçamento e 1 slot mantido (o manual)!
            Assert.AreEqual(4, layoutCounter2.SlotCount);
            Assert.AreEqual(3, mov2, "Slots 0, 1 e 2 devem ser atualizados pelo novo espaçamento.");
            Assert.AreEqual(1, m2, "Slot 3 deve ser estritamente preservado devido ao isManualOverride.");

            // Coordenada do slot 3 na Bancada 2 continua exatamente o customManualPos!
            Assert.AreEqual(customManualPos.x, layoutCounter2.slots[3].localPosition.x, 0.0001f);
            Assert.AreEqual(customManualPos.y, layoutCounter2.slots[3].localPosition.y, 0.0001f);
            Assert.AreEqual(customManualPos.z, layoutCounter2.slots[3].localPosition.z, 0.0001f);

            // 4. Aplicar layouts às duas bancadas físicas
            bool ok1 = layoutCounter1.ApplyToStation(_counterStation1);
            bool ok2 = layoutCounter2.ApplyToStation(_counterStation2);

            Assert.IsTrue(ok1);
            Assert.IsTrue(ok2);

            // Ambas as bancadas devem possuir exatamente 4 slotAnchors
            Assert.IsNotNull(_counterStation1.slotAnchors);
            Assert.AreEqual(4, _counterStation1.slotAnchors.Length);

            Assert.IsNotNull(_counterStation2.slotAnchors);
            Assert.AreEqual(4, _counterStation2.slotAnchors.Length);

            // Na Bancada 2, o 4º transform (Slot_3) tem a posição customizada exata
            Assert.AreEqual(customManualPos.x, _counterStation2.slotAnchors[3].localPosition.x, 0.001f);
            Assert.AreEqual(customManualPos.y, _counterStation2.slotAnchors[3].localPosition.y, 0.001f);
            Assert.AreEqual(customManualPos.z, _counterStation2.slotAnchors[3].localPosition.z, 0.001f);

            // 5. Teste de Restauração ("Restaurar este slot ao preset")
            layoutCounter2.ResetSlotToPreset(3);
            Assert.IsFalse(layoutCounter2.slots[3].isManualOverride, "isManualOverride deve ser desativado após reset.");
            Assert.AreEqual(expectedRearRightPure.x, layoutCounter2.slots[3].localPosition.x, 0.001f, "Slot deve voltar à fórmula pura do preset.");

            // Cleanup local
            Object.DestroyImmediate(layoutCounter1);
            Object.DestroyImmediate(layoutCounter2);
        }

        [Test]
        public void StationSlotLayout_PresetTypes_ProduceCorrectSlotCountsAndPositions()
        {
            var layout = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();

            // 1. Individual
            layout.presetType = StationPresetType.Individual;
            layout.originOffset = new Vector3(0f, 0.40f, 0f);
            layout.RecalculateFromPreset(out _, out _, out _, out _);
            Assert.AreEqual(1, layout.SlotCount);
            Assert.AreEqual(new Vector3(0f, 0.40f, 0f), layout.slots[0].localPosition);

            // 2. Line (4 slots)
            layout.originOffset = Vector3.zero;
            layout.presetType = StationPresetType.Line;
            layout.columns = 4;
            layout.spacingX = 0.20f;
            layout.RecalculateFromPreset(out _, out _, out _, out _);
            Assert.AreEqual(4, layout.SlotCount);
            Assert.Less(layout.slots[0].localPosition.x, layout.slots[3].localPosition.x);

            // 3. VerticalStack (5 caixas)
            layout.originOffset = Vector3.zero;
            layout.presetType = StationPresetType.VerticalStack;
            layout.rows = 5;
            layout.spacingY = 0.08f;
            layout.RecalculateFromPreset(out _, out _, out _, out _);
            Assert.AreEqual(5, layout.SlotCount);
            Assert.AreEqual(0f, layout.slots[0].localPosition.y, 0.001f);
            Assert.AreEqual(0.32f, layout.slots[4].localPosition.y, 0.001f);

            // 4. Shelves (3 níveis x 2 itens)
            layout.originOffset = Vector3.zero;
            layout.presetType = StationPresetType.Shelves;
            layout.rows = 3;
            layout.columns = 2;
            layout.spacingX = 0.30f;
            layout.spacingY = 0.35f;
            layout.RecalculateFromPreset(out _, out _, out _, out _);
            Assert.AreEqual(6, layout.SlotCount);

            Object.DestroyImmediate(layout);
        }

        [Test]
        public void StationSlotLayout_Validation_DetectsAnomalies()
        {
            var layout = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();
            layout.slots = new List<StationSlotDefinition>
            {
                new StationSlotDefinition { slotIndex = 0, slotName = "Slot_0", localPosition = Vector3.zero, localScale = Vector3.one },
                new StationSlotDefinition { slotIndex = 1, slotName = "Slot_0", localPosition = new Vector3(0.01f, 0f, 0f), localScale = Vector3.zero } // Nome repetido, sobreposição e escala zero!
            };

            bool valid = layout.ValidateLayout(out var errors, out var warnings);
            Assert.IsFalse(valid, "Layout deve falhar validação devido à escala zerada.");
            Assert.Greater(errors.Count, 0);
            Assert.Greater(warnings.Count, 0);

            Object.DestroyImmediate(layout);
        }
    }
}
