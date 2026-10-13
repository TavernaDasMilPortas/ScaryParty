using UnityEngine;
using Unity.Netcode;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;

namespace ScaryParty.Pizzeria.Stations
{
    public class OvenStation : StationView
    {
        [Header("Efeitos Visuais e Sonoros do Forno")]
        public Light ovenFireLight;
        public ParticleSystem smokeParticles;
        public AudioSource audioSource;
        public AudioClip customDingClip;

        private static AudioClip _proceduralDingClip;
        private bool _hasPlayedDing = false;
        private float _lastHighestCookProgress = 0f;

        public override string InteractPrompt
        {
            get
            {
                var netState = PizzeriaNetworkState.Instance;
                var networkManager = NetworkManager.Singleton;
                if (netState == null || networkManager == null) return "Forno";

                ulong localClientId = networkManager.LocalClientId;
                bool stationHasPizza = false;
                bool playerHasPizza = false;
                float cookProgress = 0f;
                float burnProgress = 0f;

                for (int i = 0; i < netState.Items.Count; i++)
                {
                    var item = netState.Items[i];
                    bool isPizza = item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza;
                    
                    if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                    {
                        stationHasPizza = true;
                        cookProgress = item.CookProgress;
                        burnProgress = item.BurnProgress;
                    }
                    if (item.LocationType == (byte)LocationType.Hand && item.HolderId == localClientId)
                    {
                        if (isPizza) playerHasPizza = true;
                    }
                }

                if (stationHasPizza)
                {
                    string status = "Assando";
                    if (burnProgress > 0) status = "Queimando!";
                    else if (cookProgress >= 1f) status = "Pronta!";
                    
                    int pct = Mathf.FloorToInt((burnProgress > 0 ? burnProgress : cookProgress) * 100);
                    return $"[E] Retirar Pizza ({status} {pct}%)";
                }
                if (!stationHasPizza && playerHasPizza) return "[E] Colocar no Forno";
                
                return "Forno vazio";
            }
        }

        protected override void Start()
        {
            base.Start();
            stationName = "Forno de Pizza";

            if (_progressBar != null)
            {
                _progressBar.transform.position = transform.position + Vector3.up * 1.2f;
            }

            SetupFireLight();
            SetupSmokeParticles();
            SetupAudioSource();
        }

        private void SetupFireLight()
        {
            if (ovenFireLight == null)
            {
                var lightGo = new GameObject("OvenFireLight");
                lightGo.transform.SetParent(transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 0.4f, 0f);
                ovenFireLight = lightGo.AddComponent<Light>();
                ovenFireLight.type = LightType.Point;
                ovenFireLight.color = new Color(1.0f, 0.45f, 0.05f);
                ovenFireLight.range = 3.5f;
                ovenFireLight.intensity = 0f;
            }
        }

        private void SetupSmokeParticles()
        {
            if (smokeParticles == null)
            {
                var smokeGo = new GameObject("OvenSmokeParticles");
                smokeGo.transform.SetParent(transform, false);
                smokeGo.transform.localPosition = new Vector3(0f, 1.3f, 0f);

                smokeParticles = smokeGo.AddComponent<ParticleSystem>();
                var main = smokeParticles.main;
                main.startLifetime = 1.5f;
                main.startSpeed = 1.0f;
                main.startSize = 0.4f;
                main.startColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.playOnAwake = false;

                var emission = smokeParticles.emission;
                emission.rateOverTime = 12f;

                var shape = smokeParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 15f;
                shape.radius = 0.2f;

                var renderer = smokeGo.GetComponent<ParticleSystemRenderer>();
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
                if (shader != null)
                {
                    renderer.material = new Material(shader);
                }

                smokeParticles.Stop();
            }
        }

        private void SetupAudioSource()
        {
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1.0f;
            audioSource.minDistance = 2f;
            audioSource.maxDistance = 15f;
        }

        protected override void Update()
        {
            base.Update();

            var state = PizzeriaNetworkState.Instance;
            if (state == null)
            {
                UpdateOvenEffects(false, 0f, 0f);
                return;
            }

            bool foundActive = false;
            float highestCook = 0f;
            float highestBurn = 0f;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                {
                    bool isPizza = item.Category == (byte)ItemCategory.PizzaBase || item.Category == (byte)ItemCategory.Pizza;
                    if (isPizza)
                    {
                        foundActive = true;
                        highestCook = Mathf.Max(highestCook, item.CookProgress);
                        highestBurn = Mathf.Max(highestBurn, item.BurnProgress);

                        if (_progressBar != null)
                        {
                            if (item.BurnProgress > 0)
                                _progressBar.SetProgress(item.BurnProgress, Color.red);
                            else if (item.CookProgress < 1f)
                                _progressBar.SetProgress(item.CookProgress, item.CookProgress > 0.8f ? Color.yellow : Color.green);
                            else
                                _progressBar.SetProgress(1f, Color.yellow);
                        }
                    }
                }
            }

            if (!foundActive && _progressBar != null)
            {
                _progressBar.Hide();
            }

            UpdateOvenEffects(foundActive, highestCook, highestBurn);
        }

        private void UpdateOvenEffects(bool hasPizzas, float cookProgress, float burnProgress)
        {
            // Efeito de Fogo / Luz
            if (hasPizzas)
            {
                if (ovenFireLight != null)
                {
                    ovenFireLight.enabled = true;
                    float flicker = Mathf.PerlinNoise(Time.time * 8f, 0f);
                    ovenFireLight.intensity = 2.0f + flicker * 1.5f;
                }

                // Tocar sinal sonoro ao atingir 100% de cozimento
                if (cookProgress >= 1f && _lastHighestCookProgress < 1f && !_hasPlayedDing)
                {
                    PlayBakeDoneDing();
                    _hasPlayedDing = true;
                }
                else if (cookProgress < 1f)
                {
                    _hasPlayedDing = false;
                }

                // Efeito de Fumaça
                if (burnProgress > 0f)
                {
                    if (smokeParticles != null && !smokeParticles.isPlaying)
                    {
                        smokeParticles.Play();
                    }

                    if (smokeParticles != null)
                    {
                        var main = smokeParticles.main;
                        if (burnProgress >= 0.5f)
                        {
                            main.startColor = new Color(0.05f, 0.05f, 0.05f, 0.85f); // Fumaça preta densa
                        }
                        else
                        {
                            main.startColor = new Color(0.4f, 0.4f, 0.4f, 0.45f); // Fumaça inicial
                        }
                    }
                }
                else
                {
                    if (smokeParticles != null && smokeParticles.isPlaying)
                    {
                        smokeParticles.Stop();
                    }
                }
            }
            else
            {
                if (ovenFireLight != null)
                {
                    ovenFireLight.intensity = 0f;
                    ovenFireLight.enabled = false;
                }
                if (smokeParticles != null && smokeParticles.isPlaying)
                {
                    smokeParticles.Stop();
                }
                _hasPlayedDing = false;
            }

            _lastHighestCookProgress = cookProgress;
        }

        private void PlayBakeDoneDing()
        {
            if (audioSource == null) return;

            if (customDingClip != null)
            {
                audioSource.PlayOneShot(customDingClip);
            }
            else
            {
                if (_proceduralDingClip == null)
                    _proceduralDingClip = GenerateProceduralDingClip();
                audioSource.PlayOneShot(_proceduralDingClip);
            }
        }

        private static AudioClip GenerateProceduralDingClip()
        {
            int sampleRate = 44100;
            float duration = 1.0f;
            int totalSamples = Mathf.FloorToInt(duration * sampleRate);
            float[] samples = new float[totalSamples];

            // Dois tons harmônicos de sino: 880Hz (A5) e 1320Hz (E6) decaindo exponencialmente
            float f1 = 880f;
            float f2 = 1320f;

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * 4.5f);
                float wave = (Mathf.Sin(2 * Mathf.PI * f1 * t) * 0.4f + Mathf.Sin(2 * Mathf.PI * f2 * t) * 0.25f) * decay;
                samples[i] = wave;
            }

            var clip = AudioClip.Create("OvenDingProcedural", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public override void OnInteract(GameObject interactor)
        {
            var netObj = interactor.GetComponent<NetworkObject>();
            if (netObj == null) return;
            ulong playerId = netObj.OwnerClientId;

            var invAdapter = interactor.GetComponent<ScaryParty.Pizzeria.Player.PlayerInventoryAdapter>();
            int activeHand = invAdapter != null ? (int)invAdapter.ActiveHand : 0;

            var state = PizzeriaNetworkState.Instance;
            var cmd = PizzeriaCommandHandler.Instance;
            if (state == null || cmd == null) return;

            NetItemDto itemOnStation = default;
            bool hasItemOnStation = false;

            NetItemDto itemInHand = default;
            bool hasItemInHand = false;

            for (int i = 0; i < state.Items.Count; i++)
            {
                var item = state.Items[i];
                if (item.LocationType == (byte)LocationType.StationSlot && item.HolderId == (ulong)stationId)
                {
                    itemOnStation = item;
                    hasItemOnStation = true;
                }
                if (item.LocationType == (byte)LocationType.Hand && item.HolderId == playerId && item.SlotId == activeHand)
                {
                    itemInHand = item;
                    hasItemInHand = true;
                }
            }

            int otherHand = activeHand == 0 ? 1 : 0;
            bool otherHandFree = true;
            for (int i = 0; i < state.Items.Count; i++)
            {
                var it = state.Items[i];
                if (it.LocationType == (byte)LocationType.Hand && it.HolderId == playerId && it.SlotId == otherHand)
                {
                    otherHandFree = false;
                    break;
                }
            }
            if (otherHandFree)
            {
                for (int i = 0; i < state.Tools.Count; i++)
                {
                    var t = state.Tools[i];
                    if (t.LocationType == (byte)LocationType.Hand && t.HolderId == playerId && t.SlotId == otherHand)
                    {
                        otherHandFree = false;
                        break;
                    }
                }
            }

            if (hasItemOnStation)
            {
                if (!hasItemInHand)
                {
                    cmd.RemoveOvenServerRpc(stationId, 0, (byte)activeHand);
                }
                else if (otherHandFree)
                {
                    cmd.RemoveOvenServerRpc(stationId, 0, (byte)otherHand);
                }
                else
                {
                    ScaryParty.Pizzeria.Presentation.NotificationManager.Show("Ambas as mãos ocupadas!", ScaryParty.Pizzeria.Presentation.NotificationType.Warning);
                }
            }
            else if (!hasItemOnStation && hasItemInHand)
            {
                cmd.InsertOvenServerRpc(itemInHand.ItemId, stationId, 0);
                if (invAdapter != null && !otherHandFree)
                {
                    invAdapter.SetActiveHand((HandSlotIndex)otherHand);
                }
            }
        }
    }
}
