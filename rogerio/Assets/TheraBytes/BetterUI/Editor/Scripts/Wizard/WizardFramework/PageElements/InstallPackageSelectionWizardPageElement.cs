using UnityEditor;
using UnityEngine;

namespace TheraBytes.BetterUi.Editor
{
    public enum InstallSelectionState
    {
        None,
        Install,
        Remove,
    }

    public class InstallPackageSelectionWizardPageElement : WizardPageElementBase
    {
        string title;
        string pathToPackage;
        string pathToFolder;
        InstallSelectionState selectionState;

        public string PathToPackage { get { return pathToPackage; } }
        public string PathToFolder { get { return pathToFolder; } }
        public InstallSelectionState SelectionState { get { return selectionState; } }

        public InstallPackageSelectionWizardPageElement(string title, string pathToPackage, string pathToFolder)
        {
            this.title = title;
            this.pathToPackage = pathToPackage;
            this.pathToFolder = pathToFolder;
        }

        public override void DrawGui()
        {
            bool isInstalled = System.IO.Directory.Exists(pathToFolder);

            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            GUILayout.FlexibleSpace();

            if (isInstalled)
            {
                EditorGUILayout.HelpBox("✓ installed", MessageType.None);
            }


            if (isInstalled)
            {
                bool remove = GUILayout.Toggle(selectionState == InstallSelectionState.Remove,
                    FormatRemove(selectionState), EditorStyles.miniButton, GUILayout.Width(100));
                selectionState = (remove) ? InstallSelectionState.Remove : InstallSelectionState.None;
            }
            else
            {
                bool install = GUILayout.Toggle(selectionState == InstallSelectionState.Install,
                    FormatInstall(selectionState), EditorStyles.miniButton, GUILayout.Width(100));
                selectionState = (install) ? InstallSelectionState.Install : InstallSelectionState.None;
            }
            EditorGUILayout.EndHorizontal();

        }

        string FormatRemove(InstallSelectionState state)
        {
            return Format(InstallSelectionState.Remove, state);
        }

        string FormatInstall(InstallSelectionState state)
        {
            return Format(InstallSelectionState.Install, state);
        }

        string Format(InstallSelectionState desiredState, InstallSelectionState actualState)
        {
            string prefix = (desiredState == actualState) ? "▣" : "☐";
            return $"{prefix} {desiredState}";
        }

    }
}
