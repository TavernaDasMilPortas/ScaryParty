#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Janela de controle interativa para o processo completo de Cel Shading da Pizzeria.
    /// Menu: Tools > Scary Party > Wizards > Pizzaria > Cel Shading Showcase Builder
    /// </summary>
    public class PizzeriaToonVariantBuilderWindow : EditorWindow
    {
        private const string SourcePrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        private const string VariantPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab";
        private const string SoftToonProfilePath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaAppearanceProfile_SoftToon.asset";

        [SerializeField] private GameObject _sourcePrefab;
        [SerializeField] private GameObject _variantPrefab;
        [SerializeField] private PizzeriaAppearanceProfile _profile;

        private Vector2 _scrollPos;
        private PizzeriaToonAudit.AuditReport _sourceAudit;
        private PizzeriaToonAudit.AuditReport _variantAudit;
        private string _statusMessage = "Pronto para iniciar auditoria ou operações.";
        private MessageType _statusType = MessageType.Info;

        [MenuItem("Tools/Scary Party/Wizards/Pizzaria/Cel Shading Showcase Builder", false, 31)]
        public static void ShowWindow()
        {
            var win = GetWindow<PizzeriaToonVariantBuilderWindow>("Cel Shading Builder");
            win.minSize = new Vector2(480, 680);
            win.Show();
        }

        private void OnEnable()
        {
            if (_sourcePrefab == null)
                _sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);

            if (_variantPrefab == null)
                _variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);

            if (_profile == null)
            {
                _profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(SoftToonProfilePath);
                if (_profile == null)
                {
                    // Tenta criar automaticamente o perfil Soft Toon se não existir
                    EnsureSoftToonProfileAsset();
                }
            }
        }

        private void OnGUI()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            EditorGUILayout.LabelField("🎨 Pizzaria — Cel Shading (Soft Toon) Showcase Builder", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Fluxo isolado e seguro para gerar a variante Cel Shading da Pizzaria, materiais Distant Lands por slot/textura, " +
                "personagens demonstrativos, iluminação uniforme e cena de comparação A/B sem tocar em produção.",
                MessageType.Info);

            EditorGUILayout.Space(8);

            // SEÇÃO 1: REFERÊNCIAS
            EditorGUILayout.LabelField("1. Configurações e Assets", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            _sourcePrefab = (GameObject)EditorGUILayout.ObjectField("Prefab Original (Read-Only)", _sourcePrefab, typeof(GameObject), false);
            EditorGUI.EndDisabledGroup();

            _variantPrefab = (GameObject)EditorGUILayout.ObjectField("Variante Toon (Destino)", _variantPrefab, typeof(GameObject), false);
            _profile = (PizzeriaAppearanceProfile)EditorGUILayout.ObjectField("Perfil Soft Toon", _profile, typeof(PizzeriaAppearanceProfile), false);

            if (_profile == null)
            {
                if (GUILayout.Button("Criar Perfil Soft Toon Padrão"))
                {
                    EnsureSoftToonProfileAsset();
                }
            }

            EditorGUILayout.Space(8);

            // SEÇÃO 2: AUDITORIA
            EditorGUILayout.LabelField("2. Auditoria e Validação", EditorStyles.boldLabel);
            if (GUILayout.Button("Auditar Prefab Original e Variante"))
            {
                RunAudit();
            }

            if (_sourceAudit != null)
            {
                EditorGUILayout.HelpBox(
                    $"Origem: {_sourceAudit.totalGameObjects} GOs, {_sourceAudit.totalMeshRenderers} MeshRenderers, " +
                    $"{_sourceAudit.totalSlots} Slots, {_sourceAudit.uniqueMaterialsCount} Materiais, " +
                    $"{_sourceAudit.stationsCount} Estações, {_sourceAudit.networkObjectsCount} NetworkObjects.",
                    MessageType.None);
            }

            if (_variantAudit != null)
            {
                EditorGUILayout.HelpBox(
                    $"Variante: {_variantAudit.totalGameObjects} GOs, {_variantAudit.totalMeshRenderers} MeshRenderers, " +
                    $"{_variantAudit.totalSlots} Slots, {_variantAudit.uniqueMaterialsCount} Materiais.",
                    MessageType.None);
            }

            EditorGUILayout.Space(8);

            // SEÇÃO 3: CÓPIA INDEPENDENTE
            EditorGUILayout.LabelField("3. Cópia Independente", EditorStyles.boldLabel);
            if (GUILayout.Button("Criar / Sincronizar Cópia Independente (Clone)"))
            {
                if (PizzeriaToonVariantBuilder.EnsureVariantPrefabExists(out string msg))
                {
                    _variantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VariantPrefabPath);
                    SetStatus(msg, MessageType.Info);
                }
                else
                {
                    SetStatus(msg, MessageType.Error);
                }
            }

            EditorGUILayout.Space(8);

            // SEÇÃO 4: MATERIAIS TOON
            EditorGUILayout.LabelField("4. Aplicação de Materiais Toon", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(_variantPrefab == null || _profile == null);
            if (GUILayout.Button("Gerar e Aplicar Materiais Toon na Variante"))
            {
                var res = PizzeriaToonVariantBuilder.ApplyToonMaterialsToVariant(_profile);
                if (res.success)
                {
                    SetStatus(res.message, MessageType.Info);
                    RunAudit();
                }
                else
                {
                    SetStatus(string.Join("\n", res.errors), MessageType.Error);
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // SEÇÃO 5: ELEMENTOS DEMONSTRATIVOS (PERSONAGENS E LUZES)
            EditorGUILayout.LabelField("5. Elementos de Demonstração (Showcase Rig)", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(_variantPrefab == null);
            if (GUILayout.Button("Adicionar / Atualizar Personagens (5) e Iluminação Suave"))
            {
                if (PizzeriaToonShowcaseBuilder.SetupShowcaseCharactersAndLights(VariantPrefabPath, out string rep))
                {
                    SetStatus(rep, MessageType.Info);
                    RunAudit();
                }
                else
                {
                    SetStatus(rep, MessageType.Error);
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);

            // SEÇÃO 6: CENA DE DEMONSTRAÇÃO
            EditorGUILayout.LabelField("6. Cena de Visualização / Comparação A/B", EditorStyles.boldLabel);
            if (GUILayout.Button("Criar / Reconstruir Cena PizzeriaCelShading_Showcase.unity"))
            {
                if (PizzeriaToonShowcaseBuilder.BuildShowcaseScene(out string msg))
                {
                    SetStatus(msg, MessageType.Info);
                }
                else
                {
                    SetStatus(msg, MessageType.Error);
                }
            }

            EditorGUILayout.Space(12);

            // STATUS
            EditorGUILayout.HelpBox(_statusMessage, _statusType);

            EditorGUILayout.EndScrollView();
        }

        private void RunAudit()
        {
            _sourceAudit = PizzeriaToonAudit.AuditPrefab(SourcePrefabPath);
            PizzeriaToonAudit.SaveAuditReport(_sourceAudit, "Audit_Original.json");

            if (File.Exists(VariantPrefabPath))
            {
                _variantAudit = PizzeriaToonAudit.AuditPrefab(VariantPrefabPath);
                PizzeriaToonAudit.SaveAuditReport(_variantAudit, "Audit_Variant.json");

                if (PizzeriaToonAudit.CompareAudits(_sourceAudit, _variantAudit, out List<string> diffs))
                {
                    SetStatus("Auditoria concluída: nenhuma divergência estrutural crítica de gameplay/rede encontrada.", MessageType.Info);
                }
                else
                {
                    SetStatus("Auditoria concluída com divergências:\n" + string.Join("\n", diffs), MessageType.Warning);
                }
            }
            else
            {
                SetStatus("Auditoria da origem concluída. Variante ainda não foi criada.", MessageType.Info);
            }
        }

        private void EnsureSoftToonProfileAsset()
        {
            _profile = AssetDatabase.LoadAssetAtPath<PizzeriaAppearanceProfile>(SoftToonProfilePath);
            if (_profile != null) return;

            string dir = Path.GetDirectoryName(SoftToonProfilePath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _profile = CreateInstance<PizzeriaAppearanceProfile>();
            _profile.name = "PizzeriaAppearanceProfile_SoftToon";
            _profile.opaqueShader = Shader.Find("Distant Lands/Illustrate/Opaque");
            _profile.simpleShader = Shader.Find("Distant Lands/Illustrate/Simple");

            // Paleta Soft Toon de Referência
            _profile.paredeClaraColor = PizzeriaAppearanceProfile.HexToColor("#F2E7D8");
            _profile.fachadaDestaqueColor = PizzeriaAppearanceProfile.HexToColor("#EF594B"); // Vermelho de marca
            _profile.destaqueSecundarioColor = PizzeriaAppearanceProfile.HexToColor("#FFD65D"); // Amarelo vivo
            _profile.geladeiraColor = PizzeriaAppearanceProfile.HexToColor("#49B7AA"); // Turquesa
            _profile.metalColor = PizzeriaAppearanceProfile.HexToColor("#A9BAC2"); // Metal neutro
            _profile.madeiraColor = PizzeriaAppearanceProfile.HexToColor("#9C6948"); // Madeira
            _profile.pisoSalaoColor = PizzeriaAppearanceProfile.HexToColor("#B99675");
            _profile.pisoCozinhaColor = PizzeriaAppearanceProfile.HexToColor("#D9DDDC");
            _profile.escuroColor = PizzeriaAppearanceProfile.HexToColor("#343846"); // Grafite azulado
            _profile.vidroColor = new Color(0.85f, 0.94f, 0.98f, 0.45f);

            _profile.enableToonLighting = true;
            _profile.enableShadows = true;
            _profile.disableWaves = true;
            _profile.disableHSVVariation = true;
            _profile.preserveOriginalTextures = true;
            _profile.preserveNormalMaps = true;
            _profile.generatedMaterialsFolder = "Assets/Piaaria/Art/Pizzeria/Toon/Materials/Architecture";

            _profile.InitializeDefaultRules();

            AssetDatabase.CreateAsset(_profile, SoftToonProfilePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private void SetStatus(string msg, MessageType type)
        {
            _statusMessage = $"[{DateTime.Now:HH:mm:ss}] {msg}";
            _statusType = type;
            Repaint();
        }
    }
}
#endif
