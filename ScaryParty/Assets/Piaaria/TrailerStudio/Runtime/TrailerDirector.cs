using System;
using System.Collections.Generic;
using Cinemachine;
using ScaryParty.Lighting;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;
using ScaryParty.Pizzeria.Presentation;
using ScaryParty.Pizzeria.Presentation.Feedback;
using ScaryParty.Pizzeria.Stations;
using ScaryParty.Player;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;

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
        [ValueDropdown(nameof(GetLightingHours)), OnValueChanged(nameof(OnLightingChanged))]
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
        private readonly Dictionary<TrailerAnchorKey, GameObject> _slotProps = new Dictionary<TrailerAnchorKey, GameObject>();
        private readonly List<GameObject> _spawnedProps = new List<GameObject>();
        private readonly HashSet<int> _executedCueIndices = new HashSet<int>();
        private readonly List<GameObject> _decorObjects = new List<GameObject>();

        private TrailerAnchorEnvironment _env;
        private CityConfig _runtimeCityConfig;
        private GameObject _motorcycleInstance;
        private Transform _motoSeatAnchor;
        private readonly List<Transform> _motoWheelTransforms = new List<Transform>();
        private CinemachineVirtualCamera _activeVCam;
        private bool _isInitialized;

        public CinemachineVirtualCamera ActiveVirtualCamera => _activeVCam;

        // Estados contínuos sincronizados ao relógio localTime
        private bool _doughAnimActive;
        private float _doughAnimStart;
        private float _doughAnimEnd;
        private GameObject _rollingPinProp;
        private Vector3 _rollingPinBasePos;
        private Vector3 _doughBaseScale = Vector3.one;

        private bool _cutAnimActive;
        private float _cutAnimStart;
        private GameObject _knifeProp;
        private Vector3 _cutCenter;

        private bool _ovenTransferActive;
        private float _ovenTransferStart;
        private float _ovenTransferDuration;
        private bool _ovenTransferInsertion;
        private GameObject _ovenTransferProp;
        private Vector3 _ovenStartPos;
        private Vector3 _ovenMidPos;
        private Vector3 _ovenEndPos;

        private bool _motoDriveActive;
        private float _motoDriveStart;
        private float _motoDriveDuration;
        private readonly List<Vector3> _activeMotoPath = new List<Vector3>();
        private float _motoTotalDist;

        private bool _dropActive;
        private float _dropStart;
        private float _dropDuration;
        private GameObject _dropProp;
        private Vector3 _dropOrigin;
        private Vector3 _dropLanding;

        private GameObject _decorCar;
        private Vector3 _decorCarStart;
        private Vector3 _decorCarEnd;
        private readonly List<TrailerActor> _decorPedestrians = new List<TrailerActor>();
        private readonly List<(Vector3 start, Vector3 end)> _decorPedRoutes = new List<(Vector3, Vector3)>();

        private static IEnumerable<float> GetLightingHours() => new[] { 17f, 19f, 22f };
        private static IEnumerable<float> GetSpeeds() => new[] { 0.5f, 1f };

        private void Start()
        {
            InitializeEnvironment();
        }

        private void OnDestroy()
        {
            if (cityGenerator != null)
                cityGenerator.OnCityGenerated -= HandleCityGenerated;
            if (_runtimeCityConfig != null)
            {
                Destroy(_runtimeCityConfig);
                _runtimeCityConfig = null;
            }
        }

        public void InitializeEnvironment()
        {
            state = TrailerDirectorState.Initializing;
            environmentReady = false;
            _isInitialized = false;
            lastValidationError = string.Empty;

            if (profile == null)
            {
                FailEnvironment("TrailerStudioProfile não atribuído no Director.");
                return;
            }

            profile.EnsureRuntimeDefaults();
            if (lightingHour <= 0f) lightingHour = profile.defaultLightingHour;
            activeSeed = profile.seed;

            HideGameplayOverlays();

            // Verifica primeiro se já existe uma cidade utilizável na cena antes de regerar
            if (HasUsableExistingCity())
            {
                FinalizeEnvironmentSetup();
                return;
            }

            if (cityGenerator == null)
                cityGenerator = FindFirstObjectByType<CityGenerator>();

            if (cityGenerator == null)
            {
                FailEnvironment("CityGenerator ausente na cena e nenhuma cidade existente encontrada.");
                return;
            }

            PrepareSafeOfflineCityConfig();
            cityGenerator.OnCityGenerated -= HandleCityGenerated;
            cityGenerator.OnCityGenerated += HandleCityGenerated;

            if (!cityGenerator.TryGenerateOfflineWithSeed(profile.seed))
            {
                cityGenerator.OnCityGenerated -= HandleCityGenerated;
                FailEnvironment("TryGenerateOfflineWithSeed retornou false (verifique CityConfig ou estado de rede).");
            }
        }

        private bool HasUsableExistingCity()
        {
            GameObject existingCity = GameObject.Find("__City__");
            var pizzeria = FindFirstObjectByType<PizzeriaRoot>();
            bool hasGraph = cityGenerator != null && cityGenerator.CityData != null && cityGenerator.CityData.streetGraph != null;
            return existingCity != null && existingCity.activeInHierarchy && pizzeria != null && hasGraph;
        }

        private void PrepareSafeOfflineCityConfig()
        {
            CityConfig sourceCfg = profile.cityConfig != null ? profile.cityConfig : cityGenerator.config;
            if (sourceCfg == null) return;

            if (_runtimeCityConfig == null)
                _runtimeCityConfig = Instantiate(sourceCfg);

            _runtimeCityConfig.gerarEmTempoReal = false;
            _runtimeCityConfig.enablePedestrians = false;
            _runtimeCityConfig.maxTrafficVehicles = 0;
            _runtimeCityConfig.minEventInterval = 9999f;
            _runtimeCityConfig.maxEventInterval = 9999f;
            cityGenerator.config = _runtimeCityConfig;

            if (cityGenerator.pizzeriaPrefab == null && profile.pizzeriaPrefab != null)
                cityGenerator.pizzeriaPrefab = profile.pizzeriaPrefab;
        }

        private void HandleCityGenerated()
        {
            if (cityGenerator != null)
                cityGenerator.OnCityGenerated -= HandleCityGenerated;

            FinalizeEnvironmentSetup();
        }

        private void FinalizeEnvironmentSetup()
        {
            if (!TrailerAnchorEnvironment.ResolveAll(cityGenerator, out _env, out string resolveError))
            {
                FailEnvironment(resolveError);
                return;
            }

            if (!EnsureCameraRigReady(out string camError))
            {
                FailEnvironment(camError);
                return;
            }

            if (cinemachineBrain != null)
            {
                cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            }

            EnsureCast();
            EnsureMotorcycle();
            ApplyStudioLighting();

            _isInitialized = true;
            environmentReady = true;
            TryPrepareTake();
        }

        private void FailEnvironment(string message)
        {
            _isInitialized = false;
            environmentReady = false;
            state = TrailerDirectorState.Error;
            lastValidationError = message;
            Debug.LogError($"[TrailerDirector] {message}");
        }

        private bool EnsureCameraRigReady(out string error)
        {
            error = string.Empty;
            if (camerasRoot == null)
            {
                error = "camerasRoot não atribuído no TrailerDirector.";
                return false;
            }
            if (cinemachineBrain == null)
            {
                Camera main = Camera.main;
                if (main != null) cinemachineBrain = main.GetComponent<CinemachineBrain>();
            }
            if (cinemachineBrain == null)
            {
                error = "CinemachineBrain / MainCamera não encontrado na cena.";
                return false;
            }
            cinemachineBrain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);

            // Garante vCams para todos os takes (preservando existentes)
            foreach (var def in profile.takes)
            {
                if (def == null) continue;
                EnsureVCamExists($"{def.id}_A", def.cameraAFov);
                if (def.hasCameraB)
                    EnsureVCamExists($"{def.id}_B", def.cameraBFov);
            }
            return true;
        }

        private void EnsureVCamExists(string vcamName, float fov)
        {
            Transform t = camerasRoot.Find(vcamName);
            if (t == null)
            {
                var go = new GameObject(vcamName);
                go.transform.SetParent(camerasRoot, false);
                t = go.transform;
            }
            var vcam = t.GetComponent<CinemachineVirtualCamera>();
            if (vcam == null) vcam = t.gameObject.AddComponent<CinemachineVirtualCamera>();
            vcam.m_Lens.FieldOfView = fov > 1f ? fov : 45f;
            vcam.Priority = 0;
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
                var cvc = vis.GetComponentInChildren<CharacterVisualController>(true);
                var actor = go.AddComponent<TrailerActor>();
                Color col = (int)id < profile.actorColors.Length ? profile.actorColors[(int)id] : Color.white;
                actor.Initialize(id, cvc, col);
                if (_env != null) actor.SetFallbackFloorY(_env.FloorSurfaceWorldY);
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
                foreach (var col in _motorcycleInstance.GetComponentsInChildren<Collider>(true))
                    col.enabled = false;

                var rig = _motorcycleInstance.GetComponentInChildren<EntityVisualAnchorRig>(true);
                _motoSeatAnchor = rig != null && rig.seatAnchor != null
                    ? rig.seatAnchor
                    : ItemVisualWrapper.FindDeepChild(_motorcycleInstance.transform, "RiderSeatAnchor");

                _motoWheelTransforms.Clear();
                foreach (var tr in _motorcycleInstance.GetComponentsInChildren<Transform>(true))
                {
                    if (tr != null && tr.name.StartsWith("Wheel", StringComparison.OrdinalIgnoreCase))
                        _motoWheelTransforms.Add(tr);
                }
            }
        }

        [Button("Prepare / Reset Take", ButtonSizes.Medium)]
        public void PrepareTake()
        {
            TryPrepareTake();
        }

        public bool TryPrepareTake()
        {
            if (!_isInitialized || !environmentReady || _env == null)
            {
                state = TrailerDirectorState.Error;
                if (string.IsNullOrEmpty(lastValidationError))
                    lastValidationError = "Ambiente ainda não inicializado. Aguarde ou verifique erros de geração.";
                return false;
            }

            ClearTransientTakeState();
            localTime = 0f;

            var def = profile.GetTake(take);
            if (def == null)
            {
                FailTake($"Take {take} não encontrado no Profile.");
                return false;
            }

            profile.EnsureRuntimeDefaults();
            currentTakeName = def.displayName;

            if (!ValidateRequiredAnchors(def, out string anchorErr))
            {
                FailTake(anchorErr);
                return false;
            }

            SetupCastAndInitialStage(def);

            if (playableDirector != null && def.timelineAsset != null)
            {
                playableDirector.playableAsset = def.timelineAsset;
                playableDirector.timeUpdateMode = DirectorUpdateMode.Manual;
                playableDirector.time = 0;
                playableDirector.Evaluate();
            }

            UpdateCameraFrame(def, 0f);
            lastValidationError = string.Empty;
            state = TrailerDirectorState.Ready;
            return true;
        }

        private void FailTake(string message)
        {
            state = TrailerDirectorState.Error;
            lastValidationError = message;
            Debug.LogError($"[TrailerDirector] {message}");
        }

        private bool ValidateRequiredAnchors(TrailerTakeDefinition def, out string error)
        {
            error = string.Empty;
            if (def.focusAnchor != TrailerAnchorKey.None && !_env.TryGetAnchor(def.focusAnchor, out _))
            {
                error = $"{def.id}: Âncora focal obrigatória ausente ({def.focusAnchor}).";
                return false;
            }
            if (def.requiredAnchors != null)
            {
                for (int i = 0; i < def.requiredAnchors.Count; i++)
                {
                    var key = def.requiredAnchors[i];
                    if (key == TrailerAnchorKey.None) continue;
                    if (!_env.TryGetAnchor(key, out _))
                    {
                        error = $"{def.id}: Âncora obrigatória ausente ({key}).";
                        return false;
                    }
                }
            }
            return true;
        }

        private void SetupCastAndInitialStage(TrailerTakeDefinition def)
        {
            // 1. Oculta todos atores, reseta e reativa apenas os participantes do take
            foreach (var kvp in _actors)
            {
                var actor = kvp.Value;
                if (actor == null) continue;
                Color col = (int)kvp.Key < profile.actorColors.Length ? profile.actorColors[(int)kvp.Key] : Color.white;
                actor.SetFallbackFloorY(_env.FloorSurfaceWorldY);
                bool active = def.activeActors != null && def.activeActors.Contains(kvp.Key);
                actor.ResetActor(_env.Pizzeria.transform.position, Quaternion.identity, col, snapToFloor: false);
                actor.gameObject.SetActive(active);
            }

            // 2. Moto visível apenas em takes externos previstos
            if (_motorcycleInstance != null)
            {
                _motorcycleInstance.SetActive(def.showMotorcycle);
                if (def.showMotorcycle && _env.TryGetAnchor(TrailerAnchorKey.PARKING, out var park))
                {
                    _motorcycleInstance.transform.SetPositionAndRotation(park.ObjectPoint, park.ObjectRotation);
                }
            }

            // 3. Estado inicial por take
            switch (take)
            {
                case TrailerTakeId.T01_CityReveal:
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.DISPATCH);
                    SpawnDecorTrafficAndPedestrians();
                    break;

                case TrailerTakeId.T02_KitchenTeam:
                    PlaceActorAtAnchor(TrailerActorId.ActorA, TrailerAnchorKey.PREP_DOUGH);
                    PlaceActorAtAnchor(TrailerActorId.ActorB, TrailerAnchorKey.ASSEMBLY);
                    PlaceActorAtAnchor(TrailerActorId.ActorC, TrailerAnchorKey.OVEN_ENTRY);
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.TICKETS);
                    SpawnPropAtAnchor("dough_raw", TrailerAnchorKey.PREP_DOUGH);
                    SpawnPropAtAnchor("pizza_stage_1_sauce", TrailerAnchorKey.ASSEMBLY);
                    SpawnPropAtAnchor("pizza_baked", TrailerAnchorKey.OVEN_SLOT);
                    ShowTicketsCount(1);
                    break;

                case TrailerTakeId.T03_Dough:
                    PlaceActorAtAnchor(TrailerActorId.ActorA, TrailerAnchorKey.PREP_DOUGH);
                    var dough = SpawnPropAtAnchor("dough_raw", TrailerAnchorKey.PREP_DOUGH);
                    _doughBaseScale = dough != null ? dough.transform.localScale : Vector3.one;
                    _rollingPinProp = SpawnPropAtAnchor("rolling_pin", TrailerAnchorKey.PREP_DOUGH, new Vector3(0.24f, 0.03f, 0f), clearPrevious: false);
                    if (_rollingPinProp != null) _rollingPinBasePos = _rollingPinProp.transform.position;
                    break;

                case TrailerTakeId.T04_Ingredients:
                    PlaceActorAtAnchor(TrailerActorId.ActorA, TrailerAnchorKey.PREP_CUT);
                    SpawnPropAtAnchor("calabresa_whole", TrailerAnchorKey.PREP_CUT);
                    _knifeProp = SpawnPropAtAnchor("knife", TrailerAnchorKey.PREP_CUT, new Vector3(0.24f, 0.02f, 0f), clearPrevious: false);
                    break;

                case TrailerTakeId.T05_Assembly:
                    PlaceActorAtAnchor(TrailerActorId.ActorB, TrailerAnchorKey.ASSEMBLY);
                    SpawnPropAtAnchor("pizza_stage_0_base", TrailerAnchorKey.ASSEMBLY);
                    break;

                case TrailerTakeId.T06_Oven:
                    PlaceActorAtAnchor(TrailerActorId.ActorC, TrailerAnchorKey.OVEN_ENTRY);
                    AttachNewPropToActor(TrailerActorId.ActorC, "pizza_stage_3_calabresa", rightHand: true);
                    SetOvenEffects(fireOn: false, smokeOn: false);
                    break;

                case TrailerTakeId.T07_Packaging:
                    PlaceActorAtAnchor(TrailerActorId.ActorC, TrailerAnchorKey.PACKAGING);
                    SpawnPropAtAnchor("pizza_baked", TrailerAnchorKey.PACKAGING);
                    break;

                case TrailerTakeId.T08_Tickets:
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.TICKETS);
                    ShowTicketsCount(1);
                    break;

                case TrailerTakeId.T09_KitchenChaos:
                    PlaceActorAtAnchor(TrailerActorId.ActorA, TrailerAnchorKey.STORAGE);
                    PlaceActorAtAnchor(TrailerActorId.ActorB, TrailerAnchorKey.ASSEMBLY);
                    PlaceActorAtAnchor(TrailerActorId.ActorC, TrailerAnchorKey.PACKAGING);
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.TICKETS);
                    SpawnPropAtAnchor("pizza_stage_1_sauce", TrailerAnchorKey.ASSEMBLY);
                    SpawnPropAtAnchor("pizza_baked", TrailerAnchorKey.PACKAGING);
                    ShowTicketsCount(2);
                    break;

                case TrailerTakeId.T10_BurntPizza:
                    PlaceActorAtAnchor(TrailerActorId.ActorC, TrailerAnchorKey.PACKAGING);
                    PlaceActorAtAnchor(TrailerActorId.ActorB, TrailerAnchorKey.ASSEMBLY);
                    SpawnPropAtAnchor("pizza_burned", TrailerAnchorKey.OVEN_SLOT);
                    SetOvenEffects(fireOn: true, smokeOn: false);
                    break;

                case TrailerTakeId.T11_Backpack:
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.DISPATCH);
                    if (_actors.TryGetValue(TrailerActorId.ActorD, out var d11))
                        d11.EquipBackpack(profile.deliveryBagVisualPrefab);
                    SpawnPropAtAnchor("pizza_box", TrailerAnchorKey.DISPATCH);
                    break;

                case TrailerTakeId.T12_MotorcycleStart:
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.DISPATCH);
                    if (_actors.TryGetValue(TrailerActorId.ActorD, out var d12))
                        d12.EquipBackpack(profile.deliveryBagVisualPrefab);
                    break;

                case TrailerTakeId.T13_CityRide:
                    if (_motorcycleInstance != null && _env.TryGetAnchor(TrailerAnchorKey.ROUTE_START, out var rStart))
                    {
                        _motorcycleInstance.transform.SetPositionAndRotation(rStart.ObjectPoint, rStart.ObjectRotation);
                    }
                    if (_actors.TryGetValue(TrailerActorId.ActorD, out var d13))
                    {
                        d13.EquipBackpack(profile.deliveryBagVisualPrefab);
                        d13.MountToMotorcycle(_motoSeatAnchor, _motorcycleInstance != null ? _motorcycleInstance.transform : null);
                    }
                    SpawnDecorTrafficAndPedestrians();
                    break;

                case TrailerTakeId.T14_Delivery:
                    if (_motorcycleInstance != null && _env.DeliveryCurbBikePos != Vector3.zero)
                    {
                        Vector3 arriveBack = _env.DeliveryCurbBikeRot * Vector3.forward;
                        _motorcycleInstance.transform.SetPositionAndRotation(
                            _env.DeliveryCurbBikePos - arriveBack * 6f,
                            _env.DeliveryCurbBikeRot);
                    }
                    if (_actors.TryGetValue(TrailerActorId.ActorD, out var d14))
                    {
                        d14.EquipBackpack(profile.deliveryBagVisualPrefab);
                        d14.MountToMotorcycle(_motoSeatAnchor, _motorcycleInstance != null ? _motorcycleInstance.transform : null);
                    }
                    if (_actors.TryGetValue(TrailerActorId.ActorC, out var c14) && _env.TryGetAnchor(TrailerAnchorKey.DELIVERY, out var delivA))
                    {
                        Vector3 custPos = delivA.StandPoint - delivA.FrameForward * 0.65f;
                        Quaternion custRot = Quaternion.LookRotation(delivA.FrameForward, Vector3.up);
                        c14.ResetActor(custPos, custRot, new Color(0.85f, 0.45f, 0.72f), snapToFloor: true);
                    }
                    break;

                case TrailerTakeId.T15_FinalGag:
                    PlaceActorAtAnchor(TrailerActorId.ActorD, TrailerAnchorKey.DISPATCH);
                    AttachNewPropToActor(TrailerActorId.ActorD, "pizza_baked", rightHand: true);
                    break;
            }
        }

        [Button("Play", ButtonSizes.Large), GUIColor(0.3f, 0.85f, 0.4f)]
        public void Play()
        {
            if (!environmentReady)
            {
                Debug.LogWarning("[TrailerDirector] Play bloqueado: ambiente não está pronto.");
                return;
            }

            if (state != TrailerDirectorState.Ready)
            {
                if (!TryPrepareTake())
                    return;
            }

            if (state == TrailerDirectorState.Error || state != TrailerDirectorState.Ready)
                return;

            foreach (var actor in _actors.Values)
            {
                if (actor != null) actor.SetPaused(false);
            }
            SetParticlePause(false);

            state = TrailerDirectorState.Playing;
        }

        [Button("Pause / Resume", ButtonSizes.Medium)]
        public void PauseOrResume()
        {
            if (state == TrailerDirectorState.Playing)
            {
                state = TrailerDirectorState.Paused;
                foreach (var actor in _actors.Values)
                    if (actor != null) actor.SetPaused(true);
                SetParticlePause(true);
            }
            else if (state == TrailerDirectorState.Paused)
            {
                state = TrailerDirectorState.Playing;
                foreach (var actor in _actors.Values)
                    if (actor != null) actor.SetPaused(false);
                SetParticlePause(false);
            }
        }

        [Button("Stop", ButtonSizes.Medium), GUIColor(0.9f, 0.35f, 0.35f)]
        public void StopTake()
        {
            TryPrepareTake();
        }

        [Button("Next Take", ButtonSizes.Medium)]
        public void NextTake()
        {
            int next = ((int)take + 1) % Enum.GetValues(typeof(TrailerTakeId)).Length;
            take = (TrailerTakeId)next;
            TryPrepareTake();
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime, manualSubsystems: false);
        }

        public void StepSimulation(float rawDt, bool manualSubsystems = true)
        {
            if (state != TrailerDirectorState.Playing || profile == null) return;
            var def = profile.GetTake(take);
            if (def == null) return;

            float dt = rawDt * Mathf.Max(0.1f, speed);
            localTime += dt;

            if (playableDirector != null && def.timelineAsset != null)
            {
                playableDirector.time = localTime;
                playableDirector.Evaluate();
            }

            float actionTime = localTime - def.preRoll;
            if (actionTime >= 0f)
            {
                float clampedEval = Mathf.Min(actionTime, def.actionDuration);
                EvaluateCuesExactOnce(def, clampedEval);
                UpdateContinuousActions(clampedEval, dt);
            }

            foreach (var actor in _actors.Values)
            {
                if (actor != null && actor.gameObject.activeInHierarchy)
                {
                    actor.TickMove(dt);
                    if (manualSubsystems && actor.animator != null && actor.animator.gameObject.activeInHierarchy)
                        actor.animator.Update(dt);
                }
            }

            if (manualSubsystems)
            {
                for (int i = 0; i < _decorPedestrians.Count; i++)
                {
                    var p = _decorPedestrians[i];
                    if (p != null && p.animator != null && p.animator.gameObject.activeInHierarchy)
                        p.animator.Update(dt);
                }
                if (_env != null && _env.Oven != null && _env.Oven.smokeParticles != null && _env.Oven.smokeParticles.isPlaying)
                {
                    _env.Oven.smokeParticles.Simulate(dt, true, false);
                }
            }

            UpdateDecorMotion(Mathf.Clamp01(localTime / Mathf.Max(0.1f, def.TotalDuration)));
            UpdateCameraFrame(def, localTime);

            if (localTime >= def.TotalDuration)
            {
                if (loopTake)
                {
                    if (TryPrepareTake()) Play();
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

        public void EvaluateCuesExactOnce(TrailerTakeDefinition def, float actionTime)
        {
            if (def.cues == null) return;
            for (int i = 0; i < def.cues.Count; i++)
            {
                if (_executedCueIndices.Contains(i)) continue;
                var cue = def.cues[i];
                if (cue == null) continue;
                if (cue.time <= actionTime + 0.0001f)
                {
                    _executedCueIndices.Add(i);
                    ExecuteCue(cue);
                }
            }
        }

        private void ExecuteCue(TrailerActionCue cue)
        {
            _actors.TryGetValue(cue.actor, out TrailerActor actor);

            switch (cue.actionType)
            {
                case TrailerActionType.SetPreparing:
                    if (actor != null) actor.SetPreparing(cue.boolValue);
                    break;

                case TrailerActionType.SetSitting:
                    if (actor != null) actor.SetSitting(cue.boolValue, true);
                    break;

                case TrailerActionType.MoveToAnchor:
                    if (actor != null && _env.TryGetAnchor(cue.anchorKey, out var moveA))
                    {
                        actor.StartMove(moveA.StandPoint, cue.duration, cue.floatValue);
                    }
                    break;

                case TrailerActionType.FaceAnchor:
                    if (actor != null && _env.TryGetAnchor(cue.anchorKey, out var faceA))
                    {
                        actor.FacePosition(faceA.ObjectPoint);
                    }
                    break;

                case TrailerActionType.AttachProp:
                    if (actor != null)
                    {
                        if (_slotProps.TryGetValue(cue.anchorKey, out var existingSlotProp) && existingSlotProp != null &&
                            (string.IsNullOrEmpty(cue.propKey) || existingSlotProp.name.IndexOf(cue.propKey, StringComparison.OrdinalIgnoreCase) >= 0 || cue.propKey == "pizza_box"))
                        {
                            _slotProps.Remove(cue.anchorKey);
                            actor.AttachProp(existingSlotProp, rightHand: true);
                        }
                        else if (cue.propKey == "rolling_pin" && _rollingPinProp != null)
                        {
                            actor.AttachProp(_rollingPinProp, rightHand: true);
                        }
                        else if (cue.propKey == "knife" && _knifeProp != null)
                        {
                            actor.AttachProp(_knifeProp, rightHand: true);
                        }
                        else if (!string.IsNullOrEmpty(cue.propKey))
                        {
                            AttachNewPropToActor(cue.actor, cue.propKey, rightHand: true);
                        }
                    }
                    break;

                case TrailerActionType.PlaceProp:
                    if (actor != null)
                    {
                        GameObject held = actor.TakeProp(rightHand: true);
                        if (held != null && _env.TryGetAnchor(cue.anchorKey, out var placeA))
                        {
                            Vector3 placePos = placeA.ObjectPoint;
                            if (cue.propKey == "rolling_pin_side" || cue.propKey == "knife_side")
                                placePos += Vector3.Cross(Vector3.up, placeA.FrameForward).normalized * 0.24f;
                            PlaceExistingPropAt(held, cue.anchorKey, placePos, placeA.ObjectRotation, overwriteSlot: string.IsNullOrEmpty(cue.propKey));
                        }
                    }
                    break;

                case TrailerActionType.SwapProp:
                    if (!string.IsNullOrEmpty(cue.propKey))
                    {
                        SpawnPropAtAnchor(cue.propKey, cue.anchorKey);
                        if (cue.propKey == "dough_open") _doughAnimActive = false;
                        if (cue.propKey == "calabresa_sliced") _cutAnimActive = false;
                    }
                    break;

                case TrailerActionType.SetPizzaStage:
                    if (_slotProps.TryGetValue(cue.anchorKey, out var pizzaObj) && pizzaObj != null)
                    {
                        TrailerPropFactory.ApplyPizzaStage(pizzaObj, cue.propKey);
                    }
                    else
                    {
                        SpawnPropAtAnchor(cue.propKey, cue.anchorKey);
                    }
                    break;

                case TrailerActionType.PackPizza:
                    SpawnPropAtAnchor("pizza_box", cue.anchorKey);
                    if (_slotProps.TryGetValue(cue.anchorKey, out var box) && box != null)
                    {
                        var pivot = box.GetComponentInChildren<GameFeelVisualPivot>();
                        if (pivot != null)
                        {
                            var r = GameFeelRecipe.CreateDefault(GameFeelEventId.BoxPackaged, GameFeelMaterialFamily.CardboardPaper);
                            pivot.PlayRecipe(in r, 1.1f, 7);
                        }
                    }
                    break;

                case TrailerActionType.OvenDoor:
                    if (_env.OvenDoorPresenter != null)
                        _env.OvenDoorPresenter.SetTransferActive(cue.boolValue);
                    break;

                case TrailerActionType.OvenTransfer:
                    StartOvenTransfer(actor, cue);
                    break;

                case TrailerActionType.PostTicket:
                    ShowTicketsCount(Mathf.Max(1, Mathf.RoundToInt(cue.floatValue)));
                    break;

                case TrailerActionType.PlayFx:
                    if (cue.fxKey == "dough_roll")
                    {
                        _doughAnimActive = true;
                        _doughAnimStart = cue.time;
                        _doughAnimEnd = 4.20f;
                    }
                    else if (cue.fxKey == "cut_calabresa")
                    {
                        _cutAnimActive = true;
                        _cutAnimStart = cue.time;
                        if (_env.TryGetAnchor(cue.anchorKey, out var cutA))
                            _cutCenter = cutA.ObjectPoint;
                    }
                    else if (cue.fxKey == "oven_bake_start")
                    {
                        SetOvenEffects(fireOn: true, smokeOn: false);
                    }
                    else if (cue.fxKey == "oven_smoke_start")
                    {
                        SetOvenEffects(fireOn: true, smokeOn: true);
                    }
                    break;

                case TrailerActionType.PlayReaction:
                    if (actor != null && Enum.TryParse(cue.fxKey, out GameFeelEventId evt))
                        actor.PlayReaction(evt, 1.2f);
                    break;

                case TrailerActionType.BagEquip:
                    if (actor != null)
                        actor.EquipBackpack(profile.deliveryBagVisualPrefab);
                    break;

                case TrailerActionType.StoreBoxInBag:
                    if (actor != null)
                    {
                        GameObject boxHeld = actor.TakeProp(rightHand: true);
                        if (boxHeld != null)
                        {
                            boxHeld.SetActive(false);
                            Destroy(boxHeld);
                        }
                        actor.TriggerBackpackPulse();
                    }
                    break;

                case TrailerActionType.TakeBoxFromBag:
                    if (actor != null)
                    {
                        AttachNewPropToActor(cue.actor, "pizza_box", rightHand: true);
                        actor.TriggerBackpackPulse();
                    }
                    break;

                case TrailerActionType.MountMoto:
                    if (actor != null && _motorcycleInstance != null)
                        actor.MountToMotorcycle(_motoSeatAnchor, _motorcycleInstance.transform);
                    break;

                case TrailerActionType.DismountMoto:
                    if (actor != null && _motorcycleInstance != null)
                    {
                        Vector3 leftOff = _motorcycleInstance.transform.TransformPoint(new Vector3(-1.15f, 0f, 0f));
                        actor.DismountFromMotorcycle(leftOff, _motorcycleInstance.transform.rotation);
                    }
                    break;

                case TrailerActionType.DriveMoto:
                    StartMotoDrive(cue);
                    break;

                case TrailerActionType.DeliverAtPoint:
                    if (actor != null)
                    {
                        GameObject deliveredBox = actor.TakeProp(rightHand: true);
                        if (deliveredBox != null)
                        {
                            if (_actors.TryGetValue(TrailerActorId.ActorC, out var custActor) && custActor != null && custActor.gameObject.activeInHierarchy)
                            {
                                custActor.AttachProp(deliveredBox, rightHand: true);
                                custActor.PlayReaction(GameFeelEventId.DeliverySuccess, 1.15f);
                            }
                            else
                            {
                                deliveredBox.SetActive(false);
                                Destroy(deliveredBox);
                            }
                        }
                        DeliveryPointView.PlayDeliveryFeedbackForAddress(_env.DeliveryAddressId);
                    }
                    break;

                case TrailerActionType.DropProp:
                    if (actor != null)
                    {
                        GameObject dropped = actor.TakeProp(rightHand: true);
                        if (dropped != null)
                        {
                            dropped.transform.SetParent(propsRoot, true);
                            _spawnedProps.Add(dropped);
                            _dropProp = dropped;
                            _dropOrigin = dropped.transform.position;
                            Vector3 probe = _dropOrigin + actor.transform.forward * 0.42f;
                            Vector3 floorPt = actor.SnapPositionToFloor(probe, _env.FloorSurfaceWorldY);
                            _dropLanding = floorPt + Vector3.up * 0.03f;
                            _dropStart = cue.time;
                            _dropDuration = Mathf.Max(0.25f, cue.duration);
                            _dropActive = true;
                        }
                    }
                    break;

                case TrailerActionType.LookAtTarget:
                    if (actor != null && Camera.main != null)
                    {
                        actor.SetHeadLookDown(0f);
                        actor.FacePosition(Camera.main.transform.position);
                    }
                    break;
            }
        }

        private void UpdateContinuousActions(float actionTime, float dt)
        {
            if (_doughAnimActive && _slotProps.TryGetValue(TrailerAnchorKey.PREP_DOUGH, out var doughObj) && doughObj != null)
            {
                float p = Mathf.Clamp01((actionTime - _doughAnimStart) / Mathf.Max(0.1f, _doughAnimEnd - _doughAnimStart));
                float xz = Mathf.Lerp(1f, 1.35f, p);
                float y = Mathf.Lerp(1f, 0.55f, p);
                doughObj.transform.localScale = new Vector3(_doughBaseScale.x * xz, _doughBaseScale.y * y, _doughBaseScale.z * xz);

                if (_rollingPinProp != null)
                {
                    float sway = Mathf.Sin(actionTime * 7.5f) * 0.09f;
                    _rollingPinProp.transform.localPosition = new Vector3(0f, 0.03f, 0.09f + sway);
                }
            }

            if (_cutAnimActive && _knifeProp != null)
            {
                float cycle = (actionTime % 0.34f) / 0.34f;
                float chop = Mathf.Sin(cycle * Mathf.PI) * 0.07f;
                _knifeProp.transform.localPosition = new Vector3(0f, 0.04f + chop, 0.08f);
                _knifeProp.transform.localRotation = Quaternion.Euler(-15f + chop * 220f, 0f, 0f);
            }

            if (_ovenTransferActive && _ovenTransferProp != null)
            {
                float t = Mathf.Clamp01((actionTime - _ovenTransferStart) / Mathf.Max(0.05f, _ovenTransferDuration));
                float smooth = Mathf.SmoothStep(0f, 1f, t);
                Vector3 pos = SampleQuadraticArc(_ovenStartPos, _ovenMidPos, _ovenEndPos, smooth);
                _ovenTransferProp.transform.position = pos;
                if (t >= 1f)
                {
                    _ovenTransferActive = false;
                    if (_ovenTransferInsertion)
                    {
                        PlaceExistingPropAt(_ovenTransferProp, TrailerAnchorKey.OVEN_SLOT, _ovenEndPos, Quaternion.identity, true);
                    }
                    else
                    {
                        if (_actors.TryGetValue(TrailerActorId.ActorC, out var actorC))
                            actorC.AttachProp(_ovenTransferProp, rightHand: true);
                    }
                    _ovenTransferProp = null;
                }
            }

            if (_motoDriveActive && _motorcycleInstance != null && _activeMotoPath.Count >= 2)
            {
                float t = Mathf.Clamp01((actionTime - _motoDriveStart) / Mathf.Max(0.1f, _motoDriveDuration));
                float eased = t < 0.25f ? Mathf.SmoothStep(0f, 0.25f, t / 0.25f) * 4f * 0.25f : t;
                SamplePathAtDistance(_activeMotoPath, eased * _motoTotalDist, out Vector3 pos, out Vector3 fwd);
                Quaternion targetRot = Quaternion.LookRotation(fwd, Vector3.up);
                _motorcycleInstance.transform.SetPositionAndRotation(pos, Quaternion.Slerp(_motorcycleInstance.transform.rotation, targetRot, Mathf.Clamp01(dt * 12f)));

                float wheelSpinDeg = (_motoTotalDist / Mathf.Max(0.1f, _motoDriveDuration)) * dt * 190f;
                for (int i = 0; i < _motoWheelTransforms.Count; i++)
                {
                    if (_motoWheelTransforms[i] != null)
                        _motoWheelTransforms[i].Rotate(Vector3.right, wheelSpinDeg, Space.Self);
                }

                if (t >= 1f) _motoDriveActive = false;
            }

            if (_dropActive && _dropProp != null)
            {
                float t = Mathf.Clamp01((actionTime - _dropStart) / Mathf.Max(0.1f, _dropDuration));
                Vector3 pos = Vector3.Lerp(_dropOrigin, _dropLanding, t);
                float arc = t < 0.72f ? Mathf.Sin((t / 0.72f) * Mathf.PI) * 0.14f : Mathf.Sin(((t - 0.72f) / 0.28f) * Mathf.PI) * 0.05f;
                pos.y += arc;
                _dropProp.transform.position = pos;
                _dropProp.transform.rotation = Quaternion.Euler(t * 24f, 0f, t * 14f);
                if (t >= 1f)
                {
                    _dropActive = false;
                    if (_actors.TryGetValue(TrailerActorId.ActorD, out var actorD))
                        actorD.SetHeadLookDown(22f);
                }
            }
        }

        private void StartOvenTransfer(TrailerActor actor, TrailerActionCue cue)
        {
            if (!_env.TryGetAnchor(TrailerAnchorKey.OVEN_SLOT, out var slotA) ||
                !_env.TryGetAnchor(TrailerAnchorKey.OVEN_ENTRY, out var entryA))
                return;

            _ovenTransferInsertion = cue.boolValue;
            GameObject pizza = null;
            if (_ovenTransferInsertion)
            {
                if (actor != null) pizza = actor.TakeProp(rightHand: true);
                if (pizza != null)
                {
                    pizza.transform.SetParent(propsRoot, true);
                    _spawnedProps.Add(pizza);
                }
                _ovenStartPos = pizza != null ? pizza.transform.position : actor.GetHandBone(true).position;
                _ovenMidPos = entryA.ObjectPoint;
                _ovenEndPos = slotA.ObjectPoint;
            }
            else
            {
                if (_slotProps.TryGetValue(TrailerAnchorKey.OVEN_SLOT, out pizza) && pizza != null)
                    _slotProps.Remove(TrailerAnchorKey.OVEN_SLOT);
                _ovenStartPos = slotA.ObjectPoint;
                _ovenMidPos = entryA.ObjectPoint;
                _ovenEndPos = actor != null ? actor.GetHandBone(true).position : entryA.StandPoint + Vector3.up * 1.0f;
            }

            if (pizza != null)
            {
                _ovenTransferProp = pizza;
                _ovenTransferStart = cue.time;
                _ovenTransferDuration = Mathf.Max(0.2f, cue.duration);
                _ovenTransferActive = true;
            }
        }

        private void StartMotoDrive(TrailerActionCue cue)
        {
            _activeMotoPath.Clear();
            if (take == TrailerTakeId.T12_MotorcycleStart)
            {
                _activeMotoPath.AddRange(_env.MotoStartRoute);
            }
            else if (take == TrailerTakeId.T13_CityRide)
            {
                _activeMotoPath.AddRange(_env.CityRideRoute);
            }
            else if (take == TrailerTakeId.T14_Delivery && _motorcycleInstance != null)
            {
                _activeMotoPath.Add(_motorcycleInstance.transform.position);
                _activeMotoPath.Add(_env.DeliveryCurbBikePos);
            }

            _motoTotalDist = 0f;
            for (int i = 1; i < _activeMotoPath.Count; i++)
                _motoTotalDist += Vector3.Distance(_activeMotoPath[i - 1], _activeMotoPath[i]);

            _motoDriveStart = cue.time;
            _motoDriveDuration = Mathf.Max(0.25f, cue.duration);
            _motoDriveActive = _activeMotoPath.Count >= 2 && _motoTotalDist > 0.1f;
        }

        private void UpdateCameraFrame(TrailerTakeDefinition def, float t)
        {
            if (camerasRoot == null || _env == null) return;

            bool useB = cameraMode == TrailerCameraMode.CameraB_Alt && def.hasCameraB;
            string targetName = $"{def.id}_{(useB ? "B" : "A")}";
            activeCameraName = targetName;

            var vcams = camerasRoot.GetComponentsInChildren<CinemachineVirtualCamera>(true);
            CinemachineVirtualCamera chosen = null;
            for (int i = 0; i < vcams.Length; i++)
            {
                bool match = vcams[i].name == targetName;
                vcams[i].Priority = match ? 20 : 0;
                if (match) chosen = vcams[i];
            }
            if (chosen == null) return;
            _activeVCam = chosen;

            if (!_env.TryGetAnchor(def.focusAnchor, out var anchor)) return;

            Vector3 q = anchor.FocusPoint;
            Vector3 f = anchor.FrameForward;

            // Foco móvel para takes dinâmicos
            if ((take == TrailerTakeId.T12_MotorcycleStart || take == TrailerTakeId.T13_CityRide) && _motorcycleInstance != null)
            {
                q = _motorcycleInstance.transform.position + Vector3.up * 0.85f;
                if (take == TrailerTakeId.T13_CityRide)
                {
                    Vector3 mf = _motorcycleInstance.transform.forward;
                    mf.y = 0f;
                    if (mf.sqrMagnitude > 0.01f) f = mf.normalized;
                }
            }
            else if ((take == TrailerTakeId.T09_KitchenChaos || take == TrailerTakeId.T11_Backpack) &&
                     _actors.TryGetValue(TrailerActorId.ActorD, out var actorD) && actorD != null && actorD.gameObject.activeInHierarchy)
            {
                q = actorD.transform.position + Vector3.up * 1.15f;
            }
            else if (take == TrailerTakeId.T15_FinalGag)
            {
                if (_actors.TryGetValue(TrailerActorId.ActorD, out var gagD) && gagD != null)
                {
                    Vector3 actorCenter = gagD.transform.position + Vector3.up * 0.85f;
                    if (_dropProp != null && t >= def.preRoll + 1.2f)
                        q = Vector3.Lerp(actorCenter, _dropProp.transform.position + Vector3.up * 0.25f, 0.52f);
                    else
                        q = actorCenter;
                }
            }

            f.y = 0f;
            if (f.sqrMagnitude < 0.001f) f = Vector3.forward;
            f.Normalize();
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized;

            if (useB && (take == TrailerTakeId.T02_KitchenTeam || take == TrailerTakeId.T05_Assembly || take == TrailerTakeId.T09_KitchenChaos))
            {
                // Perspectiva FP limpa a partir do ator
                TrailerActorId fpActorId = take == TrailerTakeId.T05_Assembly ? TrailerActorId.ActorB : TrailerActorId.ActorD;
                if (_actors.TryGetValue(fpActorId, out var fpActor) && fpActor != null)
                {
                    Vector3 eye = fpActor.transform.position + Vector3.up * 1.56f + fpActor.transform.forward * 0.24f;
                    Vector3 look = take == TrailerTakeId.T05_Assembly ? q : (q + Vector3.up * 0.1f);
                    chosen.transform.position = eye;
                    chosen.transform.LookAt(look, Vector3.up);
                    chosen.m_Lens.FieldOfView = def.cameraBFov > 1f ? def.cameraBFov : 62f;
                    return;
                }
            }

            float norm = def.TotalDuration > 0.01f ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - def.preRoll) / Mathf.Max(0.1f, def.actionDuration))) : 0f;
            Vector3 offset = useB
                ? def.cameraBOffset
                : Vector3.Lerp(def.cameraAStartOffset, def.cameraAEndOffset != Vector3.zero ? def.cameraAEndOffset : def.cameraAStartOffset, norm);

            if (offset == Vector3.zero) offset = new Vector3(1.0f, 0.8f, 1.5f);

            Vector3 desiredCamPos = q + r * offset.x + Vector3.up * offset.y + f * offset.z;

            // Previne oclusão por parede em takes internos
            if (!def.showMotorcycle && take != TrailerTakeId.T01_CityReveal && take != TrailerTakeId.T14_Delivery)
            {
                Vector3 toCam = desiredCamPos - q;
                float dist = toCam.magnitude;
                if (dist > 0.35f && Physics.SphereCast(q, 0.14f, toCam / dist, out RaycastHit hit, dist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != null && !hit.collider.isTrigger && hit.distance > 0.45f)
                    {
                        string hitName = hit.collider.name.ToLowerInvariant();
                        if (hitName.Contains("wall") || hitName.Contains("parede") || hitName.Contains("ceiling") || hitName.Contains("teto"))
                        {
                            desiredCamPos = q + (toCam / dist) * Mathf.Max(0.45f, hit.distance - 0.18f);
                        }
                    }
                }
            }

            chosen.transform.position = desiredCamPos;
            Vector3 upVec = (useB && take == TrailerTakeId.T03_Dough) || (!useB && take == TrailerTakeId.T05_Assembly)
                ? -f
                : Vector3.up;
            chosen.transform.LookAt(q, upVec);
            chosen.m_Lens.FieldOfView = useB ? (def.cameraBFov > 1f ? def.cameraBFov : def.cameraAFov) : def.cameraAFov;
        }

        private void PlaceActorAtAnchor(TrailerActorId id, TrailerAnchorKey key)
        {
            if (!_actors.TryGetValue(id, out var actor) || actor == null) return;
            if (!_env.TryGetAnchor(key, out var a)) return;
            Color col = (int)id < profile.actorColors.Length ? profile.actorColors[(int)id] : Color.white;
            actor.ResetActor(a.StandPoint, a.StandRotation, col, snapToFloor: true);
        }

        private GameObject SpawnPropAtAnchor(string propKey, TrailerAnchorKey key, Vector3 localRightUpForwardOffset = default, bool clearPrevious = true)
        {
            if (!_env.TryGetAnchor(key, out var a)) return null;
            GameObject prop = TrailerPropFactory.CreateProp(propKey, profile, propsRoot, _env.DeliveryAddressId, out _);
            if (prop == null) return null;

            Vector3 r = Vector3.Cross(Vector3.up, a.FrameForward).normalized;
            Vector3 worldPos = a.ObjectPoint + r * localRightUpForwardOffset.x + Vector3.up * localRightUpForwardOffset.y + a.FrameForward * localRightUpForwardOffset.z;
            PlaceExistingPropAt(prop, key, worldPos, a.ObjectRotation, clearPrevious);
            return prop;
        }

        private void AttachNewPropToActor(TrailerActorId id, string propKey, bool rightHand)
        {
            if (!_actors.TryGetValue(id, out var actor) || actor == null) return;
            GameObject prop = TrailerPropFactory.CreateProp(propKey, profile, propsRoot, _env.DeliveryAddressId, out var usedProfile);
            if (prop == null) return;
            _spawnedProps.Add(prop);
            actor.AttachProp(prop, rightHand, usedProfile);
        }

        private void PlaceExistingPropAt(GameObject prop, TrailerAnchorKey key, Vector3 worldPos, Quaternion worldRot, bool overwriteSlot)
        {
            if (prop == null) return;
            prop.transform.SetParent(propsRoot, false);
            prop.transform.SetPositionAndRotation(worldPos, worldRot);
            prop.SetActive(true);
            if (!_spawnedProps.Contains(prop)) _spawnedProps.Add(prop);

            if (overwriteSlot)
            {
                if (_slotProps.TryGetValue(key, out var old) && old != null && old != prop)
                {
                    old.SetActive(false);
                    Destroy(old);
                }
                _slotProps[key] = prop;
            }
        }

        private void ShowTicketsCount(int count)
        {
            if (_env == null || _env.TicketBoard == null) return;
            var board = _env.TicketBoard;
            if (board.ticketSlots == null) return;

            int max = Mathf.Min(count, board.ticketSlots.Length);
            for (int i = 0; i < board.ticketSlots.Length; i++)
            {
                var slot = board.ticketSlots[i];
                if (slot == null) continue;
                var sheet = slot.GetComponentInChildren<TicketSheetView>(true);
                if (sheet == null) continue;

                if (i < max)
                {
                    var dto = new NetOrderDto
                    {
                        OrderId = (ulong)(101 + i),
                        DestinationId = Mathf.Max(0, _env.DeliveryAddressId + i),
                        Lifecycle = (byte)OrderLifecycle.Active,
                        TaskStage = (byte)OrderTaskBoardStage.New,
                        DeadlineTime = 180.0 - i * 35.0,
                        LineCount = 1,
                        RecipeId_0 = 2,
                        Qty_0 = 1
                    };
                    sheet.BindOrder(dto, 0.0, null);
                }
                else
                {
                    sheet.Clear();
                }
            }
        }

        private void SetOvenEffects(bool fireOn, bool smokeOn)
        {
            if (_env == null || _env.Oven == null) return;
            if (_env.Oven.ovenFireLight != null)
            {
                _env.Oven.ovenFireLight.enabled = fireOn;
                _env.Oven.ovenFireLight.intensity = fireOn ? 2.4f : 0f;
            }
            if (_env.Oven.smokeParticles != null)
            {
                if (smokeOn)
                {
                    var main = _env.Oven.smokeParticles.main;
                    main.startColor = new Color(0.12f, 0.12f, 0.12f, 0.72f);
                    _env.Oven.smokeParticles.Play();
                }
                else
                {
                    _env.Oven.smokeParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void SetParticlePause(bool paused)
        {
            if (_env != null && _env.Oven != null && _env.Oven.smokeParticles != null)
            {
                if (paused) _env.Oven.smokeParticles.Pause(true);
                else if (_env.Oven.smokeParticles.isPaused) _env.Oven.smokeParticles.Play(true);
            }
        }

        private void SpawnDecorTrafficAndPedestrians()
        {
            if (take == TrailerTakeId.T01_CityReveal && _env.TryGetAnchor(TrailerAnchorKey.FACADE, out var fac))
            {
                Vector3 r = Vector3.Cross(Vector3.up, fac.FrameForward).normalized;
                Vector3 streetPos = fac.ObjectPoint + fac.FrameForward * 8.5f;
                streetPos.y = _env.FloorSurfaceWorldY - 0.15f;
                SpawnDecorCar(streetPos - r * 22f, streetPos + r * 22f);

                if (profile.characterVisualPrefab != null)
                {
                    Vector3 walkY = new Vector3(0f, _env.FloorSurfaceWorldY, 0f);
                    Vector3 p1Start = fac.StandPoint + r * 7f; p1Start.y = walkY.y;
                    Vector3 p1End = fac.StandPoint + r * 14f; p1End.y = walkY.y;
                    SpawnDecorPedestrian("DecorPed_1", p1Start, p1End, new Color(0.55f, 0.65f, 0.75f));

                    Vector3 p2Start = fac.StandPoint - r * 12f; p2Start.y = walkY.y;
                    Vector3 p2End = fac.StandPoint - r * 5f; p2End.y = walkY.y;
                    SpawnDecorPedestrian("DecorPed_2", p2Start, p2End, new Color(0.75f, 0.55f, 0.65f));
                }
            }
            else if (take == TrailerTakeId.T13_CityRide && _env.CityRideRoute.Count >= 2)
            {
                Vector3 mid = _env.CityRideRoute[_env.CityRideRoute.Count / 2];
                Vector3 dir = (_env.CityRideRoute[Mathf.Min(_env.CityRideRoute.Count - 1, _env.CityRideRoute.Count / 2 + 1)] - mid).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir).normalized * 2.8f;
                SpawnDecorCar(mid + dir * 24f + side, mid - dir * 18f + side);
            }
        }

        private void SpawnDecorCar(Vector3 start, Vector3 end)
        {
            var car = GameObject.CreatePrimitive(PrimitiveType.Cube);
            car.name = "TrailerDecorCar";
            car.transform.SetParent(vehiclesRoot, false);
            Destroy(car.GetComponent<Collider>());
            car.transform.localScale = new Vector3(2.0f, 1.15f, 4.1f);
            TrailerPropFactory.EnsureUrpMaterialAndTint(car, "car");
            car.transform.position = start + Vector3.up * 0.65f;
            car.transform.LookAt(end + Vector3.up * 0.65f);
            _decorObjects.Add(car);
            _decorCar = car;
            _decorCarStart = start + Vector3.up * 0.65f;
            _decorCarEnd = end + Vector3.up * 0.65f;
        }

        private void SpawnDecorPedestrian(string name, Vector3 start, Vector3 end, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(castRoot, false);
            var vis = Instantiate(profile.characterVisualPrefab, go.transform);
            var cvc = vis.GetComponentInChildren<CharacterVisualController>(true);
            var ped = go.AddComponent<TrailerActor>();
            ped.Initialize(TrailerActorId.ActorA, cvc, col);
            ped.ResetActor(start, Quaternion.LookRotation((end - start).normalized, Vector3.up), col, snapToFloor: true);
            if (cvc != null) cvc.SetLocomotion(1.8f, 1f, true);
            _decorObjects.Add(go);
            _decorPedestrians.Add(ped);
            _decorPedRoutes.Add((start, end));
        }

        private void UpdateDecorMotion(float normalizedTakeTime)
        {
            if (_decorCar != null)
            {
                _decorCar.transform.position = Vector3.Lerp(_decorCarStart, _decorCarEnd, normalizedTakeTime);
            }
            for (int i = 0; i < _decorPedestrians.Count; i++)
            {
                if (_decorPedestrians[i] == null) continue;
                var route = _decorPedRoutes[i];
                _decorPedestrians[i].transform.position = Vector3.Lerp(route.start, route.end, normalizedTakeTime);
            }
        }

        private void ClearTransientTakeState()
        {
            _executedCueIndices.Clear();
            _doughAnimActive = false;
            _cutAnimActive = false;
            _ovenTransferActive = false;
            _motoDriveActive = false;
            _dropActive = false;
            _rollingPinProp = null;
            _knifeProp = null;
            _ovenTransferProp = null;
            _dropProp = null;
            _decorCar = null;
            _decorPedestrians.Clear();
            _decorPedRoutes.Clear();

            for (int i = 0; i < _spawnedProps.Count; i++)
            {
                if (_spawnedProps[i] != null)
                {
                    _spawnedProps[i].SetActive(false);
                    Destroy(_spawnedProps[i]);
                }
            }
            _spawnedProps.Clear();
            _slotProps.Clear();

            for (int i = 0; i < _decorObjects.Count; i++)
            {
                if (_decorObjects[i] != null)
                {
                    _decorObjects[i].SetActive(false);
                    Destroy(_decorObjects[i]);
                }
            }
            _decorObjects.Clear();

            if (_env != null)
            {
                _env.OvenDoorPresenter?.SnapClosed();
                SetOvenEffects(fireOn: false, smokeOn: false);
                ShowTicketsCount(0);
            }
        }

        private void ApplyStudioLighting()
        {
            var tod = FindFirstObjectByType<TimeOfDayLightingDirector>();
            if (tod != null)
            {
                tod.ConfigureFromRuntimeConfig(enableCycle: false, startHour: lightingHour, lockedForTests: true);
                tod.EvaluateAndApply(lightingHour);
                return;
            }

            Light sun = RenderSettings.sun ?? FindFirstObjectByType<Light>();
            if (sun != null && sun.type == LightType.Directional)
            {
                if (lightingHour <= 17.5f)
                {
                    sun.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
                    sun.color = new Color(1.0f, 0.88f, 0.72f);
                    sun.intensity = 1.25f;
                }
                else if (lightingHour <= 20f)
                {
                    sun.transform.rotation = Quaternion.Euler(24f, -40f, 0f);
                    sun.color = new Color(1.0f, 0.76f, 0.56f);
                    sun.intensity = 1.05f;
                }
                else
                {
                    sun.transform.rotation = Quaternion.Euler(35f, -55f, 0f);
                    sun.color = new Color(0.62f, 0.72f, 0.95f);
                    sun.intensity = 0.75f;
                }
            }
        }

        private static void HideGameplayOverlays()
        {
            var uiDocs = FindObjectsByType<UnityEngine.UIElements.UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < uiDocs.Length; i++)
            {
                if (uiDocs[i] != null) uiDocs[i].gameObject.SetActive(false);
            }
        }

        private static void SamplePathAtDistance(List<Vector3> pts, float targetDist, out Vector3 pos, out Vector3 fwd)
        {
            pos = pts[0];
            fwd = (pts[1] - pts[0]).normalized;
            float accum = 0f;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = Vector3.Distance(pts[i - 1], pts[i]);
                if (accum + seg >= targetDist || i == pts.Count - 1)
                {
                    float localT = seg > 0.0001f ? Mathf.Clamp01((targetDist - accum) / seg) : 1f;
                    pos = Vector3.Lerp(pts[i - 1], pts[i], localT);
                    Vector3 d = pts[i] - pts[i - 1];
                    d.y = 0f;
                    if (d.sqrMagnitude > 0.0001f) fwd = d.normalized;
                    return;
                }
                accum += seg;
            }
        }

        private static Vector3 SampleQuadraticArc(Vector3 a, Vector3 mid, Vector3 b, float t)
        {
            float inv = 1f - t;
            return inv * inv * a + 2f * inv * t * mid + t * t * b;
        }

        private void OnTakeChanged()
        {
            if (Application.isPlaying && _isInitialized) TryPrepareTake();
        }

        private void OnCameraChanged()
        {
            if (Application.isPlaying && _isInitialized && profile != null)
            {
                var def = profile.GetTake(take);
                if (def != null) UpdateCameraFrame(def, localTime);
            }
        }

        private void OnLightingChanged()
        {
            if (Application.isPlaying && _isInitialized) ApplyStudioLighting();
        }
    }
}
