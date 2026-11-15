using ScaryParty.Lighting;
using UnityEngine;

namespace ScaryParty.VisualStyles
{
    public enum GameplaySceneRoute
    {
        Original = 0,
        CelShadedVariant = 1
    }

    public enum VisualApplicationMode
    {
        Disabled = 0,
        RuntimePreview = 1,
        PersistentDefault = 2
    }

    /// <summary>
    /// Configuracao de boot e ativacao carregada via Resources sem dependencia de Editor.
    /// Separa a rota da cena de gameplay do modo de aplicacao do estilo visual.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualStyleRuntimeConfig", menuName = "Scary Party/Visual Styles/Runtime Config")]
    public class VisualStyleRuntimeConfig : ScriptableObject
    {
        public const string ResourcesPath = "VisualStyleRuntimeConfig";
        public const string CanonicalGameplaySceneName = "GameScene";
        public const string VariantGameplaySceneName = "GameScene_CelShaded";

        [Header("Modo de Rotas e Aplicacao (Novo)")]
        public GameplaySceneRoute gameplayRoute = GameplaySceneRoute.Original;
        public VisualApplicationMode visualMode = VisualApplicationMode.Disabled;
        public LightingBackendMode lightingBackendMode = LightingBackendMode.RealtimeStylized;
        public bool enableDayNightCycle = true;
        [Range(0f, 24f)] public float initialTimeOfDayHours = 17f;
        public bool lockTimeOfDayForTests = false;
        [SerializeField, HideInInspector] private bool migratedLegacyFields;

        [Header("Compatibilidade Legada e Perfil Ativo")]
        public bool useStyledGameplay = false;
        public string activeStyleId = "";
        public string activeGameplaySceneName = CanonicalGameplaySceneName;
        public VisualStyleProfile activeProfile;
        public VisualMaterialLibrary activeLibrary;
        public bool replicateStyleIdInMultiplayer = false;

        private static VisualStyleRuntimeConfig _cached;

        // Estado transiente para Mode A (Preview em Play sem mutar permanentemente o asset em disco).
        private static bool _hasSessionOverride;
        private static VisualApplicationMode _sessionVisualMode;
        private static VisualStyleProfile _sessionProfile;
        private static VisualMaterialLibrary _sessionLibrary;
        private static float? _sessionTimeOfDayHours;

        private void OnEnable()
        {
            EnsureMigrated();
        }

        public void EnsureMigrated()
        {
            if (migratedLegacyFields) return;

            if (useStyledGameplay)
            {
                gameplayRoute = activeGameplaySceneName == VariantGameplaySceneName
                    ? GameplaySceneRoute.CelShadedVariant
                    : GameplaySceneRoute.Original;
                visualMode = VisualApplicationMode.PersistentDefault;
            }
            else
            {
                gameplayRoute = GameplaySceneRoute.Original;
                visualMode = VisualApplicationMode.Disabled;
            }

            migratedLegacyFields = true;
        }

        public static VisualStyleRuntimeConfig LoadCached()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<VisualStyleRuntimeConfig>(ResourcesPath);
            }
            if (_cached != null)
            {
                _cached.EnsureMigrated();
            }
            return _cached;
        }

        public static void ClearCache()
        {
            _cached = null;
        }

        public static void SetRuntimePreviewOverride(VisualStyleProfile profile, VisualMaterialLibrary library, VisualApplicationMode mode = VisualApplicationMode.RuntimePreview)
        {
            _hasSessionOverride = true;
            _sessionProfile = profile;
            _sessionLibrary = library;
            _sessionVisualMode = mode;
        }

        public static void SetSessionTimeOverride(float? hours)
        {
            _sessionTimeOfDayHours = hours;
        }

        public static float? GetSessionTimeOverride() => _sessionTimeOfDayHours;

        public static void ClearSessionOverride()
        {
            _hasSessionOverride = false;
            _sessionVisualMode = VisualApplicationMode.Disabled;
            _sessionProfile = null;
            _sessionLibrary = null;
            _sessionTimeOfDayHours = null;
        }

        public static bool HasSessionOverride => _hasSessionOverride;

        public bool ResolveEffectiveStyleForScene(string sceneName, out VisualApplicationMode effectiveMode, out VisualStyleProfile profile, out VisualMaterialLibrary library)
        {
            EnsureMigrated();

            if (_hasSessionOverride)
            {
                effectiveMode = _sessionVisualMode;
                profile = _sessionProfile;
                library = _sessionLibrary;
                return effectiveMode != VisualApplicationMode.Disabled && profile != null;
            }

            effectiveMode = visualMode;
            profile = activeProfile;
            library = activeLibrary;

            if (effectiveMode == VisualApplicationMode.Disabled)
            {
                return false;
            }

            if (sceneName == CanonicalGameplaySceneName)
            {
                return profile != null && (effectiveMode == VisualApplicationMode.RuntimePreview || effectiveMode == VisualApplicationMode.PersistentDefault);
            }

            if (sceneName == VariantGameplaySceneName)
            {
                return profile != null && useStyledGameplay;
            }

            return false;
        }

        public static string GetGameplaySceneName()
        {
            var cfg = LoadCached();
            if (cfg == null) return CanonicalGameplaySceneName;
            cfg.EnsureMigrated();

            if (cfg.gameplayRoute == GameplaySceneRoute.CelShadedVariant ||
                (cfg.useStyledGameplay && cfg.activeGameplaySceneName == VariantGameplaySceneName))
            {
                return string.IsNullOrWhiteSpace(cfg.activeGameplaySceneName)
                    ? VariantGameplaySceneName
                    : cfg.activeGameplaySceneName;
            }

            return CanonicalGameplaySceneName;
        }

        public static bool IsStyledGameplayActive()
        {
            if (_hasSessionOverride)
            {
                return _sessionVisualMode != VisualApplicationMode.Disabled && _sessionProfile != null;
            }
            var cfg = LoadCached();
            if (cfg == null) return false;
            cfg.EnsureMigrated();
            return cfg.visualMode != VisualApplicationMode.Disabled && cfg.activeProfile != null;
        }
    }
}
