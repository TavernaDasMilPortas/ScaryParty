using UnityEngine;
using UnityEngine.UI;

namespace ScaryParty.Pizzeria.Presentation
{
    /// <summary>
    /// Barra de progresso 3D (World Space Billboard) exibida sobre bancadas e forno da pizzaria.
    /// Suporta customização procedural de alta fidelidade (cantos arredondados, bordas, reflexo,
    /// interpolação suave e pulso de conclusão) ou uso de Prefab em Resources/PizzeriaUI/StationProgressBar.
    /// </summary>
    [DisallowMultipleComponent]
    public class StationProgressBar : MonoBehaviour
    {
        public const string PrefabResourcePath = "PizzeriaUI/StationProgressBar";

        [Header("Dimensões e Proporção")]
        [Tooltip("Tamanho da barra de progresso no Canvas (largura x altura em pixels do canvas).")]
        [SerializeField] private Vector2 barSize = new Vector2(140f, 18f);

        [Tooltip("Espessura da borda/moldura externa em pixels.")]
        [SerializeField] private float borderThickness = 2f;

        [Tooltip("Escala no espaço 3D do mundo.")]
        [SerializeField] private float worldScale = 0.005f;

        [Header("Estilo Visual e Cores")]
        [Tooltip("Cor da borda externa.")]
        [SerializeField] private Color borderColor = new Color(0.10f, 0.10f, 0.12f, 0.95f);

        [Tooltip("Cor de fundo do trilho da barra.")]
        [SerializeField] private Color backgroundColor = new Color(0.04f, 0.04f, 0.05f, 0.85f);

        [Tooltip("Utilizar cantos arredondados suavizados (9-slice).")]
        [SerializeField] private bool useRoundedCorners = true;

        [Tooltip("Exibir reflexo sutil na metade superior da barra (efeito vidro).")]
        [SerializeField] private bool showGlossOverlay = true;

        [Header("Texto de Porcentagem")]
        [Tooltip("Exibir a porcentagem numérica no centro da barra.")]
        [SerializeField] private bool showPercentageText = false;

        [Tooltip("Fonte customizada para o texto de porcentagem. Se vazio, utiliza automaticamente as fontes da UI (CastleChunk/Piaria).")]
        [SerializeField] private Font customFont;

        [Header("Animação e Feedback")]
        [Tooltip("Interpolar o preenchimento suavemente.")]
        [SerializeField] private bool smoothFill = true;

        [Tooltip("Velocidade da transição suave.")]
        [SerializeField] private float smoothSpeed = 10f;

        [Tooltip("Efeito de pulso suave ao concluir (100%) ou quando estiver queimando (vermelho).")]
        [SerializeField] private bool enablePulseFeedback = true;

        [Header("Referências UI")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private Image _borderImage;
        [SerializeField] private Image _bgImage;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Image _glossImage;
        [SerializeField] private Text _percentText;

        private Transform _mainCamera;
        private float _targetProgress = 0f;
        private float _currentProgress = 0f;
        private Color _targetColor = Color.green;
        private Vector3 _baseScale;
        private bool _isInitialized = false;

        private static Sprite s_roundedSprite;

        public Canvas Canvas => _canvas;
        public Image FillImage => _fillImage;
        public Image BgImage => _bgImage;
        public Image BorderImage => _borderImage;
        public float WorldScale => worldScale;

        public static StationProgressBar Create(Transform parent)
        {
            // 1. Tentar carregar prefab customizado configurado pelo usuário em Resources
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            GameObject go;
            StationProgressBar bar;

            if (prefab != null)
            {
                go = Instantiate(prefab, parent, false);
                bar = go.GetComponent<StationProgressBar>();
                if (bar == null)
                    bar = go.AddComponent<StationProgressBar>();
            }
            else
            {
                go = new GameObject("StationProgressBar");
                go.transform.SetParent(parent, false);
                bar = go.AddComponent<StationProgressBar>();
                bar.BuildProceduralUI();
            }

            // Offset padrão acima do slot anchor
            go.transform.localPosition = new Vector3(0, 0.4f, 0);
            go.transform.localScale = Vector3.one * bar.worldScale;
            bar._baseScale = go.transform.localScale;

            bar.Hide();
            return bar;
        }

        public void BuildProceduralUI()
        {
            if (_isInitialized) return;
            _isInitialized = true;

            // Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
                _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            _canvas.sortingOrder = 100;

            var rt = GetComponent<RectTransform>();
            if (rt == null)
                rt = gameObject.AddComponent<RectTransform>();
            rt.sizeDelta = barSize;

            Sprite rounded = useRoundedCorners ? GetRoundedSprite() : null;

            // Frame / Borda Externa
            GameObject borderObj = new GameObject("Frame");
            borderObj.transform.SetParent(transform, false);
            _borderImage = borderObj.AddComponent<Image>();
            _borderImage.color = borderColor;
            if (rounded != null)
            {
                _borderImage.sprite = rounded;
                _borderImage.type = Image.Type.Sliced;
            }
            var borderRt = _borderImage.rectTransform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.sizeDelta = Vector2.zero;

            // Track / Fundo
            GameObject bgObj = new GameObject("Track");
            bgObj.transform.SetParent(borderObj.transform, false);
            _bgImage = bgObj.AddComponent<Image>();
            _bgImage.color = backgroundColor;
            if (rounded != null)
            {
                _bgImage.sprite = rounded;
                _bgImage.type = Image.Type.Sliced;
            }
            var bgRt = _bgImage.rectTransform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(borderThickness, borderThickness);
            bgRt.offsetMax = new Vector2(-borderThickness, -borderThickness);

            // Fill / Barra de Preenchimento
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(bgObj.transform, false);
            _fillImage = fillObj.AddComponent<Image>();
            _fillImage.color = Color.green;
            if (rounded != null)
            {
                _fillImage.sprite = rounded;
                _fillImage.type = Image.Type.Sliced;
            }
            var fillRt = _fillImage.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.sizeDelta = Vector2.zero;

            // Gloss / Efeito Vidro
            GameObject glossObj = new GameObject("Gloss");
            glossObj.transform.SetParent(bgObj.transform, false);
            _glossImage = glossObj.AddComponent<Image>();
            _glossImage.color = new Color(1f, 1f, 1f, 0.15f);
            if (rounded != null)
            {
                _glossImage.sprite = rounded;
                _glossImage.type = Image.Type.Sliced;
            }
            var glossRt = _glossImage.rectTransform;
            glossRt.anchorMin = new Vector2(0f, 0.5f);
            glossRt.anchorMax = new Vector2(1f, 1f);
            glossRt.sizeDelta = Vector2.zero;
            glossRt.offsetMin = Vector2.zero;
            glossRt.offsetMax = Vector2.zero;
            _glossImage.raycastTarget = false;
            glossObj.SetActive(showGlossOverlay);

            // Texto de Porcentagem
            GameObject textObj = new GameObject("PercentageText");
            textObj.transform.SetParent(transform, false);
            _percentText = textObj.AddComponent<Text>();
            Font font = customFont != null ? customFont : ResolveFont();
            if (font != null)
            {
                _percentText.font = font;
                customFont = font;
            }
            _percentText.alignment = TextAnchor.MiddleCenter;
            _percentText.fontSize = 12;
            _percentText.color = Color.white;
            _percentText.raycastTarget = false;

            var outline = textObj.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.95f);
            outline.effectDistance = new Vector2(1f, -1f);

            var textRt = _percentText.rectTransform;
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textObj.SetActive(showPercentageText);

            if (Camera.main != null)
                _mainCamera = Camera.main.transform;
        }

        private void Awake()
        {
            if (_baseScale == Vector3.zero)
                _baseScale = transform.localScale != Vector3.zero ? transform.localScale : Vector3.one * worldScale;
            if (Camera.main != null)
                _mainCamera = Camera.main.transform;
        }

        private void Update()
        {
            if (_mainCamera == null && Camera.main != null)
            {
                _mainCamera = Camera.main.transform;
            }

            if (_canvas == null || !_canvas.enabled)
                return;

            // Billboard effect: sempre encarar a câmera
            if (_mainCamera != null)
            {
                transform.rotation = _mainCamera.rotation;
            }

            // Interpolação suave do preenchimento
            if (smoothFill)
            {
                _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, Time.deltaTime * smoothSpeed);
            }
            else
            {
                _currentProgress = _targetProgress;
            }

            if (_fillImage != null)
            {
                _fillImage.rectTransform.anchorMax = new Vector2(_currentProgress, 1f);
                _fillImage.color = _targetColor;
            }

            // Atualização de texto de porcentagem
            if (_percentText != null && showPercentageText)
            {
                _percentText.text = $"{Mathf.RoundToInt(_currentProgress * 100f)}%";
            }

            // Efeito de pulso quando atinge 100% ou em estado crítico (vermelho de queima)
            if (enablePulseFeedback && (_targetProgress >= 0.99f || _targetColor == Color.red))
            {
                float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 8f);
                transform.localScale = _baseScale * pulse;
            }
            else
            {
                transform.localScale = _baseScale;
            }
        }

        public void SetProgress(float normalizedProgress, Color color)
        {
            if (_canvas != null && !_canvas.enabled)
            {
                _canvas.enabled = true;
                _currentProgress = normalizedProgress; // Evita salto na primeira aparição
            }

            _targetProgress = Mathf.Clamp01(normalizedProgress);
            _targetColor = color;

            if (!smoothFill)
            {
                _currentProgress = _targetProgress;
                if (_fillImage != null)
                {
                    _fillImage.rectTransform.anchorMax = new Vector2(_currentProgress, 1f);
                    _fillImage.color = _targetColor;
                }
            }
        }

        public void Hide()
        {
            if (_canvas != null && _canvas.enabled)
            {
                _canvas.enabled = false;
            }
            _currentProgress = 0f;
            _targetProgress = 0f;
            if (_fillImage != null)
            {
                _fillImage.rectTransform.anchorMax = new Vector2(0f, 1f);
            }
            transform.localScale = _baseScale;
        }

        public static Sprite GetRoundedSprite()
        {
            if (s_roundedSprite != null) return s_roundedSprite;

            const int size = 32;
            const int radius = 7;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "StationProgressBar_RoundedSprite",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0;
                    float dy = 0;

                    if (x < radius) dx = radius - x - 0.5f;
                    else if (x >= size - radius) dx = x - (size - radius) + 0.5f;

                    if (y < radius) dy = radius - y - 0.5f;
                    else if (y >= size - radius) dy = y - (size - radius) + 0.5f;

                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 1f;
                    if (dist > radius)
                    {
                        alpha = Mathf.Clamp01(1f - (dist - radius));
                    }

                    byte a = (byte)(alpha * 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            s_roundedSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius)
            );

            return s_roundedSprite;
        }

        public static Font ResolveFont()
        {
            // 1. Tentar obter as fontes do catálogo oficial da UI (P.I.A.R.I.A)
            try
            {
                var catalog = ScaryParty.UI.Piaria.Core.PiariaUIAssetCatalog.Instance;
                if (catalog != null)
                {
                    if (catalog.fontCastleChunk != null) return catalog.fontCastleChunk;
                    if (catalog.fontMunstar != null) return catalog.fontMunstar;
                    if (catalog.fontLostTumbler != null) return catalog.fontLostTumbler;
                    if (catalog.fontDeanna != null) return catalog.fontDeanna;
                }
            }
            catch { }

#if UNITY_EDITOR
            // 2. Busca direta no AssetDatabase durante execução no Editor
            try
            {
                Font fontAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/castle-chunk/CastleChunk-Regular.ttf")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/munstar/Munstar-Regular.ttf")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/lost-tumbler/Lost Tumbler.ttf")
                              ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Piaaria/Art/UI/Piaaria/uis giugiu/fonts/deanna/Deanna/Deanna.ttf");
                if (fontAsset != null) return fontAsset;
            }
            catch { }
#endif

            // 3. Fallback de fonte runtime nativa do Unity (LegacyRuntime.ttf)
            try
            {
                Font legacy = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (legacy != null) return legacy;
            }
            catch { }

            // 4. Fallback do sistema operacional
            return Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Segoe UI", "Tahoma" }, 12);
        }
    }
}
