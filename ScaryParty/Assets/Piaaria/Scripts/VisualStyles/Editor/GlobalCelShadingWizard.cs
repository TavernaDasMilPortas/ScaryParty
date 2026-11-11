#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScaryParty.City.Modular;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UIImage = UnityEngine.UIElements.Image;

namespace ScaryParty.VisualStyles.Editor
{
    public enum WizardStyleState
    {
        Unvalidated = 0,
        Valid = 1,
        Generated = 2,
        Applied = 3,
        Error = 4
    }

    public sealed class CapturedABSnapshot : IDisposable
    {
        public string title;
        public string timestamp;
        public Texture2D originalTex;
        public Texture2D toonTex;

        public void Dispose()
        {
            if (originalTex != null) UnityEngine.Object.DestroyImmediate(originalTex);
            if (toonTex != null) UnityEngine.Object.DestroyImmediate(toonTex);
            originalTex = null;
            toonTex = null;
        }
    }

    /// <summary>
    /// Universal Global Cel Shading & Visual Style Wizard EditorWindow (UI Toolkit).
    /// Zero canonical asset mutations and zero disk writes during live parameter previewing.
    /// </summary>
    public sealed class GlobalCelShadingWizard : EditorWindow
    {
        public const string MenuPath = "Tools/Scary Party/Visuals/Global Cel Shading Wizard";
        private const string UxmlPath = "Assets/Piaaria/Scripts/VisualStyles/Editor/UI/GlobalCelShadingWizard.uxml";
        private const string UssPath = "Assets/Piaaria/Scripts/VisualStyles/Editor/UI/GlobalCelShadingWizard.uss";
        private const string PerformanceBenchmarkJsonPath = "Assets/Piaaria/Art/VisualStyles/Reports/PlayMode_Benchmark_Latest.json";

        // Extension hooks populated by downstream Phase 5 / Phase 10 coordinators
        public static Func<VisualStyleProfile, VisualConversionRuleSet, bool> OnApplyToGameplayRequested;
        public static Func<VisualStyleProfile, bool> OnActivateInGameplayRequested;
        public static Func<VisualStyleProfile, bool> OnRestoreOriginalRequested;
        public static Func<VisualStyleProfile, VisualConversionRuleSet, string> OnBuildOrOpenShowcaseRequested;

        private readonly List<VisualStyleProfile> _availableProfiles = new List<VisualStyleProfile>();
        private VisualStyleProfile _sourceProfile;
        private VisualStyleProfile _workingProfile; // Transient in-memory clone for zero-write preview
        private VisualConversionRuleSet _ruleSet;
        private DiscoveryScanResult _latestScan;
        private VisualStyleAuditSummary _latestAudit;
        private VisualStyleValidationReport _latestValidation;

        private VisualStylePreviewRenderer _previewRenderer;
        private readonly List<CapturedABSnapshot> _abSnapshots = new List<CapturedABSnapshot>();
        private readonly List<string> _logEntries = new List<string>();
        private readonly Dictionary<string, UIImage> _slotOriginalImages = new Dictionary<string, UIImage>();
        private readonly Dictionary<string, UIImage> _slotToonImages = new Dictionary<string, UIImage>();
        private readonly Dictionary<string, VisualElement> _slotSplitClipBoxes = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, UIImage> _slotSplitOriginalImages = new Dictionary<string, UIImage>();
        private readonly Dictionary<string, Label> _slotSlotSummaryLabels = new Dictionary<string, Label>();
        private readonly Dictionary<string, VisualElement> _slotInspectorContainers = new Dictionary<string, VisualElement>();

        private WizardStyleState _statusState = WizardStyleState.Unvalidated;
        private string _activeTabId = "preview";
        private string _coverageFilter = "All";
        private string _discoverySearchFilter = "";
        private VisualAssetSyncStatus? _discoveryStatusFilter;
        private VisualDiscoveryScope _discoveryScope = VisualDiscoveryScope.GameplayReachable;
        private bool _hasUnsavedChanges;
        private double _previewRebuildDueAt = -1d;
        private double _previewRenderOnlyDueAt = -1d;
        private bool _uiBuilt;

        // Cached UI references
        private VisualElement _root;
        private Label _lblStatusBadge;
        private Label _lblDirtyBadge;
        private Label _lblProfileDesc;
        private Label _lblPendingBadge;
        private Label _lblAuditSummary;
        private ScrollView _profileListScroll;
        private ScrollView _settingsScroll;
        private ScrollView _discoveryListScroll;
        private ScrollView _coverageListScroll;
        private ScrollView _exceptionsListScroll;
        private ScrollView _beforeAfterScroll;
        private ScrollView _performanceScroll;
        private ScrollView _logScroll;
        private VisualElement _previewGridContainer;
        private VisualElement _discoveryControls;
        private VisualElement _coverageFilterBar;
        private VisualElement _beforeAfterToolbar;

        private readonly Dictionary<string, VisualElement> _tabViews = new Dictionary<string, VisualElement>();
        private readonly Dictionary<string, Button> _tabButtons = new Dictionary<string, Button>();

        [MenuItem(MenuPath, false, 10)]
        public static void ShowWindow()
        {
            var wnd = GetWindow<GlobalCelShadingWizard>();
            wnd.titleContent = new GUIContent("Global Cel Shading Wizard");
            wnd.minSize = new Vector2(1240, 760);
            wnd.Show();
        }

        public static GlobalCelShadingWizard OpenForTests()
        {
            var wnd = CreateInstance<GlobalCelShadingWizard>();
            wnd.titleContent = new GUIContent("Global Cel Shading Wizard (Test)");
            wnd.Show();
            return wnd;
        }

        public VisualStylePreviewRenderer PreviewRendererForTests => _previewRenderer;
        public VisualStyleProfile WorkingProfileForTests => _workingProfile;
        public bool HasUnsavedChanges => _hasUnsavedChanges;

        private void OnEnable()
        {
            VisualStylePresetSeeder.EnsureAllDefaultsExist();
            _previewRenderer = new VisualStylePreviewRenderer();
            LoadProfilesAndRuleSet();

            EditorApplication.update += OnEditorUpdate;
            VisualStyleImportWatcher.OnPendingQueueChanged += OnPendingImportsChanged;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            VisualStyleImportWatcher.OnPendingQueueChanged -= OnPendingImportsChanged;

            if (_previewRenderer != null)
            {
                _previewRenderer.Dispose();
                _previewRenderer = null;
            }

            for (int i = 0; i < _abSnapshots.Count; i++)
            {
                _abSnapshots[i]?.Dispose();
            }
            _abSnapshots.Clear();

            if (_workingProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(_workingProfile);
                _workingProfile = null;
            }

            _uiBuilt = false;
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.Clear();

            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml != null)
            {
                uxml.CloneTree(_root);
            }
            else
            {
                _root.Add(new Label($"UXML not found at {UxmlPath}"));
                return;
            }

            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (uss != null)
            {
                _root.styleSheets.Add(uss);
            }

            QueryElements();
            BindStaticHeaderAndFooterEvents();
            EnsurePreviewInitialized();

            RefreshProfileListUI();
            RebuildSettingsInspector();
            RebuildPreviewCardsUI();
            RefreshPendingBadge();
            UpdateDirtyAndStatusBadges();
            RefreshCurrentTabContent();

            _uiBuilt = true;
            SchedulePreviewRebuild(immediate: true);
        }

        private void QueryElements()
        {
            _lblStatusBadge = _root.Q<Label>("lbl-status-badge");
            _lblDirtyBadge = _root.Q<Label>("lbl-dirty-badge");
            _lblProfileDesc = _root.Q<Label>("lbl-profile-desc");
            _lblPendingBadge = _root.Q<Label>("lbl-pending-badge");
            _lblAuditSummary = _root.Q<Label>("lbl-audit-summary");

            _profileListScroll = _root.Q<ScrollView>("profile-list-scroll");
            _settingsScroll = _root.Q<ScrollView>("settings-scroll");
            _discoveryListScroll = _root.Q<ScrollView>("discovery-list-scroll");
            _coverageListScroll = _root.Q<ScrollView>("coverage-list-scroll");
            _exceptionsListScroll = _root.Q<ScrollView>("exceptions-list-scroll");
            _beforeAfterScroll = _root.Q<ScrollView>("beforeafter-scroll");
            _performanceScroll = _root.Q<ScrollView>("performance-scroll");
            _logScroll = _root.Q<ScrollView>("log-scroll");

            _previewGridContainer = _root.Q<VisualElement>("preview-grid-container");
            _discoveryControls = _root.Q<VisualElement>("discovery-controls");
            _coverageFilterBar = _root.Q<VisualElement>("coverage-filter-bar");
            _beforeAfterToolbar = _root.Q<VisualElement>("beforeafter-toolbar");

            BindTabMapping("preview", "tab-preview", "view-preview");
            BindTabMapping("discovery", "tab-discovery", "view-discovery");
            BindTabMapping("coverage", "tab-coverage", "view-coverage");
            BindTabMapping("exceptions", "tab-exceptions", "view-exceptions");
            BindTabMapping("beforeafter", "tab-beforeafter", "view-beforeafter");
            BindTabMapping("performance", "tab-performance", "view-performance");
            BindTabMapping("log", "tab-log", "view-log");
        }

        private void BindTabMapping(string id, string btnName, string viewName)
        {
            var btn = _root.Q<Button>(btnName);
            var view = _root.Q<VisualElement>(viewName);
            if (btn != null)
            {
                _tabButtons[id] = btn;
                btn.clicked += () => SwitchTab(id);
            }
            if (view != null)
            {
                _tabViews[id] = view;
            }
        }

        private void LoadProfilesAndRuleSet()
        {
            _availableProfiles.Clear();
            string[] guids = AssetDatabase.FindAssets("t:VisualStyleProfile", new[] { VisualStylePresetSeeder.ProfilesFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var prof = AssetDatabase.LoadAssetAtPath<VisualStyleProfile>(path);
                if (prof != null) _availableProfiles.Add(prof);
            }
            _availableProfiles.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));

            _ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var runtimeCfg = VisualStyleRuntimeConfig.LoadCached();
            VisualStyleProfile initial = null;
            if (runtimeCfg != null && runtimeCfg.activeProfile != null && _availableProfiles.Contains(runtimeCfg.activeProfile))
            {
                initial = runtimeCfg.activeProfile;
            }
            else if (_availableProfiles.Count > 0)
            {
                initial = _availableProfiles.FirstOrDefault(p => p.presetType == VisualStylePresetType.SoftToonVibrant)
                          ?? _availableProfiles[0];
            }

            SelectSourceProfile(initial, rebuildUi: false);
        }

        public void SelectSourceProfile(VisualStyleProfile profile, bool rebuildUi = true)
        {
            if (profile == null) return;
            _sourceProfile = profile;
            if (profile.discovery.conversionRuleSet != null)
            {
                _ruleSet = profile.discovery.conversionRuleSet;
            }

            if (_workingProfile != null)
            {
                UnityEngine.Object.DestroyImmediate(_workingProfile);
            }
            _workingProfile = profile.CloneTransient();
            _hasUnsavedChanges = false;
            _discoveryScope = _workingProfile.discovery.defaultScope;

            UpdateStatusFromDiskState();
            AppendLog($"Perfil carregado (preview sem escrita em disco): {_workingProfile.displayName} [{_workingProfile.styleId}]");

            if (rebuildUi && _uiBuilt)
            {
                RefreshProfileListUI();
                RebuildSettingsInspector();
                UpdateDirtyAndStatusBadges();
                SchedulePreviewRebuild(immediate: true);
                RefreshCurrentTabContent();
            }
        }

        private void EnsurePreviewInitialized()
        {
            if (_previewRenderer == null) _previewRenderer = new VisualStylePreviewRenderer();
            if (_previewRenderer.Slots.Count == 0)
            {
                _previewRenderer.InitializeDefaultSlots(_workingProfile, _ruleSet);
            }
        }

        private void BindStaticHeaderAndFooterEvents()
        {
            // Comparison mode segmented buttons
            BindButton("btn-cmp-original", () => SetComparisonMode(VisualPreviewComparisonMode.Original));
            BindButton("btn-cmp-side", () => SetComparisonMode(VisualPreviewComparisonMode.SideBySideAB));
            BindButton("btn-cmp-split", () => SetComparisonMode(VisualPreviewComparisonMode.SplitAB));
            BindButton("btn-cmp-toon", () => SetComparisonMode(VisualPreviewComparisonMode.Toon));

            // Lighting presets
            BindButton("btn-light-interior", () => SetLightingMode(VisualLightingPreviewMode.Interior));
            BindButton("btn-light-exterior", () => SetLightingMode(VisualLightingPreviewMode.Exterior));
            BindButton("btn-light-neutral", () => SetLightingMode(VisualLightingPreviewMode.Neutral));

            // Profile management
            BindButton("btn-profile-new", CreateNewProfile);
            BindButton("btn-profile-dup", DuplicateCurrentProfile);
            BindButton("btn-profile-save", SaveWorkingProfileToDisk);
            BindButton("btn-profile-revert", RevertWorkingProfile);
            BindButton("btn-open-ruleset", () =>
            {
                if (_ruleSet != null)
                {
                    Selection.activeObject = _ruleSet;
                    EditorGUIUtility.PingObject(_ruleSet);
                }
            });

            // Viewport global toolbar
            BindButton("btn-cam-persp", () => SetGlobalCameraMode(VisualPreviewCameraMode.Perspective));
            BindButton("btn-cam-iso", () => SetGlobalCameraMode(VisualPreviewCameraMode.Isometric));
            BindButton("btn-cam-ortho", () => SetGlobalCameraMode(VisualPreviewCameraMode.Orthographic));
            BindButton("btn-cam-reset-all", () =>
            {
                _previewRenderer?.ResetAllCameras();
                SchedulePreviewRenderOnly(immediate: true);
            });

            var gridToggle = _root.Q<Toggle>("tgl-preview-grid");
            if (gridToggle != null)
            {
                gridToggle.value = _previewRenderer != null && _previewRenderer.ShowGrid;
                gridToggle.RegisterValueChangedCallback(evt =>
                {
                    if (_previewRenderer != null) _previewRenderer.ShowGrid = evt.newValue;
                    SchedulePreviewRenderOnly(immediate: true);
                });
            }

            var lightsToggle = _root.Q<Toggle>("tgl-preview-lights");
            if (lightsToggle != null)
            {
                lightsToggle.value = _previewRenderer != null && _previewRenderer.ShowLightHandles;
                lightsToggle.RegisterValueChangedCallback(evt =>
                {
                    if (_previewRenderer != null) _previewRenderer.ShowLightHandles = evt.newValue;
                    SchedulePreviewRenderOnly(immediate: true);
                });
            }

            BindResolutionDropdown();

            BindButton("btn-add-sample", () =>
            {
                var slot = _previewRenderer.AddCustomSampleSlot("Nova Amostra", null, _workingProfile, _ruleSet);
                RebuildPreviewCardsUI();
                _previewRenderer.RenderSlot(slot, _workingProfile);
                RefreshPreviewCardImageBindings(slot);
                AppendLog($"Amostra custom adicionada ao Preview Lab: {slot.title}");
            });

            // Discovery controls & Log clear
            BuildDiscoveryToolbar();
            BuildBeforeAfterToolbar();
            BindButton("btn-clear-log", () =>
            {
                _logEntries.Clear();
                RefreshLogTab();
            });

            // Footer actions
            BindButton("btn-audit", RunAuditAction);
            BindButton("btn-refresh-preview", () => SchedulePreviewRebuild(immediate: true));
            BindButton("btn-validate", ValidateAction);
            BindButton("btn-sync-new", SyncNewAssetsAction);
            BindButton("btn-generate", GenerateMaterialsAction);
            BindButton("btn-apply", ApplyToGameplayAction);
            BindButton("btn-generate-apply-all", GenerateAndApplyAllAction);
            BindButton("btn-open-showcase", OpenShowcaseAction);
            BindButton("btn-activate-gameplay", ActivateGameplayAction);
            BindButton("btn-restore-original", RestoreOriginalAction);
            BindButton("btn-export-report", ExportReportAction);
        }

        private void BindResolutionDropdown()
        {
            var resDropdown = _root.Q<DropdownField>("pop-preview-res");
            if (resDropdown != null)
            {
                resDropdown.choices = new List<string> { "256", "384", "512" };
                resDropdown.value = (_previewRenderer?.Resolution ?? 256).ToString();
                resDropdown.RegisterValueChangedCallback(evt =>
                {
                    if (int.TryParse(evt.newValue, out int res) && _previewRenderer != null)
                    {
                        _previewRenderer.Resolution = res;
                        SchedulePreviewRenderOnly(immediate: true);
                    }
                });
                return;
            }

            var host = _root.Q<VisualElement>("pop-preview-res");
            if (host != null)
            {
                host.Clear();
                var dd = new DropdownField(new List<string> { "256", "384", "512" }, 0);
                dd.RegisterValueChangedCallback(evt =>
                {
                    if (int.TryParse(evt.newValue, out int res) && _previewRenderer != null)
                    {
                        _previewRenderer.Resolution = res;
                        SchedulePreviewRenderOnly(immediate: true);
                    }
                });
                host.Add(dd);
            }
        }

        private void BindButton(string name, Action handler)
        {
            var btn = _root.Q<Button>(name);
            if (btn != null) btn.clicked += handler;
        }

        private void SetComparisonMode(VisualPreviewComparisonMode mode)
        {
            if (_previewRenderer == null) return;
            _previewRenderer.ComparisonMode = mode;
            SetGroupActiveClass("seg-btn-active", mode switch
            {
                VisualPreviewComparisonMode.Original => "btn-cmp-original",
                VisualPreviewComparisonMode.SideBySideAB => "btn-cmp-side",
                VisualPreviewComparisonMode.SplitAB => "btn-cmp-split",
                _ => "btn-cmp-toon"
            }, "btn-cmp-original", "btn-cmp-side", "btn-cmp-split", "btn-cmp-toon");

            RebuildPreviewCardsUI();
            SchedulePreviewRenderOnly(immediate: true);
        }

        private void SetLightingMode(VisualLightingPreviewMode mode)
        {
            if (_previewRenderer == null) return;
            _previewRenderer.LightingMode = mode;
            SetGroupActiveClass("seg-btn-active", mode switch
            {
                VisualLightingPreviewMode.Exterior => "btn-light-exterior",
                VisualLightingPreviewMode.Neutral => "btn-light-neutral",
                _ => "btn-light-interior"
            }, "btn-light-interior", "btn-light-exterior", "btn-light-neutral");

            SchedulePreviewRenderOnly(immediate: true);
        }

        private void SetGlobalCameraMode(VisualPreviewCameraMode mode)
        {
            if (_previewRenderer == null) return;
            _previewRenderer.GlobalCameraMode = mode;
            SetGroupActiveClass("mini-btn-active", mode switch
            {
                VisualPreviewCameraMode.Isometric => "btn-cam-iso",
                VisualPreviewCameraMode.Orthographic => "btn-cam-ortho",
                _ => "btn-cam-persp"
            }, "btn-cam-persp", "btn-cam-iso", "btn-cam-ortho");

            SchedulePreviewRenderOnly(immediate: true);
        }

        private void SetGroupActiveClass(string activeClass, string activeBtnName, params string[] groupNames)
        {
            for (int i = 0; i < groupNames.Length; i++)
            {
                var btn = _root.Q<Button>(groupNames[i]);
                if (btn == null) continue;
                btn.EnableInClassList(activeClass, groupNames[i] == activeBtnName);
            }
        }

        private void SwitchTab(string tabId)
        {
            _activeTabId = tabId;
            foreach (var kvp in _tabButtons)
            {
                kvp.Value.EnableInClassList("tab-btn-active", kvp.Key == tabId);
            }
            foreach (var kvp in _tabViews)
            {
                kvp.Value.EnableInClassList("hidden", kvp.Key != tabId);
            }
            RefreshCurrentTabContent();
        }

        private void RefreshCurrentTabContent()
        {
            switch (_activeTabId)
            {
                case "discovery":
                    RefreshDiscoveryTab();
                    break;
                case "coverage":
                    RefreshCoverageTab();
                    break;
                case "exceptions":
                    RefreshExceptionsTab();
                    break;
                case "beforeafter":
                    RefreshBeforeAfterTab();
                    break;
                case "performance":
                    RefreshPerformanceTab();
                    break;
                case "log":
                    RefreshLogTab();
                    break;
            }
        }

        #region Left Sidebar (Profiles)
        private void RefreshProfileListUI()
        {
            if (_profileListScroll == null) return;
            _profileListScroll.Clear();

            for (int i = 0; i < _availableProfiles.Count; i++)
            {
                VisualStyleProfile p = _availableProfiles[i];
                if (p == null) continue;
                bool selected = ReferenceEquals(p, _sourceProfile);
                var btn = new Button(() =>
                {
                    if (_hasUnsavedChanges &&
                        !EditorUtility.DisplayDialog("Descartar alterações no Preview?",
                            $"Existem alterações não salvas em '{_workingProfile.displayName}'. Deseja descartar e abrir '{p.displayName}'?",
                            "Descartar e Abrir", "Cancelar"))
                    {
                        return;
                    }
                    SelectSourceProfile(p, rebuildUi: true);
                })
                {
                    text = $"{p.displayName}  [{p.presetType}]"
                };
                btn.AddToClassList("profile-item-btn");
                btn.EnableInClassList("profile-item-selected", selected);
                _profileListScroll.Add(btn);
            }

            if (_lblProfileDesc != null && _workingProfile != null)
            {
                _lblProfileDesc.text = $"{_workingProfile.description}\nStyleId: {_workingProfile.styleId}  |  v{_workingProfile.profileVersion}";
            }
        }

        private void CreateNewProfile()
        {
            VisualStylePresetSeeder.EnsureFolder(VisualStylePresetSeeder.ProfilesFolder);
            string uniqueName = $"custom_style_{DateTime.Now:yyyyMMdd_HHmmss}";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{VisualStylePresetSeeder.ProfilesFolder}/VS_{uniqueName}.asset");
            var created = ScriptableObject.CreateInstance<VisualStyleProfile>();
            created.ApplyPresetDefaults(VisualStylePresetType.SoftToonVibrant);
            created.presetType = VisualStylePresetType.Custom;
            created.styleId = uniqueName;
            created.displayName = "Custom Toon Style";
            created.description = "Perfil custom criado no Global Cel Shading Wizard.";
            created.discovery.conversionRuleSet = _ruleSet;

            AssetDatabase.CreateAsset(created, path);
            AssetDatabase.SaveAssets();
            LoadProfilesAndRuleSet();
            SelectSourceProfile(created);
        }

        private void DuplicateCurrentProfile()
        {
            if (_workingProfile == null) return;
            VisualStylePresetSeeder.EnsureFolder(VisualStylePresetSeeder.ProfilesFolder);
            string baseId = $"{_workingProfile.styleId}_copy";
            string path = AssetDatabase.GenerateUniqueAssetPath($"{VisualStylePresetSeeder.ProfilesFolder}/VS_{baseId}.asset");

            var dup = ScriptableObject.CreateInstance<VisualStyleProfile>();
            dup.CopyValuesFrom(_workingProfile);
            dup.presetType = VisualStylePresetType.Custom;
            dup.styleId = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            dup.displayName = $"{_workingProfile.displayName} (Copy)";
            dup.profileVersion = _workingProfile.profileVersion + 1;
            dup.discovery.conversionRuleSet = _ruleSet;

            AssetDatabase.CreateAsset(dup, path);
            AssetDatabase.SaveAssets();
            LoadProfilesAndRuleSet();
            SelectSourceProfile(dup);
        }

        private void SaveWorkingProfileToDisk()
        {
            if (_sourceProfile == null || _workingProfile == null) return;
            Undo.RecordObject(_sourceProfile, "Save VisualStyleProfile from Wizard");
            _sourceProfile.CopyValuesFrom(_workingProfile);
            _sourceProfile.profileVersion = Mathf.Max(1, _sourceProfile.profileVersion + 1);
            _workingProfile.profileVersion = _sourceProfile.profileVersion;

            EditorUtility.SetDirty(_sourceProfile);
            AssetDatabase.SaveAssets();
            _hasUnsavedChanges = false;

            if (_lblProfileDesc != null)
            {
                _lblProfileDesc.text = $"{_workingProfile.description}\nStyleId: {_workingProfile.styleId}  |  v{_workingProfile.profileVersion}";
            }
            UpdateStatusFromDiskState();
            UpdateDirtyAndStatusBadges();
            AppendLog($"Perfil '{_sourceProfile.displayName}' salvo em disco (v{_sourceProfile.profileVersion}).");
        }

        private void RevertWorkingProfile()
        {
            if (_sourceProfile == null) return;
            SelectSourceProfile(_sourceProfile, rebuildUi: true);
            AppendLog("Alterações temporárias revertidas ao perfil salvo em disco.");
        }
        #endregion

        #region Preview Lab Cards & Interaction
        private void RebuildPreviewCardsUI()
        {
            if (_previewGridContainer == null || _previewRenderer == null) return;
            _previewGridContainer.Clear();
            _slotOriginalImages.Clear();
            _slotToonImages.Clear();
            _slotSplitClipBoxes.Clear();
            _slotSplitOriginalImages.Clear();
            _slotSlotSummaryLabels.Clear();
            _slotInspectorContainers.Clear();

            var slots = _previewRenderer.Slots;
            var cmp = _previewRenderer.ComparisonMode;

            for (int i = 0; i < slots.Count; i++)
            {
                VisualPreviewSlotState slot = slots[i];
                var card = new VisualElement();
                card.AddToClassList("preview-card");
                card.EnableInClassList("preview-card-expanded", slot.isExpanded);

                // Header
                var header = new VisualElement();
                header.AddToClassList("preview-card-header");
                var titleLbl = new Label(slot.title);
                titleLbl.AddToClassList("preview-card-title");
                header.Add(titleLbl);

                var headerActions = new VisualElement { style = { flexDirection = FlexDirection.Row } };

                var expandBtn = new Button(() =>
                {
                    slot.isExpanded = !slot.isExpanded;
                    RebuildPreviewCardsUI();
                    _previewRenderer.RenderSlot(slot, _workingProfile);
                    RefreshPreviewCardImageBindings(slot);
                })
                {
                    text = slot.isExpanded ? "1x" : "2x"
                };
                expandBtn.AddToClassList("mini-btn");
                headerActions.Add(expandBtn);

                var resetBtn = new Button(() =>
                {
                    slot.ResetCamera();
                    _previewRenderer.RenderSlot(slot, _workingProfile);
                    RefreshPreviewCardImageBindings(slot);
                })
                {
                    text = "Reset"
                };
                resetBtn.AddToClassList("mini-btn");
                headerActions.Add(resetBtn);

                if (!slot.isDefaultSlot)
                {
                    var remBtn = new Button(() =>
                    {
                        _previewRenderer.RemoveSlot(slot.slotId);
                        RebuildPreviewCardsUI();
                    })
                    {
                        text = "X"
                    };
                    remBtn.AddToClassList("mini-btn");
                    headerActions.Add(remBtn);
                }

                header.Add(headerActions);
                card.Add(header);

                // Viewport Host
                var host = new VisualElement();
                host.AddToClassList("preview-image-host");
                host.EnableInClassList("preview-image-host-expanded", slot.isExpanded);
                host.style.position = Position.Relative;
                host.style.flexDirection = FlexDirection.Row;

                if (cmp == VisualPreviewComparisonMode.SideBySideAB)
                {
                    var origImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    origImg.style.width = Length.Percent(50f);
                    origImg.style.height = Length.Percent(100f);
                    var toonImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    toonImg.style.width = Length.Percent(50f);
                    toonImg.style.height = Length.Percent(100f);
                    host.Add(origImg);
                    host.Add(toonImg);
                    _slotOriginalImages[slot.slotId] = origImg;
                    _slotToonImages[slot.slotId] = toonImg;
                }
                else if (cmp == VisualPreviewComparisonMode.SplitAB)
                {
                    var toonImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    toonImg.style.width = Length.Percent(100f);
                    toonImg.style.height = Length.Percent(100f);
                    host.Add(toonImg);

                    var clipBox = new VisualElement();
                    clipBox.style.position = Position.Absolute;
                    clipBox.style.left = 0;
                    clipBox.style.top = 0;
                    clipBox.style.bottom = 0;
                    clipBox.style.width = Length.Percent(slot.splitRatio * 100f);
                    clipBox.style.overflow = Overflow.Hidden;
                    clipBox.style.borderRightWidth = 2;
                    clipBox.style.borderRightColor = new Color(0.95f, 0.96f, 1f, 0.9f);

                    var origSplitImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    origSplitImg.style.height = Length.Percent(100f);
                    origSplitImg.style.width = 256;
                    clipBox.Add(origSplitImg);
                    host.Add(clipBox);

                    host.RegisterCallback<GeometryChangedEvent>(evt =>
                    {
                        if (evt.newRect.width > 10f)
                        {
                            origSplitImg.style.width = evt.newRect.width;
                        }
                    });

                    _slotToonImages[slot.slotId] = toonImg;
                    _slotSplitClipBoxes[slot.slotId] = clipBox;
                    _slotSplitOriginalImages[slot.slotId] = origSplitImg;
                }
                else if (cmp == VisualPreviewComparisonMode.Original)
                {
                    var origImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    origImg.style.width = Length.Percent(100f);
                    origImg.style.height = Length.Percent(100f);
                    host.Add(origImg);
                    _slotOriginalImages[slot.slotId] = origImg;
                }
                else
                {
                    var toonImg = new UIImage { scaleMode = ScaleMode.ScaleToFit };
                    toonImg.style.width = Length.Percent(100f);
                    toonImg.style.height = Length.Percent(100f);
                    host.Add(toonImg);
                    _slotToonImages[slot.slotId] = toonImg;
                }

                AttachViewportOrbitControls(host, slot);
                card.Add(host);

                if (cmp == VisualPreviewComparisonMode.SplitAB)
                {
                    var splitSlider = new Slider("Split A/B", 0.05f, 0.95f) { value = slot.splitRatio };
                    splitSlider.RegisterValueChangedCallback(evt =>
                    {
                        slot.splitRatio = evt.newValue;
                        if (_slotSplitClipBoxes.TryGetValue(slot.slotId, out var box))
                        {
                            box.style.width = Length.Percent(slot.splitRatio * 100f);
                        }
                    });
                    card.Add(splitSlider);
                }

                // Controls strip: Drag & Drop ObjectField + Sample Specific Controls
                var controls = new VisualElement();
                controls.AddToClassList("preview-card-controls");

                var assetField = new ObjectField("Asset / Modelo")
                {
                    objectType = typeof(UnityEngine.Object),
                    allowSceneObjects = false,
                    value = slot.customAsset
                };
                assetField.RegisterValueChangedCallback(evt =>
                {
                    _previewRenderer.SetSlotCustomAsset(slot, evt.newValue, _workingProfile, _ruleSet);
                    _previewRenderer.RenderSlot(slot, _workingProfile);
                    RefreshPreviewCardImageBindings(slot);
                    RebuildSlotInspector(slot);
                });
                controls.Add(assetField);

                BuildSampleSpecificControls(slot, controls);

                // Slot Inspector foldout
                var slotSummary = new Label($"{slot.targets.Count} renderer slots mapeados");
                slotSummary.AddToClassList("muted-label");
                _slotSlotSummaryLabels[slot.slotId] = slotSummary;
                controls.Add(slotSummary);

                var inspectorFoldout = new Foldout { text = "Inspecionar Renderers / Slots", value = false };
                _slotInspectorContainers[slot.slotId] = inspectorFoldout;
                RebuildSlotInspector(slot);
                controls.Add(inspectorFoldout);

                card.Add(controls);
                _previewGridContainer.Add(card);
                RefreshPreviewCardImageBindings(slot);
            }
        }

        private void BuildSampleSpecificControls(VisualPreviewSlotState slot, VisualElement controls)
        {
            if (slot.sampleType == VisualPreviewSampleType.Character)
            {
                var row = new VisualElement();
                row.AddToClassList("preview-card-row");

                var poseChoices = new List<string>(VisualStylePreviewRenderer.CharacterPoseNames);
                var poseDd = new DropdownField("Pose", poseChoices, Mathf.Clamp(slot.selectedPoseIndex, 0, poseChoices.Count - 1));
                poseDd.style.flexGrow = 1;
                poseDd.RegisterValueChangedCallback(evt =>
                {
                    slot.selectedPoseIndex = Mathf.Max(0, poseChoices.IndexOf(evt.newValue));
                    _previewRenderer.ApplyCharacterPoseSample(slot.subjectGo, slot.selectedPoseIndex);
                    SchedulePreviewRenderOnly(immediate: true);
                });
                row.Add(poseDd);

                var swatchBtn = new Button(() =>
                {
                    slot.swatchIndex = (slot.swatchIndex + 1) % VisualStylePreviewRenderer.CharacterSwatches.Length;
                    SchedulePreviewRenderOnly(immediate: true);
                })
                {
                    text = $"Cor Corpo #{slot.swatchIndex + 1}"
                };
                swatchBtn.AddToClassList("mini-btn");
                row.Add(swatchBtn);

                controls.Add(row);
            }
            else if (slot.sampleType == VisualPreviewSampleType.ModularBuilding)
            {
                var row = new VisualElement();
                row.AddToClassList("preview-card-row");

                var recipeField = new EnumField("Receita", slot.buildingRecipe);
                recipeField.style.flexGrow = 1;
                recipeField.RegisterValueChangedCallback(evt =>
                {
                    slot.buildingRecipe = (ModularBuildingRecipe)evt.newValue;
                    slot.selectedModuleId = "";
                    _previewRenderer.RebuildSlotSubject(slot, _ruleSet);
                    _previewRenderer.RebuildSlotTransientMaterials(slot, _workingProfile, _ruleSet);
                    _previewRenderer.RenderSlot(slot, _workingProfile);
                    RefreshPreviewCardImageBindings(slot);
                    RebuildSlotInspector(slot);
                });
                row.Add(recipeField);

                var pieceChoices = BuildModularPieceChoices();
                var pieceDd = new DropdownField("Peça", pieceChoices, string.IsNullOrEmpty(slot.selectedModuleId) ? 0 : Math.Max(0, pieceChoices.IndexOf(slot.selectedModuleId)));
                pieceDd.RegisterValueChangedCallback(evt =>
                {
                    slot.selectedModuleId = evt.newValue == "Receita" ? "" : evt.newValue;
                    _previewRenderer.RebuildSlotSubject(slot, _ruleSet);
                    _previewRenderer.RebuildSlotTransientMaterials(slot, _workingProfile, _ruleSet);
                    _previewRenderer.RenderSlot(slot, _workingProfile);
                    RefreshPreviewCardImageBindings(slot);
                    RebuildSlotInspector(slot);
                });
                row.Add(pieceDd);

                var wallColorBtn = new Button(() =>
                {
                    slot.swatchIndex = (slot.swatchIndex + 1) % VisualStylePreviewRenderer.BuildingSwatches.Length;
                    SchedulePreviewRenderOnly(immediate: true);
                })
                {
                    text = $"Cor Parede #{slot.swatchIndex + 1}"
                };
                wallColorBtn.AddToClassList("mini-btn");
                row.Add(wallColorBtn);

                controls.Add(row);
            }
        }

        private static List<string> BuildModularPieceChoices()
        {
            var list = new List<string> { "Receita" };
            var catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(VisualStyleAuditService.CityModuleCatalogPath);
            if (catalog != null && catalog.definitions != null)
            {
                for (int i = 0; i < catalog.definitions.Count; i++)
                {
                    if (catalog.definitions[i] != null)
                    {
                        string token = catalog.definitions[i].id.ToString();
                        if (!list.Contains(token)) list.Add(token);
                    }
                }
            }
            return list;
        }

        private void RebuildSlotInspector(VisualPreviewSlotState slot)
        {
            if (_slotSlotSummaryLabels.TryGetValue(slot.slotId, out var lbl))
            {
                lbl.text = $"{slot.targets.Count} renderer slots | {(slot.cameraMode)}";
            }

            if (!_slotInspectorContainers.TryGetValue(slot.slotId, out var container)) return;
            container.Clear();

            int shown = Mathf.Min(24, slot.targets.Count);
            for (int i = 0; i < shown; i++)
            {
                var t = slot.targets[i];
                var row = new VisualElement();
                row.AddToClassList("table-row");
                string matName = t.originalMaterial != null ? t.originalMaterial.name : "<None>";
                string info = $"{t.renderer?.name}[{t.slotIndex}] {matName} -> {t.decision.categoryId}/{t.decision.shaderFamily}";
                var rLbl = new Label(info) { style = { flexGrow = 1, fontSize = 10 } };
                row.Add(rLbl);

                if (t.originalMaterial != null)
                {
                    var toggleActionBtn = new Button(() =>
                    {
                        AddQuickSlotPreserveToggle(t);
                    })
                    {
                        text = t.decision.action == VisualConversionAction.Preserve ? "Convert" : "Preserve"
                    };
                    toggleActionBtn.AddToClassList("mini-btn");
                    row.Add(toggleActionBtn);
                }

                container.Add(row);
            }

            if (slot.targets.Count > shown)
            {
                container.Add(new Label($"... +{slot.targets.Count - shown} slots adicionais (veja aba Cobertura)") { style = { fontSize = 10 } });
            }
        }

        private void AddQuickSlotPreserveToggle(PreviewRendererSlotTarget target)
        {
            if (_ruleSet == null) return;
            Undo.RecordObject(_ruleSet, "Toggle Slot Override in RuleSet");

            VisualConversionAction nextAction = target.decision.action == VisualConversionAction.Preserve
                ? VisualConversionAction.Convert
                : VisualConversionAction.Preserve;

            var existing = _ruleSet.manualOverrides.FirstOrDefault(o =>
                o != null &&
                o.sourceAssetGuid == target.descriptor.sourceMaterialGuid &&
                o.sourceLocalFileId == target.descriptor.sourceLocalFileId &&
                o.slotIndex == target.slotIndex);

            if (existing == null)
            {
                existing = new VisualSlotOverride
                {
                    sourceAssetGuid = target.descriptor.sourceMaterialGuid,
                    sourceLocalFileId = target.descriptor.sourceLocalFileId,
                    slotIndex = target.slotIndex,
                    rendererRelativePath = target.rendererPath,
                    displayLabel = $"{target.descriptor.materialName} [{target.slotIndex}]",
                    overrideCategoryId = target.decision.categoryId,
                    overrideRole = target.decision.semanticRole,
                    overrideShaderFamily = target.decision.shaderFamily,
                    overrideAction = nextAction,
                    preserveTexture = true,
                    preserveColor = true,
                    allowDynamicTint = target.supportsDynamicTint,
                    notes = "Toggled from Preview Slot Inspector"
                };
                _ruleSet.manualOverrides.Add(existing);
            }
            else
            {
                existing.overrideAction = nextAction;
            }

            EditorUtility.SetDirty(_ruleSet);
            AssetDatabase.SaveAssets();
            SchedulePreviewRebuild(immediate: true);
            RefreshCurrentTabContent();
        }

        private void AttachViewportOrbitControls(VisualElement host, VisualPreviewSlotState slot)
        {
            bool dragging = false;
            bool panning = false;
            Vector2 lastPos = Vector2.zero;

            host.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 0 || evt.button == 2)
                {
                    dragging = true;
                    panning = evt.button == 2 || evt.shiftKey;
                    lastPos = evt.localPosition;
                    host.CapturePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            host.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (!dragging || !host.HasPointerCapture(evt.pointerId)) return;
                Vector2 delta = (Vector2)evt.localPosition - lastPos;
                lastPos = evt.localPosition;

                if (panning)
                {
                    float panScale = slot.distance * 0.0025f;
                    Quaternion camRot = Quaternion.Euler(slot.pitch, slot.yaw, 0f);
                    slot.panOffset -= (camRot * Vector3.right) * (delta.x * panScale);
                    slot.panOffset += (camRot * Vector3.up) * (delta.y * panScale);
                }
                else
                {
                    slot.yaw += delta.x * 0.55f;
                    slot.pitch = Mathf.Clamp(slot.pitch + delta.y * 0.45f, -40f, 85f);
                }

                _previewRenderer.RenderSlot(slot, _workingProfile);
                RefreshPreviewCardImageBindings(slot);
                evt.StopPropagation();
            });

            host.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (dragging && host.HasPointerCapture(evt.pointerId))
                {
                    dragging = false;
                    host.ReleasePointer(evt.pointerId);
                    evt.StopPropagation();
                }
            });

            host.RegisterCallback<WheelEvent>(evt =>
            {
                slot.distance = Mathf.Clamp(slot.distance * (1f + evt.delta.y * 0.04f), 0.5f, 55f);
                _previewRenderer.RenderSlot(slot, _workingProfile);
                RefreshPreviewCardImageBindings(slot);
                evt.StopPropagation();
            });
        }

        private void RefreshPreviewCardImageBindings(VisualPreviewSlotState slot)
        {
            if (slot == null) return;
            if (_slotToonImages.TryGetValue(slot.slotId, out var toonImg))
            {
                toonImg.image = slot.toonTexture;
                toonImg.MarkDirtyRepaint();
            }
            if (_slotOriginalImages.TryGetValue(slot.slotId, out var origImg))
            {
                origImg.image = slot.originalTexture;
                origImg.MarkDirtyRepaint();
            }
            if (_slotSplitOriginalImages.TryGetValue(slot.slotId, out var splitOrigImg))
            {
                splitOrigImg.image = slot.originalTexture;
                splitOrigImg.MarkDirtyRepaint();
            }
        }
        #endregion

        #region Right Inspector (Settings & Categories)
        private void RebuildSettingsInspector()
        {
            if (_settingsScroll == null || _workingProfile == null) return;
            _settingsScroll.Clear();

            BuildLightingSettingsSection();
            BuildSpecularRimSettingsSection();
            BuildPaletteSettingsSection();
            BuildOutlineSettingsSection();
            BuildCharacterAndMaterialSettingsSection();
            BuildSceneLightingSettingsSection();
            BuildCategoryOverridesSection();
            BuildDiscoverySettingsSection();
        }

        private void BuildLightingSettingsSection()
        {
            var fold = AddFoldoutSection("Iluminação Toon", true);
            var l = _workingProfile.lighting;
            var modeChoices = new List<string> { "Unlit (0)", "Lit (1)" };
            var modeDd = new DropdownField("Lighting Mode", modeChoices, Mathf.Clamp(l.lightingMode, 0, 1));
            modeDd.RegisterValueChangedCallback(evt =>
            {
                l.lightingMode = modeChoices.IndexOf(evt.newValue);
                MarkWorkingProfileEdited();
            });
            fold.Add(modeDd);

            AddToggle(fold, "Posterize Light", l.posterizeLight, v => l.posterizeLight = v);
            AddIntSlider(fold, "Light Steps", l.lightSteps, 1, 6, v => l.lightSteps = v);
            AddSlider(fold, "Light Ramp Softness", l.lightRamp, 0f, 2f, v => l.lightRamp = v);
            AddSlider(fold, "Light Ramp Offset", l.lightRampOffset, 0f, 1f, v => l.lightRampOffset = v);
            AddColorField(fold, "Light Color Tint", l.lightColorTint, v => l.lightColorTint = v);
            AddColorField(fold, "Shadow Tint", l.shadowTint, v => l.shadowTint = v);
            AddToggle(fold, "Multiply By Light Color", l.multiplyByLightColor, v => l.multiplyByLightColor = v);
            AddToggle(fold, "Use Shadows", l.useShadows, v => l.useShadows = v);
            AddToggle(fold, "Receive Shadows", l.receiveShadows, v => l.receiveShadows = v);
            AddSlider(fold, "Main Light Influence", l.mainLightInfluence, 0f, 1f, v => l.mainLightInfluence = v);
        }

        private void BuildSpecularRimSettingsSection()
        {
            var fold = AddFoldoutSection("Specular & Rim Light", false);
            var sr = _workingProfile.specularRim;
            AddToggle(fold, "Use Specular", sr.useSpecular, v => sr.useSpecular = v);
            AddColorField(fold, "Specular Color", sr.specularColor, v => sr.specularColor = v);
            AddSlider(fold, "Specular Ramp", sr.specularRamp, 0f, 1f, v => sr.specularRamp = v);
            AddSlider(fold, "Specular Offset", sr.specularRampOffset, 0f, 1f, v => sr.specularRampOffset = v);

            AddToggle(fold, "Use Rim Lighting", sr.useRimLighting, v => sr.useRimLighting = v);
            AddColorField(fold, "Rim Light Color", sr.rimLightColor, v => sr.rimLightColor = v);
            AddSlider(fold, "Rim Ramp", sr.rimRamp, 0f, 1f, v => sr.rimRamp = v);
            AddSlider(fold, "Rim Ramp Offset", sr.rimLightRampOffset, 0f, 1f, v => sr.rimLightRampOffset = v);
            AddSlider(fold, "Rim Lit Intensity", sr.rimLightLitIntensity, 0f, 2f, v => sr.rimLightLitIntensity = v);
            AddSlider(fold, "Rim Shadow Intensity", sr.rimLightShadowIntensity, 0f, 2f, v => sr.rimLightShadowIntensity = v);
        }

        private void BuildPaletteSettingsSection()
        {
            var fold = AddFoldoutSection("Paleta & Cor", true);
            var c = _workingProfile.colorPalette;
            AddSlider(fold, "Contrast", c.contrast, 0.5f, 1.5f, v => c.contrast = v);
            AddSlider(fold, "Saturation Shift", c.saturationShift, -1f, 1f, v => c.saturationShift = v);
            AddSlider(fold, "Value Shift", c.valueShift, -1f, 1f, v => c.valueShift = v);
            AddToggle(fold, "Clamp Adjustments", c.clampAdjustments, v => c.clampAdjustments = v);
            AddToggle(fold, "Posterize Colors", c.posterizeColors, v => c.posterizeColors = v);
            AddIntSlider(fold, "Color Numbers", c.colorNumbers, 2, 20, v => c.colorNumbers = v);

            AddColorField(fold, "Accent Red", c.accentRed, v => c.accentRed = v);
            AddColorField(fold, "Accent Yellow", c.accentYellow, v => c.accentYellow = v);
            AddColorField(fold, "Accent Turquoise", c.accentTurquoise, v => c.accentTurquoise = v);
            AddColorField(fold, "Neutral Light", c.neutralLight, v => c.neutralLight = v);
            AddColorField(fold, "Neutral Dark", c.neutralDark, v => c.neutralDark = v);
        }

        private void BuildOutlineSettingsSection()
        {
            var fold = AddFoldoutSection("Contornos (Outlines)", true);
            var o = _workingProfile.outlines;
            AddEnumField(fold, "Outline Mode", o.outlineMode, v => o.outlineMode = (VisualStyleOutlineMode)v);
            AddColorField(fold, "Outline Color", o.outlineColor, v => o.outlineColor = v);
            AddSlider(fold, "Screen-Space Width", o.screenSpaceWidth, 0f, 4f, v => o.screenSpaceWidth = v);
            AddSlider(fold, "Material Outline Width", o.outlineWidth, 0f, 0.35f, v => o.outlineWidth = v);
            AddToggle(fold, "Normal Surface Outline", o.normalSurfaceOutline, v => o.normalSurfaceOutline = v);
            AddToggle(fold, "Use Depth Test", o.useDepthTest, v => o.useDepthTest = v);
            AddSlider(fold, "Depth Threshold", o.depthThreshold, 0.05f, 10f, v => o.depthThreshold = v);
            AddToggle(fold, "Use Normals Test", o.useNormalsTest, v => o.useNormalsTest = v);
            AddSlider(fold, "Normal Threshold", o.normalThreshold, 0.05f, 2f, v => o.normalThreshold = v);
            AddToggle(fold, "Use Color Test", o.useColorTest, v => o.useColorTest = v);
            AddSlider(fold, "Color Threshold", o.colorThreshold, 0.01f, 1f, v => o.colorThreshold = v);

            AddSlider(fold, "Character Mult", o.characterOutlineMultiplier, 0f, 2f, v => o.characterOutlineMultiplier = v);
            AddSlider(fold, "Building Mult", o.buildingOutlineMultiplier, 0f, 2f, v => o.buildingOutlineMultiplier = v);
            AddSlider(fold, "Food Mult", o.foodOutlineMultiplier, 0f, 2f, v => o.foodOutlineMultiplier = v);

            AddSlider(fold, "Max Dist Arch", o.maxOutlineDistanceArchitecture, 5f, 150f, v => o.maxOutlineDistanceArchitecture = v);
            AddSlider(fold, "Max Dist Char", o.maxOutlineDistanceCharacters, 5f, 100f, v => o.maxOutlineDistanceCharacters = v);
            AddSlider(fold, "Max Dist Props", o.maxOutlineDistanceProps, 5f, 80f, v => o.maxOutlineDistanceProps = v);
            AddEnumField(fold, "Outline Quality", o.outlineQuality, v => o.outlineQuality = (VisualStyleOutlineQuality)v);
            AddToggle(fold, "Suppress During Focus", o.suppressOutlineOnInteractionFocus, v => o.suppressOutlineOnInteractionFocus = v);
        }

        private void BuildCharacterAndMaterialSettingsSection()
        {
            var foldC = AddFoldoutSection("Personagens & Rosto", false);
            var ch = _workingProfile.characters;
            AddToggle(foldC, "Character Rim Enabled", ch.rimLightEnabled, v => ch.rimLightEnabled = v);
            AddColorField(foldC, "Character Rim Color", ch.rimColor, v => ch.rimColor = v);
            AddSlider(foldC, "Character Rim Intensity", ch.rimIntensity, 0f, 1f, v => ch.rimIntensity = v);
            AddSlider(foldC, "Contrast Boost", ch.characterContrastBoost, 0.8f, 1.4f, v => ch.characterContrastBoost = v);
            AddToggle(foldC, "Preserve Body Tint (MPB)", ch.preserveDynamicBodyColor, v => ch.preserveDynamicBodyColor = v);
            AddSlider(foldC, "Skin Ramp Softness", ch.skinLightRampSoftness, 0f, 2f, v => ch.skinLightRampSoftness = v);
            AddToggle(foldC, "Eyes Unlit Crisp", ch.eyesUnlitCrisp, v => ch.eyesUnlitCrisp = v);

            var foldM = AddFoldoutSection("Materiais & Recursos", false);
            var m = _workingProfile.materials;
            AddToggle(foldM, "Preserve Textures", m.preserveTextures, v => m.preserveTextures = v);
            AddToggle(foldM, "Preserve Normal Maps", m.preserveNormalMaps, v => m.preserveNormalMaps = v);
            AddToggle(foldM, "Preserve UV Tiling/Offset", m.preserveUVTilingOffset, v => m.preserveUVTilingOffset = v);
            AddToggle(foldM, "Preserve Alpha/Transparency", m.preserveAlphaAndTransparency, v => m.preserveAlphaAndTransparency = v);
            AddToggle(foldM, "Preserve Emission", m.preserveEmission, v => m.preserveEmission = v);
            AddToggle(foldM, "Prefer Simple for Streets", m.preferSimpleForStreets, v => m.preferSimpleForStreets = v);
            AddToggle(foldM, "Disable Perturbations", m.disableIllustratePerturbations, v => m.disableIllustratePerturbations = v);
        }

        private void BuildSceneLightingSettingsSection()
        {
            var fold = AddFoldoutSection("Iluminação Cena Variante", false);
            var s = _workingProfile.sceneLighting;
            AddToggle(fold, "Override Lighting in Variant", s.overrideSceneLightingInVariant, v => s.overrideSceneLightingInVariant = v);
            AddSlider(fold, "Directional Intensity", s.directionalLightIntensity, 0.2f, 3f, v => s.directionalLightIntensity = v);
            AddColorField(fold, "Directional Color", s.directionalLightColor, v => s.directionalLightColor = v);
            AddColorField(fold, "Ambient Sky", s.ambientSkyColor, v => s.ambientSkyColor = v);
            AddColorField(fold, "Ambient Equator", s.ambientEquatorColor, v => s.ambientEquatorColor = v);
            AddColorField(fold, "Ambient Ground", s.ambientGroundColor, v => s.ambientGroundColor = v);
            AddSlider(fold, "Ambient Intensity", s.ambientIntensity, 0f, 2f, v => s.ambientIntensity = v);
            AddToggle(fold, "Add Interior Fill Lights", s.addInteriorFillLights, v => s.addInteriorFillLights = v);
            AddSlider(fold, "Interior Fill Intensity", s.interiorFillIntensity, 0f, 5f, v => s.interiorFillIntensity = v);
            AddSlider(fold, "Interior Fill Range", s.interiorFillRange, 2f, 25f, v => s.interiorFillRange = v);
        }

        private void BuildCategoryOverridesSection()
        {
            var fold = AddFoldoutSection("Overrides por Categoria (Extensível)", false);
            var catIds = new List<string>();
            if (_ruleSet != null && _ruleSet.categories != null)
            {
                for (int i = 0; i < _ruleSet.categories.Count; i++)
                {
                    if (_ruleSet.categories[i] != null && !string.IsNullOrEmpty(_ruleSet.categories[i].categoryId))
                    {
                        if (!catIds.Contains(_ruleSet.categories[i].categoryId))
                        {
                            catIds.Add(_ruleSet.categories[i].categoryId);
                        }
                    }
                }
            }
            for (int i = 0; i < VisualCategoryIds.BuiltInCategories.Length; i++)
            {
                if (!catIds.Contains(VisualCategoryIds.BuiltInCategories[i]))
                {
                    catIds.Add(VisualCategoryIds.BuiltInCategories[i]);
                }
            }

            for (int i = 0; i < catIds.Count; i++)
            {
                string catId = catIds[i];
                if (catId == VisualCategoryIds.Effects || catId == VisualCategoryIds.Interaction) continue;
                var catFold = new Foldout { text = catId, value = false };
                var eff = _workingProfile.GetEffectiveCategorySettings(catId);

                AddEnumField(catFold, "Shader Family", eff.preferredShaderFamily, v =>
                {
                    var ov = _workingProfile.GetOrAddCategoryOverride(catId);
                    ov.preferredShaderFamily = (IllustrateShaderFamily)v;
                });
                AddToggle(catFold, "Preserve Textures", eff.preserveTextures, v =>
                {
                    var ov = _workingProfile.GetOrAddCategoryOverride(catId);
                    ov.preserveTextures = v;
                });
                AddToggle(catFold, "Allow Posterize", eff.allowColorPosterize, v =>
                {
                    var ov = _workingProfile.GetOrAddCategoryOverride(catId);
                    ov.allowColorPosterize = v;
                });
                AddIntSlider(catFold, "Light Steps Offset", eff.lightStepsOffset, -3, 3, v =>
                {
                    var ov = _workingProfile.GetOrAddCategoryOverride(catId);
                    ov.lightStepsOffset = v;
                });
                AddSlider(catFold, "Outline Multiplier", eff.outlineMultiplier, 0f, 2f, v =>
                {
                    var ov = _workingProfile.GetOrAddCategoryOverride(catId);
                    ov.outlineMultiplier = v;
                });

                fold.Add(catFold);
            }
        }

        private void BuildDiscoverySettingsSection()
        {
            var fold = AddFoldoutSection("Descoberta & Política", false);
            var d = _workingProfile.discovery;
            AddEnumField(fold, "Unknown Material Policy", d.newUnknownMaterialPolicy, v => d.newUnknownMaterialPolicy = (VisualDiscoveryPolicy)v);
            AddSlider(fold, "Auto-Apply Confidence", d.autoApplyConfidenceThreshold, 0.5f, 1f, v => d.autoApplyConfidenceThreshold = v);
            AddToggle(fold, "Auto-Sync Watched", d.autoSyncWatchedChangesToVariants, v => d.autoSyncWatchedChangesToVariants = v);
            AddEnumField(fold, "Default Discovery Scope", d.defaultScope, v => d.defaultScope = (VisualDiscoveryScope)v);

            var ruleField = new ObjectField("Conversion RuleSet")
            {
                objectType = typeof(VisualConversionRuleSet),
                allowSceneObjects = false,
                value = _ruleSet
            };
            ruleField.RegisterValueChangedCallback(evt =>
            {
                var nextRuleSet = evt.newValue as VisualConversionRuleSet;
                if (nextRuleSet != null)
                {
                    _ruleSet = nextRuleSet;
                    d.conversionRuleSet = nextRuleSet;
                    MarkWorkingProfileEdited();
                    RebuildSettingsInspector();
                }
            });
            fold.Add(ruleField);
        }

        private Foldout AddFoldoutSection(string title, bool expandedByDefault)
        {
            var fold = new Foldout { text = title, value = expandedByDefault };
            fold.AddToClassList("settings-section-title");
            _settingsScroll.Add(fold);
            return fold;
        }

        private void AddSlider(VisualElement parent, string label, float initial, float min, float max, Action<float> setter)
        {
            var s = new Slider(label, min, max) { value = Mathf.Clamp(initial, min, max), showInputField = true };
            s.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkWorkingProfileEdited();
            });
            parent.Add(s);
        }

        private void AddIntSlider(VisualElement parent, string label, int initial, int min, int max, Action<int> setter)
        {
            var s = new SliderInt(label, min, max) { value = Mathf.Clamp(initial, min, max), showInputField = true };
            s.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkWorkingProfileEdited();
            });
            parent.Add(s);
        }

        private void AddToggle(VisualElement parent, string label, bool initial, Action<bool> setter)
        {
            var t = new Toggle(label) { value = initial };
            t.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkWorkingProfileEdited();
            });
            parent.Add(t);
        }

        private void AddColorField(VisualElement parent, string label, Color initial, Action<Color> setter)
        {
            var c = new ColorField(label) { value = initial, showAlpha = false };
            c.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkWorkingProfileEdited();
            });
            parent.Add(c);
        }

        private void AddEnumField(VisualElement parent, string label, Enum initial, Action<Enum> setter)
        {
            var e = new EnumField(label, initial);
            e.RegisterValueChangedCallback(evt =>
            {
                setter(evt.newValue);
                MarkWorkingProfileEdited();
            });
            parent.Add(e);
        }

        private void MarkWorkingProfileEdited()
        {
            _hasUnsavedChanges = true;
            if (_statusState == WizardStyleState.Valid || _statusState == WizardStyleState.Generated || _statusState == WizardStyleState.Applied)
            {
                _statusState = WizardStyleState.Unvalidated;
            }
            UpdateDirtyAndStatusBadges();
            SchedulePreviewRebuild(immediate: false);
        }
        #endregion

        #region Debounce & Status Updates
        private void SchedulePreviewRebuild(bool immediate)
        {
            _previewRebuildDueAt = immediate ? 0.0001d : EditorApplication.timeSinceStartup + 0.16d;
        }

        private void SchedulePreviewRenderOnly(bool immediate)
        {
            _previewRenderOnlyDueAt = immediate ? 0.0001d : EditorApplication.timeSinceStartup + 0.04d;
        }

        private void OnEditorUpdate()
        {
            if (_previewRenderer == null || _previewRenderer.IsDisposed || _workingProfile == null) return;

            double now = EditorApplication.timeSinceStartup;
            if (_previewRebuildDueAt > 0d && now >= _previewRebuildDueAt)
            {
                _previewRebuildDueAt = -1d;
                _previewRenderOnlyDueAt = -1d;
                _previewRenderer.RebuildAllTransientMaterials(_workingProfile, _ruleSet);
                _previewRenderer.RenderAllSlots(_workingProfile);
                RefreshAllSlotImagesAndInspectors();
                return;
            }

            if (_previewRenderOnlyDueAt > 0d && now >= _previewRenderOnlyDueAt)
            {
                _previewRenderOnlyDueAt = -1d;
                _previewRenderer.RenderAllSlots(_workingProfile);
                RefreshAllSlotImages();
            }
        }

        private void RefreshAllSlotImages()
        {
            if (_previewRenderer == null) return;
            for (int i = 0; i < _previewRenderer.Slots.Count; i++)
            {
                RefreshPreviewCardImageBindings(_previewRenderer.Slots[i]);
            }
        }

        private void RefreshAllSlotImagesAndInspectors()
        {
            if (_previewRenderer == null) return;
            for (int i = 0; i < _previewRenderer.Slots.Count; i++)
            {
                var slot = _previewRenderer.Slots[i];
                RefreshPreviewCardImageBindings(slot);
                RebuildSlotInspector(slot);
            }
        }

        private void OnPendingImportsChanged()
        {
            RefreshPendingBadge();
        }

        private void RefreshPendingBadge()
        {
            if (_lblPendingBadge == null) return;
            int count = VisualStyleImportWatcher.GetPendingCount();
            _lblPendingBadge.text = count > 0
                ? $"Pending Imports: {count} novo(s)/alterado(s)"
                : "Pending Imports: 0";
        }

        private void UpdateStatusFromDiskState()
        {
            if (_workingProfile == null) return;
            var manifest = VisualStyleMaterialFactory.LoadManifest(_workingProfile.styleId);
            var cfg = VisualStyleRuntimeConfig.LoadCached();
            if (cfg != null && cfg.useStyledGameplay && cfg.activeStyleId == _workingProfile.styleId)
            {
                _statusState = WizardStyleState.Applied;
            }
            else if (manifest != null && manifest.entries != null && manifest.entries.Count > 0)
            {
                _statusState = manifest.profileContentHash == _workingProfile.ComputeContentHash()
                    ? WizardStyleState.Generated
                    : WizardStyleState.Unvalidated;
            }
            else
            {
                _statusState = WizardStyleState.Unvalidated;
            }
        }

        private void UpdateDirtyAndStatusBadges()
        {
            if (_lblDirtyBadge != null)
            {
                _lblDirtyBadge.EnableInClassList("hidden", !_hasUnsavedChanges);
            }

            if (_lblStatusBadge != null)
            {
                _lblStatusBadge.text = _statusState.ToString();
                _lblStatusBadge.RemoveFromClassList("status-unvalidated");
                _lblStatusBadge.RemoveFromClassList("status-valid");
                _lblStatusBadge.RemoveFromClassList("status-generated");
                _lblStatusBadge.RemoveFromClassList("status-applied");
                _lblStatusBadge.RemoveFromClassList("status-error");

                string cls = _statusState switch
                {
                    WizardStyleState.Valid => "status-valid",
                    WizardStyleState.Generated => "status-generated",
                    WizardStyleState.Applied => "status-applied",
                    WizardStyleState.Error => "status-error",
                    _ => "status-unvalidated"
                };
                _lblStatusBadge.AddToClassList(cls);
            }
        }
        #endregion

        #region Other Tabs (Discovery, Coverage, Exceptions, Before/After, Performance, Log)
        private void BuildDiscoveryToolbar()
        {
            if (_discoveryControls == null) return;
            _discoveryControls.Clear();

            var scopeEnum = new EnumField("Escopo", _discoveryScope);
            scopeEnum.RegisterValueChangedCallback(evt =>
            {
                _discoveryScope = (VisualDiscoveryScope)evt.newValue;
                RunDiscoveryScan();
            });
            _discoveryControls.Add(scopeEnum);

            var scanBtn = new Button(RunDiscoveryScan) { text = "Escanear Agora" };
            scanBtn.AddToClassList("mini-btn");
            _discoveryControls.Add(scanBtn);

            var searchField = new TextField("Filtro") { value = _discoverySearchFilter };
            searchField.style.minWidth = 220;
            searchField.RegisterValueChangedCallback(evt =>
            {
                _discoverySearchFilter = evt.newValue ?? "";
                RefreshDiscoveryTab();
            });
            _discoveryControls.Add(searchField);
        }

        private void BuildBeforeAfterToolbar()
        {
            if (_beforeAfterToolbar == null) return;
            _beforeAfterToolbar.Clear();

            var capBtn = new Button(CaptureABSnapshotsFromPreview) { text = "Capturar Snapshots A/B (Preview Lab)" };
            capBtn.AddToClassList("footer-btn");
            capBtn.AddToClassList("footer-btn-primary");
            _beforeAfterToolbar.Add(capBtn);

            var exportPngBtn = new Button(ExportABSnapshotsToReportsAction) { text = "Exportar PNGs (Reports)" };
            exportPngBtn.AddToClassList("mini-btn");
            _beforeAfterToolbar.Add(exportPngBtn);

            var clrBtn = new Button(() =>
            {
                for (int i = 0; i < _abSnapshots.Count; i++) _abSnapshots[i]?.Dispose();
                _abSnapshots.Clear();
                RefreshBeforeAfterTab();
            })
            {
                text = "Limpar Galeria"
            };
            clrBtn.AddToClassList("mini-btn");
            _beforeAfterToolbar.Add(clrBtn);
        }

        private void CaptureABSnapshotsFromPreview()
        {
            if (_previewRenderer == null) return;
            var prevCmp = _previewRenderer.ComparisonMode;
            _previewRenderer.ComparisonMode = VisualPreviewComparisonMode.SideBySideAB;

            for (int i = 0; i < _abSnapshots.Count; i++) _abSnapshots[i]?.Dispose();
            _abSnapshots.Clear();

            for (int i = 0; i < _previewRenderer.Slots.Count; i++)
            {
                var slot = _previewRenderer.Slots[i];
                _previewRenderer.RenderSlot(slot, _workingProfile);
                var snap = new CapturedABSnapshot
                {
                    title = slot.title,
                    timestamp = DateTime.Now.ToString("HH:mm:ss"),
                    originalTex = _previewRenderer.CaptureSlotTextureCopy(slot, captureToon: false),
                    toonTex = _previewRenderer.CaptureSlotTextureCopy(slot, captureToon: true)
                };
                _abSnapshots.Add(snap);
            }

            _previewRenderer.ComparisonMode = prevCmp;
            RefreshPreviewCardsComparisonTextures();
            RefreshBeforeAfterTab();
            AppendLog($"Capturados {_abSnapshots.Count} pares Before/After do Preview Lab.");
        }

        private void ExportABSnapshotsToReportsAction()
        {
            if (_abSnapshots.Count == 0)
            {
                CaptureABSnapshotsFromPreview();
            }

            int saved = ExportSnapshotsToReports(_workingProfile, _abSnapshots);
            AppendLog($"Exportadas {saved} imagens PNG Before/After em '{VisualStyleAuditService.ReportsFolder}/Screenshots/'.");
        }

        public static int CaptureAndExportABScreenshotsHeadless(VisualStyleProfile profile, VisualConversionRuleSet ruleSet)
        {
            if (profile == null) return 0;
            if (ruleSet == null) ruleSet = VisualStylePresetSeeder.EnsureDefaultRuleSet();

            var snapshots = new List<CapturedABSnapshot>();
            using (var renderer = new VisualStylePreviewRenderer())
            {
                renderer.InitializeDefaultSlots(profile, ruleSet);
                renderer.ComparisonMode = VisualPreviewComparisonMode.SideBySideAB;
                for (int i = 0; i < renderer.Slots.Count; i++)
                {
                    var slot = renderer.Slots[i];
                    renderer.RenderSlot(slot, profile);
                    snapshots.Add(new CapturedABSnapshot
                    {
                        title = slot.title,
                        timestamp = DateTime.Now.ToString("HHmmss"),
                        originalTex = renderer.CaptureSlotTextureCopy(slot, captureToon: false),
                        toonTex = renderer.CaptureSlotTextureCopy(slot, captureToon: true)
                    });
                }
            }

            try
            {
                return ExportSnapshotsToReports(profile, snapshots);
            }
            finally
            {
                for (int i = 0; i < snapshots.Count; i++) snapshots[i]?.Dispose();
            }
        }

        private static int ExportSnapshotsToReports(VisualStyleProfile profile, List<CapturedABSnapshot> snapshots)
        {
            if (profile == null || snapshots == null || snapshots.Count == 0) return 0;
            string safeStyle = VisualStyleMaterialFactory.SanitizeFileToken(profile.styleId);
            string folder = $"{VisualStyleAuditService.ReportsFolder}/Screenshots/{safeStyle}";
            VisualStylePresetSeeder.EnsureFolder(folder);

            int count = 0;
            for (int i = 0; i < snapshots.Count; i++)
            {
                var snap = snapshots[i];
                if (snap == null) continue;
                string safeSlot = VisualStyleMaterialFactory.SanitizeFileToken(snap.title);

                if (snap.originalTex != null)
                {
                    File.WriteAllBytes($"{folder}/{safeSlot}_Original.png", snap.originalTex.EncodeToPNG());
                    count++;
                }
                if (snap.toonTex != null)
                {
                    File.WriteAllBytes($"{folder}/{safeSlot}_CelShaded.png", snap.toonTex.EncodeToPNG());
                    count++;
                }
            }

            AssetDatabase.Refresh();
            return count;
        }

        private void RefreshPreviewCardsComparisonTextures()
        {
            SchedulePreviewRenderOnly(immediate: true);
        }

        private void RunDiscoveryScan()
        {
            if (_workingProfile == null) return;
            var manifest = VisualStyleMaterialFactory.LoadManifest(_workingProfile.styleId);
            IReadOnlyList<UnityEngine.Object> selection = Selection.objects;
            _latestScan = VisualStyleAssetDiscoveryService.Scan(
                _workingProfile,
                _ruleSet,
                manifest,
                _discoveryScope,
                _workingProfile.discovery.selectedFolders,
                selection);

            UpdateAuditSummaryFooter();
            RefreshDiscoveryTab();
            RefreshCoverageTab();
            RefreshExceptionsTab();
            AppendLog($"Scan '{_discoveryScope}': {_latestScan.targets.Count} materiais únicos, {_latestScan.totalSlotOccurrences} usos, {_latestScan.CountByStatus(VisualAssetSyncStatus.ManualReview)} pendentes.");
        }

        private void RefreshDiscoveryTab()
        {
            if (_discoveryListScroll == null) return;
            _discoveryListScroll.Clear();

            if (_latestScan == null) RunDiscoveryScan();
            if (_latestScan == null) return;

            var summary = new Label(
                $"Escopo: {_latestScan.scope} | Alvos: {_latestScan.targets.Count} | " +
                $"UpToDate: {_latestScan.CountByStatus(VisualAssetSyncStatus.UpToDate)} | " +
                $"New: {_latestScan.CountByStatus(VisualAssetSyncStatus.Unprocessed)} | " +
                $"Modified: {_latestScan.CountByStatus(VisualAssetSyncStatus.SourceModified)} | " +
                $"Outdated: {_latestScan.CountByStatus(VisualAssetSyncStatus.ProfileOutdated)} | " +
                $"Review: {_latestScan.CountByStatus(VisualAssetSyncStatus.ManualReview)} | " +
                $"Orphans: {_latestScan.orphanedEntries.Count}");
            summary.style.marginBottom = 6;
            _discoveryListScroll.Add(summary);

            int count = 0;
            for (int i = 0; i < _latestScan.targets.Count; i++)
            {
                var t = _latestScan.targets[i];
                if (_discoveryStatusFilter.HasValue && t.incrementalStatus != _discoveryStatusFilter.Value) continue;
                if (!string.IsNullOrEmpty(_discoverySearchFilter))
                {
                    bool hit = (t.descriptor.materialName?.IndexOf(_discoverySearchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (t.decision.categoryId?.IndexOf(_discoverySearchFilter, StringComparison.OrdinalIgnoreCase) >= 0) ||
                               (t.sourceAssetPath?.IndexOf(_discoverySearchFilter, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!hit) continue;
                }

                var row = new VisualElement();
                row.AddToClassList("table-row");
                string info = $"[{t.incrementalStatus}] {t.descriptor.materialName} ({t.decision.categoryId}/{t.decision.shaderFamily}) | conf {t.decision.confidence:P0} | usos {t.usageContexts.Count}";
                row.Add(new Label(info) { style = { flexGrow = 1, fontSize = 11 } });

                var promoteBtn = new Button(() =>
                {
                    bool ok = VisualStyleIncrementalRebuilder.PromoteToCustomMaterial(_workingProfile.styleId, t.conversionKey);
                    AppendLog(ok ? $"Material promovido a Custom no manifesto: {t.descriptor.materialName}" : $"Gere a biblioteca primeiro para promover {t.descriptor.materialName}.");
                    RunDiscoveryScan();
                })
                {
                    text = "Promover Custom"
                };
                promoteBtn.AddToClassList("mini-btn");
                row.Add(promoteBtn);

                _discoveryListScroll.Add(row);
                if (++count >= 250) break;
            }
        }

        private void RefreshCoverageTab()
        {
            if (_coverageFilterBar != null && _coverageFilterBar.childCount == 0)
            {
                string[] filters = { "All", "Convert", "Preserve", "Review" };
                for (int i = 0; i < filters.Length; i++)
                {
                    string f = filters[i];
                    var btn = new Button(() =>
                    {
                        _coverageFilter = f;
                        RefreshCoverageTab();
                    })
                    {
                        text = f
                    };
                    btn.AddToClassList("mini-btn");
                    _coverageFilterBar.Add(btn);
                }
            }

            if (_coverageListScroll == null) return;
            _coverageListScroll.Clear();

            if (_latestAudit != null)
            {
                _coverageListScroll.Add(new Label(
                    $"AUDITORIA GLOBAL: Renderers={_latestAudit.totalRenderersInspected} | Slots={_latestAudit.totalSlotsInspected} | " +
                    $"Convert={_latestAudit.convertibleCount} | Preserve={_latestAudit.preservedCount} | Review={_latestAudit.manualReviewCount} | Warnings={_latestAudit.warningCount}")
                { style = { marginBottom = 6, unityFontStyleAndWeight = FontStyle.Bold } });

                foreach (var grp in _latestAudit.slots.GroupBy(s => s.decision.categoryId ?? "Unclassified").OrderByDescending(g => g.Count()))
                {
                    _coverageListScroll.Add(new Label($"  Categoria '{grp.Key}': {grp.Count()} slots"));
                }
            }

            if (_latestScan != null)
            {
                _coverageListScroll.Add(new Label("Detalhes do Scan Atual:") { style = { marginTop = 8, unityFontStyleAndWeight = FontStyle.Bold } });
                int shown = 0;
                for (int i = 0; i < _latestScan.targets.Count; i++)
                {
                    var t = _latestScan.targets[i];
                    if (_coverageFilter == "Convert" && t.decision.action != VisualConversionAction.Convert) continue;
                    if (_coverageFilter == "Preserve" && t.decision.action != VisualConversionAction.Preserve && t.decision.action != VisualConversionAction.Skip) continue;
                    if (_coverageFilter == "Review" && t.decision.action != VisualConversionAction.ManualReview) continue;

                    var row = new VisualElement();
                    row.AddToClassList("table-row");
                    string badge = t.decision.action switch
                    {
                        VisualConversionAction.Convert => "badge-converted",
                        VisualConversionAction.Preserve => "badge-preserved",
                        VisualConversionAction.ManualReview => "badge-review",
                        _ => "badge-error"
                    };
                    var bLbl = new Label(t.decision.action.ToString()) { style = { minWidth = 72, unityTextAlign = TextAnchor.MiddleCenter } };
                    bLbl.AddToClassList("badge");
                    bLbl.AddToClassList(badge);
                    row.Add(bLbl);
                    row.Add(new Label($" {t.descriptor.materialName} [{t.decision.categoryId}/{t.decision.semanticRole}] <- {t.decision.reason}") { style = { flexGrow = 1, fontSize = 11 } });
                    _coverageListScroll.Add(row);
                    if (++shown >= 220) break;
                }
            }
        }

        private void RefreshExceptionsTab()
        {
            if (_exceptionsListScroll == null || _ruleSet == null) return;
            _exceptionsListScroll.Clear();

            _exceptionsListScroll.Add(new Label("Regras de Exclusão / Preservação Automática (RuleSet):") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            for (int i = 0; i < _ruleSet.exclusions.Count; i++)
            {
                var ex = _ruleSet.exclusions[i];
                var row = new VisualElement();
                row.AddToClassList("table-row");
                var tgl = new Toggle(ex.ruleName) { value = ex.enabled };
                tgl.RegisterValueChangedCallback(evt =>
                {
                    Undo.RecordObject(_ruleSet, "Toggle Exclusion Rule");
                    ex.enabled = evt.newValue;
                    EditorUtility.SetDirty(_ruleSet);
                    AssetDatabase.SaveAssets();
                    SchedulePreviewRebuild(immediate: true);
                });
                row.Add(tgl);
                row.Add(new Label($" ({ex.shaderNameContains}{ex.assetPathPrefix}{ex.rendererTypeName}) — {ex.reason}") { style = { fontSize = 11 } });
                _exceptionsListScroll.Add(row);
            }

            _exceptionsListScroll.Add(new Label("Overrides Manuais por Slot:") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 4 } });
            if (_ruleSet.manualOverrides.Count == 0)
            {
                _exceptionsListScroll.Add(new Label("Nenhum override manual cadastrado ainda (use o mini-inspetor nos cards de Preview)."));
            }
            else
            {
                for (int i = 0; i < _ruleSet.manualOverrides.Count; i++)
                {
                    int idx = i;
                    var ov = _ruleSet.manualOverrides[i];
                    var row = new VisualElement();
                    row.AddToClassList("table-row");
                    row.Add(new Label($"{ov.displayLabel} -> {ov.overrideAction} ({ov.overrideCategoryId}/{ov.overrideShaderFamily})") { style = { flexGrow = 1, fontSize = 11 } });
                    var del = new Button(() =>
                    {
                        Undo.RecordObject(_ruleSet, "Remove Slot Override");
                        _ruleSet.manualOverrides.RemoveAt(idx);
                        EditorUtility.SetDirty(_ruleSet);
                        AssetDatabase.SaveAssets();
                        RefreshExceptionsTab();
                        SchedulePreviewRebuild(immediate: true);
                    })
                    { text = "Remover" };
                    del.AddToClassList("mini-btn");
                    row.Add(del);
                    _exceptionsListScroll.Add(row);
                }
            }

            if (_latestScan != null)
            {
                var pendingReviews = _latestScan.targets.Where(t => t.decision.action == VisualConversionAction.ManualReview).ToList();
                _exceptionsListScroll.Add(new Label($"Pendentes ManualReview ({pendingReviews.Count}):") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 4 } });
                for (int i = 0; i < Math.Min(60, pendingReviews.Count); i++)
                {
                    var t = pendingReviews[i];
                    var row = new VisualElement();
                    row.AddToClassList("table-row");
                    row.Add(new Label($"{t.descriptor.materialName} ({t.sourceAssetPath})") { style = { flexGrow = 1, fontSize = 11 } });
                    var btnConvert = new Button(() => CreateOverrideDecision(t, VisualConversionAction.Convert)) { text = "Converter" };
                    var btnPreserve = new Button(() => CreateOverrideDecision(t, VisualConversionAction.Preserve)) { text = "Preservar" };
                    btnConvert.AddToClassList("mini-btn");
                    btnPreserve.AddToClassList("mini-btn");
                    row.Add(btnConvert);
                    row.Add(btnPreserve);
                    _exceptionsListScroll.Add(row);
                }
            }
        }

        private void CreateOverrideDecision(DiscoveredMaterialTarget target, VisualConversionAction action)
        {
            Undo.RecordObject(_ruleSet, "Resolve ManualReview Material");
            _ruleSet.manualOverrides.Add(new VisualSlotOverride
            {
                sourceAssetGuid = target.sourceMaterialGuid,
                sourceLocalFileId = target.sourceLocalFileId,
                slotIndex = target.descriptor.slotIndex,
                displayLabel = target.descriptor.materialName,
                overrideCategoryId = target.decision.categoryId,
                overrideRole = target.decision.semanticRole,
                overrideShaderFamily = target.decision.shaderFamily,
                overrideAction = action,
                preserveTexture = true,
                preserveColor = true,
                allowDynamicTint = target.decision.allowDynamicTint,
                notes = "Resolved in Exceptions Tab"
            });
            EditorUtility.SetDirty(_ruleSet);
            AssetDatabase.SaveAssets();
            RunDiscoveryScan();
            RefreshExceptionsTab();
        }

        private void RefreshBeforeAfterTab()
        {
            if (_beforeAfterScroll == null) return;
            _beforeAfterScroll.Clear();

            if (_abSnapshots.Count == 0)
            {
                _beforeAfterScroll.Add(new Label("Clique em 'Capturar Snapshots A/B (Preview Lab)' acima para gerar a galeria lado a lado Original vs Toon."));
                return;
            }

            for (int i = 0; i < _abSnapshots.Count; i++)
            {
                var snap = _abSnapshots[i];
                var card = new VisualElement();
                card.AddToClassList("preview-card");
                card.style.width = 560;
                card.Add(new Label($"{snap.title}  ({snap.timestamp}) — Original vs Toon") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });

                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, height = 240 } };
                var imgA = new UIImage { image = snap.originalTex, scaleMode = ScaleMode.ScaleToFit, style = { flexGrow = 1, marginRight = 4 } };
                var imgB = new UIImage { image = snap.toonTex, scaleMode = ScaleMode.ScaleToFit, style = { flexGrow = 1 } };
                row.Add(imgA);
                row.Add(imgB);
                card.Add(row);
                _beforeAfterScroll.Add(card);
            }
        }

        private void RefreshPerformanceTab()
        {
            if (_performanceScroll == null) return;
            _performanceScroll.Clear();

            _performanceScroll.Add(new Label("Métricas Estáticas de Materiais & Pooling (Editor):") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 4 } });
            var manifest = _workingProfile != null ? VisualStyleMaterialFactory.LoadManifest(_workingProfile.styleId) : null;
            var library = _workingProfile != null ? VisualStyleMaterialFactory.LoadLibrary(_workingProfile.styleId) : null;

            if (library != null)
            {
                int simpleCount = library.mappedMaterials.Count(m => m.shaderFamily == IllustrateShaderFamily.Simple);
                int opaqueCount = library.mappedMaterials.Count(m => m.shaderFamily == IllustrateShaderFamily.Opaque);
                int alphaCount = library.mappedMaterials.Count(m => m.shaderFamily == IllustrateShaderFamily.Transparent || m.shaderFamily == IllustrateShaderFamily.AlphaClipped);
                int totalSlotsCovered = manifest != null ? manifest.entries.Sum(e => e.referencingAssets != null ? e.referencingAssets.Count : 0) : 0;

                _performanceScroll.Add(new Label($"  Library ativa: {library.styleId} (v{library.profileVersion})"));
                _performanceScroll.Add(new Label($"  Role Defaults compartilhados: {library.roleDefaults.Count}"));
                _performanceScroll.Add(new Label($"  Materiais Mapeados únicos: {library.mappedMaterials.Count} (Opaque={opaqueCount}, Simple={simpleCount}, Transp/Clip={alphaCount})"));
                _performanceScroll.Add(new Label($"  Usos de slots referenciados no manifesto: {totalSlotsCovered}"));
            }
            else
            {
                _performanceScroll.Add(new Label("  Biblioteca ainda não gerada para o perfil atual."));
            }

            _performanceScroll.Add(new Label("Benchmark PlayMode (GameScene vs GameScene_CelShaded):") { style = { unityFontStyleAndWeight = FontStyle.Bold, marginTop = 10, marginBottom = 4 } });
            if (File.Exists(PerformanceBenchmarkJsonPath))
            {
                _performanceScroll.Add(new Label(File.ReadAllText(PerformanceBenchmarkJsonPath)));
            }
            else
            {
                _performanceScroll.Add(new Label("  Nenhum benchmark PlayMode gravado ainda (será preenchido na suíte PlayMode da Fase 11). Dados não são estimados nem simulados."));
            }
        }

        private void RefreshLogTab()
        {
            if (_logScroll == null) return;
            _logScroll.Clear();
            for (int i = _logEntries.Count - 1; i >= 0; i--)
            {
                _logScroll.Add(new Label(_logEntries[i]) { style = { fontSize = 11, marginBottom = 2 } });
            }
        }

        private void AppendLog(string message)
        {
            _logEntries.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
            if (_logEntries.Count > 300) _logEntries.RemoveAt(0);
            if (_activeTabId == "log" && _uiBuilt) RefreshLogTab();
        }
        #endregion

        #region Footer Actions
        private void UpdateAuditSummaryFooter()
        {
            if (_lblAuditSummary == null) return;
            if (_latestAudit != null)
            {
                _lblAuditSummary.text =
                    $"Auditoria: {_latestAudit.totalRenderersInspected} renderers | {_latestAudit.totalSlotsInspected} slots | " +
                    $"Convert={_latestAudit.convertibleCount} | Preserve={_latestAudit.preservedCount} | Warnings={_latestAudit.warningCount}";
            }
            else if (_latestScan != null)
            {
                _lblAuditSummary.text =
                    $"Scan ({_latestScan.scope}): {_latestScan.targets.Count} materiais únicos | {_latestScan.totalSlotOccurrences} usos | Pending Imports={VisualStyleImportWatcher.GetPendingCount()}";
            }
        }

        private void RunAuditAction()
        {
            _latestAudit = VisualStyleAuditService.RunFullAudit(_ruleSet, writeMarkdownReport: true);
            UpdateAuditSummaryFooter();
            AppendLog($"Auditoria completa gravada em '{_latestAudit.reportPath}': Renderers={_latestAudit.totalRenderersInspected}, Slots={_latestAudit.totalSlotsInspected}, Convert={_latestAudit.convertibleCount}, Preserve={_latestAudit.preservedCount}.");
            if (_activeTabId == "coverage") RefreshCoverageTab();
        }

        private void ValidateAction()
        {
            if (_latestScan == null) RunDiscoveryScan();
            _latestValidation = VisualStyleIncrementalRebuilder.ValidateStyle(_workingProfile, _ruleSet, _latestScan);
            _statusState = _latestValidation.isValid ? WizardStyleState.Valid : WizardStyleState.Error;
            UpdateDirtyAndStatusBadges();

            for (int i = 0; i < _latestValidation.errors.Count; i++) AppendLog($"[ERROR] {_latestValidation.errors[i]}");
            for (int i = 0; i < _latestValidation.warnings.Count; i++) AppendLog($"[WARN] {_latestValidation.warnings[i]}");
            AppendLog($"Validação concluída: valid={_latestValidation.isValid}, toGenerate={_latestValidation.materialsToGenerate}, toUpdate={_latestValidation.materialsToUpdate}, errors={_latestValidation.errorCount}, warnings={_latestValidation.warningCount}.");
        }

        private void SyncNewAssetsAction()
        {
            if (_hasUnsavedChanges) SaveWorkingProfileToDisk();
            var report = VisualStyleIncrementalRebuilder.SyncPendingAndChangedAssets(_sourceProfile, _ruleSet, _discoveryScope);
            RefreshPendingBadge();
            RunDiscoveryScan();
            AppendLog($"Sync Incremental: created={report.createdCount}, updated={report.updatedCount}, skipped={report.skippedUnchangedCount}, errors={report.errorCount}.");
        }

        private void GenerateMaterialsAction()
        {
            if (_hasUnsavedChanges) SaveWorkingProfileToDisk();
            if (_latestScan == null) RunDiscoveryScan();
            var report = VisualStyleMaterialFactory.GenerateOrUpdateLibrary(_sourceProfile, _ruleSet, _latestScan, onlyDirtyOrNew: false);
            _statusState = report.errorCount == 0 ? WizardStyleState.Generated : WizardStyleState.Error;
            UpdateDirtyAndStatusBadges();
            RunDiscoveryScan();
            AppendLog($"Geração completa ({report.libraryPath}): created={report.createdCount}, updated={report.updatedCount}, unchanged={report.skippedUnchangedCount}, preserved={report.preservedCount}, errors={report.errorCount}.");
        }

        private void ApplyToGameplayAction()
        {
            if (OnApplyToGameplayRequested == null)
            {
                AppendLog("Hook de aplicação na gameplay ainda será registrado pela Fase 5 (Scene/Prefab Variant Builder).");
                return;
            }

            if (OnApplyToGameplayRequested(_sourceProfile, _ruleSet))
            {
                _statusState = WizardStyleState.Applied;
                UpdateDirtyAndStatusBadges();
                AppendLog($"Estilo '{_sourceProfile.displayName}' aplicado na cena variante de gameplay.");
            }
        }

        private void GenerateAndApplyAllAction()
        {
            if (!EditorUtility.DisplayDialog(
                "Gerar e Aplicar Estilo na Gameplay",
                $"Isto irá gerar/atualizar os materiais Illustrate de '{_workingProfile.displayName}' em Assets/Piaaria/Art/VisualStyles/Generated/ e atualizar a cena variante sem sobrescrever GameScene original.\nDeseja continuar?",
                "Gerar e Aplicar", "Cancelar"))
            {
                return;
            }

            GenerateMaterialsAction();
            if (_statusState != WizardStyleState.Error)
            {
                ApplyToGameplayAction();
            }
        }

        private void OpenShowcaseAction()
        {
            if (OnBuildOrOpenShowcaseRequested == null)
            {
                AppendLog("Showcase Scene Builder será habilitado na Fase 5.");
                return;
            }

            string scenePath = OnBuildOrOpenShowcaseRequested(_sourceProfile, _ruleSet);
            if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                }
            }
        }

        private void ActivateGameplayAction()
        {
            if (OnActivateInGameplayRequested == null)
            {
                AppendLog("Ativação persistente na gameplay será habilitada na Fase 10.");
                return;
            }
            if (OnActivateInGameplayRequested(_sourceProfile))
            {
                _statusState = WizardStyleState.Applied;
                UpdateDirtyAndStatusBadges();
                AppendLog($"Estilo '{_sourceProfile.displayName}' ativado na configuração persistente de gameplay.");
            }
        }

        private void RestoreOriginalAction()
        {
            if (OnRestoreOriginalRequested == null)
            {
                AppendLog("Rollback original será habilitado na Fase 10.");
                return;
            }
            if (OnRestoreOriginalRequested(_sourceProfile))
            {
                _statusState = WizardStyleState.Generated;
                UpdateDirtyAndStatusBadges();
                AppendLog("Configuração restaurada exatamente para o visual original (GameScene canônica intacta).");
            }
        }

        private void ExportReportAction()
        {
            VisualStylePresetSeeder.EnsureFolder(VisualStyleAuditService.ReportsFolder);
            if (_latestAudit == null) _latestAudit = VisualStyleAuditService.RunFullAudit(_ruleSet, writeMarkdownReport: false);
            if (_latestScan == null) RunDiscoveryScan();

            string mdPath = $"{VisualStyleAuditService.ReportsFolder}/Coverage_Report_{_workingProfile.styleId}_{DateTime.Now:yyyyMMdd_HHmmss}.md";
            var sb = new StringBuilder();
            sb.AppendLine($"# Global Cel Shading Coverage Report — {_workingProfile.displayName} (`{_workingProfile.styleId}`)");
            sb.AppendLine($"- GeneratedUtc: `{DateTime.UtcNow:O}`");
            sb.AppendLine($"- Profile Hash: `{_workingProfile.ComputeContentHash()}`");
            sb.AppendLine($"- Scope: `{_discoveryScope}` | Targets: `{_latestScan.targets.Count}` | Total Slot Usages: `{_latestScan.totalSlotOccurrences}`");
            sb.AppendLine($"- Full Audit Renderers: `{_latestAudit.totalRenderersInspected}` | Slots: `{_latestAudit.totalSlotsInspected}` | Convert: `{_latestAudit.convertibleCount}` | Preserve: `{_latestAudit.preservedCount}`");
            sb.AppendLine();
            sb.AppendLine("| Material | Category | Role | ShaderFamily | Action | Confidence | Status |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            for (int i = 0; i < _latestScan.targets.Count; i++)
            {
                var t = _latestScan.targets[i];
                sb.AppendLine($"| `{t.descriptor.materialName}` | {t.decision.categoryId} | {t.decision.semanticRole} | {t.decision.shaderFamily} | {t.decision.action} | {t.decision.confidence:P0} | {t.incrementalStatus} |");
            }

            File.WriteAllText(mdPath, sb.ToString());
            AssetDatabase.ImportAsset(mdPath);
            AppendLog($"Relatório exportado em '{mdPath}'.");
        }
        #endregion
    }
}
#endif
