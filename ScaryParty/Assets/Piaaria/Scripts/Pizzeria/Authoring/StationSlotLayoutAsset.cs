using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Stations;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ScaryParty.Pizzeria.Authoring
{
    /// <summary>
    /// Asset reutilizável de layout espacial de slots para bancadas e estações da pizzaria.
    /// Define o algoritmo de distribuição espacial (Individual, Linha, Grade, Prateleiras, Pilha Vertical, Livre)
    /// e mantém uma lista estável de slots com suporte a travamento de ajustes manuais por slot (isManualOverride).
    /// </summary>
    [CreateAssetMenu(fileName = "SlotLayout_", menuName = "Scary Party/Pizzeria/Station Slot Layout")]
    public class StationSlotLayoutAsset : ScriptableObject
    {
        [Header("Identificação do Layout")]
        public string layoutName = "Layout_Grade2x2";
        public PizzeriaStationKind targetStationKind = PizzeriaStationKind.Prep;

        [Header("Preset de Distribuição Espacial")]
        public StationPresetType presetType = StationPresetType.Grid;

        [Tooltip("Quantidade de linhas no preset (ou níveis em Prateleiras / Pilha)")]
        [Range(1, 10)]
        public int rows = 2;

        [Tooltip("Quantidade de colunas no preset (ou itens por nível em Prateleiras)")]
        [Range(1, 10)]
        public int columns = 2;

        [Tooltip("Espaçamento horizontal ao longo do eixo X (em metros)")]
        public float spacingX = 0.35f;

        [Tooltip("Espaçamento de profundidade ao longo do eixo Z (em metros)")]
        public float spacingZ = 0.35f;

        [Tooltip("Espaçamento de altura ao longo do eixo Y (em metros) para Prateleiras ou Pilhas")]
        public float spacingY = 0.30f;

        [Tooltip("Margem interna de borda da bancada (em metros)")]
        public float margin = 0.05f;

        [Tooltip("Offset central relativo à superfície de trabalho")]
        public Vector3 originOffset = Vector3.zero;

        [Tooltip("Normal da superfície de trabalho (Vector3.up para tampos horizontais, Vector3.forward para quadros verticais)")]
        public Vector3 surfaceNormal = Vector3.up;

        [Header("Slots Calculados e Sobrescritos")]
        public List<StationSlotDefinition> slots = new List<StationSlotDefinition>();

        public int SlotCount => slots != null ? slots.Count : 0;

        public Vector3 GetCalculatedPresetPosition(int r, int c)
        {
            return StationSlotLayoutCalculator.GetCalculatedPresetPosition(
                presetType, r, c, rows, columns, spacingX, spacingZ, spacingY, originOffset);
        }

        public Vector3 GetCalculatedPresetEuler()
        {
            return StationSlotLayoutCalculator.GetCalculatedPresetEuler(surfaceNormal);
        }

        public void RecalculateFromPreset(
            out int maintainedCount,
            out int movedCount,
            out int createdCount,
            out int removedCount)
        {
            StationSlotLayoutCalculator.RecalculateSlots(
                presetType, rows, columns, spacingX, spacingZ, spacingY, originOffset, surfaceNormal,
                targetStationKind, ref slots, out maintainedCount, out movedCount, out createdCount, out removedCount);

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public bool ResetSlotToPreset(int slotIndex)
        {
            bool ok = StationSlotLayoutCalculator.ResetSlotToPreset(
                presetType, rows, columns, spacingX, spacingZ, spacingY, originOffset, surfaceNormal, slots, slotIndex);
#if UNITY_EDITOR
            if (ok) EditorUtility.SetDirty(this);
#endif
            return ok;
        }

        public bool ApplyToStation(StationView station, Transform surfaceReference = null)
        {
            if (station == null) return false;

#if UNITY_EDITOR
            Undo.RegisterCompleteObjectUndo(station, $"Aplicar Layout '{name}'");
#endif

            Transform slotsContainer = station.transform.Find("Slots");
            if (slotsContainer == null)
            {
                var go = new GameObject("Slots");
#if UNITY_EDITOR
                Undo.RegisterCreatedObjectUndo(go, "Criar Contêiner de Slots");
#endif
                slotsContainer = go.transform;
                slotsContainer.SetParent(station.transform, false);
                slotsContainer.localPosition = Vector3.zero;
                slotsContainer.localRotation = Quaternion.identity;
                slotsContainer.localScale = Vector3.one;
            }

            int count = slots != null ? slots.Count : 0;
            var newAnchors = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                var def = slots[i];
                string expectedName = !string.IsNullOrEmpty(def.slotName) ? def.slotName : $"Slot_{i}";

                Transform slotTransform = null;
                if (station.slotAnchors != null && i < station.slotAnchors.Length && station.slotAnchors[i] != null)
                {
                    slotTransform = station.slotAnchors[i];
                }
                if (slotTransform == null)
                {
                    slotTransform = slotsContainer.Find(expectedName);
                }

                if (slotTransform == null)
                {
                    var sGo = new GameObject(expectedName);
#if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(sGo, $"Criar {expectedName}");
#endif
                    slotTransform = sGo.transform;
                    slotTransform.SetParent(slotsContainer, false);
                }

#if UNITY_EDITOR
                Undo.RecordObject(slotTransform, $"Atualizar Pose de {expectedName}");
#endif
                slotTransform.name = expectedName;
                slotTransform.localPosition = def.localPosition;
                slotTransform.localEulerAngles = def.localEulerAngles;
                slotTransform.localScale = def.localScale != Vector3.zero ? def.localScale : Vector3.one;

                newAnchors[i] = slotTransform;
            }

            station.slotAnchors = newAnchors;

#if UNITY_EDITOR
            EditorUtility.SetDirty(station);
#endif
            return true;
        }

        public void ExtractFromStation(StationView station)
        {
            if (station == null || station.slotAnchors == null) return;

            slots.Clear();
            for (int i = 0; i < station.slotAnchors.Length; i++)
            {
                var anchor = station.slotAnchors[i];
                if (anchor == null) continue;

                slots.Add(new StationSlotDefinition
                {
                    slotIndex = i,
                    slotName = anchor.name,
                    role = DetermineDefaultRoleForKind(targetStationKind),
                    localPosition = anchor.localPosition,
                    localEulerAngles = anchor.localEulerAngles,
                    localScale = anchor.localScale,
                    isManualOverride = true
                });
            }

            presetType = StationPresetType.Free;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        public bool ValidateLayout(out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (slots == null || slots.Count == 0)
            {
                warnings.Add("Nenhum slot definido no layout.");
                return true;
            }

            var names = new HashSet<string>();
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null)
                {
                    errors.Add($"Slot no índice {i} está nulo.");
                    continue;
                }

                if (names.Contains(s.slotName))
                {
                    warnings.Add($"Nome de slot duplicado '{s.slotName}' no índice {i}.");
                }
                names.Add(s.slotName);

                if (s.localScale.sqrMagnitude < 0.001f)
                {
                    errors.Add($"Slot '{s.slotName}' possui escala zerada ({s.localScale}).");
                }

                if (s.localPosition.magnitude > 5.0f)
                {
                    warnings.Add($"Slot '{s.slotName}' está a mais de 5 metros da origem local ({s.localPosition}).");
                }

                for (int j = i + 1; j < slots.Count; j++)
                {
                    var other = slots[j];
                    if (other != null && Vector3.Distance(s.localPosition, other.localPosition) < 0.05f)
                    {
                        warnings.Add($"Slots '{s.slotName}' e '{other.slotName}' estão quase sobrepostos (distância < 5 cm).");
                    }
                }
            }

            return errors.Count == 0;
        }

        public static StationSlotRole DetermineDefaultRoleForKind(PizzeriaStationKind kind)
        {
            switch (kind)
            {
                case PizzeriaStationKind.Prep:
                case PizzeriaStationKind.Assembly:
                    return StationSlotRole.Workpost;
                case PizzeriaStationKind.Counter:
                case PizzeriaStationKind.Staging:
                    return StationSlotRole.Holding;
                case PizzeriaStationKind.Pantry:
                case PizzeriaStationKind.Fridge:
                    return StationSlotRole.Storage;
                case PizzeriaStationKind.ToolRack:
                    return StationSlotRole.ToolRest;
                case PizzeriaStationKind.TicketBoard:
                    return StationSlotRole.Ticket;
                case PizzeriaStationKind.BoxStack:
                    return StationSlotRole.PackagingStack;
                case PizzeriaStationKind.Trash:
                    return StationSlotRole.Trash;
                default:
                    return StationSlotRole.Holding;
            }
        }
    }
}
