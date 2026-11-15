using System.Collections.Generic;
using UnityEngine;
using MoreMountains.Feedbacks;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    /// <summary>
    /// Pivô cosmético isolado (CosmeticPivot) para animações de squash/stretch, saltos, inclinação e pulso de cor.
    /// Nunca altera a pose autoritativa da raiz nem a escala base do VisualInstance filho.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFeelVisualPivot : MonoBehaviour
    {
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Vector3 _baseLocalPosition = Vector3.zero;
        private Quaternion _baseLocalRotation = Quaternion.identity;
        private Vector3 _baseLocalScale = Vector3.one;
        private bool _basePoseCaptured;

        private bool _isPlaying;
        private float _elapsed;
        private float _effectiveDuration;
        private float _intensityMultiplier = 1f;
        private int _variantSeed;
        private GameFeelRecipe _activeRecipe;

        // Balanço contínuo opcional (ex: mochila em movimento ou forno assando)
        private bool _hasContinuousOffset;
        private Vector3 _continuousPosOffset;
        private Vector3 _continuousEulerOffset;
        private Vector3 _continuousScaleOffset = Vector3.one;

        // Flash emissivo seguro via MaterialPropertyBlock
        private bool _hasActiveHighlight;
        private float _highlightElapsed;
        private float _highlightDuration;
        private Color _highlightColor;
        private MaterialPropertyBlock _mpb;
        private readonly List<(Renderer renderer, int slot, Color baseEmission)> _highlightCache =
            new List<(Renderer, int, Color)>();

        [Tooltip("Opcional: MMF_Player dedicado neste pivô para sobrescrever ou somar feedbacks configurados no Inspector.")]
        public MMF_Player dedicatedFeelPlayer;

        public bool LockVerticalAndHipOffset { get; set; }
        public bool ClampAsCharacter { get; set; }
        public int VisualGeneration { get; set; }
        public bool IsPlaying => _isPlaying;

        private void Awake()
        {
            if (!_basePoseCaptured)
            {
                CaptureCurrentAsBasePose();
            }
        }

        public void CaptureCurrentAsBasePose()
        {
            _baseLocalPosition = transform.localPosition;
            _baseLocalRotation = transform.localRotation;
            _baseLocalScale = transform.localScale;
            _basePoseCaptured = true;
        }

        public void SetExplicitBasePose(Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            _baseLocalPosition = localPos;
            _baseLocalRotation = localRot;
            _baseLocalScale = localScale;
            _basePoseCaptured = true;
            ApplyComposedPose(Vector3.zero, Quaternion.identity, Vector3.one);
        }

        public void GetBasePose(out Vector3 localPos, out Quaternion localRot, out Vector3 localScale)
        {
            if (!_basePoseCaptured) CaptureCurrentAsBasePose();
            localPos = _baseLocalPosition;
            localRot = _baseLocalRotation;
            localScale = _baseLocalScale;
        }

        public void PlayRecipe(in GameFeelRecipe recipe, float intensityMultiplier, int variantSeed)
        {
            if (!_basePoseCaptured) CaptureCurrentAsBasePose();

            if (dedicatedFeelPlayer != null && GameFeelSettings.Preset != GameFeelPresetMode.Reduzido)
            {
                dedicatedFeelPlayer.PlayFeedbacks(transform.position, Mathf.Clamp(intensityMultiplier, 0.2f, 1.5f));
            }

            if (intensityMultiplier <= 0.001f || GameFeelSettings.ReduceMotion)
            {
                _isPlaying = false;
                ApplyComposedPose(Vector3.zero, Quaternion.identity, Vector3.one);
                return;
            }

            _activeRecipe = recipe;
            _intensityMultiplier = Mathf.Clamp(intensityMultiplier, 0f, 1.5f);
            _variantSeed = variantSeed;
            _effectiveDuration = Mathf.Max(0.04f, recipe.Duration);
            _elapsed = 0f;
            _isPlaying = true;
        }

        public void SetContinuousOffset(Vector3 localPosOffset, Vector3 localEulerOffset, Vector3 localScaleFactor)
        {
            if (!_basePoseCaptured) CaptureCurrentAsBasePose();
            _hasContinuousOffset = true;
            _continuousPosOffset = localPosOffset;
            _continuousEulerOffset = localEulerOffset;
            _continuousScaleOffset = localScaleFactor;

            if (!_isPlaying)
            {
                ApplyComposedPose(Vector3.zero, Quaternion.identity, Vector3.one);
            }
        }

        public void ClearContinuousOffset()
        {
            _hasContinuousOffset = false;
            _continuousPosOffset = Vector3.zero;
            _continuousEulerOffset = Vector3.zero;
            _continuousScaleOffset = Vector3.one;

            if (!_isPlaying)
            {
                ResetToBasePose();
            }
        }

        public void PulseMaterialHighlight(Color emissionTint, float duration = 0.22f)
        {
            if (!GameFeelSettings.AllowFlashes || duration <= 0.01f) return;

            ClearMaterialHighlight();
            _mpb ??= new MaterialPropertyBlock();

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                int slotCount = r.sharedMaterials != null ? Mathf.Max(1, r.sharedMaterials.Length) : 1;
                for (int s = 0; s < slotCount; s++)
                {
                    var mat = r.sharedMaterials != null && s < r.sharedMaterials.Length ? r.sharedMaterials[s] : null;
                    if (mat == null || !mat.HasProperty(EmissionColorId)) continue;

                    r.GetPropertyBlock(_mpb, s);
                    Color baseEm = _mpb.GetColor(EmissionColorId);
                    if (baseEm.maxColorComponent <= 0.0001f && mat.HasProperty(EmissionColorId))
                    {
                        baseEm = mat.GetColor(EmissionColorId);
                    }
                    _highlightCache.Add((r, s, baseEm));
                }
            }

            if (_highlightCache.Count > 0)
            {
                _hasActiveHighlight = true;
                _highlightElapsed = 0f;
                _highlightDuration = duration;
                _highlightColor = emissionTint;
            }
        }

        public void StopAndReset()
        {
            _isPlaying = false;
            _hasContinuousOffset = false;
            _continuousPosOffset = Vector3.zero;
            _continuousEulerOffset = Vector3.zero;
            _continuousScaleOffset = Vector3.one;
            ClearMaterialHighlight();
            ResetToBasePose();
        }

        public void ResetToBasePose()
        {
            if (!_basePoseCaptured) CaptureCurrentAsBasePose();
            transform.localPosition = _baseLocalPosition;
            transform.localRotation = _baseLocalRotation;
            transform.localScale = _baseLocalScale;
        }

        private void Update()
        {
            if (_isPlaying)
            {
                _elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_elapsed / _effectiveDuration);
                EvaluateRecipe(t, out var posOff, out var rotOff, out var scaleFactor);
                ApplyComposedPose(posOff, rotOff, scaleFactor);

                if (t >= 1f)
                {
                    _isPlaying = false;
                    ApplyComposedPose(Vector3.zero, Quaternion.identity, Vector3.one);
                }
            }
            else if (_hasContinuousOffset)
            {
                ApplyComposedPose(Vector3.zero, Quaternion.identity, Vector3.one);
            }

            if (_hasActiveHighlight)
            {
                _highlightElapsed += Time.deltaTime;
                float ht = Mathf.Clamp01(_highlightElapsed / _highlightDuration);
                float weight = Mathf.Sin(ht * Mathf.PI); // sobe suave e volta a zero
                ApplyHighlightWeight(weight);

                if (ht >= 1f)
                {
                    ClearMaterialHighlight();
                }
            }
        }

        public void EvaluateRecipeForTest(in GameFeelRecipe recipe, float t, float intensityMultiplier, int variantSeed,
            out Vector3 posOffset, out Quaternion rotOffset, out Vector3 scaleFactor)
        {
            _activeRecipe = recipe;
            _intensityMultiplier = Mathf.Clamp(intensityMultiplier, 0f, 1.5f);
            _variantSeed = variantSeed;
            EvaluateRecipe(Mathf.Clamp01(t), out posOffset, out rotOffset, out scaleFactor);
        }

        private void EvaluateRecipe(float t, out Vector3 posOffset, out Quaternion rotOffset, out Vector3 scaleFactor)
        {
            float imp = Mathf.Clamp(_activeRecipe.ImpactFraction, 0.05f, 0.85f);
            float reb = Mathf.Clamp(_activeRecipe.ReboundFraction, imp + 0.05f, 0.95f);
            float ant = Mathf.Clamp(_activeRecipe.AnticipationFraction, 0f, imp - 0.02f);

            float dirSign = ((_variantSeed & 1) == 0) ? 1f : -1f;
            float asymDelay = GameFeelSettings.EnableSloppyAsymmetry ? _activeRecipe.AsymmetryRatio : 0f;

            float sx = EvaluateAxisScale(t, ant, imp, reb, _activeRecipe.AnticipationScale.x, _activeRecipe.SquashScale.x, _activeRecipe.ReboundScale.x);
            float sy = EvaluateAxisScale(t, ant, imp, reb, _activeRecipe.AnticipationScale.y, _activeRecipe.SquashScale.y, _activeRecipe.ReboundScale.y);
            float sz = EvaluateAxisScale(Mathf.Clamp01(t - asymDelay), ant, imp, reb, _activeRecipe.AnticipationScale.z, _activeRecipe.SquashScale.z, _activeRecipe.ReboundScale.z);

            Vector3 rawScale = Vector3.LerpUnclamped(Vector3.one, new Vector3(sx, sy, sz), _intensityMultiplier);
            scaleFactor = ClampAsCharacter
                ? GameFeelSettings.ClampCharacterLocalScale(rawScale)
                : GameFeelSettings.ClampItemLocalScale(rawScale);

            float arc = 0f;
            if (!LockVerticalAndHipOffset && _activeRecipe.ArcHeight > 0.0001f)
            {
                if (t < imp)
                {
                    float u = t / imp;
                    arc = Mathf.Sin(u * Mathf.PI) * _activeRecipe.ArcHeight * _intensityMultiplier;
                }
                else
                {
                    float u = (t - imp) / Mathf.Max(0.01f, 1f - imp);
                    arc = Mathf.Abs(Mathf.Sin(u * Mathf.PI)) * (_activeRecipe.ArcHeight * 0.25f) * (1f - u) * _intensityMultiplier;
                }
            }

            posOffset = new Vector3(0f, arc, 0f);

            float maxTilt = GameFeelSettings.ClampTiltDegrees(_activeRecipe.MaxTiltDegrees * _intensityMultiplier);
            if (LockVerticalAndHipOffset)
            {
                maxTilt = Mathf.Min(maxTilt, 4f);
            }

            float dampedWave = Mathf.Sin(t * Mathf.PI * 2.25f) * Mathf.Exp(-3.2f * t);
            float pitch = dampedWave * maxTilt * 0.45f;
            float roll = dampedWave * maxTilt * dirSign;
            rotOffset = Quaternion.Euler(pitch, 0f, roll);
        }

        private static float EvaluateAxisScale(float t, float ant, float imp, float reb, float antVal, float squashVal, float rebVal)
        {
            if (ant > 0.001f && t < ant)
            {
                float u = t / ant;
                return Mathf.Lerp(1f, antVal, Mathf.SmoothStep(0f, 1f, u));
            }
            if (t < imp)
            {
                float u = (t - ant) / Mathf.Max(0.001f, imp - ant);
                float start = ant > 0.001f ? antVal : 1f;
                return Mathf.Lerp(start, squashVal, Mathf.SmoothStep(0f, 1f, u));
            }
            if (t < reb)
            {
                float u = (t - imp) / Mathf.Max(0.001f, reb - imp);
                return Mathf.Lerp(squashVal, rebVal, Mathf.SmoothStep(0f, 1f, u));
            }
            {
                float u = (t - reb) / Mathf.Max(0.001f, 1f - reb);
                return Mathf.Lerp(rebVal, 1f, Mathf.SmoothStep(0f, 1f, u));
            }
        }

        private void ApplyComposedPose(Vector3 animPosOffset, Quaternion animRotOffset, Vector3 animScaleFactor)
        {
            Vector3 posOff = animPosOffset + (_hasContinuousOffset ? _continuousPosOffset : Vector3.zero);
            if (LockVerticalAndHipOffset)
            {
                posOff.y = 0f;
            }

            Quaternion contRot = _hasContinuousOffset ? Quaternion.Euler(_continuousEulerOffset) : Quaternion.identity;
            Quaternion rot = _baseLocalRotation * contRot * animRotOffset;

            Vector3 combinedScale = Vector3.Scale(animScaleFactor, _hasContinuousOffset ? _continuousScaleOffset : Vector3.one);
            combinedScale = ClampAsCharacter
                ? GameFeelSettings.ClampCharacterLocalScale(combinedScale)
                : GameFeelSettings.ClampItemLocalScale(combinedScale);

            transform.localPosition = _baseLocalPosition + posOff;
            transform.localRotation = rot;
            transform.localScale = Vector3.Scale(_baseLocalScale, combinedScale);
        }

        private void ApplyHighlightWeight(float weight)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < _highlightCache.Count; i++)
            {
                var (r, slot, baseEm) = _highlightCache[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb, slot);
                _mpb.SetColor(EmissionColorId, Color.Lerp(baseEm, baseEm + _highlightColor, weight));
                r.SetPropertyBlock(_mpb, slot);
            }
        }

        private void ClearMaterialHighlight()
        {
            if (_hasActiveHighlight && _mpb != null)
            {
                for (int i = 0; i < _highlightCache.Count; i++)
                {
                    var (r, slot, baseEm) = _highlightCache[i];
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb, slot);
                    _mpb.SetColor(EmissionColorId, baseEm);
                    r.SetPropertyBlock(_mpb, slot);
                }
            }
            _highlightCache.Clear();
            _hasActiveHighlight = false;
        }

        private void OnDisable()
        {
            StopAndReset();
        }

        private void OnDestroy()
        {
            ClearMaterialHighlight();
        }
    }
}
