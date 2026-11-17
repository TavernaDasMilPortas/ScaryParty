using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace ScaryParty.Cutscenes
{
    public sealed class CutsceneStage : MonoBehaviour
    {
        [Header("Stage Roots")]
        public Transform generatedRoot;
        public Transform authoringRoot;

        [Header("Playback References")]
        public PlayableDirector director;
        public Camera outputCamera;
        public AudioListener cutsceneAudioListener;

        [Header("Isolation")]
        public Vector3 stageSpatialOffset = new Vector3(0f, -500f, 0f);
        public Volume[] stageVolumes;
        public List<GameObject> spawnedActors = new List<GameObject>();

        public void EnsureRoots()
        {
            if (generatedRoot == null)
            {
                var gen = new GameObject("Generated");
                gen.transform.SetParent(transform, false);
                generatedRoot = gen.transform;
            }

            if (authoringRoot == null)
            {
                var auth = new GameObject("Authoring");
                auth.transform.SetParent(transform, false);
                authoringRoot = auth.transform;
            }
        }
    }
}
