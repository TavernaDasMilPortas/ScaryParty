#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Authoring
{
    /// <summary>
    /// Gerador e repositório de presets padrão de layout de slots da pizzaria.
    /// Garante a existência dos presets reutilizáveis:
    /// - Bancada de Preparo (Grade 2x2)
    /// - Suporte de Utensílios (Linha 4)
    /// - Bancada de Apoio (Linha 2)
    /// - Geladeira / Dispensa (Prateleiras)
    /// - Pilha de Caixas (Pilha Vertical)
    /// - Quadro de Comandas (Grade 2x3 Vertical)
    /// </summary>
    public static class StationLayoutPresetLibrary
    {
        public const string LayoutsDirectory = "Assets/Piaaria/ScriptableObjects/Pizzeria/Layouts";

        [InitializeOnLoadMethod]
        public static void EnsureDefaultLayoutsExist()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Directory.Exists(LayoutsDirectory))
            {
                Directory.CreateDirectory(LayoutsDirectory);
            }

            EnsureLayout("Layout_PrepBench_Grid2x2", PizzeriaStationKind.Prep, StationPresetType.Grid, 2, 2, 0.35f, 0.35f, 0.30f, Vector3.up);
            EnsureLayout("Layout_ToolRack_Line4", PizzeriaStationKind.ToolRack, StationPresetType.Line, 1, 4, 0.18f, 0.18f, 0.30f, Vector3.up);
            EnsureLayout("Layout_Counter_Line2", PizzeriaStationKind.Counter, StationPresetType.Line, 1, 2, 0.40f, 0.40f, 0.30f, Vector3.up);
            EnsureLayout("Layout_Fridge_Shelves", PizzeriaStationKind.Fridge, StationPresetType.Shelves, 3, 2, 0.30f, 0.30f, 0.35f, Vector3.up);
            EnsureLayout("Layout_BoxStack_Vertical", PizzeriaStationKind.BoxStack, StationPresetType.VerticalStack, 5, 1, 0.30f, 0.30f, 0.08f, Vector3.up);
            EnsureLayout("Layout_TicketBoard_Grid", PizzeriaStationKind.TicketBoard, StationPresetType.Grid, 2, 3, 0.22f, 0.02f, 0.28f, Vector3.forward);
        }

        private static StationSlotLayoutAsset EnsureLayout(
            string layoutName,
            PizzeriaStationKind kind,
            StationPresetType presetType,
            int rows,
            int cols,
            float spX,
            float spZ,
            float spY,
            Vector3 normal)
        {
            string path = $"{LayoutsDirectory}/{layoutName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<StationSlotLayoutAsset>(path);
            if (existing != null) return existing;

            var layout = ScriptableObject.CreateInstance<StationSlotLayoutAsset>();
            layout.layoutName = layoutName;
            layout.targetStationKind = kind;
            layout.presetType = presetType;
            layout.rows = rows;
            layout.columns = cols;
            layout.spacingX = spX;
            layout.spacingZ = spZ;
            layout.spacingY = spY;
            layout.surfaceNormal = normal;

            layout.RecalculateFromPreset(out _, out _, out _, out _);

            AssetDatabase.CreateAsset(layout, path);
            AssetDatabase.SaveAssets();
            return layout;
        }
    }
}
#endif
