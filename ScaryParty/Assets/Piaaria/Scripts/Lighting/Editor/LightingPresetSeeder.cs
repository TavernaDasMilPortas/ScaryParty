#if UNITY_EDITOR
using System.IO;
using ScaryParty.VisualStyles.Editor;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Lighting.Editor
{
    /// <summary>
    /// Cria e valida os ScriptableObjects padrao de Ciclo Dia/Noite, Orcamento URP (Low/Med/High)
    /// e contrato B2 de ambientes fixos em Assets/Piaaria/Resources e Art/VisualStyles/Lighting.
    /// </summary>
    public static class LightingPresetSeeder
    {
        public const string ResourcesFolder = "Assets/Piaaria/Resources";
        public const string TimeOfDayDefaultPath = ResourcesFolder + "/TimeOfDayProfile_Default.asset";
        public const string QualityBudgetDefaultPath = ResourcesFolder + "/LightingQualityBudget_Default.asset";
        public const string LightingDataFolder = "Assets/Piaaria/Art/VisualStyles/Lighting";
        public const string PizzeriaB2StubPath = LightingDataFolder + "/FixedInteriorLightingData_Pizzeria.asset";

        [MenuItem("Tools/Scary Party/Setup e Uso Único/VFX & Visuals/Seed Perfis de Iluminação e Ciclo Dia-Noite", false, 264)]
        public static void SeedDefaultLightingAssetsMenu()
        {
            EnsureTimeOfDayDefaultProfile();
            EnsureQualityBudgetDefault();
            EnsurePizzeriaB2ContractStub();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[LightingPresetSeeder] Perfis de iluminacao, orcamento URP (2048/128-256-512) e contrato B2 garantidos.");
        }

        public static TimeOfDayProfile EnsureTimeOfDayDefaultProfile()
        {
            VisualStylePresetSeeder.EnsureFolder(ResourcesFolder);
            TimeOfDayProfile asset = AssetDatabase.LoadAssetAtPath<TimeOfDayProfile>(TimeOfDayDefaultPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<TimeOfDayProfile>();
                asset.ApplyCartoonDefaults();
                AssetDatabase.CreateAsset(asset, TimeOfDayDefaultPath);
            }
            return asset;
        }

        public static LightingQualityBudget EnsureQualityBudgetDefault()
        {
            VisualStylePresetSeeder.EnsureFolder(ResourcesFolder);
            LightingQualityBudget asset = AssetDatabase.LoadAssetAtPath<LightingQualityBudget>(QualityBudgetDefaultPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<LightingQualityBudget>();
                AssetDatabase.CreateAsset(asset, QualityBudgetDefaultPath);
            }
            return asset;
        }

        public static FixedInteriorLightingData EnsurePizzeriaB2ContractStub()
        {
            VisualStylePresetSeeder.EnsureFolder(LightingDataFolder);
            FixedInteriorLightingData asset = AssetDatabase.LoadAssetAtPath<FixedInteriorLightingData>(PizzeriaB2StubPath);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<FixedInteriorLightingData>();
                asset.spaceId = "pizzeria_modular_manual";
                asset.layoutFingerprint = "L_shape_kitchen_counter_dining_v1";
                asset.isBakedDataValid = false; // Futuro B2; mantem fallback seguro A6 por padrao
                AssetDatabase.CreateAsset(asset, PizzeriaB2StubPath);
            }
            return asset;
        }
    }
}
#endif
