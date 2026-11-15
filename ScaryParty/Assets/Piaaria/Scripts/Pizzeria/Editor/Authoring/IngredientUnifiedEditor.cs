using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Sirenix.OdinInspector;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Visual;
using ScaryParty.Pizzeria.Editor.PizzaVisual;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Editor unificado de ingrediente para o PizzeriaContentWizard.
    /// Centraliza em 4 sub-abas a configuração de todas as representações do item:
    /// 1. Visual na Pizza (PizzaIngredientVisualProfile - Modo Sliced/Specific/Central/Ring, poses manuais, raios, rotação, escala)
    /// 2. Item na Mão do Personagem (ItemPresentationProfile - Poses FP e Terceira Pessoa: posição, rotação, escala)
    /// 3. Item na Bancada (ItemPresentationProfile - Pose de slot de estação: posição, rotação, escala)
    /// 4. Definição & Preparo (IngredientDef - IDs lógicos e transformação cru/processado)
    /// </summary>
    [Serializable]
    public class IngredientUnifiedEditor
    {
        [HideInInspector]
        public IngredientDef Definition;

        [ShowInInspector, HideLabel, Title("$HeaderTitle", subtitle: null, titleAlignment: TitleAlignments.Centered)]
        [DisplayAsString(false)]
        private string _headerNotice => "Edite aqui todas as poses de bancada, mãos e pizza para este ingrediente de forma centralizada.";

        private string HeaderTitle => Definition != null
            ? $"Ingrediente: {(!string.IsNullOrEmpty(Definition.ingredientName) ? Definition.ingredientName : Definition.name)} (#{Definition.id})"
            : "Ingrediente";

        #region Sub-Aba 1: Visual na Pizza
        [TabGroup("Abas", "🍕 Visual na Pizza")]
        [InfoBox("Configura a fatia ou cobertura sobre a pizza. No modo 'Specific', você define a posição XYZ, rotação Euler e escala exatas de cada fatia manual. No modo 'Sliced', você calibra a variação de escala, altura Y, rotações e densidade.")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        [OnValueChanged(nameof(OnPizzaProfileChanged))]
        public PizzaIngredientVisualProfile PerfilVisualNaPizza;

        [TabGroup("Abas", "🍕 Visual na Pizza")]
        [Button("🍕 Abrir / Testar no Pizza Studio 2.0 (Prévia 3D ao Vivo)", ButtonSizes.Large)]
        [GUIColor(0.95f, 0.55f, 0.22f)]
        public void TestarNoPizzaStudio()
        {
            if (Definition == null) return;
            PizzaVisualPreviewWindow.ShowWindow();
            PizzaVisualPreviewWindow.NotifyProfileChanged();
        }

        [TabGroup("Abas", "🍕 Visual na Pizza")]
        [ShowIf("@PerfilVisualNaPizza == null")]
        [Button("➕ Criar Perfil Visual na Pizza para este Ingrediente", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void CriarPerfilVisual()
        {
            if (Definition == null) return;
            string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Profiles";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string assetPath = $"{dir}/Profile_Pizza_{Definition.name}.asset";
            var profile = ScriptableObject.CreateInstance<PizzaIngredientVisualProfile>();
            profile.displayName = !string.IsNullOrEmpty(Definition.ingredientName) ? Definition.ingredientName : Definition.name;
            profile.ingredientId = Definition.id;
            profile.stage = Definition.requiresPrep ? ProcessingStage.Raw : ProcessingStage.Prepared;
            profile.placementMode = PizzaPlacementMode.Sliced;
            profile.unitPrefab = Definition.placeholderPrefab;
            profile.tintColor = Definition.placeholderColor;

            AssetDatabase.CreateAsset(profile, assetPath);
            AssetDatabase.SaveAssets();
            PerfilVisualNaPizza = profile;
            OnPizzaProfileChanged();
        }

        private void OnPizzaProfileChanged()
        {
            if (PerfilVisualNaPizza != null)
            {
                EditorUtility.SetDirty(PerfilVisualNaPizza);
                PizzaVisualPreviewWindow.NotifyProfileChanged();
            }
        }
        #endregion

        #region Sub-Aba 2: Item na Mão do Personagem
        [TabGroup("Abas", "✋ Item na Mão")]
        [InfoBox("Ajuste as coordenadas finas (Posição XYZ, Rotação Euler e Escala) de como este ingrediente é segurado nas mãos do jogador em Primeira Pessoa (FP) e Terceira Pessoa (Remoto).")]
        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [ShowInInspector]
        public ItemPresentationProfile PerfilApresentacaoMao;

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [Title("Prefab Visual Segurado na Mão")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public GameObject VisualPrefabMao
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.visualPrefab : null;
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.visualPrefab = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public string PontoDePegaMao
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.gripPointName : "GripPoint";
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.gripPointName = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [BoxGroup("Abas/✋ Item na Mão/Primeira Pessoa (Câmera FP)")]
        [Title("Mão Direita (FP) — Posição, Rotação e Escala")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public ItemPoseOffset PoseMaoDireitaFP
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.fpRightHandPose : ItemPoseOffset.Identity;
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.fpRightHandPose = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [BoxGroup("Abas/✋ Item na Mão/Primeira Pessoa (Câmera FP)")]
        [Title("Mão Esquerda (FP) — Posição, Rotação e Escala")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public ItemPoseOffset PoseMaoEsquerdaFP
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.fpLeftHandPose : ItemPoseOffset.Identity;
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.fpLeftHandPose = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [BoxGroup("Abas/✋ Item na Mão/Terceira Pessoa (Corpo Remoto)")]
        [Title("Mão Direita Remota — Posição, Rotação e Escala")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public ItemPoseOffset PoseMaoDireitaRemota
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.remoteRightHandPose : ItemPoseOffset.Identity;
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.remoteRightHandPose = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao != null")]
        [BoxGroup("Abas/✋ Item na Mão/Terceira Pessoa (Corpo Remoto)")]
        [Title("Mão Esquerda Remota — Posição, Rotação e Escala")]
        [OnValueChanged(nameof(SaveHandChanges))]
        public ItemPoseOffset PoseMaoEsquerdaRemota
        {
            get => PerfilApresentacaoMao != null ? PerfilApresentacaoMao.remoteLeftHandPose : ItemPoseOffset.Identity;
            set { if (PerfilApresentacaoMao != null) { PerfilApresentacaoMao.remoteLeftHandPose = value; SaveHandChanges(); } }
        }

        [TabGroup("Abas", "✋ Item na Mão")]
        [ShowIf("@PerfilApresentacaoMao == null")]
        [Button("➕ Criar Perfil de Apresentação (Mão e Bancada)", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void CriarPerfilApresentacaoMao()
        {
            CriarPerfilApresentacaoGenerico();
        }

        private void SaveHandChanges()
        {
            if (PerfilApresentacaoMao != null)
            {
                EditorUtility.SetDirty(PerfilApresentacaoMao);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }
        #endregion

        #region Sub-Aba 3: Item na Bancada & Estações
        [TabGroup("Abas", "🪵 Item na Bancada")]
        [InfoBox("Ajuste as coordenadas finas (Posição XYZ, Rotação Euler e Escala) de como este ingrediente repousa nos slots das bancadas, tábuas de corte e estações da cozinha.")]
        [InlineEditor(InlineEditorObjectFieldModes.Foldout)]
        [ShowInInspector]
        public ItemPresentationProfile PerfilApresentacaoBancada;

        [TabGroup("Abas", "🪵 Item na Bancada")]
        [ShowIf("@PerfilApresentacaoBancada != null")]
        [Title("Ponto de Apoio e Alinhamento")]
        [OnValueChanged(nameof(SaveCounterChanges))]
        public string PontoDeApoioBancada
        {
            get => PerfilApresentacaoBancada != null ? PerfilApresentacaoBancada.supportPointName : "SupportPoint";
            set { if (PerfilApresentacaoBancada != null) { PerfilApresentacaoBancada.supportPointName = value; SaveCounterChanges(); } }
        }

        [TabGroup("Abas", "🪵 Item na Bancada")]
        [ShowIf("@PerfilApresentacaoBancada != null")]
        [Title("Pose de Encaixe no Slot da Bancada — Posição, Rotação e Escala")]
        [OnValueChanged(nameof(SaveCounterChanges))]
        public ItemPoseOffset PoseSlotBancada
        {
            get => PerfilApresentacaoBancada != null ? PerfilApresentacaoBancada.stationSlotPose : ItemPoseOffset.Identity;
            set { if (PerfilApresentacaoBancada != null) { PerfilApresentacaoBancada.stationSlotPose = value; SaveCounterChanges(); } }
        }

        [TabGroup("Abas", "🪵 Item na Bancada")]
        [ShowIf("@PerfilApresentacaoBancada != null")]
        [Button("Resetar Pose de Bancada para Identidade", ButtonSizes.Small)]
        public void ResetarPoseBancada()
        {
            if (PerfilApresentacaoBancada != null)
            {
                PerfilApresentacaoBancada.stationSlotPose = ItemPoseOffset.Identity;
                SaveCounterChanges();
            }
        }

        [TabGroup("Abas", "🪵 Item na Bancada")]
        [Button("📌 Fixar este Ingrediente na Bancada de Preparo (Estúdio 3D)", ButtonSizes.Large)]
        [GUIColor(1.0f, 0.65f, 0.25f)]
        public void FixarNaBancada()
        {
            PizzeriaContentStudioSession.PinAssembly(null, null, 0, Definition, null);
            PizzeriaContentStudioWindow.ShowWindow();
        }

        [TabGroup("Abas", "🪵 Item na Bancada")]
        [ShowIf("@PerfilApresentacaoBancada == null")]
        [Button("➕ Criar Perfil de Apresentação (Mão e Bancada)", ButtonSizes.Medium)]
        [GUIColor(0.2f, 0.8f, 0.4f)]
        public void CriarPerfilApresentacaoBancada()
        {
            CriarPerfilApresentacaoGenerico();
        }

        private void SaveCounterChanges()
        {
            if (PerfilApresentacaoBancada != null)
            {
                EditorUtility.SetDirty(PerfilApresentacaoBancada);
                PizzeriaContentStudioSession.NotifySessionChanged();
            }
        }

        private void CriarPerfilApresentacaoGenerico()
        {
            if (Definition == null) return;
            string dir = "Assets/Piaaria/ScriptableObjects/Pizzeria/Profiles";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string assetPath = $"{dir}/Profile_Ingredient_{Definition.name}.asset";
            var pres = ScriptableObject.CreateInstance<ItemPresentationProfile>();
            pres.presentationKey = $"Ingredient_{Definition.id}";
            pres.definitionId = Definition.id;
            pres.category = Definition.category;
            pres.visualPrefab = Definition.placeholderPrefab;
            pres.stationSlotPose = ItemPoseOffset.Identity;
            pres.fpRightHandPose = ItemPoseOffset.Identity;
            pres.fpLeftHandPose = ItemPoseOffset.Identity;

            AssetDatabase.CreateAsset(pres, assetPath);
            AssetDatabase.SaveAssets();
            PerfilApresentacaoMao = pres;
            PerfilApresentacaoBancada = pres;
        }
        #endregion

        #region Sub-Aba 4: Definição & Processamento
        [TabGroup("Abas", "📦 Definição & Preparo")]
        [InfoBox("Configurações fundamentais da receita e ciclo de produção do ingrediente.")]
        [InlineEditor(InlineEditorObjectFieldModes.Boxed)]
        public IngredientDef DefinicaoLogica => Definition;
        #endregion

        public IngredientUnifiedEditor(IngredientDef def)
        {
            Definition = def;
            ResolveAllProfiles();
        }

        public void ResolveAllProfiles()
        {
            if (Definition == null) return;

            // 1. Resolver Perfil Visual na Pizza
            if (PerfilVisualNaPizza == null)
            {
                var stage = Definition.requiresPrep ? ProcessingStage.Raw : ProcessingStage.Prepared;
                PerfilVisualNaPizza = PizzaVisualCatalog.Instance?.GetProfile(Definition.id, stage);

                if (PerfilVisualNaPizza == null)
                {
                    string[] guids = AssetDatabase.FindAssets("t:PizzaIngredientVisualProfile");
                    for (int i = 0; i < guids.Length; i++)
                    {
                        var p = AssetDatabase.LoadAssetAtPath<PizzaIngredientVisualProfile>(AssetDatabase.GUIDToAssetPath(guids[i]));
                        if (p != null && p.ingredientId == Definition.id)
                        {
                            PerfilVisualNaPizza = p;
                            break;
                        }
                    }
                }
            }

            // 2. Resolver Perfil de Apresentação (Mão / Bancada)
            if (PerfilApresentacaoMao == null)
            {
                string[] presGuids = AssetDatabase.FindAssets("t:ItemPresentationProfile");
                for (int i = 0; i < presGuids.Length; i++)
                {
                    var p = AssetDatabase.LoadAssetAtPath<ItemPresentationProfile>(AssetDatabase.GUIDToAssetPath(presGuids[i]));
                    if (p != null && (p.definitionId == Definition.id || p.presentationKey.EndsWith($"_{Definition.id}")))
                    {
                        PerfilApresentacaoMao = p;
                        break;
                    }
                }
            }

            PerfilApresentacaoBancada = PerfilApresentacaoMao;
        }
    }
}
