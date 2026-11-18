using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;
using ScaryParty.UI.Piaria.Core;
using ScaryParty.UI.Piaria.Presenters;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Wizard Odin para preview e inspeção da Preparação entre Noites (2.1) e parâmetros de Feel/DOTween.
    /// </summary>
    public class NightIntermissionPreviewWizard : OdinEditorWindow
    {
        [MenuItem("Tools/Scary Party/Wizards/UI/Preview Preparação entre Noites (2.1)", false, 94)]
        private static void OpenWindow()
        {
            var win = GetWindow<NightIntermissionPreviewWizard>();
            win.titleContent = new GUIContent("Preview Intermissão 2.1");
            win.minSize = new Vector2(420f, 340f);
            win.Show();
        }

        [Title("Configuração Visual & Juice")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public NightIntermissionTheme theme;

        protected override void OnEnable()
        {
            base.OnEnable();
            if (theme == null) theme = NightIntermissionTheme.ResolveActive();
        }

        [Title("Ações de Preview (PlayMode)")]
        [ButtonGroup("Tabs"), Button("1. Resumo")]
        private void ShowSummary() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Summary);

        [ButtonGroup("Tabs"), Button("2. Imprevistos")]
        private void ShowHindrance() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Hindrance);

        [ButtonGroup("Tabs"), Button("3. Oportunidades")]
        private void ShowOpportunity() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Opportunity);

        [ButtonGroup("Tabs2"), Button("4. Loja")]
        private void ShowShop() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Shop);

        [ButtonGroup("Tabs2"), Button("5. Preparar")]
        private void ShowPrepare() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Prepare);

        [ButtonGroup("Tabs2"), Button("6. Revelação")]
        private void ShowReveal() => SelectTabInPlayMode(NightIntermissionPresenter.TabPage.Reveal);

        private static void SelectTabInPlayMode(NightIntermissionPresenter.TabPage tab)
        {
            if (!Application.isPlaying || NightIntermissionPresenter.Instance == null)
            {
                Debug.LogWarning("[NightIntermissionPreviewWizard] Entre em PlayMode com NightIntermissionPresenter ativo para pré-visualizar abas.");
                return;
            }
            NightIntermissionPresenter.Instance.ShowIntermission();
            NightIntermissionPresenter.Instance.SelectTab(tab, animate: true);
        }
    }
}
