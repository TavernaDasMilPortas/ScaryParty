using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using Sirenix.OdinInspector;

namespace ProceduralGraphicSystem
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class ProceduralGraphic : MaskableGraphic
    {
        [SerializeField, Required]
        private ShapePreset preset;

        [SerializeField]
        private Texture texture;

        [SerializeField, ValueDropdown("GetKeyframeNames"), OnValueChanged("InitializeFromPreset")]
        [Tooltip("Deixe vazio para usar a forma base.")]
        private string startingKeyframe = "";

        private IEnumerable<string> GetKeyframeNames()
        {
            if (preset == null || preset.Keyframes == null)
            {
                yield return "";
                yield break;
            }
            
            yield return "";
            foreach (var k in preset.Keyframes)
            {
                yield return k.name;
            }
        }

        private Vector2[] _currentPositions;
        private List<int> _cachedIndices = new List<int>();

        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;

        public ShapePreset Preset => preset;
        public int PointCount => _currentPositions?.Length ?? 0;

        protected override void Awake()
        {
            base.Awake();
            InitializeFromPreset();
        }

        private void InitializeFromPreset()
        {
            if (preset == null) return;
            
            _currentPositions = new Vector2[preset.Points.Count];

            if (!string.IsNullOrEmpty(startingKeyframe))
            {
                int kfIndex = preset.GetKeyframeIndexByName(startingKeyframe);
                if (kfIndex != -1)
                {
                    var kf = preset.Keyframes[kfIndex];
                    for (int i = 0; i < preset.Points.Count; i++)
                    {
                        if (i < kf.positions.Count)
                            _currentPositions[i] = kf.positions[i];
                        else
                            _currentPositions[i] = preset.Points[i].position;
                    }
                    SetAllDirty();
                    return;
                }
            }

            for (int i = 0; i < preset.Points.Count; i++)
            {
                _currentPositions[i] = preset.Points[i].position;
            }
            SetAllDirty();
        }
        
        protected override void OnEnable()
        {
            base.OnEnable();
            if (preset != null)
            {
                preset.OnPresetChanged += HandlePresetChanged;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (preset != null)
            {
                preset.OnPresetChanged -= HandlePresetChanged;
            }
        }

        private void HandlePresetChanged()
        {
            InitializeFromPreset();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            if (preset != null && (_currentPositions == null || _currentPositions.Length != preset.Points.Count))
            {
                InitializeFromPreset();
            }
            SetVerticesDirty();
        }
#endif

        public void SetPreset(ShapePreset newPreset)
        {
            if (preset != null)
            {
                preset.OnPresetChanged -= HandlePresetChanged;
            }
            preset = newPreset;
            if (preset != null && isActiveAndEnabled)
            {
                preset.OnPresetChanged += HandlePresetChanged;
            }
            InitializeFromPreset();
        }

        public void SetPointPosition(int index, Vector2 normalizedPos)
        {
            if (_currentPositions == null || index < 0 || index >= _currentPositions.Length) return;
            _currentPositions[index] = normalizedPos;
            SetVerticesDirty();
        }

        public Vector2 GetPointPosition(int index)
        {
            if (_currentPositions == null || index < 0 || index >= _currentPositions.Length) return Vector2.zero;
            return _currentPositions[index];
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (preset == null || _currentPositions == null || _currentPositions.Length < 3)
                return;

            // 1. Resolver pontos
            IList<Vector2> evaluatedPoints = preset.EvaluateSpline(_currentPositions);
            if (evaluatedPoints.Count < 3) return;

            // 2. Calcular limites para mapeamento UV
            Rect r = GetPixelAdjustedRect();
            
            // Encontrar a caixa delimitadora (bounding box) dos pontos avaliados normalizados
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            for (int i = 0; i < evaluatedPoints.Count; i++)
            {
                minX = Mathf.Min(minX, evaluatedPoints[i].x);
                minY = Mathf.Min(minY, evaluatedPoints[i].y);
                maxX = Mathf.Max(maxX, evaluatedPoints[i].x);
                maxY = Mathf.Max(maxY, evaluatedPoints[i].y);
            }
            
            float width = maxX - minX;
            float height = maxY - minY;
            if (width == 0) width = 1;
            if (height == 0) height = 1;

            // 3. Triangulação (Ear clipping)
            EarClipping.Triangulate(evaluatedPoints, _cachedIndices);

            // 4. Preencher o VertexHelper
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;

            for (int i = 0; i < evaluatedPoints.Count; i++)
            {
                Vector2 normPos = evaluatedPoints[i];
                
                // Posição relativa ao RectTransform
                vertex.position = new Vector3(
                    Mathf.Lerp(r.xMin, r.xMax, normPos.x),
                    Mathf.Lerp(r.yMin, r.yMax, normPos.y),
                    0
                );

                // Mapeamento UV ancorado aos limites da forma
                vertex.uv0 = new Vector2(
                    (normPos.x - minX) / width,
                    (normPos.y - minY) / height
                );

                vh.AddVert(vertex);
            }

            for (int i = 0; i < _cachedIndices.Count; i += 3)
            {
                vh.AddTriangle(_cachedIndices[i], _cachedIndices[i + 1], _cachedIndices[i + 2]);
            }
        }
    }
}
