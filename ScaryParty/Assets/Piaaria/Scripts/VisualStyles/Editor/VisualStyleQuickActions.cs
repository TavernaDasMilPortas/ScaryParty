#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace ScaryParty.VisualStyles.Editor
{
    public static class VisualStyleQuickActions
    {
        [MenuItem("Tools/Scary Party/Setup e Uso Único/VFX & Visuals/Apply & Activate Dispatch Style", false, 263)]
        public static void ApplyAndActivateDispatchStyle()
        {
            const string profilePath = "Assets/Piaaria/Art/VisualStyles/Profiles/VS_DispatchInspired.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(profilePath);
            if (profile == null)
            {
                Debug.LogError($"[VisualStyleQuickActions] Perfil Dispatch não encontrado em: {profilePath}");
                return;
            }

            VisualConversionRuleSet ruleSet = profile.discovery?.conversionRuleSet ?? VisualStylePresetSeeder.EnsureDefaultRuleSet();

            Debug.Log("[VisualStyleQuickActions] Aplicando estilo Dispatch na cena de gameplay (GameScene_CelShaded)...");
            bool applied = VisualStyleGameplayIntegrator.ApplyToGameplayVariant(profile, ruleSet, keepVariantSceneOpen: true);
            if (!applied)
            {
                Debug.LogError("[VisualStyleQuickActions] Falha ao aplicar estilo Dispatch na cena variante!");
                return;
            }

            Debug.Log("[VisualStyleQuickActions] Ativando estilo Dispatch persistentemente no VisualStyleRuntimeConfig...");
            bool activated = VisualStyleGameplayIntegrator.ActivateInGameplayPersistent(profile);
            if (activated)
            {
                Debug.Log("<b>[VisualStyleQuickActions] SUCESSO! Estilo 'Dispatch Inspired' aplicado e ativado na gameplay!</b>");
                EditorUtility.DisplayDialog(
                    "Estilo Dispatch Aplicado",
                    "O estilo Dispatch Inspired foi aplicado com sucesso na cena 'GameScene_CelShaded' e ativado na configuração de runtime (VisualStyleRuntimeConfig)!\n\nAo iniciar o jogo pela ReadyScene ou jogar pela cena variante, os novos materiais serão exibidos.",
                    "OK"
                );
            }
            else
            {
                Debug.LogWarning("[VisualStyleQuickActions] Aplicado na cena, mas houve falha na ativação persistente de runtime.");
            }
        }
    }
}
#endif
