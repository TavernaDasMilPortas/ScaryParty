using System;
using ScaryParty.UI.Piaria.Core;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    /// <summary>
    /// Configurações globais de Game Feel, presets (Suave/Cartoon/Sloppy/Reduzido), orçamentos e clamps de segurança.
    /// Sincronizado com UIToolkitTweenService.ReduceMotion para acessibilidade.
    /// </summary>
    public static class GameFeelSettings
    {
        private const string PrefPreset = "SP_GameFeel_Preset";
        private const string PrefCameraImpulse = "SP_GameFeel_CamImpulse";
        private const string PrefFlashes = "SP_GameFeel_Flashes";
        private const string PrefIntenseVfx = "SP_GameFeel_IntenseVfx";
        private const string PrefMovementMult = "SP_GameFeel_MoveMult";
        private const string PrefParticlesMult = "SP_GameFeel_PartMult";
        private const string PrefAudioMult = "SP_GameFeel_AudioMult";
        private const string PrefUiMult = "SP_GameFeel_UiMult";

        public const int MaxSimultaneousBursts = 12;
        public const int MaxSimultaneousLoops = 8;
        public const int MaxKitchenAudioVoices = 8;

        public const float MinItemScale = 0.78f;
        public const float MaxItemScale = 1.22f;
        public const float MaxItemTiltDegrees = 12f;
        public const float MinCharacterScale = 0.95f;
        public const float MaxCharacterScale = 1.05f;
        public const float MinUiScale = 0.94f;
        public const float MaxUiScale = 1.08f;

        private static bool _initialized;
        private static GameFeelPresetMode _preset = GameFeelPresetMode.Cartoon;
        private static bool _allowCameraImpulse;
        private static bool _allowFlashes = true;
        private static bool _allowIntenseVfx = true;
        private static float _movementMultiplier = 1f;
        private static float _particlesMultiplier = 1f;
        private static float _audioMultiplier = 1f;
        private static float _uiMultiplier = 1f;
        private static bool _usePlayerPrefs = true;

        public static event Action OnSettingsChanged;

        public static void SetPersistenceEnabled(bool enabled)
        {
            _usePlayerPrefs = enabled;
        }

        private static void EnsureLoaded()
        {
            if (_initialized) return;
            _initialized = true;
            if (!_usePlayerPrefs) return;

            _preset = (GameFeelPresetMode)Mathf.Clamp(PlayerPrefs.GetInt(PrefPreset, (int)GameFeelPresetMode.Cartoon), 0, 3);
            _allowCameraImpulse = PlayerPrefs.GetInt(PrefCameraImpulse, 0) == 1;
            _allowFlashes = PlayerPrefs.GetInt(PrefFlashes, 1) == 1;
            _allowIntenseVfx = PlayerPrefs.GetInt(PrefIntenseVfx, 1) == 1;
            _movementMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(PrefMovementMult, 1f), 0f, 1.5f);
            _particlesMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(PrefParticlesMult, 1f), 0f, 1.5f);
            _audioMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(PrefAudioMult, 1f), 0f, 1.5f);
            _uiMultiplier = Mathf.Clamp(PlayerPrefs.GetFloat(PrefUiMult, 1f), 0f, 1.5f);

            if (UIToolkitTweenService.ReduceMotion)
            {
                _preset = GameFeelPresetMode.Reduzido;
            }
        }

        public static GameFeelPresetMode Preset
        {
            get
            {
                EnsureLoaded();
                if (ReduceMotion) return GameFeelPresetMode.Reduzido;
                return _preset;
            }
            set
            {
                EnsureLoaded();
                _preset = value;
                if (_usePlayerPrefs) PlayerPrefs.SetInt(PrefPreset, (int)value);
                UIToolkitTweenService.ReduceMotion = value == GameFeelPresetMode.Reduzido;
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool ReduceMotion
        {
            get => UIToolkitTweenService.ReduceMotion;
            set
            {
                EnsureLoaded();
                UIToolkitTweenService.ReduceMotion = value;
                if (!value && _preset == GameFeelPresetMode.Reduzido)
                {
                    _preset = GameFeelPresetMode.Cartoon;
                    if (_usePlayerPrefs) PlayerPrefs.SetInt(PrefPreset, (int)_preset);
                }
                OnSettingsChanged?.Invoke();
            }
        }

        public static bool AllowCameraImpulse
        {
            get { EnsureLoaded(); return _allowCameraImpulse && !ReduceMotion; }
            set { EnsureLoaded(); _allowCameraImpulse = value; if (_usePlayerPrefs) PlayerPrefs.SetInt(PrefCameraImpulse, value ? 1 : 0); OnSettingsChanged?.Invoke(); }
        }

        public static bool AllowFlashes
        {
            get { EnsureLoaded(); return _allowFlashes && !ReduceMotion; }
            set { EnsureLoaded(); _allowFlashes = value; if (_usePlayerPrefs) PlayerPrefs.SetInt(PrefFlashes, value ? 1 : 0); OnSettingsChanged?.Invoke(); }
        }

        public static bool AllowIntenseVfx
        {
            get { EnsureLoaded(); return _allowIntenseVfx && !ReduceMotion; }
            set { EnsureLoaded(); _allowIntenseVfx = value; if (_usePlayerPrefs) PlayerPrefs.SetInt(PrefIntenseVfx, value ? 1 : 0); OnSettingsChanged?.Invoke(); }
        }

        public static float MovementMultiplier
        {
            get { EnsureLoaded(); return _movementMultiplier; }
            set { EnsureLoaded(); _movementMultiplier = Mathf.Clamp(value, 0f, 1.5f); if (_usePlayerPrefs) PlayerPrefs.SetFloat(PrefMovementMult, _movementMultiplier); OnSettingsChanged?.Invoke(); }
        }

        public static float ParticlesMultiplier
        {
            get { EnsureLoaded(); return _particlesMultiplier; }
            set { EnsureLoaded(); _particlesMultiplier = Mathf.Clamp(value, 0f, 1.5f); if (_usePlayerPrefs) PlayerPrefs.SetFloat(PrefParticlesMult, _particlesMultiplier); OnSettingsChanged?.Invoke(); }
        }

        public static float AudioMultiplier
        {
            get { EnsureLoaded(); return _audioMultiplier; }
            set { EnsureLoaded(); _audioMultiplier = Mathf.Clamp(value, 0f, 1.5f); if (_usePlayerPrefs) PlayerPrefs.SetFloat(PrefAudioMult, _audioMultiplier); OnSettingsChanged?.Invoke(); }
        }

        public static float UIMultiplier
        {
            get { EnsureLoaded(); return _uiMultiplier; }
            set { EnsureLoaded(); _uiMultiplier = Mathf.Clamp(value, 0f, 1.5f); if (_usePlayerPrefs) PlayerPrefs.SetFloat(PrefUiMult, _uiMultiplier); OnSettingsChanged?.Invoke(); }
        }

        public static bool EnableSloppyAsymmetry
        {
            get
            {
                var p = Preset;
                return p == GameFeelPresetMode.Cartoon || p == GameFeelPresetMode.Sloppy;
            }
        }

        public static float GetEffectiveChannelIntensity(GameFeelChannel channel)
        {
            var preset = Preset;
            switch (channel)
            {
                case GameFeelChannel.Movement:
                {
                    float baseVal = preset switch
                    {
                        GameFeelPresetMode.Suave => 0.55f,
                        GameFeelPresetMode.Cartoon => 1.0f,
                        GameFeelPresetMode.Sloppy => 1.35f,
                        _ => 0f
                    };
                    return baseVal * MovementMultiplier;
                }
                case GameFeelChannel.Particles:
                {
                    float baseVal = preset switch
                    {
                        GameFeelPresetMode.Suave => 0.65f,
                        GameFeelPresetMode.Cartoon => 1.0f,
                        GameFeelPresetMode.Sloppy => 1.25f,
                        _ => 0.35f
                    };
                    if (!_allowIntenseVfx) baseVal *= 0.55f;
                    return baseVal * ParticlesMultiplier;
                }
                case GameFeelChannel.Audio:
                {
                    float baseVal = preset switch
                    {
                        GameFeelPresetMode.Suave => 0.85f,
                        GameFeelPresetMode.Cartoon => 1.0f,
                        GameFeelPresetMode.Sloppy => 1.05f,
                        _ => 0.9f
                    };
                    return baseVal * AudioMultiplier;
                }
                case GameFeelChannel.UI:
                {
                    float baseVal = preset switch
                    {
                        GameFeelPresetMode.Suave => 0.7f,
                        GameFeelPresetMode.Cartoon => 1.0f,
                        GameFeelPresetMode.Sloppy => 1.15f,
                        _ => 0f
                    };
                    return baseVal * UIMultiplier;
                }
                case GameFeelChannel.Camera:
                    return AllowCameraImpulse ? (preset == GameFeelPresetMode.Sloppy ? 0.85f : 0.5f) : 0f;
                default:
                    return 0f;
            }
        }

        public static Vector3 ClampItemLocalScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Clamp(scale.x, MinItemScale, MaxItemScale),
                Mathf.Clamp(scale.y, MinItemScale, MaxItemScale),
                Mathf.Clamp(scale.z, MinItemScale, MaxItemScale));
        }

        public static float ClampTiltDegrees(float degrees)
        {
            return Mathf.Clamp(degrees, -MaxItemTiltDegrees, MaxItemTiltDegrees);
        }

        public static Vector3 ClampCharacterLocalScale(Vector3 scale)
        {
            return new Vector3(
                Mathf.Clamp(scale.x, MinCharacterScale, MaxCharacterScale),
                Mathf.Clamp(scale.y, MinCharacterScale, MaxCharacterScale),
                Mathf.Clamp(scale.z, MinCharacterScale, MaxCharacterScale));
        }

        public static float ClampUIScale(float scale)
        {
            return Mathf.Clamp(scale, MinUiScale, MaxUiScale);
        }
    }
}
