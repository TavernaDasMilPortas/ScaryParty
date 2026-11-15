using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.VisualStyles;
using UnityEngine;

namespace ScaryParty.Pizzeria.Stations
{
    [RequireComponent(typeof(AudioSource))]
    public class PhoneRinger : MonoBehaviour
    {
        private const float RingCycleDuration = 3.0f;

        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip customRingClip;

        [Header("Visual")]
        public Light beaconLight;
        public Renderer beaconRenderer;
        [SerializeField] private Transform phoneVisualPivot;
        public Color ringingColor = Color.yellow;
        public Color idleColor = Color.gray;

        private bool _isRinging = false;
        private static AudioClip _proceduralClip;
        private MaterialPropertyBlock _beaconMpb;
        private float _ringStartTime;
        private int _lastPulsePhase;
        private bool _pivotBaseCaptured;
        private Vector3 _pivotBaseLocalPos;
        private Quaternion _pivotBaseLocalRot = Quaternion.identity;

        public bool IsRinging => _isRinging;

        private void Awake()
        {
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            audioSource.playOnAwake = false;
            audioSource.loop = true;
            audioSource.spatialBlend = 1.0f; // 3D sound
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 20f;

            _beaconMpb = new MaterialPropertyBlock();
            ApplyBeaconColor(idleColor, Color.black);
            CapturePivotBaseIfAvailable();

            if (beaconLight == null)
            {
                // Criar luz local suave se não configurada
                var lightObj = new GameObject("RingerBeaconLight");
                lightObj.transform.SetParent(transform, false);
                lightObj.transform.localPosition = Vector3.up * 0.5f;
                beaconLight = lightObj.AddComponent<Light>();
                beaconLight.type = LightType.Point;
                beaconLight.color = ringingColor;
                beaconLight.intensity = 0f;
                beaconLight.range = 4f;
            }
        }

        private void Update()
        {
            if (!_isRinging) return;

            float elapsed = Mathf.Max(0f, Time.time - _ringStartTime);
            float cycleTime = elapsed % RingCycleDuration;
            int pulsePhase = cycleTime < 0.4f ? 1 : (cycleTime >= 0.6f && cycleTime < 1.0f ? 2 : 0);

            if (pulsePhase != 0 && pulsePhase != _lastPulsePhase)
            {
                GameFeelPresenter.EnsureInstance().TriggerSimple(
                    GameFeelEventId.PhoneRingPulse,
                    ResolvePivotTransform() ?? transform,
                    0,
                    0,
                    0u,
                    GameFeelMaterialFamily.RigidMetal,
                    1f,
                    false);
            }
            _lastPulsePhase = pulsePhase;

            // Pulso visual (piscar suave a ~4 Hz ou brilho constante em movimento reduzido)
            float pulse = GameFeelSettings.AllowFlashes && !GameFeelSettings.ReduceMotion
                ? Mathf.PingPong(Time.time * 4f, 1f)
                : 0.65f;

            if (beaconLight != null)
            {
                beaconLight.intensity = pulse * 2.5f;
            }

            ApplyBeaconColor(Color.Lerp(idleColor, ringingColor, pulse), ringingColor * pulse);
            UpdateRingRattle(pulsePhase != 0, cycleTime);
        }

        public void SetRinging(bool ringing)
        {
            if (_isRinging == ringing) return;
            _isRinging = ringing;

            if (ringing)
            {
                _ringStartTime = Time.time;
                _lastPulsePhase = 0;
                CapturePivotBaseIfAvailable();

                if (customRingClip != null)
                {
                    audioSource.clip = customRingClip;
                }
                else
                {
                    if (_proceduralClip == null)
                        _proceduralClip = GenerateProceduralRingClip();
                    audioSource.clip = _proceduralClip;
                }

                audioSource.Play();
                if (beaconLight != null) beaconLight.enabled = true;
            }
            else
            {
                audioSource.Stop();
                if (beaconLight != null)
                {
                    beaconLight.intensity = 0f;
                    beaconLight.enabled = false;
                }

                _lastPulsePhase = 0;
                ApplyBeaconColor(idleColor, Color.black);
                ResetVisualPivotToRest();
            }
        }

        private Transform ResolvePivotTransform()
        {
            if (phoneVisualPivot != null && phoneVisualPivot != transform)
                return phoneVisualPivot;
            if (beaconRenderer != null && beaconRenderer.transform != transform)
                return beaconRenderer.transform;
            return null;
        }

        private void CapturePivotBaseIfAvailable()
        {
            if (_pivotBaseCaptured) return;
            var pivot = ResolvePivotTransform();
            if (pivot == null) return;
            _pivotBaseLocalPos = pivot.localPosition;
            _pivotBaseLocalRot = pivot.localRotation;
            _pivotBaseCaptured = true;
        }

        private void UpdateRingRattle(bool inToneWindow, float cycleTime)
        {
            var pivot = ResolvePivotTransform();
            if (pivot == null) return;
            CapturePivotBaseIfAvailable();

            if (!inToneWindow || GameFeelSettings.ReduceMotion)
            {
                pivot.localPosition = _pivotBaseLocalPos;
                pivot.localRotation = _pivotBaseLocalRot;
                return;
            }

            float intensity = GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Movement);
            float localT = cycleTime < 0.4f ? cycleTime / 0.4f : (cycleTime - 0.6f) / 0.4f;
            float envelope = Mathf.Sin(Mathf.Clamp01(localT) * Mathf.PI);
            float rattleAngle = Mathf.Sin(Time.time * 48f) * 6.5f * envelope * intensity;
            float hopY = Mathf.Abs(Mathf.Sin(Time.time * 24f)) * 0.008f * envelope * intensity;

            pivot.localPosition = _pivotBaseLocalPos + new Vector3(0f, hopY, 0f);
            pivot.localRotation = _pivotBaseLocalRot * Quaternion.Euler(0f, 0f, rattleAngle);
        }

        private void ResetVisualPivotToRest()
        {
            if (!_pivotBaseCaptured) return;
            var pivot = ResolvePivotTransform();
            if (pivot == null) return;
            pivot.localPosition = _pivotBaseLocalPos;
            pivot.localRotation = _pivotBaseLocalRot;
        }

        private void ApplyBeaconColor(Color baseColor, Color emissionColor)
        {
            if (beaconRenderer == null) return;
            _beaconMpb ??= new MaterialPropertyBlock();
            int slots = beaconRenderer.sharedMaterials != null ? Mathf.Max(1, beaconRenderer.sharedMaterials.Length) : 1;
            for (int s = 0; s < slots; s++)
            {
                VisualStyleShaderUtility.ApplyColorBlockToSlot(beaconRenderer, s, baseColor, _beaconMpb);
                beaconRenderer.GetPropertyBlock(_beaconMpb, s);
                _beaconMpb.SetColor(VisualStyleShaderUtility.EmissionColorId, emissionColor);
                beaconRenderer.SetPropertyBlock(_beaconMpb, s);
            }
        }

        private void OnDisable()
        {
            ResetVisualPivotToRest();
        }

        private static AudioClip GenerateProceduralRingClip()
        {
            int sampleRate = 44100;
            float totalDuration = 3.0f; // 0.4s tom, 0.2s pausa, 0.4s tom, 2.0s silêncio
            int totalSamples = Mathf.FloorToInt(totalDuration * sampleRate);
            float[] samples = new float[totalSamples];

            float f1 = 440f;
            float f2 = 480f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                bool isTone1 = t >= 0f && t < 0.4f;
                bool isTone2 = t >= 0.6f && t < 1.0f;

                if (isTone1 || isTone2)
                {
                    float tone = 0.25f * (Mathf.Sin(2 * Mathf.PI * f1 * t) + Mathf.Sin(2 * Mathf.PI * f2 * t));
                    samples[i] = tone;
                }
                else
                {
                    samples[i] = 0f;
                }
            }

            var clip = AudioClip.Create("PhoneRingProcedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            ResetVisualPivotToRest();
        }
    }
}
