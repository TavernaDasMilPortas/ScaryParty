#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Ponto de entrada via linha de comando para rodar o pipeline completo em batchmode.
    /// </summary>
    public static class PizzeriaToonBatchRunner
    {
        public static void RunFullPipeline()
        {
            Debug.Log("[PizzeriaToonBatchRunner] Iniciando pipeline Cel Shading...");

            // 1. Garante Perfil Soft Toon
            string profilePath = PizzeriaToonVariantBuilder.SoftToonProfilePath;
            var profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<PizzeriaAppearanceProfile>();
                profile.name = "PizzeriaAppearanceProfile_SoftToon";
                profile.opaqueShader = Shader.Find("Distant Lands/Illustrate/Opaque");
                profile.simpleShader = Shader.Find("Distant Lands/Illustrate/Simple");

                profile.paredeClaraColor = PizzeriaAppearanceProfile.HexToColor("#F2E7D8");
                profile.fachadaDestaqueColor = PizzeriaAppearanceProfile.HexToColor("#EF594B");
                profile.destaqueSecundarioColor = PizzeriaAppearanceProfile.HexToColor("#FFD65D");
                profile.geladeiraColor = PizzeriaAppearanceProfile.HexToColor("#49B7AA");
                profile.metalColor = PizzeriaAppearanceProfile.HexToColor("#A9BAC2");
                profile.madeiraColor = PizzeriaAppearanceProfile.HexToColor("#9C6948");
                profile.pisoSalaoColor = PizzeriaAppearanceProfile.HexToColor("#B99675");
                profile.pisoCozinhaColor = PizzeriaAppearanceProfile.HexToColor("#D9DDDC");
                profile.escuroColor = PizzeriaAppearanceProfile.HexToColor("#343846");
                profile.vidroColor = new Color(0.85f, 0.94f, 0.98f, 0.45f);

                profile.enableToonLighting = true;
                profile.enableShadows = true;
                profile.disableWaves = true;
                profile.disableHSVVariation = true;
                profile.preserveOriginalTextures = true;
                profile.preserveNormalMaps = true;
                profile.generatedMaterialsFolder = "Assets/Piaaria/Art/Pizzeria/Toon/Materials/Architecture";
                profile.InitializeDefaultRules();

                string dir = Path.GetDirectoryName(profilePath).Replace("\\", "/");
                if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[PizzeriaToonBatchRunner] Perfil Soft Toon criado.");
            }

            // 2. Clone do Prefab
            if (PizzeriaToonVariantBuilder.EnsureVariantPrefabExists(out string cloneMsg))
            {
                Debug.Log($"[PizzeriaToonBatchRunner] Clone: {cloneMsg}");
            }
            else
            {
                Debug.LogError($"[PizzeriaToonBatchRunner] Erro no clone: {cloneMsg}");
                return;
            }

            // 3. Aplicação de Materiais Toon
            var buildRes = PizzeriaToonVariantBuilder.ApplyToonMaterialsToVariant(profile);
            if (buildRes.success)
            {
                Debug.Log($"[PizzeriaToonBatchRunner] Materiais: {buildRes.message}");
            }
            else
            {
                Debug.LogError($"[PizzeriaToonBatchRunner] Erro nos materiais: {string.Join(" | ", buildRes.errors)}");
                return;
            }

            // 4. Showcase Characters e Lights
            if (PizzeriaToonShowcaseBuilder.SetupShowcaseCharactersAndLights(PizzeriaToonVariantBuilder.VariantPrefabPath, out string showcaseMsg))
            {
                Debug.Log($"[PizzeriaToonBatchRunner] Showcase Rig: {showcaseMsg}");
            }
            else
            {
                Debug.LogError($"[PizzeriaToonBatchRunner] Erro no Showcase Rig: {showcaseMsg}");
            }

            // 5. Montagem da Cena
            if (PizzeriaToonShowcaseBuilder.BuildShowcaseScene(out string sceneMsg))
            {
                Debug.Log($"[PizzeriaToonBatchRunner] Cena: {sceneMsg}");
            }
            else
            {
                Debug.LogError($"[PizzeriaToonBatchRunner] Erro na Cena: {sceneMsg}");
            }

            // 6. Auditorias Finais
            var srcAudit = PizzeriaToonAudit.AuditPrefab(PizzeriaToonVariantBuilder.SourcePrefabPath);
            PizzeriaToonAudit.SaveAuditReport(srcAudit, "Audit_Original.json");

            var varAudit = PizzeriaToonAudit.AuditPrefab(PizzeriaToonVariantBuilder.VariantPrefabPath);
            PizzeriaToonAudit.SaveAuditReport(varAudit, "Audit_Variant.json");

            Debug.Log("[PizzeriaToonBatchRunner] Pipeline concluído com sucesso!");
        }
    }
}
#endif
