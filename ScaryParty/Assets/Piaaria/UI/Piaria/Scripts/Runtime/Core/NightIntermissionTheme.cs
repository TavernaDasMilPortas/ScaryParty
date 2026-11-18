using System;
using Sirenix.OdinInspector;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Services;

namespace ScaryParty.UI.Piaria.Core
{
    /// <summary>
    /// Configuração visual, terminologia de categorias e parâmetros de animação/Feel
    /// para a Preparação entre Noites (2.1 — UI Toolkit First + DOTween + Feel).
    /// </summary>
    [CreateAssetMenu(fileName = "NightIntermissionTheme", menuName = "Scary Party/UI/Night Intermission Theme")]
    public class NightIntermissionTheme : ScriptableObject
    {
        private static NightIntermissionTheme _runtimeFallback;

        public static NightIntermissionTheme ResolveActive()
        {
            var catalog = PiariaUIAssetCatalog.Instance;
            if (catalog != null && catalog.nightIntermissionTheme != null)
            {
                return catalog.nightIntermissionTheme;
            }

#if UNITY_EDITOR
            var editorAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<NightIntermissionTheme>(
                "Assets/Piaaria/UI/Piaria/Config/NightIntermissionTheme.asset");
            if (editorAsset != null) return editorAsset;
#endif
            if (_runtimeFallback == null)
            {
                _runtimeFallback = CreateInstance<NightIntermissionTheme>();
                _runtimeFallback.name = "NightIntermissionTheme (RuntimeDefault)";
                _runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
            }
            return _runtimeFallback;
        }

        [BoxGroup("Terminologia & Identidade")]
        [LabelText("Usar 'Complicações' em vez de 'Imprevistos'")]
        [Tooltip("Altera apenas o rótulo visual da categoria Hindrance, sem modificar o domínio.")]
        public bool useComplicacoesLabel = false;

        [BoxGroup("Terminologia & Identidade")]
        [LabelText("Título Aba Resumo")]
        public string summaryTabTitle = "Resumo";

        [BoxGroup("Terminologia & Identidade")]
        [LabelText("Título Aba Oportunidades")]
        public string opportunityTabTitle = "Oportunidades";

        [BoxGroup("Terminologia & Identidade")]
        [LabelText("Título Aba Loja")]
        public string shopTabTitle = "Loja";

        [BoxGroup("Terminologia & Identidade")]
        [LabelText("Título Aba Preparar")]
        public string prepareTabTitle = "Preparar";

        [BoxGroup("Cores Digitais Cartoon (P.I.A.R.I.A)")]
        [LabelText("Acento Imprevistos")]
        public Color hindranceAccentColor = new Color(0.95f, 0.36f, 0.22f, 1f);

        [BoxGroup("Cores Digitais Cartoon (P.I.A.R.I.A)")]
        [LabelText("Acento Oportunidades")]
        public Color opportunityAccentColor = new Color(0.24f, 0.64f, 0.30f, 1f);

        [BoxGroup("Cores Digitais Cartoon (P.I.A.R.I.A)")]
        [LabelText("Acento Loja")]
        public Color shopAccentColor = new Color(0.18f, 0.47f, 0.73f, 1f);

        [BoxGroup("Cores Digitais Cartoon (P.I.A.R.I.A)")]
        [LabelText("Acento Seleção / Voto")]
        public Color selectedOrangeColor = new Color(1.0f, 0.455f, 0.122f, 1f);

        [BoxGroup("Cores Digitais Cartoon (P.I.A.R.I.A)")]
        [LabelText("Acento Destaque Dourado")]
        public Color goldHighlightColor = new Color(0.973f, 0.753f, 0.271f, 1f);

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("EnableUIJuice")]
        public bool enableUIJuice = true;

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("OverallIntensity"), Range(0f, 1.5f)]
        public float overallIntensity = 1.0f;

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("ReducedMotion (Override Local)")]
        public bool reducedMotion = false;

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("SfxVolume"), Range(0f, 1f)]
        public float sfxVolume = 0.85f;

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("EnableDecorativeAccents")]
        public bool enableDecorativeAccents = true;

        [BoxGroup("Controles Gerais de Juice & Acessibilidade")]
        [LabelText("MaxSimultaneousFeedbacks"), Range(1, 16)]
        public int maxSimultaneousFeedbacks = 6;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("HoverScale"), Range(1.0f, 1.08f)]
        public float hoverScale = 1.015f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("PressScale"), Range(0.90f, 1.0f)]
        public float pressScale = 0.97f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("CardDropDistance (px)"), Range(20f, 180f)]
        public float cardDropDistance = 90f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("CardDropStagger (s)"), Range(0.05f, 0.50f)]
        public float cardDropStagger = 0.20f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("CardDropDuration (s)"), Range(0.15f, 0.80f)]
        public float cardDropDuration = 0.45f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("ImpactScale"), Range(1.0f, 1.15f)]
        public float impactScale = 1.04f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("RevealDelay (s)"), Range(0.20f, 2.0f)]
        public float revealDelay = 1.05f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("RevealDuration (s)"), Range(0.15f, 0.80f)]
        public float revealDuration = 0.42f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("GlowIntensity"), Range(0f, 1.5f)]
        public float glowIntensity = 1.0f;

        [BoxGroup("Timings & Escalas de Cards (UI Toolkit + DOTween)")]
        [LabelText("ErrorShakePixels"), Range(0f, 16f)]
        public float errorShakePixels = 6f;

        [BoxGroup("Revelação & Contagem Regressiva Sincronizada")]
        [LabelText("Duração Total da Revelação (s)"), Range(2.0f, 8.0f)]
        public float revealTotalDuration = 4.5f;

        [BoxGroup("Revelação & Contagem Regressiva Sincronizada")]
        [LabelText("Varredura Cosmética por Categoria (s)"), Range(0.5f, 2.5f)]
        public float lotterySweepDuration = 1.2f;

        [BoxGroup("Revelação & Contagem Regressiva Sincronizada")]
        [LabelText("CountdownDuration (s)"), Range(1.5f, 5.0f)]
        public float countdownDuration = 3.0f;

        public string GetCategoryLabel(RunModifierKind kind)
        {
            return kind switch
            {
                RunModifierKind.Hindrance => useComplicacoesLabel ? "Complicações" : "Imprevistos",
                RunModifierKind.Opportunity => string.IsNullOrEmpty(opportunityTabTitle) ? "Oportunidades" : opportunityTabTitle,
                RunModifierKind.Event => "Eventos",
                RunModifierKind.PermanentUpgrade => "Melhorias",
                _ => kind.ToString()
            };
        }

        public Color GetCategoryColor(RunModifierKind kind)
        {
            return kind switch
            {
                RunModifierKind.Hindrance => hindranceAccentColor,
                RunModifierKind.Opportunity => opportunityAccentColor,
                RunModifierKind.PermanentUpgrade => shopAccentColor,
                _ => selectedOrangeColor
            };
        }

        public bool EffectiveReducedMotion => reducedMotion || UIToolkitTweenService.ReduceMotion;
    }
}
