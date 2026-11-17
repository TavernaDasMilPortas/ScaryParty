using UnityEngine;

namespace ScaryParty.Cutscenes
{
    /// <summary>
    /// Connects a gameplay event (e.g. Night Start) to CutscenePlaybackService
    /// without modifying core gameplay prefabs.
    /// </summary>
    public sealed class NightCutsceneTrigger : MonoBehaviour
    {
        [SerializeField] private CutsceneDefinition cutsceneDefinition;
        [SerializeField] private bool prepareOnStart = true;

        private void Start()
        {
            if (cutsceneDefinition != null && CutscenePlaybackService.Instance != null)
            {
                CutscenePlaybackService.Instance.RegisterDefinition(cutsceneDefinition);
                if (prepareOnStart)
                {
                    CutscenePlaybackService.Instance.Prepare(cutsceneDefinition.cutsceneId);
                }
            }
        }

        public void TriggerCutscene()
        {
            if (cutsceneDefinition == null || CutscenePlaybackService.Instance == null)
                return;

            CutscenePlaybackService.Instance.Play(cutsceneDefinition.cutsceneId);
        }
    }
}
