#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    /// <summary>
    /// Creates and validates the 4 default VisualStyleProfile presets, default VisualConversionRuleSet,
    /// and default Resources/VisualStyleRuntimeConfig without overwriting user edits unless requested.
    /// </summary>
    public static class VisualStylePresetSeeder
    {
        public const string ProfilesFolder = "Assets/Piaaria/Art/VisualStyles/Profiles";
        public const string RuleSetPath = "Assets/Piaaria/Art/VisualStyles/Profiles/VisualConversionRuleSet_Default.asset";
        public const string RuntimeConfigAssetPath = "Assets/Piaaria/Resources/VisualStyleRuntimeConfig.asset";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/VFX & Visuals/Seed Default Presets & RuleSet", false, 262)]
        public static void EnsureAllDefaultsExist()
        {
            EnsureFolder(ProfilesFolder);
            EnsureFolder("Assets/Piaaria/Resources");

            VisualConversionRuleSet ruleSet = EnsureDefaultRuleSet();

            EnsurePresetAsset(VisualStylePresetType.SoftToonVibrant, "VS_SoftToonVibrant.asset", ruleSet);
            EnsurePresetAsset(VisualStylePresetType.AnimeSoft, "VS_AnimeSoft.asset", ruleSet);
            EnsurePresetAsset(VisualStylePresetType.ComicBold, "VS_ComicBold.asset", ruleSet);
            EnsurePresetAsset(VisualStylePresetType.FlatGraphic, "VS_FlatGraphic.asset", ruleSet);

            EnsureRuntimeConfig(LoadPreset(VisualStylePresetType.SoftToonVibrant));

            AssetDatabase.SaveAssets();
        }

        public static VisualConversionRuleSet EnsureDefaultRuleSet()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VisualConversionRuleSet>(RuleSetPath);
            if (existing != null)
            {
                if (existing.categories == null || existing.categories.Count == 0)
                {
                    VisualConversionRuleSet.PopulateDefaults(existing);
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            EnsureFolder(ProfilesFolder);
            var asset = ScriptableObject.CreateInstance<VisualConversionRuleSet>();
            VisualConversionRuleSet.PopulateDefaults(asset);
            AssetDatabase.CreateAsset(asset, RuleSetPath);
            AssetDatabase.ImportAsset(RuleSetPath);
            return asset;
        }

        public static VisualStyleProfile EnsurePresetAsset(VisualStylePresetType presetType, string fileName, VisualConversionRuleSet ruleSet)
        {
            string path = $"{ProfilesFolder}/{fileName}";
            var existing = AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(path);
            if (existing != null)
            {
                if (existing.discovery.conversionRuleSet == null && ruleSet != null)
                {
                    existing.discovery.conversionRuleSet = ruleSet;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var profile = ScriptableObject.CreateInstance<VisualStyleProfile>();
            profile.ApplyPresetDefaults(presetType);
            profile.discovery.conversionRuleSet = ruleSet;
            AssetDatabase.CreateAsset(profile, path);
            AssetDatabase.ImportAsset(path);
            return profile;
        }

        public static VisualStyleProfile LoadPreset(VisualStylePresetType presetType)
        {
            string fileName = presetType switch
            {
                VisualStylePresetType.AnimeSoft => "VS_AnimeSoft.asset",
                VisualStylePresetType.ComicBold => "VS_ComicBold.asset",
                VisualStylePresetType.FlatGraphic => "VS_FlatGraphic.asset",
                _ => "VS_SoftToonVibrant.asset"
            };
            return AssetDatabase.LoadAssetAtPath<VisualStyleProfile>($"{ProfilesFolder}/{fileName}");
        }

        public static VisualStyleRuntimeConfig EnsureRuntimeConfig(VisualStyleProfile defaultProfile = null)
        {
            EnsureFolder("Assets/Piaaria/Resources");
            var existing = AssetDatabase.LoadAssetAtPath<VisualStyleRuntimeConfig>(RuntimeConfigAssetPath);
            if (existing != null)
            {
                if (existing.activeProfile == null && defaultProfile != null)
                {
                    existing.activeProfile = defaultProfile;
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            var cfg = ScriptableObject.CreateInstance<VisualStyleRuntimeConfig>();
            cfg.useStyledGameplay = false;
            cfg.activeGameplaySceneName = VisualStyleRuntimeConfig.CanonicalGameplaySceneName;
            cfg.activeStyleId = defaultProfile != null ? defaultProfile.styleId : "soft_toon_vibrant";
            cfg.activeProfile = defaultProfile;
            AssetDatabase.CreateAsset(cfg, RuntimeConfigAssetPath);
            AssetDatabase.ImportAsset(RuntimeConfigAssetPath);
            VisualStyleRuntimeConfig.ClearCache();
            return cfg;
        }

        public static void EnsureFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath)) return;
            string[] parts = folderPath.Replace("\\", "/").Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }
    }
}
#endif
