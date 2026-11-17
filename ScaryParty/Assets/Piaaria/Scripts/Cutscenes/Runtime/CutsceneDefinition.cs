using UnityEngine;
using UnityEngine.Timeline;

namespace ScaryParty.Cutscenes
{
    public enum CutsceneQualityPreset
    {
        Native = 0,
        High1080p = 1,
        Medium720p = 2
    }

    [CreateAssetMenu(fileName = "CutsceneDefinition", menuName = "ScaryParty/Cutscenes/Cutscene Definition")]
    public sealed class CutsceneDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string cutsceneId = "cutscene_01";
        public string targetScenePath;
        public string targetSceneName;

        [Header("Timeline & Presentation")]
        public TimelineAsset timelineAsset;
        public float durationSeconds;
        public int referenceWidth = 1920;
        public int referenceHeight = 1080;
        public CutsceneQualityPreset qualityPreset = CutsceneQualityPreset.High1080p;

        [Header("Playback Policy")]
        public bool allowSkip = true;
        public float fadeInDuration = 0.35f;
        public float fadeOutDuration = 0.35f;
        public bool disableGameplay3DRenderingWhenOpaque = true;
        public bool useUnscaledTime = true;
    }
}
