using UnityEngine;
using UnityEngine.Playables;

namespace ScaryParty.Cutscenes
{
    public sealed class CutsceneCameraMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            var cam = playerData as Camera;
            if (cam == null) return;

            int inputCount = playable.GetInputCount();
            CutsceneCameraBehaviour activeBehaviour = null;

            for (int i = 0; i < inputCount; i++)
            {
                float weight = playable.GetInputWeight(i);
                if (weight > 0.5f)
                {
                    var inputPlayable = (ScriptPlayable<CutsceneCameraBehaviour>)playable.GetInput(i);
                    activeBehaviour = inputPlayable.GetBehaviour();
                    break;
                }
            }

            if (activeBehaviour == null) return;

            double graphTime = playable.GetGraph().IsValid() ? playable.GetTime() : 0.0;
            if (!activeBehaviour.TryEvaluate(graphTime, out var pos, out var rot, out var sample))
                return;

            cam.transform.SetPositionAndRotation(pos, rot);
            cam.nearClipPlane = Mathf.Max(0.01f, sample.clipStart);
            cam.farClipPlane = Mathf.Max(cam.nearClipPlane + 1f, sample.clipEnd);

            bool isOrtho = string.Equals(activeBehaviour.projection, "ORTHO", System.StringComparison.OrdinalIgnoreCase);
            cam.orthographic = isOrtho;

            if (isOrtho)
            {
                cam.orthographicSize = sample.orthoScale * 0.5f;
            }
            else
            {
                cam.usePhysicalProperties = true;
                cam.focalLength = Mathf.Max(1f, sample.lens);
                cam.sensorSize = new Vector2(Mathf.Max(1f, sample.sensorWidth), Mathf.Max(1f, sample.sensorHeight));
                cam.lensShift = new Vector2(sample.shiftX, sample.shiftY);
                cam.gateFit = sample.sensorFit == "HORIZONTAL"
                    ? Camera.GateFitMode.Horizontal
                    : sample.sensorFit == "VERTICAL"
                        ? Camera.GateFitMode.Vertical
                        : Camera.GateFitMode.Overscan;
            }
        }
    }
}
