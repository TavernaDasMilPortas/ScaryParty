using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ScaryParty.Cutscenes
{
    [TrackColor(0.2f, 0.65f, 0.95f)]
    [TrackClipType(typeof(CutsceneCameraClip))]
    [TrackBindingType(typeof(Camera))]
    public sealed class CutsceneCameraTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<CutsceneCameraMixer>.Create(graph, inputCount);
        }
    }
}
