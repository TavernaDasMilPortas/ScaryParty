using System;
using System.Collections.Generic;
using Cinemachine;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Pizzeria.Composition;

namespace Piaaria.TrailerStudio
{
    public enum TrailerDirectorState
    {
        Initializing = 0,
        Ready = 1,
        Playing = 2,
        Paused = 3,
        Finished = 4,
        Error = 5
    }

    public class TrailerDirector : MonoBehaviour
    {
        [Header("Core References")]
        public TrailerStudioProfile profile;
        public PlayableDirector playableDirector;
        public CinemachineBrain cinemachineBrain;
        public AudioSource audioSource;
        public CityGenerator cityGenerator;

        [Header("Scene Roots")]
        public Transform castRoot;
        public Transform propsRoot;
        public Transform vehiclesRoot;
        public Transform anchorsRoot;
        public Transform camerasRoot;

        [Header("Playback Controls")]
        [OnValueChanged(nameof(OnTakeChanged))]
        public TrailerTakeId take = TrailerTakeId.T03_Dough;

        [OnValueChanged(nameof(OnCameraChanged))]
        public TrailerCameraMode cameraMode = TrailerCameraMode.CameraA;

        public bool loopTake;
        public bool playAll;
        [ValueDropdown(nameof(GetLightingHours))]
        public float lightingHour = 19f;
        [ValueDropdown(nameof(GetSpeeds))]
        public float speed = 1f;

        [Header("Status (Read-Only)")]
        [ReadOnly] public bool environmentReady;
        [ReadOnly] public string currentTakeName;
        [ReadOnly] public float localTime;
        [ReadOnly] public TrailerDirectorState state = TrailerDirectorState.Initializing;
        [ReadOnly] public string lastValidationError;
        [ReadOnly] public int activeSeed;
        [ReadOnly] public string activeCameraName;

        private readonly Dictionary<TrailerActorId, TrailerActor> _actors = new Dictionary<TrailerActorId, TrailerActor>();
        private readonly Dictionary<TrailerAnchorKey, Transform> _anchors = new Dictionary<TrailerAnchorKey, Transform>();
        private readonly List<GameObject> _spawnedProps = new List<GameObject>();
        private GameObject _motorcycleInstance;
        private CinemachineVirtualCamera _activeVCam;
        private float _lastActionTime = -1f;
        private bool _isInitialized;

        private static IEnumerable<float> GetLightingHours() => new[] { 17f, 19f, 22f };
        private static IEnumerable<float> GetSpeeds() => new[] { 0.5f, 1f };

        private void Start()
        {
            InitializeEnvironment();
        }

        private void InitializeEnvironment()
        {
            state = TrailerDirectorState.Initializing;
            environmentReady = false;
            lastValidationError = string.Empty;

            if (profile == null)
            {
                state = TrailerDirectorState.Error;
                lastValidationError = "TrailerStudioProfile não atribuído no Director.";
                return;
            }

            if (cityGenerator != null)
            {
                activeSeed = profile.seed;
                cityGenerator.OnCityGenerated -= HandleCityGenerated;
                cityGenerator.OnCityGenerated += HandleCityGenerated;
                if (!cityGenerator.TryGenerateOfflineWithSeed(profile.seed))
                {
                    // Se já gerou ou não está disponível, tenta resolver mesmo assim
                    HandleCityGenerated();
                }
            }
            else
            {
                HandleCityGenerated();
            }
        }

        private void HandleCityGenerated()
        {
            if (cityGenerator != null)
                cityGenerator.OnCityGenerated -= HandleCityGenerated;

            ResolveAnchors();
            EnsureCast();
            EnsureMotorcycle();

            _isInitialized = true;
            environmentReady = true;
            PrepareTake();
        }

        private void ResolveAnchors()
        {
            _anchors.Clear();
            // Coleta âncoras explícitas na cena ou nas estações da pizzaria
            PrepStation[] preps = FindObjectsByType<PrepStation>(FindObjectsSortMode.None);
            foreach (var p in preps)
            {
                if (p.slotAnchors != null && p.slotAnchors.Length > 0)
                {
                    if (!_anchors.ContainsKey(TrailerAnchorKey.PREP_DOUGH))
                        _anchors[TrailerAnchorKey.PREP_DOUGH] = p.slotAnchors[0];
                    else if (!_anchors.ContainsKey(TrailerAnchorKey.PREP_CUT))
                        _anchors[TrailerAnchorKey.PREP_CUT] = p.slotAnchors[0];
                }
            }

            var packaging = FindFirstObjectByType<PackagingStation>();
            if (packaging != null && packaging.slotAnchors != null && packaging.slotAnchors.Length > 0)
                _anchors[TrailerAnchorKey.PACKAGING] = packaging.slotAnchors[0];

            var oven = FindFirstObjectByType<OvenStation>();
            if (oven != null)
            {
                if (oven.slotAnchors != null && oven.slotAnchors.Length > 0)
                    _anchors[TrailerAnchorKey.OVEN_SLOT] = oven.slotAnchors[0];
                _anchors[TrailerAnchorKey.OVEN_ENTRY] = oven.GetEntryAnchor();
            }

            var assembly = FindFirstObjectByType<AssemblyStation>();
            if (assembly != null && assembly.slotAnchors != null && assembly.slotAnchors.Length > 0)
                _anchors[TrailerAnchorKey.ASSEMBLY] = assembly.slotAnchors[0];

            var ext = FindFirstObjectByType<PizzeriaExteriorAnchors>();
            if (ext != null)
            {
                if (ext.motorcycleSpawn_01 != null) _anchors[TrailerAnchorKey.PARKING] = ext.motorcycleSpawn_01;
                if (ext.streetExit != null) _anchors[TrailerAnchorKey.STREET_EXIT] = ext.streetExit;
                if (ext.dispatch != null) _anchors[TrailerAnchorKey.DISPATCH] = ext.dispatch;
            }
        }

        private void EnsureCast()
        {
            if (profile.characterVisualPrefab == null || castRoot == null) return;
            foreach (TrailerActorId id in Enum.GetValues(typeof(TrailerActorId)))
            {
                if (_actors.ContainsKey(id) && _actors[id] != null) continue;
                GameObject go = new GameObject(id.ToString());
                go.transform.SetParent(castRoot, false);
                GameObject vis = Instantiate(profile.characterVisualPrefab, go.transform);
                var cvc = vis.GetComponentInChildren<CharacterVisualController>();
                var actor = go.AddComponent<TrailerActor>();
                Color col = (int)id < profile.actorColors.Length ? profile.actorColors[(int)id] : Color.white;
                actor.Initialize(id, cvc, col);
                _actors[id] = actor;
            }
        }

        private void EnsureMotorcycle()
        {
            if (profile.motorcyclePrefab == null || vehiclesRoot == null) return;
            if (_motorcycleInstance == null)
            {
                _motorcycleInstance = Instantiate(profile.motorcyclePrefab, vehiclesRoot);
                _motorcycleInstance.name = "TrailerMotorcycle";
            }
        }

        [Button("Prepare / Reset Take", ButtonSizes.Medium)]
        public void PrepareTake()
        {
            if (!_isInitialized) return;

            ClearSpawnedProps();
            localTime = 0f;
            _lastActionTime = -1f;

            var def = profile.GetTake(take);
            if (def == null)
            {
                state = TrailerDirectorState.Error;
                lastValidationError = $"Take {take} não encontrado no Profile.";
                return;
            }

            currentTakeName = def.displayName;

            // Reset atores
            foreach (var kvp in _actors)
            {
                Color col = (int)kvp.Key < profile.actorColors.Length ? profile.actorColors[(int)kvp.Key] : Color.white;
                Vector3 anchorPos = GetAnchorPosition(def.focusAnchor);
                Vector3 anchorFwd = GetAnchorForward(def.focusAnchor);
                Vector3 pos = anchorPos + anchorFwd * 0.85f + new Vector3(((int)kvp.Key - 1.5f) * 1.1f, 0f, 0f);
                Quaternion rot = Quaternion.LookRotation(-anchorFwd, Vector3.up);
                kvp.Value.ResetActor(pos, rot, col);
            }

            // Props iniciais por take
            if (take == TrailerTakeId.T03_Dough && profile.doughPrefab != null)
            {
                SpawnPropAtAnchor(profile.doughPrefab, TrailerAnchorKey.PREP_DOUGH);
                if (_actors.TryGetValue(TrailerActorId.ActorA, out var actorA))
                {
                    Vector3 p = GetAnchorPosition(TrailerAnchorKey.PREP_DOUGH) + GetAnchorForward(TrailerAnchorKey.PREP_DOUGH) * 0.85f;
                    actorA.transform.position = p;
                    actorA.FacePosition(GetAnchorPosition(TrailerAnchorKey.PREP_DOUGH));
                }
            }
            else if (take == TrailerTakeId.T07_Packaging && profile.pizzaWipPrefab != null)
            {
                SpawnPropAtAnchor(profile.pizzaWipPrefab, TrailerAnchorKey.PACKAGING);
                if (_actors.TryGetValue(TrailerActorId.ActorC, out var actorC))
                {
                    Vector3 p = GetAnchorPosition(TrailerAnchorKey.PACKAGING) + GetAnchorForward(TrailerAnchorKey.PACKAGING) * 0.85f;
                    actorC.transform.position = p;
                    actorC.FacePosition(GetAnchorPosition(TrailerAnchorKey.PACKAGING));
                }
            }

            // Reset moto
            if (_motorcycleInstance != null && _anchors.TryGetValue(TrailerAnchorKey.PARKING, out Transform park) && park != null)
            {
                _motorcycleInstance.transform.SetPositionAndRotation(park.position, park.rotation);
            }

            // Configurar Timeline
            if (playableDirector != null && def.timelineAsset != null)
            {
                playableDirector.playableAsset = def.timelineAsset;
                playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
                playableDirector.time = 0;
                playableDirector.Evaluate();
            }

            UpdateActiveCamera(def);
            state = TrailerDirectorState.Ready;
        }

        [Button("Play", ButtonSizes.Large), GUIColor(0.3f, 0.85f, 0.4f)]
        public void Play()
        {
            if (!environmentReady) return;
            if (state == TrailerDirectorState.Finished || state == TrailerDirectorState.Error)
                PrepareTake();

            foreach (var actor in _actors.Values)
                actor.SetPaused(false);

            state = TrailerDirectorState.Playing;
        }

        [Button("Pause / Resume", ButtonSizes.Medium)]
        public void PauseOrResume()
        {
            if (state == TrailerDirectorState.Playing)
            {
                state = TrailerDirectorState.Paused;
                foreach (var actor in _actors.Values) actor.SetPaused(true);
            }
            else if (state == TrailerDirectorState.Paused)
            {
                state = TrailerDirectorState.Playing;
                foreach (var actor in _actors.Values) actor.SetPaused(false);
            }
        }

        [Button("Stop", ButtonSizes.Medium), GUIColor(0.9f, 0.35f, 0.35f)]
        public void StopTake()
        {
            PrepareTake();
        }

        [Button("Next Take", ButtonSizes.Medium)]
        public void NextTake()
        {
            int next = ((int)take + 1) % Enum.GetValues(typeof(TrailerTakeId)).Length;
            take = (TrailerTakeId)next;
            PrepareTake();
        }

        private void Update()
        {
            if (state != TrailerDirectorState.Playing || profile == null) return;
            var def = profile.GetTake(take);
            if (def == null) return;

            float dt = Time.deltaTime * Mathf.Max(0.1f, speed);
            localTime += dt;

            if (playableDirector != null && def.timelineAsset != null)
            {
                playableDirector.time = localTime;
                playableDirector.Evaluate();
            }

            foreach (var actor in _actors.Values)
                actor.TickMove(dt);

            float actionTime = localTime - def.preRoll;
            if (actionTime >= 0f && actionTime <= def.actionDuration)
            {
                EvaluateCues(def, _lastActionTime, actionTime);
                _lastActionTime = actionTime;
            }

            if (localTime >= def.TotalDuration)
            {
                if (loopTake)
                {
                    PrepareTake();
                    Play();
                }
                else if (playAll)
                {
                    NextTake();
                    Play();
                }
                else
                {
                    state = TrailerDirectorState.Finished;
                }
            }
        }

        private void EvaluateCues(TrailerTakeDefinition def, float oldT, float newT)
        {
            if (def.cues == null) return;
            for (int i = 0; i < def.cues.Count; i++)
            {
                var cue = def.cues[i];
                bool crossed = (oldT < 0f && Mathf.Approximately(cue.time, 0f)) || (oldT < cue.time && cue.time <= newT);
                if (!crossed) continue;

                if (_actors.TryGetValue(cue.actor, out TrailerActor actor))
                {
                    switch (cue.actionType)
                    {
                        case TrailerActionType.SetPreparing:
                            actor.SetPreparing(cue.boolValue);
                            break;
                        case TrailerActionType.SetSitting:
                            actor.SetSitting(cue.boolValue, true);
                            break;
                        case TrailerActionType.MoveToAnchor:
                            actor.StartMove(GetAnchorPosition(cue.anchorKey), cue.duration);
                            break;
                        case TrailerActionType.FaceAnchor:
                            actor.FacePosition(GetAnchorPosition(cue.anchorKey));
                            break;
                    }
                }
            }
        }

        private Vector3 GetAnchorPosition(TrailerAnchorKey key)
        {
            if (_anchors.TryGetValue(key, out Transform t) && t != null)
                return t.position;
            return Vector3.zero;
        }

        private Vector3 GetAnchorForward(TrailerAnchorKey key)
        {
            if (_anchors.TryGetValue(key, out Transform t) && t != null)
            {
                Vector3 f = t.forward;
                f.y = 0f;
                if (f.sqrMagnitude > 0.001f) return f.normalized;
            }
            return Vector3.forward;
        }

        private GameObject SpawnPropAtAnchor(GameObject prefab, TrailerAnchorKey key)
        {
            if (prefab == null || propsRoot == null) return null;
            GameObject go = Instantiate(prefab, propsRoot);
            go.transform.SetPositionAndRotation(GetAnchorPosition(key), Quaternion.identity);
            _spawnedProps.Add(go);
            return go;
        }

        private void UpdateActiveCamera(TrailerTakeDefinition def)
        {
            if (camerasRoot == null) return;
            string targetName = $"{def.id}_{(cameraMode == TrailerCameraMode.CameraB_Alt && def.hasCameraB ? "B" : "A")}";
            activeCameraName = targetName;
            var vcams = camerasRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true);
            foreach (var v in vcams)
            {
                bool match = v.name == targetName;
                v.Priority = match ? 20 : 0;
                if (match)
                {
                    _activeVCam = v;
                    Vector3 q = GetAnchorPosition(def.focusAnchor) + Vector3.up * 1.0f;
                    Vector3 f = GetAnchorForward(def.focusAnchor);
                    Vector3 r = Vector3.Cross(Vector3.up, f).normalized;
                    Vector3 off = cameraMode == TrailerCameraMode.CameraB_Alt && def.hasCameraB
                        ? def.cameraBOffset
                        : def.cameraAStartOffset;
                    if (off == Vector3.zero) off = new Vector3(1.0f, 0.8f, 1.5f);
                    v.transform.position = q + r * off.x + Vector3.up * off.y + f * off.z;
                    v.transform.LookAt(q);
                }
            }
        }

        private void ClearSpawnedProps()
        {
            for (int i = 0; i < _spawnedProps.Count; i++)
            {
                if (_spawnedProps[i] != null) Destroy(_spawnedProps[i]);
            }
            _spawnedProps.Clear();
        }

        private void OnTakeChanged()
        {
            if (Application.isPlaying && _isInitialized) PrepareTake();
        }

        private void OnCameraChanged()
        {
            if (Application.isPlaying && _isInitialized && profile != null)
            {
                var def = profile.GetTake(take);
                if (def != null) UpdateActiveCamera(def);
            }
        }
    }
}
