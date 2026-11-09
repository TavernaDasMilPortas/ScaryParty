using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace TheraBytes.BetterUi.Editor
{
    public delegate void ScreenConfigConnectionCallback(string configName, SerializedProperty property);

    public static class ScreenConfigConnectionHelper
    {
        static HashSet<int> foldoutHashes = new HashSet<int>();

        static GUIStyle noPaddingLabel = new GUIStyle(EditorStyles.label)
        {
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0)
        };

        public static bool IsSmallMode { get; private set; } = true;

        public static void DrawSizerGui(string title, SerializedProperty collection, ref SerializedProperty fallback)
        {
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            DrawGui(title, collection, ref fallback, null, (name, o) =>
            {
                var obj = o.GetValue<ScreenDependentSize>();
                obj.DynamicInitialization();
            });

            EditorGUI.indentLevel = indent;
        }

        public static void DrawGui(string title, SerializedProperty collection, ref SerializedProperty fallback,
         ScreenConfigConnectionCallback drawContent = null,
         ScreenConfigConnectionCallback newElementInitCallback = null,
         ScreenConfigConnectionCallback elementDeleteCallback = null)
        {
            var configs = collection.GetValue<ISizeConfigCollection>();
            if (configs.IsDirty)
            {
                configs.Sort();
                return;
            }

            GUILayout.Space(2);
            if (IsSmallMode)
            {
                DrawSmallGui(title, collection, ref fallback, configs,
                    drawContent, newElementInitCallback);
            }
            else
            {
                DrawBigGui(title, collection, ref fallback, configs, 
                    drawContent, newElementInitCallback, elementDeleteCallback);
            }


            fallback.serializedObject.ApplyModifiedProperties();

            // immediately handle the ordering of the configs and update the serialized object
            // otherwise this would happen one frame delayed and an error would popup in the console.
            if (configs.IsDirty)
            {
                configs.Sort();
                fallback.serializedObject.Update();
            }
        }

        public static void DrawSmallGui(string title, SerializedProperty collection, ref SerializedProperty fallback,
            ISizeConfigCollection configs,
            ScreenConfigConnectionCallback drawContent = null,
            ScreenConfigConnectionCallback newElementInitCallback = null)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();

            DrawTitleAndSmallLargeModeToggle(title);

            string currentConfig = configs.GetCurrentConfigName();
            string configName = GetConfigName(currentConfig);

            GUIContent configContent = new GUIContent($"♦ {configName}", "The currently active Screen Configuration");
            GUILayout.Label(configContent, EditorStyles.helpBox, 
                GUILayout.Width(EditorStyles.helpBox.CalcSize(configContent).x));

            if (configs.Count > 0)
            {
                GUIContent additionalConfigsContent = new GUIContent($"+{configs.Count}", "The number of additional screen configurations");
                GUILayout.Label(additionalConfigsContent, EditorStyles.helpBox,
                    GUILayout.Width(EditorStyles.helpBox.CalcSize(additionalConfigsContent).x));
            }

            string currentScreen = ResolutionMonitor.CurrentScreenConfiguration?.Name;
            if (currentConfig != currentScreen)
            {
                var content = new GUIContent("", $"Add a configuration for '{currentScreen}'");
                if (GUILayout.Button(content, "OL Plus", GUILayout.Width(20)))
                {
                    AddSizerToList(currentScreen, ref fallback, collection.FindPropertyRelative("items"), newElementInitCallback);
                    configs.MarkDirty();
                }
            }

            EditorGUILayout.EndHorizontal();


            SerializedProperty items = collection.FindPropertyRelative("items");

            HashSet<string> names = IterateScreenConfigList(fallback, currentConfig, items, 
                (int index, SerializedProperty item, string name) =>
            {
                if(name == configName)
                {
                    if (drawContent != null)
                        drawContent(name, item);
                    else
                        EditorGUILayout.PropertyField(item);
                }
                return index;
            });

            EditorGUILayout.EndVertical();

        }

        public static void DrawBigGui(string title, SerializedProperty collection, ref SerializedProperty fallback, 
            ISizeConfigCollection configs,
            ScreenConfigConnectionCallback drawContent = null, 
            ScreenConfigConnectionCallback newElementInitCallback = null,
            ScreenConfigConnectionCallback elementDeleteCallback = null)
        {
            int baseHash = title.GetHashCode();

            Rect bgRect = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            DrawTitleAndSmallLargeModeToggle(title);

            EditorGUILayout.EndHorizontal();

            string currentConfig = configs.GetCurrentConfigName();
            SerializedProperty items = collection.FindPropertyRelative("items");

            Func<int, SerializedProperty, string, int> processConfig = (int index, SerializedProperty item, string name) =>
            {
                bool foldout = false;

                // DELETE
                bool isFallback = index < 0;
                bool setCurrent = isFallback && currentConfig == null; // Fallback item
                DrawItemHeader(name, baseHash, setCurrent, GetConfigName(currentConfig), out foldout,
                    isFallback ? (Action)null : () =>
                    {
                        if (elementDeleteCallback != null)
                            elementDeleteCallback(name, item);

                        items.DeleteArrayElementAtIndex(index);
                        index--;
                        foldout = false;
                    });

                if (foldout)
                {
                    if (drawContent != null)
                        drawContent(name, item);
                    else
                        EditorGUILayout.PropertyField(item);
                }

                return index;
            };

            string currentScreen = ResolutionMonitor.CurrentScreenConfiguration?.Name;

            HashSet<string> names = IterateScreenConfigList(fallback, currentConfig, items, processConfig);

            // ADD NEW
            string[] options = ResolutionMonitor.Instance.OptimizedScreens
                .Where(o => !(names.Contains(o.Name)))
                .Select(o => o.Name)
                .ToArray();

            if (options.Length > 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                // Quick Action: Add current config
                if (currentConfig != currentScreen)
                {
                    GUIStyle style = "minibutton";
                    var content = EditorGUIUtility.IconContent("OL Plus");
                    content.text = $" {currentScreen}";

                    var size = style.CalcSize(content);
                    Rect rect = new Rect(bgRect.xMax - size.x - 20, bgRect.y + 3, size.x, 16);

                    if (GUI.Button(rect, content, "minibutton"))
                    {
                        AddSizerToList(currentScreen, ref fallback, items, newElementInitCallback);
                        configs.MarkDirty();
                    }
                }

                //int idx = EditorGUILayout.Popup(-1, options, "OL Plus", GUILayout.Width(20));
                Rect r = new Rect(bgRect.x + bgRect.width - 20, bgRect.y + 3, 20, 20);
                int idx = EditorGUI.Popup(r, -1, options, "OL Plus");

                if (idx != -1)
                {
                    string name = options[idx];
                    idx = -1;

                    AddSizerToList(name, ref fallback, items, newElementInitCallback);
                    configs.MarkDirty();
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();


        }

        private static HashSet<string> IterateScreenConfigList(SerializedProperty fallback, string currentConfig, SerializedProperty items, Func<int, SerializedProperty, string, int> processConfig)
        {
            // LIST
            bool configFound = currentConfig != null;
            HashSet<string> names = new HashSet<string>();

            for (int i = 0; i < items.arraySize; i++)
            {
                SerializedProperty item = items.GetArrayElementAtIndex(i);
                var nameProp = item.FindPropertyRelative("screenConfigName");
                string name = "?";

                if (nameProp != null)
                {
                    name = nameProp.stringValue;
                    names.Add(name);
                }
                else
                {
                    Debug.LogError("no serialized property named 'screenConfigName' found.");
                }

                i = processConfig(i, item, name);
            }

            // FALLBACK
            string fallbackName = GetConfigName(null);
            processConfig(-1, fallback, fallbackName);
            return names;
        }

        private static string GetConfigName(string configName)
        {
            if(string.IsNullOrEmpty(configName))
                return string.Format("{0} (Fallback)", ResolutionMonitor.Instance.FallbackName);

            return configName;
        }

        private static void DrawTitleAndSmallLargeModeToggle(string title)
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(16), GUILayout.Height(16));
            rect.y += 1; // adjust y position to align with the label
            GUI.Box(rect, GUIContent.none, GUI.skin.textField);
            var icon = EditorGUIUtility.IconContent(IsSmallMode ? "Toolbar Plus" : "Toolbar Minus", "Switch between small- and big mode. The small mode has not as many options and you can only view the currently active screen configuration, but it consumes less screen space.");
            IsSmallMode = GUI.Toggle(rect, IsSmallMode, icon, noPaddingLabel);

            GUILayout.Space(2);
            
            GUIContent titleContent = new GUIContent(title);
            EditorGUILayout.LabelField(titleContent, EditorStyles.boldLabel);
        }

        public static void AddSizerToList(string configName, ref SerializedProperty fallback, SerializedProperty items, 
            ScreenConfigConnectionCallback callback = null)
        {
            string fallbackPath = fallback.propertyPath;
            items.arraySize += 1;
            var newElement = items.GetArrayElementAtIndex(items.arraySize - 1);

            SerializedPropertyUtil.Copy(fallback, newElement);

            // after the copy action the property pointer points somewhere.
            // so, point to the right prop again.
            newElement = items.GetArrayElementAtIndex(items.arraySize - 1);
            fallback = fallback.serializedObject.FindProperty(fallbackPath);

            if (callback != null)
            {
                callback(configName, newElement);
            }

            var prop = newElement.FindPropertyRelative("screenConfigName");
            if (prop != null)
                prop.stringValue = configName;
            else
                Debug.LogError("no serialized property named 'screenConfigName' found.");
        }

        private static void DrawItemHeader(string configName, int baseHash, bool setCurrent, string currentConfigName, out bool foldout, Action deleteCallback = null)
        {
            int hash = GetHash(baseHash, configName);
            bool isCurrentConfig = configName == currentConfigName;
            bool isSimulatedConfig = (ResolutionMonitor.SimulatedScreenConfig != null) && (configName == ResolutionMonitor.SimulatedScreenConfig.Name);
            bool exists = ResolutionMonitor.Instance.FallbackName + " (Fallback)" == configName
                || ResolutionMonitor.Instance.OptimizedScreens.Any(o => o.Name == configName);

            foldout = foldoutHashes.Contains(hash) || isCurrentConfig;

            EditorGUILayout.BeginHorizontal();

            string title = string.Format("{0} {1}{2} {3}{4}",
                (foldout) ? "▼" : "►",
                (isCurrentConfig) ? "♦" : "◊",
                (isSimulatedConfig) ? " ⃰" : " ",
                configName,
                (exists) ? "" : " (‼ not found ‼)");

            if (GUILayout.Button(title, "TextField", GUILayout.ExpandWidth(true)))//(foldout) ? "MiniPopup" : "MiniPullDown"))
            {
                if (!(isCurrentConfig) && !(foldoutHashes.Remove(hash)))
                {
                    foldoutHashes.Add(hash);
                    foldout = true;
                }
            }

           GUILayout.Space(-6);

            if (deleteCallback != null)
            {
                if (GUILayout.Button("X", "SearchCancelButton", GUILayout.Width(20)))//"MiniButton", GUILayout.Width(20))))
                {
                    if (EditorUtility.DisplayDialog("Delete?",
                    string.Format("Do you really want to delete the configuration '{0}'?", configName),
                    "Delete", "Cancel"))
                    {
                        deleteCallback();
                    }
                }
            }
            else
            {
                GUILayout.Box("", "SearchCancelButtonEmpty", GUILayout.Width(20));
            }

            EditorGUILayout.EndHorizontal();
        }

        private static int GetHash(int baseHash, string configName)
        {
            return baseHash ^ configName.GetHashCode();
        }
    }
}
