using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TheraBytes.BetterUi;
using TheraBytes.BetterUi.Editor;
using UnityEditor;
using UnityEngine;

namespace Assets.TheraBytes.BetterUI.Editor.Scripts.Utils
{
    [InitializeOnLoad]
    internal class SceneViewGuiOverlay
    {
        static GUIStyle style = null;
        static GUIStyle Style => style ?? new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleRight,
            fontSize = 18,
            normal = { textColor = Color.green }
        };

        static StringBuilder stringBuilder = new StringBuilder();

        static SceneViewGuiOverlay()
        {
            SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView sceneView)
        {
            bool hasSimulatedConfig = GetScreenConfigSimulationText(out string simulationText);
            bool hasDetachedChildren = GetDetachedChildrenText(out string detachedText);
            if (!hasDetachedChildren && hasDetachedChildren)
                return;

            stringBuilder.Clear();

            if (hasSimulatedConfig)
            {
                stringBuilder.AppendLine(simulationText);
            }

            if(hasDetachedChildren)
            {
                stringBuilder.AppendLine(detachedText);
            }

            string text = stringBuilder.ToString();
            Handles.BeginGUI();

            // Get the size of the scene view window
            var size = sceneView.position.size;

            // Measure the text size
            Vector2 textSize = Style.CalcSize(new GUIContent(text));

            // Calculate position for lower right corner with a small margin
            float x = size.x - textSize.x - 10;
            float y = size.y - textSize.y - 30;

            GUI.Label(new Rect(x, y, textSize.x, textSize.y), text, Style);

            Handles.EndGUI();
        }

        private static bool GetScreenConfigSimulationText(out string text)
        {
            text = null;
            if (!ResolutionMonitor.HasInstance)
                return false;

            if (ResolutionMonitor.SimulatedScreenConfig == null)
                return false;

            text = $"Better UI simulates the Screen Configuration '{ResolutionMonitor.SimulatedScreenConfig.Name}'.";
            return true;
        }

        private static bool GetDetachedChildrenText(out string text)
        {
            text = null;
            if (Resources.FindObjectsOfTypeAll<SmartParentWindow>().Length == 0)
                return false;

            if (!SmartParentWindow.IsFreeMovementEnabled)
                return false;

            text = $@"Better UI's Smart Parent Window is in ""Detached Children"" mode.";
            return true;
        }
    }
}