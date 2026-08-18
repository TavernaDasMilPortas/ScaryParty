using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace ProceduralGraphicSystem
{
    [CreateAssetMenu(fileName = "NewShapePreset", menuName = "Celestial Cross/Shape Preset")]
    public class ShapePreset : ScriptableObject
    {
        [Serializable]
        public struct ShapePoint
        {
            public Vector2 position;
            public bool isSharp;
        }

        [Serializable]
        public struct ShapeKeyframe
        {
            public string name;
            public float time;
            [HideInInspector]
            public List<Vector2> positions;
        }

        [SerializeField, ListDrawerSettings(ShowIndexLabels = true)]
        private List<ShapePoint> _points = new List<ShapePoint>();
        
        [HideInInspector, SerializeField]
        private List<ShapeKeyframe> _keyframes = new List<ShapeKeyframe>();
        
        [SerializeField, Range(0, 32)]
        private int _splineSubdivisions = 8;
        
        [SerializeField]
        private bool _loop = true;

        public List<ShapePoint> Points => _points;
        public List<ShapeKeyframe> Keyframes => _keyframes;
        public int SplineSubdivisions => _splineSubdivisions;
        public bool Loop => _loop;

        public event Action OnPresetChanged;

        public void NotifyPresetChanged()
        {
            OnPresetChanged?.Invoke();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            NotifyPresetChanged();
        }
#endif

        private List<Vector2> _cachedSplineList = new List<Vector2>();

        public IList<Vector2> EvaluateSpline()
        {
            SplineUtil.SubdivideSpline(_points, _splineSubdivisions, _loop, _cachedSplineList);
            return _cachedSplineList;
        }

        public IList<Vector2> EvaluateSpline(IList<Vector2> currentPositions)
        {
            if (currentPositions.Count != _points.Count) return currentPositions.Count > 0 ? new List<Vector2>(currentPositions) : new List<Vector2>();
            SplineUtil.SubdivideSplineWithPositions(_points, currentPositions, _splineSubdivisions, _loop, _cachedSplineList);
            return _cachedSplineList;
        }

        public Vector2[] EvaluateAtTime(float t, bool stepped = false)
        {
            if (_keyframes == null || _keyframes.Count == 0)
            {
                Vector2[] basePos = new Vector2[_points.Count];
                for(int i=0; i<_points.Count; i++) basePos[i] = _points[i].position;
                return basePos;
            }
            if (_keyframes.Count == 1)
            {
                return PadPositions(_keyframes[0].positions);
            }

            ShapeKeyframe prev = _keyframes[0];
            ShapeKeyframe next = _keyframes[_keyframes.Count - 1];

            if (t <= _keyframes[0].time) return PadPositions(_keyframes[0].positions);
            if (t >= _keyframes[_keyframes.Count - 1].time) return PadPositions(_keyframes[_keyframes.Count - 1].positions);

            for (int i = 0; i < _keyframes.Count - 1; i++)
            {
                if (t >= _keyframes[i].time && t <= _keyframes[i + 1].time)
                {
                    prev = _keyframes[i];
                    next = _keyframes[i + 1];
                    break;
                }
            }

            float segmentT = Mathf.InverseLerp(prev.time, next.time, t);
            
            if (stepped)
            {
                return PadPositions(segmentT < 0.5f ? prev.positions : next.positions);
            }

            Vector2[] result = new Vector2[_points.Count];
            for (int i = 0; i < _points.Count; i++)
            {
                Vector2 pA = (i < prev.positions.Count) ? prev.positions[i] : _points[i].position;
                Vector2 pB = (i < next.positions.Count) ? next.positions[i] : _points[i].position;
                result[i] = Vector2.Lerp(pA, pB, segmentT);
            }
            return result;
        }

        private Vector2[] PadPositions(List<Vector2> kfPositions)
        {
            Vector2[] result = new Vector2[_points.Count];
            for (int i = 0; i < _points.Count; i++)
            {
                result[i] = (i < kfPositions.Count) ? kfPositions[i] : _points[i].position;
            }
            return result;
        }

        public ShapeKeyframe GetKeyframeByName(string name)
        {
            foreach (var kf in _keyframes)
            {
                if (kf.name == name) return kf;
            }
            throw new Exception($"Keyframe {name} não encontrado no ShapePreset {this.name}");
        }
        
        public int GetKeyframeIndexByName(string name)
        {
            for (int i = 0; i < _keyframes.Count; i++)
            {
                if (_keyframes[i].name == name) return i;
            }
            return -1;
        }
    }
}
