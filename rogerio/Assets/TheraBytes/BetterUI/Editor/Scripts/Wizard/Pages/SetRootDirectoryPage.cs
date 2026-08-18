using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TheraBytes.BetterUi.Editor
{
    public class SetRootDirectoryPage : WizardPage
    {
        public override string NameId { get { return "SetRootDirectoryPage"; } }

        private const string RootPathKey = "RootPath";

        string path;

        public SetRootDirectoryPage(IWizard wizard) : base(wizard)
        {
        }

        protected override void OnInitialize()
        {
            path = RootDirectory.OverrideRoot;

            Add(new InfoWizardPageElement("Root Directory", InfoType.Header));
            Add(new InfoWizardPageElement("You may change the root directory for Better UI here.", InfoType.Text));
            Add(new CustomWizardPageElement((o) =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Assets/", GUILayout.Width(45));
                path = EditorGUILayout.TextField(path);
                if (GUILayout.Button("Browse . . ."))
                {
                    string selectedPath = EditorUtility.OpenFolderPanel("Select Root Directory", RootDirectory.GetAbsolutePath(""), RootDirectory.OverrideRoot);
                    if (!string.IsNullOrEmpty(selectedPath))
                    {
                        path = selectedPath.Replace(Application.dataPath, "").TrimStart('/', '\\');
                    }
                }

                EditorGUI.BeginDisabledGroup(path == RootDirectory.DefaultRoot);
                if (GUILayout.Button("Set to Default"))
                {
                    path = RootDirectory.DefaultRoot;
                }
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.EndHorizontal();
            }).MarkComplete());

            Add(new SeparatorWizardPageElement());

            Add(new ValueWizardPageElement<string>(RootPathKey,
                (o, v) =>
                {
                    EditorGUI.BeginDisabledGroup(!IsPathValid(path));
                    if (GUILayout.Button("Move Root Directory", GUILayout.Height(40)))
                    {
                        wizard.DoReloadOperation(this, () =>
                        {
                            int operationState = 0;
                            var prevPath = RootDirectory.OverrideRoot;
                            try
                            {
                                OverwriteRootDirectory(path);
                                operationState = 1;
                                string sourceDir = RootDirectory.GetAbsolutePath();
                                string targetDir = Path.Combine(Application.dataPath, path);
                                MoveAllFiles(sourceDir, targetDir);
                                operationState = 2;

                                AssetDatabase.Refresh();
                                v = path;

                                ResolutionMonitor.Editor_ResetInstance();
                                Materials.Editor_ResetInstance();
                                GlobalApplier.Editor_ResetInstance();

                                AssetDatabase.Refresh();
                            }
                            catch (Exception ex)
                            {
                                if (operationState == 1) // file was written, but move failed: revert file
                                {
                                    OverwriteRootDirectory(prevPath);
                                }

                                if (operationState < 2)
                                {
                                    Debug.LogError($"Failed to move root directory: {ex.Message}");
                                    EditorUtility.DisplayDialog("Error",
                                        "Failed to move the root directory. Please check the console for details.",
                                        "OK");
                                }


                                AssetDatabase.Refresh();
                            }
                        });
                    }

                    EditorGUI.EndDisabledGroup();



                    if (!IsPathValid(path) && path != RootDirectory.OverrideRoot)
                    {
                        EditorGUILayout.HelpBox("The path you entered is not valid. " +
                            "Please enter a valid path that does not contain invalid characters or starts with the current root directory.", MessageType.Error);
                    }
                    else if (path == RootDirectory.OverrideRoot)
                    {
                        EditorGUILayout.HelpBox($"Better UI is currently located at \"Assets/{RootDirectory.OverrideRoot}\".", MessageType.Info);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("When the root directory is changed, you need to move it back to default (using this wizard) or remove Better UI before updating to a new version.", MessageType.Warning);
                    }

                    return v;
                }).MarkComplete());
        }

        private void OverwriteRootDirectory(string newPath)
        {
            string filepath = RootDirectory.GetAbsolutePath("BetterUI/Runtime/Scripts/RootDirectory.Generated.cs");
            if (!File.Exists(filepath))
            {
                throw new FileNotFoundException($"RootDirectory.Generated.cs file not found at {filepath}. Skipping change of root path.");
            }

            File.WriteAllText(filepath,
$@"// This file is modified by the setup wizard when changing the root directory.
// Modify ""OverrideRoot"" by hand if you moved Better UI manually.
namespace TheraBytes.BetterUi
{{
    public partial class RootDirectory
    {{
        public const string OverrideRoot = ""{newPath}"";
    }}
}}
");
        }

        bool IsPathValid(string path)
        {
            return !string.IsNullOrEmpty(path)
                && !Path.GetInvalidPathChars().Any(path.Contains)
                && !path.StartsWith(RootDirectory.OverrideRoot);
        }

        private void MoveAllFiles(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir))
            {
                Debug.LogError($"Source directory does not exist: {sourceDir}");
                return;
            }
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            // move files
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                File.Move(file, destFile);
            }

            // recursively move subdirectories
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(targetDir, Path.GetFileName(subDir));
                MoveAllFiles(subDir, destSubDir);
            }
        }
    }
}
