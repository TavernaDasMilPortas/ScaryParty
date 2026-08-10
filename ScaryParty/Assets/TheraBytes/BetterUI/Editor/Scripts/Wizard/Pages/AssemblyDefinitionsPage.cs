using UnityEditor;
using UnityEngine;

namespace TheraBytes.BetterUi.Editor
{
    public class AssemblyDefinitionsPage : WizardPage
    {
        static readonly string[] ASMDEF_FILES =
            { 
                "BetterUI/Runtime/TheraBytes.BetterUi.Runtime.asmdef",
                "BetterUI/Editor/TheraBytes.BetterUi.Editor.asmdef",
                "BetterUI_TextMeshPro/Runtime/TheraBytes.BetterUi.asmref",
                "BetterUI_TextMeshPro/Editor/TheraBytes.BetterUi.Editor.asmref",
            };

        const string TEXTMESH_PRO_PATH = "BetterUI_TextMeshPro";

        const string PACKAGE_PATH_INCL_TMP = "BetterUI/packages/asmdef_incl_TextMeshPro.unitypackage";
        const string PACKAGE_PATH_EXCL_TMP = "BetterUI/packages/asmdef_excl_TextMeshPro.unitypackage";

        const string PERSISTENT_KEY = "asmdef";

        public override string NameId { get { return "AssemblyDefinitionsPage"; } }

        public AssemblyDefinitionsPage(IWizard wizard)
            : base(wizard)
        {
        }

        protected override void OnInitialize()
        {
            Add(new InfoWizardPageElement("Assembly Definition Files installation", InfoType.Header));
            
            Add(new SeparatorWizardPageElement());
#if REWIRED
            Add(new InfoWizardPageElement("You have Rewired installed.\nGetting assembly definiions compile would require many additional manual steps, including creating several assembly definition files in the Rewired folder structure and setting them up correctly. As this is pretty complicated, only advanced users should do that.\nTherefore, the installation of assembly definitions is disabled.", InfoType.ErrorBox));
#else

            Add(new InfoWizardPageElement("You can install Assembly Definition Files for Better UI here. " +
                "If you don't know what this is, you most likely don't want to install it."));

            string mainAsmdefFilePath = RootDirectory.GetAbsolutePath(ASMDEF_FILES[0]);
            if (System.IO.File.Exists(mainAsmdefFilePath))
            {
                // DELETE

                Add(new InfoWizardPageElement("Assembly Definition Files are already installed."));

                Add(new ValueWizardPageElement<string>(PERSISTENT_KEY,
                    (o, v) =>
                    {
                        if(GUILayout.Button("Remove Assembly Definition Files"))
                        {
                            wizard.DoReloadOperation(this, () =>
                            {
                                foreach (string subPath in ASMDEF_FILES)
                                {
                                    string filePath = RootDirectory.GetAbsolutePath(subPath);
                                    if (System.IO.File.Exists(filePath))
                                    {
                                        System.IO.File.Delete(filePath);
                                    }
                                }

                                AssetDatabase.Refresh();
                                v = null;
                            });
                        }

                        return v;
                    }).MarkComplete());
            }
            else
            {
                AddBelowScrollbar(new RootFolderNotificationElement(""));
                Add(new ValueWizardPageElement<string>(PERSISTENT_KEY,
                    (o, v) =>
                    {
                        if (GUILayout.Button("Install AssemblyDefinitions"))
                        {
                            string tmpPath = RootDirectory.GetAbsolutePath(TEXTMESH_PRO_PATH);
                            string packageName = (System.IO.Directory.Exists(tmpPath))
                                ? PACKAGE_PATH_INCL_TMP
                                : PACKAGE_PATH_EXCL_TMP;

                            wizard.DoReloadOperation(this, () =>
                            {
                                AssetDatabase.ImportPackage(RootDirectory.GetAbsolutePath(packageName), false);
                                v = packageName;
                            });
                        }

                        return v;
                        }).MarkComplete());
            }
#endif
        }
    }
}
