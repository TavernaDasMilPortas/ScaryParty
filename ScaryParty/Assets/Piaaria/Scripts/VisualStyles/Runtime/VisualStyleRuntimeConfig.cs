using UnityEngine;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Standalone-safe boot/activation config loaded via Resources without Editor AssetDatabase.
    /// </summary>
    [CreateAssetMenu(fileName = "VisualStyleRuntimeConfig", menuName = "Scary Party/Visual Styles/Runtime Config")]
    public class VisualStyleRuntimeConfig : ScriptableObject
    {
        public const string ResourcesPath = "VisualStyleRuntimeConfig";
        public const string CanonicalGameplaySceneName = "GameScene";

        public bool useStyledGameplay = false;
        public string activeStyleId = "";
        public string activeGameplaySceneName = CanonicalGameplaySceneName;
        public VisualStyleProfile activeProfile;
        public VisualMaterialLibrary activeLibrary;
        public bool replicateStyleIdInMultiplayer = false;

        private static VisualStyleRuntimeConfig _cached;

        public static VisualStyleRuntimeConfig LoadCached()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<VisualStyleRuntimeConfig>(ResourcesPath);
            }
            return _cached;
        }

        public static void ClearCache()
        {
            _cached = null;
        }

        public static string GetGameplaySceneName()
        {
            var cfg = LoadCached();
            if (cfg != null && cfg.useStyledGameplay && !string.IsNullOrWhiteSpace(cfg.activeGameplaySceneName))
            {
                return cfg.activeGameplaySceneName;
            }
            return CanonicalGameplaySceneName;
        }
    }
}
