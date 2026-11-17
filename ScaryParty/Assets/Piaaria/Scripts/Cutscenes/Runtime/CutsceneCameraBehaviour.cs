using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

namespace ScaryParty.Cutscenes
{
    [Serializable]
    public sealed class CutsceneCameraBehaviour : PlayableBehaviour
    {
        public string cameraId;
        public string projection = "PERSP";
        public Vector3 stageOriginOffset;
        public List<CameraSample> samples = new List<CameraSample>();

        public bool TryEvaluate(double absoluteTime, out Vector3 position, out Quaternion rotation, out CameraSample interpolated)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            interpolated = null;

            if (samples == null || samples.Count == 0)
                return false;

            float t = (float)absoluteTime;
            if (t <= samples[0].time || samples.Count == 1)
            {
                var s = samples[0];
                position = CutsceneCoordinateConverter.BlenderPositionToUnity(s.position) + stageOriginOffset;
                rotation = CutsceneCoordinateConverter.BlenderCameraRotationToUnity(s.rotation);
                interpolated = s;
                return true;
            }

            int last = samples.Count - 1;
            if (t >= samples[last].time)
            {
                var s = samples[last];
                position = CutsceneCoordinateConverter.BlenderPositionToUnity(s.position) + stageOriginOffset;
                rotation = CutsceneCoordinateConverter.BlenderCameraRotationToUnity(s.rotation);
                interpolated = s;
                return true;
            }

            // Binary search for bracketing samples
            int lo = 0;
            int hi = last;
            while (lo + 1 < hi)
            {
                int mid = (lo + hi) >> 1;
                if (samples[mid].time <= t) lo = mid;
                else hi = mid;
            }

            var a = samples[lo];
            var b = samples[hi];
            float span = b.time - a.time;
            float u = span > 1e-6f ? Mathf.Clamp01((t - a.time) / span) : 0f;

            Vector3 posA = CutsceneCoordinateConverter.BlenderPositionToUnity(a.position) + stageOriginOffset;
            Vector3 posB = CutsceneCoordinateConverter.BlenderPositionToUnity(b.position) + stageOriginOffset;
            Quaternion rotA = CutsceneCoordinateConverter.BlenderCameraRotationToUnity(a.rotation);
            Quaternion rotB = CutsceneCoordinateConverter.BlenderCameraRotationToUnity(b.rotation);

            position = Vector3.Lerp(posA, posB, u);
            rotation = Quaternion.Slerp(rotA, rotB, u);

            interpolated = new CameraSample
            {
                frame = a.frame,
                time = t,
                lens = Mathf.Lerp(a.lens, b.lens, u),
                sensorWidth = Mathf.Lerp(a.sensorWidth, b.sensorWidth, u),
                sensorHeight = Mathf.Lerp(a.sensorHeight, b.sensorHeight, u),
                sensorFit = a.sensorFit,
                shiftX = Mathf.Lerp(a.shiftX, b.shiftX, u),
                shiftY = Mathf.Lerp(a.shiftY, b.shiftY, u),
                clipStart = Mathf.Lerp(a.clipStart, b.clipStart, u),
                clipEnd = Mathf.Lerp(a.clipEnd, b.clipEnd, u),
                orthoScale = Mathf.Lerp(a.orthoScale, b.orthoScale, u)
            };
            return true;
        }
    }
}
