using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public static class KitchenVfxBuilder
    {
        private const string Source = "Assets/Piaaria/VFX/";
        private const string Output = "Assets/Resources/PizzeriaVFX/";
        private const string Export = "Assets/Piaaria/VFX/Models/Smoke01_Export.fbx";

        [MenuItem("Tools/Pizzeria/Build Kitchen VFX")]
        public static void Build()
        {
            if (!File.Exists(Export))
                ExportSmoke();
            AssetDatabase.ImportAsset(Export, ImportAssetOptions.ForceSynchronousImport);
            var mesh = AssetDatabase.LoadAllAssetsAtPath(Export).OfType<Mesh>().FirstOrDefault();
            if (mesh == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string meshGuid, out long meshId))
                throw new InvalidOperationException("Exported smoke FBX has no imported mesh.");
            Directory.CreateDirectory(Output);
            foreach (string name in new[] { "VFX_Smoke", "VFX_Mess" })
            {
                string text = File.ReadAllText(Source + name + ".vfx");
                text = Regex.Replace(text,
                    "\\\"fileID\\\":-?\\d+,\\\"guid\\\":\\\"ad082d6c31d49874e9caca25aee84311\\\"",
                    "\"fileID\":" + meshId + ",\"guid\":\"" + meshGuid + "\"");
                // The source spiral shader was not included in the merge. Use the supplied smoke shader.
                text = text.Replace("16dbac556be7c00448dd113d83bf05e1", "3001f02c542b3834d87cbb33e398ac30");
                File.WriteAllText(Output + name + ".vfx", text);
                AssetDatabase.ImportAsset(Output + name + ".vfx", ImportAssetOptions.ForceSynchronousImport);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Kitchen VFX built. Source graphs preserved; smoke mesh uses FBX. Mess uses the available smoke shader for its spiral output.");
        }

        [MenuItem("Tools/Pizzeria/Re-export Smoke FBX and Build Kitchen VFX")]
        public static void Rebuild()
        {
            ExportSmoke();
            Build();
        }

        private static void ExportSmoke()
        {
            string blender = EditorPrefs.GetString("Pizzeria.BlenderPath", "");
            if (!File.Exists(blender))
                blender = EditorUtility.OpenFilePanel("Select Blender executable", "", "exe");
            if (!File.Exists(blender)) throw new OperationCanceledException("Blender executable not selected.");
            EditorPrefs.SetString("Pizzeria.BlenderPath", blender);
            string script = Path.GetTempFileName();
            try
            {
                File.WriteAllText(script, "import bpy, sys\nbpy.ops.export_scene.fbx(filepath=sys.argv[sys.argv.index('--')+1], use_selection=False, object_types={'MESH'}, add_leaf_bones=False, bake_anim=False)\n");
                var info = new System.Diagnostics.ProcessStartInfo(blender,
                    "--background \"" + Path.GetFullPath(Source + "Smoke01.blend") + "\" --python \"" + script + "\" -- \"" + Path.GetFullPath(Export) + "\"")
                { UseShellExecute = false, CreateNoWindow = true };
                using (var process = System.Diagnostics.Process.Start(info))
                {
                    if (process == null) throw new InvalidOperationException("Could not start Blender.");
                    if (!process.WaitForExit(120000))
                    {
                        process.Kill();
                        throw new TimeoutException("Blender export exceeded two minutes.");
                    }
                    if (process.ExitCode != 0 || !File.Exists(Export))
                        throw new InvalidOperationException("Blender FBX export failed. Check Blender installation.");
                }
            }
            finally { File.Delete(script); }
        }
    }
}
