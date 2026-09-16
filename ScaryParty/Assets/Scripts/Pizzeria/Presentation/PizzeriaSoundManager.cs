using UnityEngine;

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
        
        private AudioSource _source;
        
        private void Awake()
        {
            Instance = this;
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
        }
        
        public static void Play(AudioClip clip)
        {
            if (Instance != null && clip != null && Instance._source != null)
                Instance._source.PlayOneShot(clip);
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
    }
}
