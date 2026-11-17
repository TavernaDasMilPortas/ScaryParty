using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Cutscenes
{
    [Serializable]
    public sealed class CutsceneManifest
    {
        public const string CurrentSchemaVersion = "1.0.0";

        public string schemaVersion;
        public SequenceInfo sequence = new SequenceInfo();
        public CoordinateContract coordinates = new CoordinateContract();
        public string fbxFile;
        public List<ManifestObject> objects = new List<ManifestObject>();
        public List<ManifestCamera> cameras = new List<ManifestCamera>();
        public List<ManifestShot> shots = new List<ManifestShot>();
        public List<ManifestAudioEvent> audioEvents = new List<ManifestAudioEvent>();
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public sealed class SequenceInfo
    {
        public string id;
        public float fps = 24f;
        public float fpsBase = 1f;
        public float effectiveFps = 24f;
        public int frameStart = 1;
        public int frameEnd = 120;
        public float duration;
        public int resolutionX = 1920;
        public int resolutionY = 1080;

        public float ComputeEffectiveFps()
        {
            return fpsBase > 0f ? fps / fpsBase : fps;
        }

        public float ComputeDurationSeconds()
        {
            float eff = ComputeEffectiveFps();
            if (eff <= 0f) return 0f;
            return (frameEnd - frameStart + 1) / eff;
        }
    }

    [Serializable]
    public sealed class CoordinateContract
    {
        public string unit = "meters";
        public string axisConvention = "BLENDER_Z_UP_RIGHT_HANDED";
        public string sampleSpace = "WORLD_RELATIVE_TO_ORIGIN";
    }

    [Serializable]
    public sealed class ManifestObject
    {
        public string id;
        public string name;
        public string parentId;
        public string type;
        public string fbxNodePath;
        public List<string> materialSlots = new List<string>();
    }

    [Serializable]
    public sealed class ManifestCamera
    {
        public string id;
        public string name;
        public string projection = "PERSP";
        public List<CameraSample> samples = new List<CameraSample>();
    }

    [Serializable]
    public sealed class CameraSample
    {
        public int frame;
        public float time;
        public float[] position = new float[3];
        public float[] rotation = new float[4];
        public float lens = 50f;
        public float sensorWidth = 36f;
        public float sensorHeight = 24f;
        public string sensorFit = "AUTO";
        public float shiftX;
        public float shiftY;
        public float clipStart = 0.1f;
        public float clipEnd = 1000f;
        public float orthoScale = 6f;
    }

    [Serializable]
    public sealed class ManifestShot
    {
        public int startFrame;
        public float startTime;
        public string cameraId;
        public string cameraName;
    }

    [Serializable]
    public sealed class ManifestAudioEvent
    {
        public string id;
        public float time;
        public string resourcePath;
        public string seekPolicy = "StopOnSkip";
    }

    public static class CutsceneManifestParser
    {
        public static bool TryParse(string json, out CutsceneManifest manifest, out string error)
        {
            manifest = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Manifest JSON is empty.";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<CutsceneManifest>(json);
            }
            catch (Exception ex)
            {
                error = $"Failed to parse manifest JSON: {ex.Message}";
                return false;
            }

            if (manifest == null)
            {
                error = "Deserialized manifest is null.";
                return false;
            }

            if (string.IsNullOrEmpty(manifest.schemaVersion))
            {
                error = "Manifest is missing schemaVersion.";
                return false;
            }

            if (manifest.sequence == null || string.IsNullOrEmpty(manifest.sequence.id))
            {
                error = "Manifest sequence.id is required.";
                return false;
            }

            if (manifest.sequence.frameEnd < manifest.sequence.frameStart)
            {
                error = $"Invalid frame range: {manifest.sequence.frameStart}..{manifest.sequence.frameEnd}.";
                return false;
            }

            manifest.sequence.effectiveFps = manifest.sequence.ComputeEffectiveFps();
            if (manifest.sequence.duration <= 0f)
            {
                manifest.sequence.duration = manifest.sequence.ComputeDurationSeconds();
            }

            if (manifest.shots != null)
            {
                manifest.shots.Sort((a, b) => a.startFrame.CompareTo(b.startFrame));
                for (int i = 1; i < manifest.shots.Count; i++)
                {
                    if (manifest.shots[i].startFrame == manifest.shots[i - 1].startFrame)
                    {
                        error = $"Ambiguous shot at frame {manifest.shots[i].startFrame}.";
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
