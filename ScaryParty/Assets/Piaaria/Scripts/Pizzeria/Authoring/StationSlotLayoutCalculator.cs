using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    /// <summary>
    /// Motor de cálculo determinístico e puro para posicionamento, presets e manutenção de ajustes manuais de slots.
    /// Isola a álgebra vetorial e regras de preset de dependências de engine para máxima testabilidade e robustez.
    /// </summary>
    public static class StationSlotLayoutCalculator
    {
        public static Vector3 GetCalculatedPresetPosition(
            StationPresetType presetType,
            int r,
            int c,
            int rows,
            int columns,
            float spacingX,
            float spacingZ,
            float spacingY,
            Vector3 originOffset)
        {
            Vector3 pos = originOffset;

            switch (presetType)
            {
                case StationPresetType.Individual:
                    break;

                case StationPresetType.Line:
                    int totalInLine = Mathf.Max(1, columns);
                    float lineNorm = totalInLine > 1 ? ((float)c / (totalInLine - 1)) - 0.5f : 0f;
                    pos.x += lineNorm * ((totalInLine - 1) * spacingX);
                    break;

                case StationPresetType.Grid:
                    int effectiveCols = Mathf.Max(1, columns);
                    int effectiveRows = Mathf.Max(1, rows);

                    float xNorm = effectiveCols > 1 ? ((float)c / (effectiveCols - 1)) - 0.5f : 0f;
                    float zNorm = effectiveRows > 1 ? ((float)r / (effectiveRows - 1)) - 0.5f : 0f;

                    pos.x += xNorm * ((effectiveCols - 1) * spacingX);
                    pos.z += zNorm * ((effectiveRows - 1) * spacingZ);
                    break;

                case StationPresetType.Shelves:
                    int itemsPerShelf = Mathf.Max(1, columns);
                    float shelfColNorm = itemsPerShelf > 1 ? ((float)c / (itemsPerShelf - 1)) - 0.5f : 0f;

                    pos.x += shelfColNorm * ((itemsPerShelf - 1) * spacingX);
                    pos.y += r * spacingY;
                    break;

                case StationPresetType.VerticalStack:
                    pos.y += r * spacingY;
                    break;

                case StationPresetType.Free:
                    break;
            }

            return pos;
        }

        public static Vector3 GetCalculatedPresetEuler(Vector3 surfaceNormal)
        {
            if (surfaceNormal != Vector3.up && surfaceNormal.sqrMagnitude > 0.001f)
            {
                Quaternion rot = Quaternion.LookRotation(surfaceNormal.normalized, Vector3.up);
                return rot.eulerAngles;
            }
            return Vector3.zero;
        }

        public static void RecalculateSlots(
            StationPresetType presetType,
            int rows,
            int columns,
            float spacingX,
            float spacingZ,
            float spacingY,
            Vector3 originOffset,
            Vector3 surfaceNormal,
            PizzeriaStationKind targetStationKind,
            ref List<StationSlotDefinition> slots,
            out int maintainedCount,
            out int movedCount,
            out int createdCount,
            out int removedCount)
        {
            maintainedCount = 0;
            movedCount = 0;
            createdCount = 0;
            removedCount = 0;

            if (slots == null) slots = new List<StationSlotDefinition>();

            if (presetType == StationPresetType.Free)
            {
                maintainedCount = slots.Count;
                return;
            }

            int targetCount;
            if (presetType == StationPresetType.Individual) targetCount = 1;
            else if (presetType == StationPresetType.Line) targetCount = Mathf.Max(1, columns);
            else if (presetType == StationPresetType.VerticalStack) targetCount = Mathf.Max(1, rows);
            else targetCount = Mathf.Max(1, rows * columns);

            var newSlots = new List<StationSlotDefinition>();

            int currentSlotIndex = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < (presetType == StationPresetType.Individual || presetType == StationPresetType.VerticalStack ? 1 : columns); c++)
                {
                    if (currentSlotIndex >= targetCount) break;

                    Vector3 calcPos = GetCalculatedPresetPosition(presetType, r, c, rows, columns, spacingX, spacingZ, spacingY, originOffset);
                    Vector3 calcRot = GetCalculatedPresetEuler(surfaceNormal);

                    StationSlotDefinition existing = null;
                    if (currentSlotIndex < slots.Count)
                    {
                        existing = slots[currentSlotIndex];
                    }

                    if (existing != null)
                    {
                        if (existing.isManualOverride)
                        {
                            // PRESERVAÇÃO ESTRITA: Slot ajustado à mão mantém todas as coordenadas
                            existing.slotIndex = currentSlotIndex;
                            newSlots.Add(existing);
                            maintainedCount++;
                        }
                        else
                        {
                            // Slot livre atualiza para o novo espaçamento
                            existing.slotIndex = currentSlotIndex;
                            existing.localPosition = calcPos;
                            existing.localEulerAngles = calcRot;
                            newSlots.Add(existing);
                            movedCount++;
                        }
                    }
                    else
                    {
                        // Cria novo slot
                        var newSlot = new StationSlotDefinition
                        {
                            slotIndex = currentSlotIndex,
                            slotName = $"Slot_{currentSlotIndex}",
                            role = StationSlotLayoutAsset.DetermineDefaultRoleForKind(targetStationKind),
                            localPosition = calcPos,
                            localEulerAngles = calcRot,
                            localScale = Vector3.one,
                            isManualOverride = false
                        };
                        newSlots.Add(newSlot);
                        createdCount++;
                    }

                    currentSlotIndex++;
                }
                if (currentSlotIndex >= targetCount) break;
            }

            if (slots.Count > targetCount)
            {
                for (int i = targetCount; i < slots.Count; i++)
                {
                    var excess = slots[i];
                    if (excess != null && excess.isManualOverride)
                    {
                        excess.slotIndex = newSlots.Count;
                        newSlots.Add(excess);
                        maintainedCount++;
                    }
                    else
                    {
                        removedCount++;
                    }
                }
            }

            slots = newSlots;
        }

        public static bool ResetSlotToPreset(
            StationPresetType presetType,
            int rows,
            int columns,
            float spacingX,
            float spacingZ,
            float spacingY,
            Vector3 originOffset,
            Vector3 surfaceNormal,
            List<StationSlotDefinition> slots,
            int slotIndex)
        {
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Count) return false;

            int effectiveCols = Mathf.Max(1, columns);
            int r = slotIndex / effectiveCols;
            int c = slotIndex % effectiveCols;

            if (presetType == StationPresetType.Line)
            {
                r = 0;
                c = slotIndex;
            }
            else if (presetType == StationPresetType.VerticalStack)
            {
                r = slotIndex;
                c = 0;
            }

            slots[slotIndex].localPosition = GetCalculatedPresetPosition(presetType, r, c, rows, columns, spacingX, spacingZ, spacingY, originOffset);
            slots[slotIndex].localEulerAngles = GetCalculatedPresetEuler(surfaceNormal);
            slots[slotIndex].localScale = Vector3.one;
            slots[slotIndex].isManualOverride = false;
            return true;
        }
    }
}
