using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class PlaytestScenePaths
    {
        public string lobbyScenePath = "Assets/Piaaria/Scenes/LobbyScene.unity";
        public string readyScenePath = "Assets/Piaaria/Scenes/ReadyScene.unity";
        public string canonicalGameScenePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Scenes/GameScene.unity";
        public string celShadedVariantScenePath = "Assets/Piaaria/Scenes/GameplayVariants/GameScene_CelShaded.unity";
    }

    [Serializable]
    public class PlaytestPlaceholderRecord
    {
        public string id;
        public string category;
        public string assetPath;
        public string replaceWithNote;
        public UnityEngine.Object asset;
    }

    /// <summary>
    /// Manifesto persistente de referências do ciclo de playtest (P0-P6).
    /// Mantido pelo instalador idempotente e carregado em runtime via Resources.
    /// </summary>
    [CreateAssetMenu(fileName = "PlaytestReferenceManifest", menuName = "Scary Party/Playtest/Reference Manifest")]
    public class PlaytestReferenceManifest : ScriptableObject
    {
        public const string ResourcesLoadPath = "Playtest/PlaytestReferenceManifest";

        [Header("Versão e Rastreabilidade")]
        public string branchName = "release/marcos-noturnos";
        public string baseCommitSha = "bdffe5096163c0bac817a94a1c05e4d22fdf4652";
        public string unityVersion = "6000.3.6f1";
        public string lastValidatedUtc;

        [Header("Cenas Canônicas")]
        public PlaytestScenePaths scenes = new PlaytestScenePaths();

        [Header("Configurações e Prefabs Principais")]
        public PizzeriaConfig activePizzeriaConfig;
        public GameObject activePizzeriaPrefab;
        public GameObject playerPrefab;
        public GameObject networkPrefabsConfigAsset;

        [Header("Presets de RUN (P3/P6)")]
        public List<ScriptableObject> runPresets = new List<ScriptableObject>();

        [Header("Placeholders 3D e Visuais Registrados (P5)")]
        public ScriptableObject monsterVisualCatalog;
        public GameObject cashierVisualPrefab;
        public GameObject pizzaioloVisualPrefab;
        public GameObject motorcycleVisualPrefab;
        public GameObject supplierTerminalVisualPrefab;
        public GameObject receivingZoneVisualPrefab;
        public GameObject managementBoardVisualPrefab;
        public List<PlaytestPlaceholderRecord> placeholders = new List<PlaytestPlaceholderRecord>();

        private static PlaytestReferenceManifest _cached;

        public static PlaytestReferenceManifest LoadDefault()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<PlaytestReferenceManifest>(ResourcesLoadPath);
            }
            return _cached;
        }

        public static void ClearRuntimeCache()
        {
            _cached = null;
        }
    }
}
