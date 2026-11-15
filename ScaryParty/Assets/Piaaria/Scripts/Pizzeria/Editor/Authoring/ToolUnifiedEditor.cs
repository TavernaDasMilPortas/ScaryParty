using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Editor unificado de Utensílios para o PizzeriaContentWizard.
    /// Centraliza o cadastro da ferramenta (ToolDef), modelos visuais, poses para suporte e mãos,
    /// ponto de contato de corte e teste de compatibilidade culinária.
    /// </summary>
    [Serializable]
    public class ToolUnifiedEditor
    {
        [HideInInspector] public ToolDef Definition;
        [HideInInspector] public ItemPresentationProfile PresentationProfile;

        public ToolUnifiedEditor(ToolDef def)
        {
            Definition = def;
            ResolvePresentationProfile();
        }

        private string HeaderTitle => Definition != null
            ? $"Utensílio: {Definition.toolName} (#{Definition.id})"
            : "Utensílio";

        [ShowInInspector, HideLabel, Title("$HeaderTitle", subtitle: null, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(false)]
        private string _headerNotice => "Configure aqui o modelo visual, as poses de suporte/mão, o gume de corte e teste a compatibilidade.";

        #region Aba 1: Identificação & Regras
        [TabGroup("Abas", "📋 Identificação")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int ID
        {
            get => Definition != null ? Definition.id : 0;
            set { if (Definition != null) { Definition.id = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public string NomeDoUtensilio
        {
            get => Definition != null ? Definition.toolName : "";
            set { if (Definition != null) { Definition.toolName = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public ToolCapability Capacidades
        {
            get => Definition != null ? Definition.capabilities : ToolCapability.Slice;
            set { if (Definition != null) { Definition.capabilities = value; SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public int EstoqueInicialCompartilhado
        {
            get => Definition != null ? Definition.initialSharedCount : 1;
            set { if (Definition != null) { Definition.initialSharedCount = Mathf.Clamp(value, 1, 10); SaveDefinitionChanges(); } }
        }

        [TabGroup("Abas", "📋 Identificação")]
        [OnValueChanged(nameof(SaveDefinitionChanges))]
        public GameObject ModeloPlaceholder
        {
            get => Definition != null ? Definition.placeholderPrefab : null;
            set { if (Definition != null) { Definition.placeholderPrefab = value; SaveDefinitionChanges(); } }
        }
        #endregion

        #region Aba 2: Poses (Suporte & Mãos)
        [TabGroup("Abas", "✋ Poses & Suporte")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [ShowInInspector]
        public ItemPresentationProfile PerfilDeApresentacao => PresentationProfile;

        [TabGroup("Abas", "✋ Poses & Suporte")]
        [ShowIf("@PresentationProfile == null")]
        [Button("➕ Criar Perfil de Apresentação para este Utensílio", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void CriarPerfilApresentacao()
        {
            if (Definition == null) return;
            string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Profiles";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string assetPath = $"{dir}/Profile_Tool_{Definition.name}.asset";
            var profile = ScriptableObject.CreateInstance<ItemPresentationProfile>();
            profile.presentationKey = $"Tool_{Definition.id}";
            profile.category = ItemCategory.Tool;
            profile.definitionId = Definition.id;
            profile.isTool = true;
            profile.visualPrefab = Definition.placeholderPrefab;

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            PresentationProfile = profile;
        }

        [TabGroup("Abas", "✋ Poses & Suporte")]
        [ShowIf("@PresentationProfile != null")]
        [Title("Pose no Suporte de Utensílios (ToolRack)")]
        [OnValueChanged(nameof(SaveProfileChanges))]
        public ItemPoseOffset PoseNoSuporte
        {
            get => PresentationProfile != null ? PresentationProfile.stationSlotPose : ItemPoseOffset.Identity;
            set { if (PresentationProfile != null) { PresentationProfile.stationSlotPose = value; SaveProfileChanges(); } }
        }

        [TabGroup("Abas", "✋ Poses & Suporte")]
        [ShowIf("@PresentationProfile != null")]
        [Title("Pose na Mão Direita (Primeira Pessoa)")]
        [OnValueChanged(nameof(SaveProfileChanges))]
        public ItemPoseOffset PoseMaoDireitaFP
        {
            get => PresentationProfile != null ? PresentationProfile.fpRightHandPose : ItemPoseOffset.Identity;
            set { if (PresentationProfile != null) { PresentationProfile.fpRightHandPose = value; SaveProfileChanges(); } }
        }
        #endregion

        #region Aba 3: Teste Visual & Corte
        [TabGroup("Abas", "👁️ Teste Visual & Corte")]
        [Button("📌 Fixar este Utensílio na Bancada de Preparo (Estúdio 3D)", ButtonSizes.Large)]
        [GUIColor(1.0f, 0.65f, 0.25f)]
        public void FixarNaBancada()
        {
            IngredientDef tomate = null;
            string[] guids = AssetDatabase.FindAssets("Ingredient_");
            foreach (var g in guids)
            {
                var ing = AssetDatabase.LoadAssetAtPath<IngredientDef>(AssetDatabase.GUIDToAssetPath(g));
                if (ing != null && ing.name.Contains("Tomate"))
                {
                    tomate = ing;
                    break;
                }
            }

            PizzeriaContentStudioSession.PinAssembly(null, null, 0, tomate, Definition);
            PizzeriaContentStudioWindow.ShowWindow();
        }

        [TabGroup("Abas", "👁️ Teste Visual & Corte")]
        [Title("Diagnóstico de Compatibilidade Culinária")]
        [ShowInInspector, DisplayAsString(false)]
        private string StatusCompatibilidade => Definition != null && Definition.capabilities == ToolCapability.Slice
            ? "✅ Ferramenta compatível com processos de fatiamento e corte na PrepStation."
            : (Definition != null && Definition.capabilities == ToolCapability.Grate
                ? "🧀 Ferramenta compatível com ralação de queijo na PrepStation."
                : "ℹ️ Utensílio com capacidade configurada.");
        #endregion

        private void ResolvePresentationProfile()
        {
            if (Definition == null) return;
            var catalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/Piaaria/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset");
            if (catalog != null)
            {
                PresentationProfile = catalog.FindProfile(ItemCategory.Tool, Definition.id, true, false);
            }
        }

        private void SaveDefinitionChanges()
        {
            if (Definition != null)
            {
                EditorUtility.SetDirty(Definition);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }

        private void SaveProfileChanges()
        {
            if (PresentationProfile != null)
            {
                EditorUtility.SetDirty(PresentationProfile);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }
    }
}
