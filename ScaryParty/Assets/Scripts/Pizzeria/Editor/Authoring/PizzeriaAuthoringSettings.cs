using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Presentation;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    public enum PizzeriaValidationProfileMode
    {
        FullPizzeria = 0,
        PartialTestLayout = 1
    }

    public enum PizzeriaMultiSelectionMode
    {
        SingleEquipmentFromSelection = 0,
        OneEquipmentPerObject = 1
    }

    [Serializable]
    public class PizzeriaFloorRegionConfig
    {
        public string regionName = "Regiao_Principal";
        public Vector2 minXZ = new Vector2(0f, -13.2f);
        public Vector2 maxXZ = new Vector2(32.8f, 9.2f);
        public float floorY = -1.6f;
        public float thickness = 0.2f;
    }

    [Serializable]
    public class PizzeriaRoomRegionConfig
    {
        public string roomName = "Comodo_01";
        [Tooltip("Limite mínimo X e Z (em metros locais da raiz) deste cômodo. Paredes divisórias na fronteira ficam apenas no cômodo de menor prioridade/índice.")]
        public Vector2 minXZ = new Vector2(-1.5f, -13.6f);
        [Tooltip("Limite máximo X e Z (em metros locais da raiz) deste cômodo.")]
        public Vector2 maxXZ = new Vector2(16.25f, 10.5f);
        [Tooltip("Ordem de avaliação. Paredes divisórias exatamente na fronteira entre dois cômodos pertencem ao primeiro cômodo que as contiver (sem duplicar).")]
        public int priority = 1;
    }

    [Serializable]
    public class PizzeriaHierarchyClassificationRule
    {
        public string ruleName;
        public bool enabled = true;
        public int priority = 100;
        public List<string> exactOrPrefixAliases = new List<string>();
        public PizzeriaHierarchyCategory targetCategory = PizzeriaHierarchyCategory.Decoration;
    }

    [Serializable]
    public class PizzeriaEquipmentAssemblyRule
    {
        public string setRuleName = "Conjunto";
        public bool enabled = true;
        public string groupNamePrefix = "Equipamento";
        public List<string> parentPrefixes = new List<string>();
        public List<string> childPrefixes = new List<string>();
        public float maxBoundsDistance = 1.75f;
        public float minScoreGapForAutoAssign = 0.15f;
        public int maxChildrenPerParent = 2;
        public bool createNormalizedUnitRoot = true;
        public PizzeriaHierarchyCategory targetCategory = PizzeriaHierarchyCategory.Equipment;
    }

    [Serializable]
    public class PizzeriaMinimumCoverageEntry
    {
        public PizzeriaStationKind kind;
        public int minimumRequired = 1;
    }

    [CreateAssetMenu(fileName = "PizzeriaAuthoringSettings", menuName = "ScaryParty/Pizzeria/Modular Authoring Settings")]
    public class PizzeriaAuthoringSettings : ScriptableObject
    {
        public const int CurrentSettingsVersion = 2;

        [Header("1. Modelo e Prefab")]
        [Tooltip("Modelo 3D importado (FBX) ou prefab de modelo escolhido pelo usuário.")]
        public GameObject sourceModel;

        [Tooltip("Prefab editável da pizzaria gerado/selecionado para autoria.")]
        public GameObject targetPrefab;

        [Tooltip("Pasta onde o prefab editável será criado caso ainda não exista.")]
        public string outputFolder = "Assets/Prefabs/Pizzeria";

        [Tooltip("Nome do arquivo de prefab a ser criado.")]
        public string outputPrefabName = "PizzeriaModular_Manual";

        [Tooltip("Organizar hierarquia automaticamente em grupos estruturais ao criar o prefab.")]
        public bool organizeHierarchyOnCreate = true;

        [Tooltip("Agrupar a hierarquia dividindo por cômodos (ex: Comodo_01_Salao, Comodo_02_Cozinha, Comodo_03_Logistica) com paredes divisórias em apenas 1 cômodo.")]
        public bool groupByRooms = true;

        [Tooltip("Parentalizar automaticamente objetos apoiados em cima de bancadas/mesas/prateleiras (utensílios, molhos, tábuas, registradora, mochilas etc.) e facas nos suportes.")]
        public bool parentItemsOnSurfaces = true;

        [Tooltip("Parentalizar também as cadeiras próximas à mesa correspondente para mover o conjunto de mesa inteiro.")]
        public bool parentChairsToTables = true;

        [Tooltip("Fator de escala explícito opcional (1.0 = preservar escala importada sem reescalar).")]
        public float explicitScaleFactor = 1.0f;

        [Header("Divisão Modular por Cômodos (Paredes divisórias ficam em apenas 1 cômodo)")]
        public List<PizzeriaRoomRegionConfig> roomRegions = new List<PizzeriaRoomRegionConfig>
        {
            new PizzeriaRoomRegionConfig
            {
                roomName = "Comodo_01_Salao",
                minXZ = new Vector2(-2.0f, -13.8f),
                maxXZ = new Vector2(16.25f, 11.0f),
                priority = 1
            },
            new PizzeriaRoomRegionConfig
            {
                roomName = "Comodo_02_Cozinha",
                minXZ = new Vector2(16.25f, -13.0f),
                maxXZ = new Vector2(34.0f, 12.5f),
                priority = 2
            },
            new PizzeriaRoomRegionConfig
            {
                roomName = "Comodo_03_Logistica",
                minXZ = new Vector2(15.8f, -29.0f),
                maxXZ = new Vector2(34.0f, -13.0f),
                priority = 3
            }
        };

        [Header("Configuração de Piso Opcional (Regiões / Formato em L)")]
        public Material floorMaterial;
        public List<PizzeriaFloorRegionConfig> floorRegions = new List<PizzeriaFloorRegionConfig>
        {
            new PizzeriaFloorRegionConfig
            {
                regionName = "Salao_E_Cozinha",
                minXZ = new Vector2(0f, -13.2f),
                maxXZ = new Vector2(32.8f, 9.2f),
                floorY = -1.6f,
                thickness = 0.2f
            },
            new PizzeriaFloorRegionConfig
            {
                regionName = "Ala_Embalagem_E_Estoque",
                minXZ = new Vector2(16.4f, -27.6f),
                maxXZ = new Vector2(32.8f, -13.2f),
                floorY = -1.6f,
                thickness = 0.2f
            }
        };

        [Header("2. Funcionamento Base")]
        [Tooltip("Criar uma cópia própria do PizzeriaConfig para este prefab para não mutar configurações compartilhadas.")]
        public bool cloneConfigForNewPrefab = true;

        public PizzeriaConfig pizzeriaConfig;
        public ItemPresentationCatalog presentationCatalog;

        [Header("Footprint e Âncoras Padrão")]
        public Vector2 defaultFootprintSize = new Vector2(34.5f, 38.5f);
        public Vector3 defaultFootprintCenterOffset = new Vector3(16.4f, 0f, -9.2f);
        public Vector2 defaultEntranceDirectionXZ = new Vector2(-1f, 0f);
        public float defaultEntranceOffsetAlongFacade = 7.2f;
        public float defaultSideClearance = 1.5f;
        public float defaultFrontSidewalkDepth = 3.0f;

        [Header("3. Autoria de Bancadas e Objetos")]
        public PizzeriaMultiSelectionMode multiSelectionMode = PizzeriaMultiSelectionMode.OneEquipmentPerObject;
        public string stationDefOutputFolder = "Assets/ScriptableObjects/Pizzeria/Stations";
        public string extractedVisualsOutputFolder = "Assets/Prefabs/Pizzeria/Visuals";
        public bool showSceneGizmos = true;
        public bool showSlotHandles = true;
        public bool showInteractionReachGizmos = true;

        [Header("4. Validação")]
        public PizzeriaValidationProfileMode validationProfile = PizzeriaValidationProfileMode.FullPizzeria;
        public List<PizzeriaMinimumCoverageEntry> customMinimumCoverage = new List<PizzeriaMinimumCoverageEntry>();

        [Header("Regras de Organização Estrutural (Preset Configurável)")]
        public List<PizzeriaEquipmentAssemblyRule> assemblyRules = new List<PizzeriaEquipmentAssemblyRule>();
        public List<PizzeriaHierarchyClassificationRule> classificationRules = new List<PizzeriaHierarchyClassificationRule>();

        [Header("Metadados da Ferramenta")]
        public int toolVersion = CurrentSettingsVersion;
        public List<string> configuredBindingGuids = new List<string>();

        private void OnEnable()
        {
            if (toolVersion < CurrentSettingsVersion)
            {
                multiSelectionMode = PizzeriaMultiSelectionMode.OneEquipmentPerObject;
                toolVersion = CurrentSettingsVersion;
            }
            if (assemblyRules == null || assemblyRules.Count == 0 || classificationRules == null || classificationRules.Count == 0)
            {
                InitializeDefaultFbxPreset();
            }
            if (roomRegions == null || roomRegions.Count == 0)
            {
                InitializeDefaultRooms();
            }
            EnsureCoverageEntries();
        }

        public void InitializeDefaultRooms()
        {
            roomRegions = new List<PizzeriaRoomRegionConfig>
            {
                new PizzeriaRoomRegionConfig
                {
                    roomName = "Comodo_01_Salao",
                    minXZ = new Vector2(-2.0f, -13.8f),
                    maxXZ = new Vector2(16.25f, 11.0f),
                    priority = 1
                },
                new PizzeriaRoomRegionConfig
                {
                    roomName = "Comodo_02_Cozinha",
                    minXZ = new Vector2(16.25f, -13.0f),
                    maxXZ = new Vector2(34.0f, 12.5f),
                    priority = 2
                },
                new PizzeriaRoomRegionConfig
                {
                    roomName = "Comodo_03_Logistica",
                    minXZ = new Vector2(15.8f, -29.0f),
                    maxXZ = new Vector2(34.0f, -13.0f),
                    priority = 3
                }
            };
        }

        public string GetResolvedOutputPrefabPath()
        {
            return PizzeriaPrefabAuthoringService.ResolveTargetPrefabPath(this);
        }

        public static PizzeriaAuthoringSettings FindOrCreateDefault()
        {
            const string defaultPath = "Assets/ScriptableObjects/Pizzeria/PizzeriaAuthoringSettings_Modular.asset";
            var existing = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>(defaultPath);
            if (existing != null)
            {
                if (existing.sourceModel == null)
                {
                    existing.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx");
                    EditorUtility.SetDirty(existing);
                }
                return existing;
            }

            string[] guids = AssetDatabase.FindAssets("t:PizzeriaAuthoringSettings");
            if (guids != null && guids.Length > 0)
            {
                var found = AssetDatabase.LoadAssetAtPath<PizzeriaAuthoringSettings>(AssetDatabase.GUIDToAssetPath(guids[0]));
                if (found != null) return found;
            }

            PizzeriaPrefabAuthoringService.EnsureFolderExists("Assets/ScriptableObjects/Pizzeria");
            var asset = CreateInstance<PizzeriaAuthoringSettings>();
            asset.InitializeDefaultFbxPreset();
            asset.sourceModel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Pizzeria/Source/pizzariamodular (2)2 (1) (1) (2).fbx");
            asset.pizzeriaConfig = AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/PizzeriaConfig.asset")
                                ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/ScriptableObjects/Pizzeria/Config/PizzeriaConfig.asset")
                                ?? AssetDatabase.LoadAssetAtPath<PizzeriaConfig>("Assets/Resources/Pizzeria/PizzeriaConfig.asset");
            asset.presentationCatalog = AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/ItemPresentationCatalog.asset")
                                     ?? AssetDatabase.LoadAssetAtPath<ItemPresentationCatalog>("Assets/ScriptableObjects/Pizzeria/Presentation/ItemPresentationCatalog.asset");
            AssetDatabase.CreateAsset(asset, defaultPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        public int GetMinimumRequiredForKind(PizzeriaStationKind kind)
        {
            if (kind == PizzeriaStationKind.None) return 0;
            if (validationProfile == PizzeriaValidationProfileMode.FullPizzeria)
            {
                EnsureCoverageEntries();
                for (int i = 0; i < customMinimumCoverage.Count; i++)
                {
                    if (customMinimumCoverage[i].kind == kind)
                        return Mathf.Max(1, customMinimumCoverage[i].minimumRequired);
                }
                return 1;
            }
            else
            {
                EnsureCoverageEntries();
                for (int i = 0; i < customMinimumCoverage.Count; i++)
                {
                    if (customMinimumCoverage[i].kind == kind)
                        return Mathf.Max(0, customMinimumCoverage[i].minimumRequired);
                }
                return 0;
            }
        }

        public void EnsureCoverageEntries()
        {
            if (customMinimumCoverage == null)
                customMinimumCoverage = new List<PizzeriaMinimumCoverageEntry>();

            foreach (PizzeriaStationKind kind in Enum.GetValues(typeof(PizzeriaStationKind)))
            {
                if (kind == PizzeriaStationKind.None) continue;
                bool exists = false;
                for (int i = 0; i < customMinimumCoverage.Count; i++)
                {
                    if (customMinimumCoverage[i].kind == kind)
                    {
                        exists = true;
                        break;
                    }
                }
                if (!exists)
                {
                    customMinimumCoverage.Add(new PizzeriaMinimumCoverageEntry
                    {
                        kind = kind,
                        minimumRequired = 1
                    });
                }
            }
        }

        [ContextMenu("Restaurar Preset do FBX Modular")]
        public void InitializeDefaultFbxPreset()
        {
            assemblyRules = new List<PizzeriaEquipmentAssemblyRule>
            {
                new PizzeriaEquipmentAssemblyRule
                {
                    setRuleName = "Lixeira + Tampa",
                    enabled = true,
                    groupNamePrefix = "Lixeira",
                    parentPrefixes = new List<string> { "latadelixo", "lixeira" },
                    childPrefixes = new List<string> { "tampadolixo", "tampalixeira", "tampalixo" },
                    maxBoundsDistance = 0.65f,
                    minScoreGapForAutoAssign = 0.15f,
                    maxChildrenPerParent = 1,
                    createNormalizedUnitRoot = true,
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaEquipmentAssemblyRule
                {
                    setRuleName = "Forno + Porta de Forno",
                    enabled = true,
                    groupNamePrefix = "Forno",
                    parentPrefixes = new List<string> { "forno" },
                    childPrefixes = new List<string> { "portaforno" },
                    maxBoundsDistance = 1.5f,
                    minScoreGapForAutoAssign = 0.15f,
                    maxChildrenPerParent = 2,
                    createNormalizedUnitRoot = true,
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaEquipmentAssemblyRule
                {
                    setRuleName = "Geladeira + Portas (Grande/Pequena)",
                    enabled = true,
                    groupNamePrefix = "Geladeira",
                    parentPrefixes = new List<string> { "geladeira" },
                    childPrefixes = new List<string>
                    {
                        "portamaiorgeldeira",
                        "portamaiorgeladeira",
                        "portapequenageldeira",
                        "portapequenageladeira",
                        "portamaior",
                        "portageladeira"
                    },
                    maxBoundsDistance = 1.25f,
                    minScoreGapForAutoAssign = 0.12f,
                    maxChildrenPerParent = 2,
                    createNormalizedUnitRoot = true,
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaEquipmentAssemblyRule
                {
                    setRuleName = "Caixa Registradora + Gaveta",
                    enabled = true,
                    groupNamePrefix = "CaixaRegistradora",
                    parentPrefixes = new List<string> { "caixaregistradora" },
                    childPrefixes = new List<string> { "gavetacaixa" },
                    maxBoundsDistance = 0.65f,
                    minScoreGapForAutoAssign = 0.15f,
                    maxChildrenPerParent = 2,
                    createNormalizedUnitRoot = true,
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaEquipmentAssemblyRule
                {
                    setRuleName = "Abertura Arquitetônica (Batente + Folha de Porta)",
                    enabled = true,
                    groupNamePrefix = "ConjuntoPorta",
                    parentPrefixes = new List<string> { "batenteportapequena", "batenteportamediainterior", "batentefachada" },
                    childPrefixes = new List<string> { "portapequena", "portamediainterior", "portafachada" },
                    maxBoundsDistance = 1.25f,
                    minScoreGapForAutoAssign = 0.15f,
                    maxChildrenPerParent = 2,
                    createNormalizedUnitRoot = true,
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Openings
                }
            };

            // Regras específicas SEMPRE antes de regras genéricas!
            classificationRules = new List<PizzeriaHierarchyClassificationRule>
            {
                // 1. Regras super específicas para evitar falsos positivos (ex: suportefaca != faca, paredeporta != porta, portaforno != porta de sala)
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Suporte de Faca (Equipamento)",
                    priority = 10,
                    exactOrPrefixAliases = new List<string> { "suportefaca", "suportedefaca", "rackfaca" },
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Paredes com Porta ou Janela (Arquitetura/Paredes)",
                    priority = 11,
                    exactOrPrefixAliases = new List<string>
                    {
                        "paredeporta",
                        "paredeportapequena",
                        "paredeporta2variante",
                        "paredejanela",
                        "paredejaneladir",
                        "paredejanelaesq",
                        "paredejanelameio",
                        "paredefachada",
                        "paredefachadadir",
                        "paredefachadaesq",
                        "paredesuperiorfachada"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Walls_External
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Paredes Superiores Externas",
                    priority = 12,
                    exactOrPrefixAliases = new List<string> { "paredesuperiorexterna", "paredeexterna" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Walls_External
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Paredes Superiores Internas",
                    priority = 13,
                    exactOrPrefixAliases = new List<string> { "paredesuperiorinterna", "paredeinterna" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Walls_Internal
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Paredes Comuns",
                    priority = 14,
                    exactOrPrefixAliases = new List<string> { "paredecomum", "parede" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Walls
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Portas de Equipamentos (Forno/Geladeira)",
                    priority = 15,
                    exactOrPrefixAliases = new List<string>
                    {
                        "portaforno",
                        "portamaiorgeldeira",
                        "portamaiorgeladeira",
                        "portapequenageldeira",
                        "portapequenageladeira",
                        "portageladeira",
                        "portamaior"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Batentes e Portas Arquitetônicas",
                    priority = 16,
                    exactOrPrefixAliases = new List<string>
                    {
                        "batenteportapequena",
                        "batenteportamediainterior",
                        "batentefachada",
                        "portapequena",
                        "portamediainterior",
                        "abertura"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Openings
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Pilastras, Colunas e Vigas",
                    priority = 20,
                    exactOrPrefixAliases = new List<string> { "pilastra", "coluna", "viga" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Structure
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Pisos",
                    priority = 21,
                    exactOrPrefixAliases = new List<string> { "piso", "chao", "floor" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Floors
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Tetos",
                    priority = 22,
                    exactOrPrefixAliases = new List<string> { "teto", "forro", "telhado", "cobertura" },
                    targetCategory = PizzeriaHierarchyCategory.Architecture_Ceilings
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Equipamentos Principais (Forno, Geladeira, Armário, Lixeira, Caixa Registradora)",
                    priority = 30,
                    exactOrPrefixAliases = new List<string>
                    {
                        "forno",
                        "geladeira",
                        "armario",
                        "latadelixo",
                        "tampadolixo",
                        "lixeira",
                        "caixaregistradora",
                        "gavetacaixa"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Equipment
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Mobiliário (Mesas, Cadeiras, Balcões, Prateleiras)",
                    priority = 40,
                    exactOrPrefixAliases = new List<string>
                    {
                        "mesacomum",
                        "mesamedia",
                        "mesaempacotamento",
                        "mesa",
                        "cadeira",
                        "balcaopabertura",
                        "quinabalcao",
                        "balcao",
                        "prateleira"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Furniture
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Utensílios e Caixas/Mochilas Soltas",
                    priority = 50,
                    exactOrPrefixAliases = new List<string>
                    {
                        "faca",
                        "tabuadecortar",
                        "pratoforma",
                        "cortadordepizza",
                        "panelaeconcha",
                        "espatulasla",
                        "espatula",
                        "deliverybag",
                        "caixa",
                        "prancheta"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Utensils
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Decoração",
                    priority = 60,
                    exactOrPrefixAliases = new List<string>
                    {
                        "cardapio",
                        "molhos",
                        "quadro",
                        "placa"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Decoration
                },
                new PizzeriaHierarchyClassificationRule
                {
                    ruleName = "Objetos Ambíguos ou de Referência de Escala (Revisar)",
                    priority = 90,
                    exactOrPrefixAliases = new List<string>
                    {
                        "coiso",
                        "player"
                    },
                    targetCategory = PizzeriaHierarchyCategory.Review
                }
            };
        }
    }

    [CustomEditor(typeof(PizzeriaAuthoringSettings))]
    public class PizzeriaAuthoringSettingsEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var settings = (PizzeriaAuthoringSettings)target;

            EditorGUILayout.Space(4);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Builder Manual — Pizzaria Modular", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Esta configuração alimenta a janela Tools > Scary Party > Pizzaria Modular. " +
                "Referências temporárias de Prefab Stage não são salvas neste asset.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Abrir Janela do Builder", GUILayout.Height(28)))
            {
                PizzeriaAuthoringWindow.OpenWithSettings(settings);
            }
            if (GUILayout.Button("Restaurar Preset FBX", GUILayout.Height(28), GUILayout.Width(150)))
            {
                Undo.RecordObject(settings, "Restaurar Preset FBX");
                settings.InitializeDefaultFbxPreset();
                EditorUtility.SetDirty(settings);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(6);
            DrawDefaultInspector();
        }
    }
}
