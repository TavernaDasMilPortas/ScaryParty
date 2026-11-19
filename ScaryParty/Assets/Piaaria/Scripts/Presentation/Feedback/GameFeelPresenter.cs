using System.Collections.Generic;
using MoreMountains.Feedbacks;
using ScaryParty.Player;
using ScaryParty.UI.Piaria.Core;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    /// <summary>
    /// Coordenador central de Game Feel da pizzaria/cidade.
    /// Orquestra Feel (MMF_Player), pivôs cosméticos isolados (GameFeelVisualPivot), partículas em pool e áudio espacial.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFeelPresenter : MonoBehaviour
    {
        public static GameFeelPresenter Instance { get; private set; }

        [Header("Perfil de Game Feel")]
        public GameFeelProfile activeProfile;

        private readonly GameFeelDeduplicator _deduplicator = new GameFeelDeduplicator();
        private readonly Dictionary<(ulong entityId, GameFeelEventId evt), float> _cooldownUntil =
            new Dictionary<(ulong, GameFeelEventId), float>();
        private readonly Dictionary<int, MMF_Player> _optionalFeelInstances = new Dictionary<int, MMF_Player>();

        private ParticleSystem[] _burstPool;
        private Material _sharedParticleMaterial;
        private bool _createdParticleMaterialAtRuntime;
        private int _burstCursor;
        private int _activeBurstEstimate;

        public GameFeelDeduplicator Deduplicator => _deduplicator;

        public static GameFeelPresenter EnsureInstance()
        {
            if (Instance != null) return Instance;

            var existing = FindFirstObjectByType<GameFeelPresenter>();
            if (existing != null)
            {
                Instance = existing;
                existing.EnsureProfileLoaded();
                return existing;
            }

            var go = new GameObject("GameFeelPresenter");
            Instance = go.AddComponent<GameFeelPresenter>();
            DontDestroyOnLoad(go);
            Instance.EnsureProfileLoaded();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            EnsureProfileLoaded();
            EnsureBurstPool();
        }

        private void OnDestroy()
        {
            if (_createdParticleMaterialAtRuntime && _sharedParticleMaterial != null)
            {
                Destroy(_sharedParticleMaterial);
                _sharedParticleMaterial = null;
            }
            if (Instance == this) Instance = null;
        }

        public void EnsureProfileLoaded()
        {
            if (activeProfile != null) return;
            activeProfile = Resources.Load<GameFeelProfile>("GameFeelDefaultProfile");
            if (activeProfile == null)
            {
                activeProfile = GameFeelProfile.CreateRuntimeDefault();
            }
        }

        public void SetProfile(GameFeelProfile profile)
        {
            activeProfile = profile != null ? profile : GameFeelProfile.CreateRuntimeDefault();
        }

        public void TriggerSimple(
            GameFeelEventId eventId,
            Transform target = null,
            ulong entityId = 0,
            int stationId = 0,
            uint revision = 0,
            GameFeelMaterialFamily family = GameFeelMaterialFamily.Default,
            float intensity = 1f,
            bool isLocal = true,
            int slotIndex = -1)
        {
            var ctx = GameFeelContext.Create(eventId, entityId, stationId, revision, family);
            ctx.TargetTransform = target;
            ctx.WorldPosition = target != null ? target.position : Vector3.zero;
            ctx.Intensity = intensity;
            ctx.IsLocalOwner = isLocal;
            ctx.SlotIndex = slotIndex;
            Trigger(in ctx);
        }

        public void TriggerAtPosition(
            GameFeelEventId eventId,
            Vector3 worldPos,
            Transform target = null,
            ulong entityId = 0,
            int stationId = 0,
            uint revision = 0,
            GameFeelMaterialFamily family = GameFeelMaterialFamily.Default,
            float intensity = 1f,
            bool isLocal = true,
            int slotIndex = -1)
        {
            var ctx = GameFeelContext.Create(eventId, entityId, stationId, revision, family);
            ctx.TargetTransform = target;
            ctx.WorldPosition = worldPos != Vector3.zero ? worldPos : (target != null ? target.position : Vector3.zero);
            ctx.Intensity = intensity;
            ctx.IsLocalOwner = isLocal;
            ctx.SlotIndex = slotIndex;
            Trigger(in ctx);
        }

        public void Trigger(in GameFeelContext ctx)
        {
            if (ctx.EventId == GameFeelEventId.None) return;
            EnsureProfileLoaded();

            double nowDouble = ctx.Timestamp >= 0d ? ctx.Timestamp : Time.unscaledTimeAsDouble;
            if (!_deduplicator.TryAccept(in ctx, nowDouble))
            {
                return;
            }

            var recipe = activeProfile.GetRecipeOrFallback(ctx.EventId, ctx.MaterialFamily);
            float now = Time.unscaledTime;
            var cdKey = (ctx.EntityId, ctx.EventId);
            if (recipe.Cooldown > 0.001f && _cooldownUntil.TryGetValue(cdKey, out float until) && now < until)
            {
                return;
            }
            if (recipe.Cooldown > 0.001f)
            {
                _cooldownUntil[cdKey] = now + recipe.Cooldown;
                if (_cooldownUntil.Count > 512) TrimCooldowns(now);
            }

            // 1. Canal Movimento (Pivô cosmético / Feel)
            if ((recipe.Channels & GameFeelChannel.Movement) != 0)
            {
                float moveMult = GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Movement) * Mathf.Clamp(ctx.Intensity, 0.1f, 1.5f);
                if (moveMult > 0.001f && ctx.TargetTransform != null)
                {
                    var pivot = ctx.TargetTransform.GetComponent<GameFeelVisualPivot>()
                             ?? ctx.TargetTransform.GetComponentInChildren<GameFeelVisualPivot>(true);
                    pivot?.PlayRecipe(in recipe, moveMult, ctx.VariantSeed);
                }

                // Micro-resposta na mão do jogador local
                if (ctx.IsLocalOwner && FirstPersonCameraController.LocalInstance != null)
                {
                    TriggerLocalHandReaction(in ctx);
                }
            }

            // 2. Canal Optional MMF_Player (Feel Authoring override)
            if (recipe.OptionalFeelPrefab != null && !GameFeelSettings.ReduceMotion)
            {
                PlayOptionalFeelPrefab(recipe.OptionalFeelPrefab, ctx.WorldPosition, ctx.Intensity);
            }

            // 3. Canal Partículas leves em pool
            if ((recipe.Channels & GameFeelChannel.Particles) != 0)
            {
                float partMult = GameFeelSettings.GetEffectiveChannelIntensity(GameFeelChannel.Particles);
                if (partMult > 0.01f)
                {
                    DispatchBurstForEvent(in ctx, partMult);
                }
            }

            // 4. Canal Áudio espacial / 2D
            if ((recipe.Channels & GameFeelChannel.Audio) != 0)
            {
                PizzeriaSoundManager.PlayFeelEvent(
                    ctx.EventId,
                    ctx.MaterialFamily,
                    ctx.WorldPosition,
                    recipe.PitchVariance,
                    recipe.VolumeScale * Mathf.Clamp(ctx.Intensity, 0.25f, 1.35f),
                    recipe.Priority,
                    ctx.VariantSeed);
            }

            // 5. Canal UI (confirmação / erro sutil quando aplicável)
            if ((recipe.Channels & GameFeelChannel.UI) != 0 && ctx.IsLocalOwner && UIFeedbackService.Instance != null)
            {
                if (ctx.EventId == GameFeelEventId.ActionRejected || ctx.EventId == GameFeelEventId.BackpackFullRejected)
                {
                    UIFeedbackService.Instance.PlayError();
                }
                else if (ctx.EventId == GameFeelEventId.OvenReadyDing)
                {
                    UIFeedbackService.Instance.PlayReady();
                }
            }
        }

        private void TriggerLocalHandReaction(in GameFeelContext ctx)
        {
            var fpCam = FirstPersonCameraController.LocalInstance;
            if (fpCam == null) return;

            int hand = ctx.SlotIndex == 0 || ctx.SlotIndex == 1 ? ctx.SlotIndex : -1;
            switch (ctx.EventId)
            {
                case GameFeelEventId.ItemPickedUp:
                case GameFeelEventId.ToolPickedUp:
                case GameFeelEventId.StorageDispensed:
                    fpCam.TriggerHandImpulse(hand, new Vector3(0f, -0.022f, 0.015f), new Vector3(4.5f, 0f, 0f));
                    break;
                case GameFeelEventId.ItemPlaced:
                case GameFeelEventId.BoxPlaced:
                case GameFeelEventId.ItemDropped:
                    fpCam.TriggerHandImpulse(hand, new Vector3(0f, -0.018f, -0.025f), new Vector3(-5f, 0f, 0f));
                    break;
                case GameFeelEventId.SliceTomatoPepperoniStep:
                case GameFeelEventId.SliceMushroomStep:
                case GameFeelEventId.GrateCheeseStep:
                    fpCam.TriggerHandImpulse(1, new Vector3(0f, -0.028f, -0.018f), new Vector3(-7f, 0f, 2f));
                    break;
                case GameFeelEventId.ActionRejected:
                case GameFeelEventId.BackpackFullRejected:
                    fpCam.TriggerHandImpulse(hand, new Vector3(0.022f, 0f, 0f), new Vector3(0f, 0f, 5f));
                    break;
                case GameFeelEventId.WeaponFire:
                    fpCam.TriggerHandImpulse(1, new Vector3(0f, 0.015f, -0.04f), new Vector3(-9f, 0f, 0f));
                    break;
                case GameFeelEventId.WeaponReload:
                    fpCam.TriggerHandImpulse(1, new Vector3(0f, -0.02f, 0.01f), new Vector3(4f, 0f, -4f));
                    break;
                case GameFeelEventId.WeaponAim:
                    fpCam.TriggerHandImpulse(1, new Vector3(-0.01f, 0.008f, 0.015f), new Vector3(-2f, 0f, 0f));
                    break;
                case GameFeelEventId.VehicleImpact:
                case GameFeelEventId.PlayerDamaged:
                    fpCam.TriggerHandImpulse(-1, new Vector3(0f, -0.03f, -0.035f), new Vector3(-8f, 0f, 6f));
                    break;
            }
        }

        private void DispatchBurstForEvent(in GameFeelContext ctx, float partMult)
        {
            if (ctx.WorldPosition == Vector3.zero && ctx.TargetTransform != null)
            {
                // fallback pos
            }

            Vector3 pos = ctx.TargetTransform != null ? ctx.TargetTransform.position : ctx.WorldPosition;
            switch (ctx.EventId)
            {
                case GameFeelEventId.ItemPlaced when ctx.MaterialFamily == GameFeelMaterialFamily.Dough:
                case GameFeelEventId.RollDoughStep:
                    SpawnBurstParticles(pos + Vector3.up * 0.04f, new Color(0.96f, 0.93f, 0.85f, 0.85f), Mathf.RoundToInt(7 * partMult), 0.7f, 0.04f);
                    break;
                case GameFeelEventId.SliceTomatoPepperoniStep:
                    SpawnBurstParticles(pos + Vector3.up * 0.06f, new Color(0.92f, 0.28f, 0.22f, 0.9f), Mathf.RoundToInt(6 * partMult), 1.1f, 0.032f);
                    break;
                case GameFeelEventId.GrateCheeseStep:
                case GameFeelEventId.SprinkleCheese:
                    SpawnBurstParticles(pos + Vector3.up * 0.07f, new Color(1.0f, 0.90f, 0.35f, 0.95f), Mathf.RoundToInt(8 * partMult), 0.9f, 0.028f);
                    break;
                case GameFeelEventId.OvenReadyDing:
                    SpawnBurstParticles(pos + Vector3.up * 0.35f, new Color(1f, 0.96f, 0.88f, 0.7f), Mathf.RoundToInt(10 * partMult), 1.1f, 0.06f);
                    break;
                case GameFeelEventId.OvenBurningWarning:
                case GameFeelEventId.OvenBurned:
                    SpawnBurstParticles(pos + Vector3.up * 0.35f, new Color(0.18f, 0.16f, 0.16f, 0.8f), Mathf.RoundToInt(9 * partMult), 0.95f, 0.075f);
                    break;
                case GameFeelEventId.BoxPackaged:
                case GameFeelEventId.ProcessCompleted:
                    SpawnBurstParticles(pos + Vector3.up * 0.08f, new Color(1.0f, 0.92f, 0.60f, 0.9f), Mathf.RoundToInt(7 * partMult), 1.0f, 0.035f);
                    break;
                case GameFeelEventId.DeliverySuccess:
                case GameFeelEventId.PlayerRescued:
                case GameFeelEventId.EmployeeWorkComplete:
                    SpawnBurstParticles(pos + Vector3.up * 1.2f, new Color(0.35f, 0.95f, 0.45f, 0.95f), Mathf.RoundToInt(12 * partMult), 1.5f, 0.045f);
                    break;
                case GameFeelEventId.WeaponFire:
                    SpawnBurstParticles(pos + Vector3.up * 1.25f, new Color(1.0f, 0.78f, 0.28f, 0.95f), Mathf.RoundToInt(6 * partMult), 2.2f, 0.035f);
                    break;
                case GameFeelEventId.WeaponHit:
                case GameFeelEventId.VehicleImpact:
                case GameFeelEventId.PlayerDamaged:
                    SpawnBurstParticles(pos, new Color(0.95f, 0.35f, 0.22f, 0.95f), Mathf.RoundToInt(9 * partMult), 1.8f, 0.045f);
                    break;
                case GameFeelEventId.EmployeeBlocked:
                    SpawnBurstParticles(pos + Vector3.up * 1.6f, new Color(0.95f, 0.55f, 0.18f, 0.9f), Mathf.RoundToInt(5 * partMult), 0.9f, 0.035f);
                    break;
            }
        }

        private void EnsureBurstPool()
        {
            if (_burstPool != null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Particles/Standard Unlit");
            if (shader != null)
            {
                _sharedParticleMaterial = new Material(shader) { name = "GameFeel_PooledParticleMat" };
                _createdParticleMaterialAtRuntime = true;
            }

            _burstPool = new ParticleSystem[GameFeelSettings.MaxSimultaneousBursts];
            for (int i = 0; i < _burstPool.Length; i++)
            {
                var go = new GameObject($"GameFeelBurst_{i}");
                go.transform.SetParent(transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                var main = ps.main;
                main.playOnAwake = false;
                main.loop = false;
                main.startLifetime = 0.36f;
                main.startSpeed = 1.0f;
                main.startSize = 0.04f;
                main.gravityModifier = 0.85f;
                main.maxParticles = 24;
                main.simulationSpace = ParticleSystemSimulationSpace.World;

                var em = ps.emission;
                em.enabled = false;

                var shape = ps.shape;
                shape.shapeType = ParticleSystemShapeType.Hemisphere;
                shape.radius = 0.06f;

                var rend = go.GetComponent<ParticleSystemRenderer>();
                if (_sharedParticleMaterial != null) rend.sharedMaterial = _sharedParticleMaterial;

                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _burstPool[i] = ps;
            }
        }

        public void SpawnBurstParticles(Vector3 worldPos, Color tint, int count = 8, float speed = 1.0f, float size = 0.04f)
        {
            if (count <= 0) return;
            EnsureBurstPool();

            ParticleSystem ps = _burstPool[_burstCursor];
            _burstCursor = (_burstCursor + 1) % _burstPool.Length;
            if (ps == null) return;

            ps.transform.position = worldPos;
            var main = ps.main;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = tint;
            ps.Emit(Mathf.Clamp(count, 1, 18));
            _activeBurstEstimate++;
        }

        private void PlayOptionalFeelPrefab(MMF_Player prefab, Vector3 worldPos, float intensity)
        {
            if (prefab == null) return;
            int key = prefab.GetInstanceID();
            if (!_optionalFeelInstances.TryGetValue(key, out var inst) || inst == null)
            {
                inst = Instantiate(prefab, transform);
                inst.name = $"FeelOverride_{prefab.name}";
                _optionalFeelInstances[key] = inst;
            }

            inst.transform.position = worldPos;
            inst.PlayFeedbacks(worldPos, Mathf.Clamp(intensity, 0.2f, 1.5f));
        }

        private void TrimCooldowns(float now)
        {
            var expired = new List<(ulong, GameFeelEventId)>();
            foreach (var kv in _cooldownUntil)
            {
                if (now >= kv.Value) expired.Add(kv.Key);
            }
            for (int i = 0; i < expired.Count; i++)
            {
                _cooldownUntil.Remove(expired[i]);
            }
        }
    }
}
