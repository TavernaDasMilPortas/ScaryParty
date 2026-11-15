using UnityEngine;
using ScaryParty.Pizzeria.Presentation.Feedback;

namespace ScaryParty.Pizzeria.Presentation
{
    public class PizzeriaSoundManager : MonoBehaviour
    {
        public static PizzeriaSoundManager Instance { get; private set; }

        [Header("Sons de Interação")]
        public AudioClip pickupSound;
        public AudioClip placeSound;
        public AudioClip cutSound;
        public AudioClip ovenInsertSound;
        public AudioClip ovenReadySound;
        public AudioClip ovenBurnSound;
        public AudioClip packageSound;
        public AudioClip deliverySound;
        public AudioClip errorSound;

        [Header("Sons Adicionais de Game Feel (Opcional)")]
        public AudioClip doughSound;
        public AudioClip sauceSound;
        public AudioClip cheeseSound;
        public AudioClip backpackSound;
        public AudioClip ticketSound;
        public AudioClip phoneAnswerSound;
        public AudioClip uiHoverSound;
        public AudioClip uiClickSound;

        private AudioSource _source;
        private AudioSource _ui2DSource;
        private AudioSource[] _spatialPool;
        private int[] _spatialPoolPriority;
        private float[] _spatialPoolBusyUntil;
        private int _poolCursor;

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            EnsureAudioPools();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void EnsureAudioPools()
        {
            if (_ui2DSource == null)
            {
                var uiGo = new GameObject("SoundEmitter_UI2D");
                uiGo.transform.SetParent(transform, false);
                _ui2DSource = uiGo.AddComponent<AudioSource>();
                _ui2DSource.playOnAwake = false;
                _ui2DSource.spatialBlend = 0f;
            }

            int voiceCount = GameFeelSettings.MaxKitchenAudioVoices;
            if (_spatialPool == null || _spatialPool.Length != voiceCount)
            {
                _spatialPool = new AudioSource[voiceCount];
                _spatialPoolPriority = new int[voiceCount];
                _spatialPoolBusyUntil = new float[voiceCount];

                for (int i = 0; i < voiceCount; i++)
                {
                    var go = new GameObject($"SoundEmitter_3D_{i}");
                    go.transform.SetParent(transform, false);
                    var src = go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0.85f;
                    src.minDistance = 2.0f;
                    src.maxDistance = 22.0f;
                    src.rolloffMode = AudioRolloffMode.Logarithmic;
                    _spatialPool[i] = src;
                }
            }
        }

        public static void Play(AudioClip clip)
        {
            if (Instance != null && clip != null && Instance._source != null)
                Instance._source.PlayOneShot(clip);
        }

        public static void PlayAtPosition(AudioClip clip, Vector3 worldPos, float pitchVariance = 0.04f, float volumeScale = 1f, int priority = 50, int seed = 0)
        {
            if (Instance == null || clip == null) return;
            Instance.PlaySpatialInternal(clip, worldPos, pitchVariance, volumeScale, priority, seed);
        }

        public static void PlayFeelEvent(GameFeelEventId eventId, GameFeelMaterialFamily family, Vector3 worldPos, float pitchVariance = 0.04f, float volumeScale = 1f, int priority = 50, int seed = 0, bool force2D = false)
        {
            if (Instance == null) return;
            AudioClip clip = Instance.ResolveClipForEvent(eventId, family, out float basePitchBias);
            if (clip == null) return;

            float vol = Mathf.Clamp01(volumeScale * GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Audio));
            if (vol <= 0.01f) return;

            if (force2D || eventId == GameFeelEventId.UIHover || eventId == GameFeelEventId.UIClick ||
                eventId == GameFeelEventId.UISuccess || eventId == GameFeelEventId.UIError)
            {
                Instance.Play2DInternal(clip, basePitchBias, pitchVariance, vol, seed);
            }
            else
            {
                Instance.PlaySpatialInternal(clip, worldPos, pitchVariance, vol, priority, seed, basePitchBias);
            }
        }

        private AudioClip ResolveClipForEvent(GameFeelEventId eventId, GameFeelMaterialFamily family, out float pitchBias)
        {
            pitchBias = family switch
            {
                GameFeelMaterialFamily.RigidMetal => 1.07f,
                GameFeelMaterialFamily.Dough => 0.92f,
                GameFeelMaterialFamily.CardboardPaper => 1.04f,
                GameFeelMaterialFamily.SmallBounceIngredient => 1.02f,
                _ => 1f
            };

            switch (eventId)
            {
                case GameFeelEventId.ItemPickedUp:
                case GameFeelEventId.ToolPickedUp:
                case GameFeelEventId.ActiveHandChanged:
                case GameFeelEventId.StorageDispensed:
                    return pickupSound;
                case GameFeelEventId.ItemPlaced:
                case GameFeelEventId.BoxPlaced:
                case GameFeelEventId.StackPlaced:
                case GameFeelEventId.StackRemoved:
                case GameFeelEventId.DoorOpened:
                case GameFeelEventId.DoorClosed:
                    return placeSound != null ? placeSound : pickupSound;
                case GameFeelEventId.RollDoughStep:
                    return doughSound != null ? doughSound : (cutSound != null ? cutSound : placeSound);
                case GameFeelEventId.SpreadSauce:
                    return sauceSound != null ? sauceSound : placeSound;
                case GameFeelEventId.SprinkleCheese:
                case GameFeelEventId.GrateCheeseStep:
                    return cheeseSound != null ? cheeseSound : (cutSound != null ? cutSound : placeSound);
                case GameFeelEventId.SliceTomatoPepperoniStep:
                case GameFeelEventId.SliceMushroomStep:
                case GameFeelEventId.ProcessTick:
                    return cutSound;
                case GameFeelEventId.ProcessCompleted:
                    return ovenReadySound != null ? ovenReadySound : pickupSound;
                case GameFeelEventId.OvenInserted:
                case GameFeelEventId.OvenRemoved:
                    return ovenInsertSound != null ? ovenInsertSound : placeSound;
                case GameFeelEventId.OvenReadyDing:
                    return ovenReadySound;
                case GameFeelEventId.OvenBurningWarning:
                case GameFeelEventId.OvenBurned:
                    return ovenBurnSound != null ? ovenBurnSound : errorSound;
                case GameFeelEventId.BoxOpened:
                case GameFeelEventId.BoxPackaged:
                case GameFeelEventId.BoxLabeled:
                    return packageSound != null ? packageSound : placeSound;
                case GameFeelEventId.BackpackEquipped:
                case GameFeelEventId.BackpackStored:
                case GameFeelEventId.BackpackRemoved:
                    return backpackSound != null ? backpackSound : (packageSound != null ? packageSound : pickupSound);
                case GameFeelEventId.OrderPosted:
                case GameFeelEventId.OrderInspected:
                case GameFeelEventId.OrderCompleted:
                    return ticketSound != null ? ticketSound : packageSound;
                case GameFeelEventId.PhoneAnswered:
                    return phoneAnswerSound != null ? phoneAnswerSound : pickupSound;
                case GameFeelEventId.DeliverySuccess:
                case GameFeelEventId.DeliveryAccepted:
                case GameFeelEventId.NpcOrderGood:
                    return deliverySound != null ? deliverySound : ovenReadySound;
                case GameFeelEventId.DeliveryPartial:
                    return deliverySound != null ? deliverySound : placeSound;
                case GameFeelEventId.DeliveryFailed:
                case GameFeelEventId.ActionRejected:
                case GameFeelEventId.BackpackFullRejected:
                case GameFeelEventId.NpcOrderBad:
                case GameFeelEventId.NpcLeavingAngry:
                case GameFeelEventId.OrderDeadlineWarning:
                    return errorSound;
                case GameFeelEventId.UIHover:
                    return uiHoverSound != null ? uiHoverSound : pickupSound;
                case GameFeelEventId.UIClick:
                    return uiClickSound != null ? uiClickSound : pickupSound;
                case GameFeelEventId.UISuccess:
                    return deliverySound != null ? deliverySound : ovenReadySound;
                case GameFeelEventId.UIError:
                    return errorSound;
                default:
                    return null;
            }
        }

        private void Play2DInternal(AudioClip clip, float basePitch, float pitchVariance, float volume, int seed)
        {
            EnsureAudioPools();
            if (_ui2DSource == null || clip == null) return;
            _ui2DSource.pitch = ComputeSeededPitch(basePitch, pitchVariance, seed);
            _ui2DSource.PlayOneShot(clip, volume);
        }

        private void PlaySpatialInternal(AudioClip clip, Vector3 worldPos, float pitchVariance, float volumeScale, int priority, int seed, float basePitch = 1f)
        {
            EnsureAudioPools();
            if (_spatialPool == null || clip == null) return;

            float now = Time.unscaledTime;
            int chosen = -1;
            int lowestPriority = int.MaxValue;
            int lowestIdx = 0;

            for (int i = 0; i < _spatialPool.Length; i++)
            {
                int idx = (_poolCursor + i) % _spatialPool.Length;
                if (now >= _spatialPoolBusyUntil[idx] || !_spatialPool[idx].isPlaying)
                {
                    chosen = idx;
                    break;
                }
                if (_spatialPoolPriority[idx] < lowestPriority)
                {
                    lowestPriority = _spatialPoolPriority[idx];
                    lowestIdx = idx;
                }
            }

            if (chosen < 0)
            {
                if (priority >= lowestPriority)
                {
                    chosen = lowestIdx;
                    _spatialPool[chosen].Stop();
                }
                else
                {
                    return; // Orçamento saturado e prioridade menor -> pula sem estourar vozes
                }
            }

            _poolCursor = (chosen + 1) % _spatialPool.Length;
            var src = _spatialPool[chosen];
            src.transform.position = worldPos;
            src.pitch = ComputeSeededPitch(basePitch, pitchVariance, seed);
            src.clip = clip;
            src.volume = Mathf.Clamp01(volumeScale);
            src.Play();
            _spatialPoolPriority[chosen] = priority;
            _spatialPoolBusyUntil[chosen] = now + Mathf.Min(clip.length / Mathf.Max(0.5f, src.pitch), 2.5f);
        }

        private static float ComputeSeededPitch(float basePitch, float variance, int seed)
        {
            if (variance <= 0.0001f) return basePitch;
            // Mistura temporal discreta + seed determinística
            int mixed = unchecked(seed * 73856093 ^ (Mathf.FloorToInt(Time.unscaledTime * 100f) * 19349663));
            float norm = ((mixed & 0xFFFF) / 32767.5f) - 1f;
            return Mathf.Clamp(basePitch + norm * variance, 0.7f, 1.35f);
        }

        public static void PlayPickup() => Play(Instance?.pickupSound);
        public static void PlayPlace() => Play(Instance?.placeSound);
        public static void PlayCut() => Play(Instance?.cutSound);
        public static void PlayOvenInsert() => Play(Instance?.ovenInsertSound);
        public static void PlayOvenReady() => Play(Instance?.ovenReadySound);
        public static void PlayBurn() => Play(Instance?.ovenBurnSound);
        public static void PlayPackage() => Play(Instance?.packageSound);
        public static void PlayDelivery() => Play(Instance?.deliverySound);
        public static void PlayError() => Play(Instance?.errorSound);
        public void PlayDing() => Play(deliverySound != null ? deliverySound : ovenReadySound);
    }
}
