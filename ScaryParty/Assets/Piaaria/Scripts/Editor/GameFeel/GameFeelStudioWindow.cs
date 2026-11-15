using System;
using System.Collections.Generic;
using System.IO;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Player;
using ScaryParty.UI.Piaria.Core;
using UnityEditor;
using UnityEngine;

namespace ScaryParty.Pizzeria.EditorTools.GameFeel
{
    /// <summary>
    /// Wizard central de Game Feel e animações cartoon do ScaryParty.
    /// Organiza auditoria, perfis, ajuste por família/processo/personagem/UI, aplicação segura (Undo) e validação.
    /// </summary>
    public sealed class GameFeelStudioWindow : EditorWindow
    {
        private const string DefaultProfileAssetPath = "Assets/Piaaria/Resources/GameFeelDefaultProfile.asset";
        private static readonly string[] PizzeriaPrefabCandidates =
        {
            "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab",
            "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab",
            "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaPrefab.prefab"
        };

        private static readonly string[] TabLabels =
        {
            "Auditoria",
            "Perfis",
            "Objeto",
            "Processos",
            "Personagens",
            "UI",
            "Aplicação",
            "Validação"
        };

        private static readonly GameFeelEventId[] ObjectEvents =
        {
            GameFeelEventId.FocusEnter,
            GameFeelEventId.ItemPickedUp,
            GameFeelEventId.ToolPickedUp,
            GameFeelEventId.ItemPlaced,
            GameFeelEventId.BoxPlaced,
            GameFeelEventId.BoxPackaged,
            GameFeelEventId.BackpackEquipped,
            GameFeelEventId.BackpackStored,
            GameFeelEventId.BackpackRemoved,
            GameFeelEventId.StorageDispensed,
            GameFeelEventId.DoorOpened,
            GameFeelEventId.DoorClosed,
            GameFeelEventId.ActionRejected
        };

        private static readonly GameFeelEventId[] ProcessEvents =
        {
            GameFeelEventId.ProcessStarted,
            GameFeelEventId.RollDoughStep,
            GameFeelEventId.SliceTomatoPepperoniStep,
            GameFeelEventId.SliceMushroomStep,
            GameFeelEventId.GrateCheeseStep,
            GameFeelEventId.SpreadSauce,
            GameFeelEventId.SprinkleCheese,
            GameFeelEventId.PlaceToppings,
            GameFeelEventId.ProcessCompleted,
            GameFeelEventId.OvenInserted,
            GameFeelEventId.OvenReadyDing,
            GameFeelEventId.OvenBurningWarning,
            GameFeelEventId.OvenBurned,
            GameFeelEventId.OvenRemoved,
            GameFeelEventId.PhoneRingPulse,
            GameFeelEventId.OrderPosted
        };

        private static readonly GameFeelEventId[] CharacterEvents =
        {
            GameFeelEventId.NpcCounterArrived,
            GameFeelEventId.NpcOrderRegistered,
            GameFeelEventId.NpcImpatient,
            GameFeelEventId.NpcOrderGood,
            GameFeelEventId.NpcOrderBad,
            GameFeelEventId.NpcSeatedSettle,
            GameFeelEventId.NpcEatingLoop,
            GameFeelEventId.NpcLeavingAngry,
            GameFeelEventId.DeliverySuccess,
            GameFeelEventId.DeliveryPartial,
            GameFeelEventId.DeliveryFailed,
            GameFeelEventId.PlayerWorkLoop
        };

        private int _selectedTab;
        private Vector2 _scroll;
        private GameFeelProfile _profileAsset;
        private int _selectedObjectEventIdx;
        private GameFeelMaterialFamily _selectedFamily = GameFeelMaterialFamily.Default;
        private int _selectedProcessEventIdx;
        private int _selectedCharacterEventIdx;

        // Auditoria cache
        private string _auditSummary = "Clique em 'Executar Auditoria' para inspecionar cena aberta e prefabs.";
        private readonly List<string> _auditFindings = new List<string>();
        private readonly List<string> _validationMessages = new List<string>();

        [MenuItem("Tools/Scary Party/Wizards/Visuals/Game Feel Studio", false, 42)]
        public static void OpenWindow()
        {
            var win = GetWindow<GameFeelStudioWindow>("Game Feel Studio");
            win.minSize = new Vector2(680f, 520f);
            win.Show();
        }

        private void OnEnable()
        {
            LoadProfileAsset();
        }

        private void LoadProfileAsset()
        {
            _profileAsset = AssetDatabase.LoadAssetAtPath<GameFeelProfile>(DefaultProfileAssetPath);
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Label("ScaryParty — Estúdio de Game Feel & Animações", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUI.color = Application.isPlaying ? new Color(0.6f, 1f, 0.7f) : Color.white;
            GUILayout.Label(Application.isPlaying ? "PlayMode Ativo" : "EditMode", EditorStyles.miniLabel);
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();

            _selectedTab = GUILayout.Toolbar(_selectedTab, TabLabels, GUILayout.Height(26));
            EditorGUILayout.Space(6);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            switch (_selectedTab)
            {
                case 0: DrawAuditTab(); break;
                case 1: DrawProfilesTab(); break;
                case 2: DrawRecipeCategoryTab("Objeto, Mãos & Mochila", ObjectEvents, ref _selectedObjectEventIdx, allowFamilySelector: true); break;
                case 3: DrawRecipeCategoryTab("Processos de Cozinha & Forno", ProcessEvents, ref _selectedProcessEventIdx, allowFamilySelector: false); break;
                case 4: DrawCharactersTab(); break;
                case 5: DrawUiTab(); break;
                case 6: DrawApplyTab(); break;
                case 7: DrawValidationTab(); break;
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawAuditTab()
        {
            EditorGUILayout.HelpBox(
                "Inspeciona de forma somente-leitura os scripts e prefabs ativos (PizzeriaModular_Manual, PizzeriaModular_CelShaded, PizzeriaPrefab) e a cena aberta.",
                MessageType.Info);

            if (GUILayout.Button("Executar Auditoria Completa", GUILayout.Height(28)))
            {
                RunAudit();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Resumo", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(_auditSummary, MessageType.None);

            for (int i = 0; i < _auditFindings.Count; i++)
            {
                EditorGUILayout.LabelField("• " + _auditFindings[i], EditorStyles.wordWrappedLabel);
            }
        }

        private void RunAudit()
        {
            _auditFindings.Clear();
            LoadProfileAsset();

            if (_profileAsset != null)
            {
                _auditFindings.Add($"Perfil Resources ativo: {DefaultProfileAssetPath} ({_profileAsset.Recipes.Count} receitas).");
            }
            else
            {
                _auditFindings.Add($"AVISO: {DefaultProfileAssetPath} não existe ainda em Assets/Piaaria/Resources (runtime usa fallback em memória até ser salvo na aba Aplicação).");
            }

            var presenterInScene = FindFirstObjectByType<GameFeelPresenter>();
            var bridgeInScene = FindFirstObjectByType<GameFeelNetworkBridge>();
            var uiServiceInScene = FindFirstObjectByType<UIFeedbackService>();
            var soundInScene = FindFirstObjectByType<PizzeriaSoundManager>();

            _auditFindings.Add($"Cena aberta: GameFeelPresenter={(presenterInScene != null ? "OK" : "Auto em runtime")} | GameFeelNetworkBridge={(bridgeInScene != null ? "OK" : "Auto em runtime")} | UIFeedbackService={(uiServiceInScene != null ? "OK" : "Ausente")} | PizzeriaSoundManager={(soundInScene != null ? "OK" : "Ausente")}.");

            int foundPrefabs = 0;
            foreach (string path in PizzeriaPrefabCandidates)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    _auditFindings.Add($"Prefab ausente neste caminho: {path}");
                    continue;
                }

                foundPrefabs++;
                int visCount = prefab.GetComponentsInChildren<PizzeriaItemVisualizer>(true).Length;
                int fxCount = prefab.GetComponentsInChildren<KitchenProcessEffects>(true).Length;
                int ovens = prefab.GetComponentsInChildren<OvenStation>(true).Length;
                var phones = prefab.GetComponentsInChildren<PhoneRinger>(true);
                var tickets = prefab.GetComponentsInChildren<TicketSheetView>(true);

                int phoneSharedRoot = 0;
                for (int p = 0; p < phones.Length; p++)
                {
                    if (phones[p].beaconRenderer == null || phones[p].beaconRenderer.transform == phones[p].transform)
                        phoneSharedRoot++;
                }

                int ticketSharedRoot = 0;
                for (int t = 0; t < tickets.Length; t++)
                {
                    if (tickets[t].paperRenderer == null || tickets[t].paperRenderer.transform == tickets[t].transform)
                        ticketSharedRoot++;
                }

                _auditFindings.Add(
                    $"{Path.GetFileName(path)}: Visualizer={visCount}, KitchenFX={fxCount}, Ovens={ovens}, Phones={phones.Length} (raiz={phoneSharedRoot}), Tickets={tickets.Length} (raiz={ticketSharedRoot}).");
            }

            _auditSummary = $"Auditoria concluída: {foundPrefabs}/{PizzeriaPrefabCandidates.Length} prefabs de pizzaria inspecionados.";
        }

        private void DrawProfilesTab()
        {
            EditorGUILayout.LabelField("Perfil Persistido (ScriptableObject)", EditorStyles.boldLabel);
            _profileAsset = (GameFeelProfile)EditorGUILayout.ObjectField("GameFeelDefaultProfile", _profileAsset, typeof(GameFeelProfile), false);

            if (_profileAsset == null)
            {
                EditorGUILayout.HelpBox(
                    "Nenhum asset GameFeelDefaultProfile encontrado em Assets/Piaaria/Resources/. Crie na aba 'Aplicação' para habilitar edição persistente no Editor.",
                    MessageType.Warning);
            }
            else
            {
                EditorGUILayout.LabelField($"Receitas cadastradas: {_profileAsset.Recipes.Count}");
                if (GUILayout.Button("Preencher / Atualizar Receitas Padrão no Asset (Undo)"))
                {
                    Undo.RecordObject(_profileAsset, "Populate Default GameFeel Recipes");
                    _profileAsset.PopulateDefaultRecipes();
                    EditorUtility.SetDirty(_profileAsset);
                }
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Parâmetros Globais em Tempo Real (GameFeelSettings)", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            var newPreset = (GameFeelPresetMode)EditorGUILayout.EnumPopup("Preset Ativo", GameFeelSettings.Preset);
            bool newReduce = EditorGUILayout.Toggle("ReduceMotion (Acessibilidade UI + 3D)", GameFeelSettings.ReduceMotion);
            bool newCam = EditorGUILayout.Toggle("Permitir Micro-impulso de Câmera (Opt-in)", GameFeelSettings.AllowCameraImpulse);
            bool newFlashes = EditorGUILayout.Toggle("Permitir Flashes / Brilhos", GameFeelSettings.AllowFlashes);
            bool newVfx = EditorGUILayout.Toggle("Permitir VFX Intensos", GameFeelSettings.AllowIntenseVfx);
            float mov = EditorGUILayout.Slider("Multiplicador Movimento", GameFeelSettings.MovementMultiplier, 0f, 1.5f);
            float part = EditorGUILayout.Slider("Multiplicador Partículas", GameFeelSettings.ParticlesMultiplier, 0f, 1.5f);
            float aud = EditorGUILayout.Slider("Multiplicador Áudio", GameFeelSettings.AudioMultiplier, 0f, 1.5f);
            float ui = EditorGUILayout.Slider("Multiplicador UI", GameFeelSettings.UIMultiplier, 0f, 1.5f);

            if (EditorGUI.EndChangeCheck())
            {
                GameFeelSettings.Preset = newPreset;
                GameFeelSettings.ReduceMotion = newReduce;
                GameFeelSettings.AllowCameraImpulse = newCam;
                GameFeelSettings.AllowFlashes = newFlashes;
                GameFeelSettings.AllowIntenseVfx = newVfx;
                GameFeelSettings.MovementMultiplier = mov;
                GameFeelSettings.ParticlesMultiplier = part;
                GameFeelSettings.AudioMultiplier = aud;
                GameFeelSettings.UIMultiplier = ui;
            }
        }

        private void DrawRecipeCategoryTab(string title, GameFeelEventId[] events, ref int selectedIdx, bool allowFamilySelector)
        {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (_profileAsset == null)
            {
                EditorGUILayout.HelpBox("Carregue ou crie GameFeelDefaultProfile na aba 'Perfis' / 'Aplicação' para editar receitas.", MessageType.Info);
                return;
            }

            string[] names = new string[events.Length];
            for (int i = 0; i < events.Length; i++) names[i] = events[i].ToString();

            selectedIdx = Mathf.Clamp(selectedIdx, 0, events.Length - 1);
            selectedIdx = EditorGUILayout.Popup("Evento", selectedIdx, names);
            var evt = events[selectedIdx];

            if (allowFamilySelector)
            {
                _selectedFamily = (GameFeelMaterialFamily)EditorGUILayout.EnumPopup("Família de Material", _selectedFamily);
            }
            var family = allowFamilySelector ? _selectedFamily : ResolveDefaultFamilyForEvent(evt);

            DrawRecipeEditor(evt, family, isCharacterEvent: false);
        }

        private void DrawCharactersTab()
        {
            EditorGUILayout.LabelField("Personagens & Clientes NPC", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"Clamps ativos para rig Generic / NavMeshAgent: escala entre [{GameFeelSettings.MinCharacterScale:0.00} .. {GameFeelSettings.MaxCharacterScale:0.00}] e bloqueio de deslocamento vertical quando sentado (LockVerticalAndHipOffset).",
                MessageType.Info);

            if (_profileAsset == null)
            {
                EditorGUILayout.HelpBox("Carregue ou crie GameFeelDefaultProfile na aba 'Aplicação' para ajustar reações corporais.", MessageType.Info);
                return;
            }

            string[] names = new string[CharacterEvents.Length];
            for (int i = 0; i < CharacterEvents.Length; i++) names[i] = CharacterEvents[i].ToString();

            _selectedCharacterEventIdx = Mathf.Clamp(_selectedCharacterEventIdx, 0, CharacterEvents.Length - 1);
            _selectedCharacterEventIdx = EditorGUILayout.Popup("Reação Corporal", _selectedCharacterEventIdx, names);
            DrawRecipeEditor(CharacterEvents[_selectedCharacterEventIdx], GameFeelMaterialFamily.CharacterBody, isCharacterEvent: true);
        }

        private void DrawRecipeEditor(GameFeelEventId evt, GameFeelMaterialFamily family, bool isCharacterEvent)
        {
            if (_profileAsset == null) return;

            int exactIdx = FindRecipeIndex(_profileAsset, evt, family);
            bool hasExact = exactIdx >= 0;
            GameFeelRecipe current = hasExact ? _profileAsset.Recipes[exactIdx] : _profileAsset.GetRecipeOrFallback(evt, family);

            EditorGUILayout.LabelField(hasExact ? "Editando receita específica" : "Usando fallback (clique abaixo para criar variante específica)", EditorStyles.miniLabel);
            if (!hasExact && GUILayout.Button($"Adicionar Receita ({evt} / {family})"))
            {
                Undo.RecordObject(_profileAsset, "Add GameFeel Recipe");
                current.EventId = evt;
                current.MaterialFamily = family;
                _profileAsset.Recipes.Add(current);
                _profileAsset.InvalidateLookup();
                EditorUtility.SetDirty(_profileAsset);
                return;
            }

            EditorGUI.BeginChangeCheck();
            current.Channels = (GameFeelChannel)EditorGUILayout.EnumFlagsField("Canais", current.Channels);
            current.Duration = EditorGUILayout.Slider("Duração (s)", current.Duration, 0.04f, 1.2f);
            current.AnticipationFraction = EditorGUILayout.Slider("Fração Antecipação", current.AnticipationFraction, 0f, 0.35f);
            current.ImpactFraction = EditorGUILayout.Slider("Fração Impacto", current.ImpactFraction, 0.05f, 0.70f);
            current.ReboundFraction = EditorGUILayout.Slider("Fração Overshoot/Rebound", current.ReboundFraction, 0.20f, 0.90f);

            float minS = isCharacterEvent ? GameFeelSettings.MinCharacterScale : GameFeelSettings.MinItemScale;
            float maxS = isCharacterEvent ? GameFeelSettings.MaxCharacterScale : GameFeelSettings.MaxItemScale;
            current.SquashScale = ClampVec(EditorGUILayout.Vector3Field("Squash Scale", current.SquashScale), minS, maxS);
            current.ReboundScale = ClampVec(EditorGUILayout.Vector3Field("Rebound Scale", current.ReboundScale), minS, maxS);
            current.ArcHeight = EditorGUILayout.Slider("Altura Arco (m)", current.ArcHeight, 0f, isCharacterEvent ? 0.04f : 0.14f);
            current.MaxTiltDegrees = EditorGUILayout.Slider("Inclinação Máx (°)", current.MaxTiltDegrees, 0f, GameFeelSettings.MaxItemTiltDegrees);
            current.AsymmetryRatio = EditorGUILayout.Slider("Assimetria Sloppy", current.AsymmetryRatio, 0f, 0.22f);
            current.Cooldown = EditorGUILayout.Slider("Cooldown (s)", current.Cooldown, 0f, 3.0f);
            current.Priority = EditorGUILayout.IntSlider("Prioridade Voz/Evento", current.Priority, 0, 100);
            current.PitchVariance = EditorGUILayout.Slider("Variação Pitch", current.PitchVariance, 0f, 0.12f);
            current.VolumeScale = EditorGUILayout.Slider("Volume", current.VolumeScale, 0f, 1.4f);
            current.OptionalFeelPrefab = (MoreMountains.Feedbacks.MMF_Player)EditorGUILayout.ObjectField(
                "MMF_Player Override (Feel)", current.OptionalFeelPrefab, typeof(MoreMountains.Feedbacks.MMF_Player), false);

            if (EditorGUI.EndChangeCheck() && hasExact)
            {
                Undo.RecordObject(_profileAsset, "Edit GameFeel Recipe");
                _profileAsset.Recipes[exactIdx] = current;
                _profileAsset.InvalidateLookup();
                EditorUtility.SetDirty(_profileAsset);
            }

            EditorGUILayout.Space(6);
            GUI.enabled = Application.isPlaying;
            if (GUILayout.Button("Testar Evento no Objeto Selecionado (PlayMode)"))
            {
                Transform target = Selection.activeGameObject != null ? Selection.activeGameObject.transform : null;
                var presenter = GameFeelPresenter.EnsureInstance();
                presenter.SetProfile(_profileAsset);
                presenter.TriggerSimple(evt, target, 999999UL, 0, (uint)Time.frameCount, family, 1f, true);
            }
            GUI.enabled = true;
        }

        private void DrawUiTab()
        {
            EditorGUILayout.LabelField("UI Toolkit & Feedback Sonoro 2D", EditorStyles.boldLabel);
            UIToolkitTweenService.ReduceMotion = EditorGUILayout.Toggle("UIToolkitTweenService.ReduceMotion", UIToolkitTweenService.ReduceMotion);
            EditorGUILayout.LabelField($"Elementos com tweens ativos: {UIToolkitTweenService.ActiveTweenElementCount}");
            EditorGUILayout.LabelField($"UIFeedbackService ativo na cena: {(UIFeedbackService.Instance != null ? "Sim" : "Não")}");

            EditorGUILayout.Space(6);
            GUI.enabled = Application.isPlaying;
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Testar Hover (Feel)")) GameFeelPresenter.EnsureInstance().TriggerSimple(GameFeelEventId.UIHover);
            if (GUILayout.Button("Testar Click (Feel)")) GameFeelPresenter.EnsureInstance().TriggerSimple(GameFeelEventId.UIClick);
            GUI.enabled = Application.isPlaying && UIFeedbackService.Instance != null;
            if (GUILayout.Button("Testar Sucesso")) UIFeedbackService.Instance.PlaySuccess();
            if (GUILayout.Button("Testar Ready")) UIFeedbackService.Instance.PlayReady();
            if (GUILayout.Button("Testar Erro")) UIFeedbackService.Instance.PlayError();
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
        }

        private void DrawApplyTab()
        {
            EditorGUILayout.LabelField("Persistência e Aplicação Segura (com Undo)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Todas as ações abaixo preservam GUIDs existentes e usam Undo. Prefabs .prefab não são sobrescritos automaticamente em lote.",
                MessageType.Info);

            if (GUILayout.Button("1. Criar / Garantir GameFeelDefaultProfile em Resources", GUILayout.Height(28)))
            {
                EnsureDefaultProfileAsset();
            }

            if (GUILayout.Button("2. Garantir GameFeelPresenter + NetworkBridge na Cena Aberta (Undo)", GUILayout.Height(28)))
            {
                EnsureSceneCoordinatorWithUndo();
            }

            if (GUILayout.Button("3. Adicionar GameFeelVisualPivot aos Objetos Selecionados (Undo)", GUILayout.Height(28)))
            {
                AddPivotToSelectionWithUndo();
            }

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Reversão Seletiva (Rollback)", EditorStyles.boldLabel);

            if (GUILayout.Button("Reverter alterações pendentes em memória no GameFeelDefaultProfile"))
            {
                if (_profileAsset != null)
                {
                    Undo.RecordObject(_profileAsset, "Revert GameFeelProfile to Defaults");
                    _profileAsset.PopulateDefaultRecipes();
                    EditorUtility.SetDirty(_profileAsset);
                    AssetDatabase.SaveAssets();
                }
            }

            if (GUILayout.Button("Remover GameFeelPresenter / Bridge adicionados na Cena Aberta (Undo)"))
            {
                var presenter = FindFirstObjectByType<GameFeelPresenter>();
                if (presenter != null) Undo.DestroyObjectImmediate(presenter.gameObject);
                var bridge = FindFirstObjectByType<GameFeelNetworkBridge>();
                if (bridge != null) Undo.DestroyObjectImmediate(bridge);
            }
        }

        private void DrawValidationTab()
        {
            EditorGUILayout.LabelField("Checklist de Validação Estática & PlayMode", EditorStyles.boldLabel);
            if (GUILayout.Button("Executar Verificações de Integridade do Perfil", GUILayout.Height(28)))
            {
                RunProfileValidation();
            }

            for (int i = 0; i < _validationMessages.Count; i++)
            {
                EditorGUILayout.LabelField("• " + _validationMessages[i], EditorStyles.wordWrappedLabel);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.HelpBox(
                "Para testes unitários NUnit (GameFeelSystemTests): abra Window > General > Test Runner > EditMode e execute 'GameFeelSystemTests'.\n" +
                "Para gameplay, teste no Unity Editor em PlayMode: pickup/drop, corte tomate, rolo massa, forno, entrega boa/ruim, backpack, telefone e ReduceMotion.",
                MessageType.None);
        }

        private void RunProfileValidation()
        {
            _validationMessages.Clear();
            LoadProfileAsset();
            var profile = _profileAsset != null ? _profileAsset : GameFeelProfile.CreateRuntimeDefault();
            int violations = 0;

            for (int i = 0; i < profile.Recipes.Count; i++)
            {
                var r = profile.Recipes[i];
                bool isChar = r.MaterialFamily == GameFeelMaterialFamily.CharacterBody;
                float min = isChar ? GameFeelSettings.MinCharacterScale : GameFeelSettings.MinItemScale;
                float max = isChar ? GameFeelSettings.MaxCharacterScale : GameFeelSettings.MaxItemScale;

                if (OutOfRange(r.SquashScale, min, max) || OutOfRange(r.ReboundScale, min, max) || r.MaxTiltDegrees > GameFeelSettings.MaxItemTiltDegrees + 0.001f)
                {
                    violations++;
                    _validationMessages.Add($"VIOLAÇÃO [{r.EventId}/{r.MaterialFamily}]: escala ou tilt fora dos limites seguros.");
                }
            }

            _validationMessages.Add(violations == 0
                ? $"OK: {profile.Recipes.Count} receitas dentro dos clamps de segurança (itens [0.78..1.22], chars [0.95..1.05], tilt <= 12°)."
                : $"Encontradas {violations} violações nos clamps de segurança.");
        }

        private void EnsureDefaultProfileAsset()
        {
            const string resourcesDir = "Assets/Piaaria/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesDir))
            {
                AssetDatabase.CreateFolder("Assets/Piaaria", "Resources");
            }

            var existing = AssetDatabase.LoadAssetAtPath<GameFeelProfile>(DefaultProfileAssetPath);
            if (existing != null)
            {
                _profileAsset = existing;
                Selection.activeObject = existing;
                return;
            }

            var created = CreateInstance<GameFeelProfile>();
            created.PopulateDefaultRecipes();
            AssetDatabase.CreateAsset(created, DefaultProfileAssetPath);
            AssetDatabase.SaveAssets();
            _profileAsset = created;
            Selection.activeObject = created;
        }

        private static void EnsureSceneCoordinatorWithUndo()
        {
            var presenter = FindFirstObjectByType<GameFeelPresenter>();
            if (presenter == null)
            {
                var go = new GameObject("GameFeelCoordinator");
                Undo.RegisterCreatedObjectUndo(go, "Create GameFeelCoordinator");
                presenter = go.AddComponent<GameFeelPresenter>();
                go.AddComponent<GameFeelNetworkBridge>();
                Selection.activeGameObject = go;
                return;
            }

            if (presenter.GetComponent<GameFeelNetworkBridge>() == null)
            {
                Undo.AddComponent<GameFeelNetworkBridge>(presenter.gameObject);
            }
            Selection.activeGameObject = presenter.gameObject;
        }

        private static void AddPivotToSelectionWithUndo()
        {
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                if (go.GetComponent<GameFeelVisualPivot>() == null)
                {
                    var pivot = Undo.AddComponent<GameFeelVisualPivot>(go);
                    pivot.CaptureCurrentAsBasePose();
                }
            }
        }

        private static int FindRecipeIndex(GameFeelProfile profile, GameFeelEventId evt, GameFeelMaterialFamily family)
        {
            for (int i = 0; i < profile.Recipes.Count; i++)
            {
                if (profile.Recipes[i].EventId == evt && profile.Recipes[i].MaterialFamily == family)
                    return i;
            }
            return -1;
        }

        private static GameFeelMaterialFamily ResolveDefaultFamilyForEvent(GameFeelEventId evt)
        {
            switch (evt)
            {
                case GameFeelEventId.RollDoughStep:
                    return GameFeelMaterialFamily.Dough;
                case GameFeelEventId.GrateCheeseStep:
                case GameFeelEventId.SpreadSauce:
                case GameFeelEventId.SprinkleCheese:
                    return GameFeelMaterialFamily.FluidGranular;
                case GameFeelEventId.SliceTomatoPepperoniStep:
                case GameFeelEventId.SliceMushroomStep:
                case GameFeelEventId.PlaceToppings:
                    return GameFeelMaterialFamily.SmallBounceIngredient;
                case GameFeelEventId.OvenInserted:
                case GameFeelEventId.OvenReadyDing:
                case GameFeelEventId.OvenBurningWarning:
                case GameFeelEventId.OvenBurned:
                case GameFeelEventId.OvenRemoved:
                case GameFeelEventId.PhoneRingPulse:
                    return GameFeelMaterialFamily.RigidMetal;
                case GameFeelEventId.OrderPosted:
                    return GameFeelMaterialFamily.CardboardPaper;
                default:
                    return GameFeelMaterialFamily.Default;
            }
        }

        private static Vector3 ClampVec(Vector3 v, float min, float max) =>
            new Vector3(Mathf.Clamp(v.x, min, max), Mathf.Clamp(v.y, min, max), Mathf.Clamp(v.z, min, max));

        private static bool OutOfRange(Vector3 v, float min, float max) =>
            v.x < min || v.x > max || v.y < min || v.y > max || v.z < min || v.z > max;
    }
}
