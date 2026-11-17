using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace ScaryParty.Cutscenes
{
    [Serializable]
    public sealed class CutsceneCameraClip : PlayableAsset, ITimelineClipAsset
    {
        [HideInInspector] public CutsceneCameraBehaviour template = new CutsceneCameraBehaviour();

        public ClipCaps clipCaps => ClipCaps.None;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<CutsceneCameraBehaviour>.Create(graph, template);
            return playable;
        }
    }
}
