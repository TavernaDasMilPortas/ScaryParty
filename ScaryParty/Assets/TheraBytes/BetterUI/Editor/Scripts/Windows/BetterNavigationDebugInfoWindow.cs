using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheraBytes.BetterUi.Editor
{
    public class BetterNavigationDebugInfoWindow : EditorWindow
    {
        [MenuItem("Tools/Better UI/Debug/Navigation", false, 120)]
        public static void ShowWindow()
        {
            var win = EditorWindow.GetWindow<BetterNavigationDebugInfoWindow>("Better Navigation Debug Info") as BetterNavigationDebugInfoWindow;
            win.Show();
        }

        Vector2 scroll;

        void OnInspectorUpdate()
        {
            Repaint();
        }

        private void OnGUI()
        {
            float lblWdth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 200;

            scroll = EditorGUILayout.BeginScrollView(scroll);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Data is updated during play mode. You are not in play mode.", MessageType.Info);
            }

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.ObjectField("Current Better Navigation", 
                BetterNavigation.Current, typeof(BetterNavigation), true);

            EditorGUILayout.ObjectField("Focused Navigation Group",
                NavigationGroup.Current, typeof(NavigationGroup), true);

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.ObjectField("Last Remembered Selection",
                BetterNavigation.LastSelection, typeof(Selectable), true);

            EditorGUILayout.ObjectField("Current Selection",
                EventSystem.current?.currentSelectedGameObject, typeof(GameObject), true);

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"Active Navigation Controllers:\t\t{NavigationController.ActiveControllers.Count}");
            
            EditorGUI.indentLevel++;
            foreach (var ctrl in NavigationController.ActiveControllers)
            {
                EditorGUILayout.ObjectField(ctrl, typeof(NavigationController), true);
            }
            
            EditorGUI.indentLevel--;

            var inactiveControllers = NavigationController.AllControllers
                .Where(o => !NavigationController.ActiveControllers.Contains(o));

            EditorGUILayout.LabelField($"Inactive Navigation Controllers:\t\t{inactiveControllers.Count()}");


            EditorGUI.indentLevel++;
            foreach (var ctrl in inactiveControllers)
            {
                EditorGUILayout.ObjectField(ctrl, typeof(NavigationController), true);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField($"Active Button Interaction Handlers:\t{ButtonInteractionHandler.ActiveHandlers.Count}");

            EditorGUI.indentLevel++;
            foreach (var handler in ButtonInteractionHandler.ActiveHandlers)
            {
                EditorGUILayout.ObjectField(handler, typeof(ButtonInteractionHandler), true);
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Current Device Type", BetterNavigation.Current?.InputDetector.CurrentNavigationInfo.Device.ToString());
            EditorGUILayout.LabelField("Current Input Action", BetterNavigation.Current?.InputDetector.CurrentNavigationInfo.Action.ToString());

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");

            EditorGUILayout.LabelField("Last Device Type", BetterNavigation.Current?.InputDetector.LastValidNavigationInfo.Device.ToString());
            EditorGUILayout.LabelField("Last Input Action", BetterNavigation.Current?.InputDetector.LastValidNavigationInfo.Action.ToString());

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(true);
            GUILayout.Toggle(BetterNavigation.Current?.InputDetector.LastInputWasGamepad ?? false,
                "Last Input Was Gamepad");
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();

            EditorGUIUtility.labelWidth = lblWdth;
        }
    }
}
