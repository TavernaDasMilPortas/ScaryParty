using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace ProceduralGraphicSystem
{
    [RequireComponent(typeof(ProceduralGraphic))]
    [ExecuteAlways]
    public class PaperCutBorderGenerator : MonoBehaviour
    {
        [Title("Target")]
        [Tooltip("A imagem que servirá de molde para o recorte")]
        public Image targetImage;

        [Tooltip("Opcional: O SpriteRenderer que servirá de molde para o recorte")]
        public SpriteRenderer targetSpriteRenderer;

        public Sprite TargetSprite => targetImage != null ? targetImage.sprite : (targetSpriteRenderer != null ? targetSpriteRenderer.sprite : null);

        [Tooltip("Atualiza a borda automaticamente se a imagem (Sprite) mudar. Útil para animações 2D quadro a quadro.")]
        public bool autoUpdateOnChange = true;

        [Title("Optimization")]
        [Tooltip("Resolução máxima para processamento do contorno. Valores menores são muito mais rápidos.")]
        [Range(32, 4096)]
        public int maxProcessingResolution = 2048;

        [Tooltip("Linhas processadas por frame ao calcular a forma assincronamente.")]
        public int linesPerBatch = 64;

        [Title("Settings")]
        [Tooltip("Expansão da borda para fora (ex: 0.15 = 15%)")]
        [Range(0f, 1f)]
        public float borderExpansion = 0.15f;

        [Tooltip("Quantidade de irregularidade no corte do papel")]
        [Range(0f, 0.2f)]
        public float jitterAmount = 0.03f;

        [Tooltip("Quantos pontos a borda de papel deve ter")]
        [Range(4, 128)]
        public int borderPointCount = 24;

        [Tooltip("Seed fixa para a aleatoriedade do corte (mesmo valor = mesma forma)")]
        public int randomSeed = 42;
        
        [Tooltip("Limiar do canal alpha para considerar como contorno")]
        [Range(0.01f, 1f)]
        public float alphaThreshold = 0.5f;

        [Tooltip("Preenche espaços vazios e concavidades (como braços na cintura). Um valor maior une vãos maiores (em pixels). 0 = Desativado.")]
        [Range(0, 50)]
        public int closeGapsRadius = 10;

        private ProceduralGraphic _graphic;
        private Sprite _lastProcessedSprite;
        private ShapePreset _runtimePreset;

        private Dictionary<Sprite, List<Vector2>> _borderCache = new Dictionary<Sprite, List<Vector2>>();
        private Coroutine _generationCoroutine;

        private void Awake()
        {
            _graphic = GetComponent<ProceduralGraphic>();
        }

        private void Update()
        {
            if (autoUpdateOnChange && TargetSprite != null)
            {
                if (TargetSprite != _lastProcessedSprite)
                {
                    _lastProcessedSprite = TargetSprite;
                    if (Application.isPlaying)
                    {
                        if (_generationCoroutine != null) StopCoroutine(_generationCoroutine);
                        _generationCoroutine = StartCoroutine(GenerateBorderAsync());
                    }
                    else
                    {
                        GenerateBorder();
                    }
                }
            }
        }

        [Button(ButtonSizes.Large), GUIColor(1f, 0.9f, 0.5f)]
        public void GenerateBorder()
        {
            if (_graphic == null) _graphic = GetComponent<ProceduralGraphic>();

            Sprite sprite = TargetSprite;

            if (sprite == null || sprite.texture == null)
            {
                if (_graphic.Preset != null && _runtimePreset != null)
                {
                    _runtimePreset.Points.Clear();
                    _graphic.SetVerticesDirty();
                }
                return;
            }

            Texture2D tex = sprite.texture;
            if (!tex.isReadable)
            {
                Debug.LogWarning($"[PaperCutBorder] A textura '{tex.name}' não possui 'Read/Write' ativado nas import settings.");
                return;
            }

            Color[] pixels = tex.GetPixels();
            Color[] downsampled = ContourExtractor.DownsamplePixels(pixels, tex.width, tex.height, out int newWidth, out int newHeight, maxProcessingResolution);

            // 1. Extrair
            var contour = ContourExtractor.ExtractContour(downsampled, newWidth, newHeight, alphaThreshold, closeGapsRadius);
            if (contour.Count == 0) return;

            // 2. Simplificar
            var simplified = ContourExtractor.SimplifyContour(contour, borderPointCount);
            
            // 3. Normalizar
            var normalized = ContourExtractor.NormalizeContour(simplified, newWidth, newHeight);

            // 4. Expandir
            var expanded = ContourExtractor.ExpandContour(normalized, borderExpansion);

            // 5. Jitter
            var finalPoints = ContourExtractor.ApplyJitter(expanded, jitterAmount, randomSeed);

            ApplyPointsToPreset(finalPoints);
            _lastProcessedSprite = sprite;
        }

        private global::System.Collections.IEnumerator GenerateBorderAsync()
        {
            if (_graphic == null) _graphic = GetComponent<ProceduralGraphic>();

            Sprite currentSprite = TargetSprite;

            if (currentSprite == null || currentSprite.texture == null)
            {
                if (_graphic.Preset != null && _runtimePreset != null)
                {
                    _runtimePreset.Points.Clear();
                    _graphic.SetVerticesDirty();
                }
                yield break;
            }

            if (_borderCache.TryGetValue(currentSprite, out List<Vector2> cachedPoints))
            {
                ApplyPointsToPreset(cachedPoints);
                yield break;
            }

            Texture2D tex = currentSprite.texture;
            if (!tex.isReadable)
            {
                Debug.LogWarning($"[PaperCutBorder] A textura '{tex.name}' não possui 'Read/Write' ativado nas import settings.");
                yield break;
            }

            Color[] pixels = tex.GetPixels();
            yield return null;

            Color[] downsampled = ContourExtractor.DownsamplePixels(pixels, tex.width, tex.height, out int newWidth, out int newHeight, maxProcessingResolution);
            yield return null;

            List<Vector2> contour = null;
            var extractRoutine = ContourExtractor.ExtractContourAsync(downsampled, newWidth, newHeight, alphaThreshold, closeGapsRadius, result => contour = result, linesPerBatch);
            while (extractRoutine.MoveNext())
            {
                yield return extractRoutine.Current;
            }

            if (contour == null || contour.Count == 0) yield break;

            var simplified = ContourExtractor.SimplifyContour(contour, borderPointCount);
            var normalized = ContourExtractor.NormalizeContour(simplified, newWidth, newHeight);
            var expanded = ContourExtractor.ExpandContour(normalized, borderExpansion);
            var finalPoints = ContourExtractor.ApplyJitter(expanded, jitterAmount, randomSeed);

            _borderCache[currentSprite] = finalPoints;
            ApplyPointsToPreset(finalPoints);
        }

        private void ApplyPointsToPreset(List<Vector2> finalPoints)
        {
            if (_runtimePreset == null)
            {
                _runtimePreset = ScriptableObject.CreateInstance<ShapePreset>();
                _runtimePreset.name = "RuntimePaperCutPreset";
            }
            
            _runtimePreset.Points.Clear();
            foreach (var p in finalPoints)
            {
                _runtimePreset.Points.Add(new ShapePreset.ShapePoint 
                { 
                    position = p, 
                    isSharp = true 
                });
            }

            _graphic.SetPreset(_runtimePreset);
        }

        [Button("Clear Cache"), GUIColor(1f, 0.5f, 0.5f)]
        public void ClearCache()
        {
            _borderCache.Clear();
            Debug.Log("[PaperCutBorder] Cache cleared.");
        }
    }
}
