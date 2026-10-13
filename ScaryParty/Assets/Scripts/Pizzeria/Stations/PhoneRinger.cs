using UnityEngine;

namespace ScaryParty.Pizzeria.Stations
{
    [RequireComponent(typeof(AudioSource))]
    public class PhoneRinger : MonoBehaviour
    {
        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip customRingClip;

        [Header("Visual")]
        public Light beaconLight;
        public Renderer beaconRenderer;
        public Color ringingColor = Color.yellow;
        public Color idleColor = Color.gray;

        private bool _isRinging = false;
        private static AudioClip _proceduralClip;
        private Material _instancedMat;

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

            if (beaconRenderer != null)
            {
                _instancedMat = beaconRenderer.material;
            }

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

            // Pulso visual (piscar suave a ~4 Hz)
            float pulse = Mathf.PingPong(Time.time * 4f, 1f);
            if (beaconLight != null)
            {
                beaconLight.intensity = pulse * 2.5f;
            }

            if (_instancedMat != null)
            {
                _instancedMat.color = Color.Lerp(idleColor, ringingColor, pulse);
                if (_instancedMat.HasProperty("_EmissionColor"))
                {
                    _instancedMat.SetColor("_EmissionColor", ringingColor * pulse);
                }
            }
        }

        public void SetRinging(bool ringing)
        {
            if (_isRinging == ringing) return;
            _isRinging = ringing;

            if (ringing)
            {
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

                if (_instancedMat != null)
                {
                    _instancedMat.color = idleColor;
                    if (_instancedMat.HasProperty("_EmissionColor"))
                    {
                        _instancedMat.SetColor("_EmissionColor", Color.black);
                    }
                }
            }
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
            if (_instancedMat != null)
            {
                Destroy(_instancedMat);
            }
        }
    }
}
