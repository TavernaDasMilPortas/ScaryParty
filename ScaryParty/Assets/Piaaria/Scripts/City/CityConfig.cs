using UnityEngine;
using Sirenix.OdinInspector;

/// <summary>
/// Configuration data for procedural city generation.
/// Create from Assets menu: ScaryParty > City > CityConfig.
/// </summary>
[CreateAssetMenu(fileName = "NewCityConfig", menuName = "ScaryParty/City/CityConfig")]
public class CityConfig : ScriptableObject
{
    public event System.Action OnConfigChanged;

    [Title("Configurações da Cidade", "Painel Geral do Gerador (Redesign 4.0)")]
    [ToggleLeft]
    [LabelText("Atualizar em Tempo Real")]
    [PropertyTooltip("Se ativado, ao mexer nos botões abaixo, a cidade se reconstrói instantaneamente.")]
    public bool gerarEmTempoReal = true;

    [ToggleLeft]
    [LabelText("Gerar Muralhas de Contorno")]
    [PropertyTooltip("Gera um muro acompanhando exatamente as ruas que ficam na borda da cidade.")]
    public bool generateBorderWalls = true;

    [ToggleLeft]
    [LabelText("Gerar Miolo Sólido Maciço")]
    [PropertyTooltip("Preenche o centro das quadras com um bloco sólido. Útil se os prédios deixarem frestas.")]
    public bool generateSolidCore = false;

    [ToggleLeft]
    [LabelText("Preencher Chão da Quadra na Malha Viária")]
    [PropertyTooltip("Se verdadeiro, o chão do miolo das quadras é gerado como extensão contínua da mesma malha viária (UnifiedRoadMesh) na altura exata da calçada, preenchendo todos os vãos sem frestas.")]
    public bool fillBlockGroundInRoadMesh = true;

    [LabelText("Semente (Seed)")]
    [PropertyTooltip("Semente para a geração. 0 = cidade totalmente nova a cada vez.")]
    public int seed = 0;

    // --- GEOMETRIA ---
    [TabGroup("Geometria e Chunks")]
    [LabelText("Tamanho do Mapa X (Chunks)")]
    [Range(1, 50)] public int gridWidth = 6;

    [TabGroup("Geometria e Chunks")]
    [LabelText("Tamanho do Mapa Z (Chunks)")]
    [Range(1, 50)] public int gridHeight = 6;

    [TabGroup("Geometria e Chunks")]
    [LabelText("Comprimento Mín. da Rua")]
    public float minStreetBranchLength = 30f;
    
    [TabGroup("Geometria e Chunks")]
    [LabelText("Comprimento Máx. da Rua")]
    public float maxStreetBranchLength = 80f;

    [TabGroup("Geometria e Chunks")]
    [LabelText("Largura da Rua (Metros)")]
    public float streetWidth = 12f;

    [TabGroup("Geometria e Chunks")]
    [LabelText("Largura da Calçada")]
    public float sidewalkWidth = 2f;

    [TabGroup("Geometria e Chunks")]
    [LabelText("Altura da Calçada (Desnível)")]
    public float sidewalkHeight = 0.15f;


    // --- CONSTRUÇÕES MODULARES (KIT 12 PEÇAS) ---
    [TabGroup("Construções (Perímetro)")]
    [Title("Modo Modular (Kit 12 Peças)", "Substitui cubos esticados por blocos montados")]
    [LabelText("Modo de Geração dos Prédios")]
    public ScaryParty.City.Modular.CityBuildingGenerationMode buildingGenerationMode = ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Catálogo de Módulos (12 Peças)")]
    public ScaryParty.City.Modular.CityModuleCatalog modularCatalog;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Perfil Modular (Opcional)")]
    public ScaryParty.City.Modular.ModularBuildingProfile modularBuildingProfile;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Largura Mínima (Módulos de 2m)")]
    [PropertyTooltip("Largura mínima em módulos de 2 metros (Ex: 2 = 4 metros).")]
    [Range(2, 20)] public int minBuildingModulesWidth = 2;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Largura Máxima (Módulos de 2m)")]
    [PropertyTooltip("Largura máxima em módulos de 2 metros (Ex: 8 = 16 metros).")]
    [Range(2, 30)] public int maxBuildingModulesWidth = 6;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Profundidade Mínima (Módulos de 2m)")]
    [PropertyTooltip("Profundidade mínima em módulos de 2 metros (Ex: 2 = 4 metros).")]
    [Range(2, 20)] public int minBuildingModulesDepth = 2;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Profundidade Máxima (Módulos de 2m)")]
    [PropertyTooltip("Profundidade máxima em módulos de 2 metros (Ex: 10 = 20 metros).")]
    [Range(2, 30)] public int maxBuildingModulesDepth = 6;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Pavimentos Mínimos (Andares)")]
    [PropertyTooltip("Quantidade mínima de andares do prédio.")]
    [Range(1, 50)] public int minBuildingFloors = 1;

    [TabGroup("Construções (Perímetro)")]
    [ShowIf("buildingGenerationMode", ScaryParty.City.Modular.CityBuildingGenerationMode.ModularKit)]
    [LabelText("Pavimentos Máximos (Andares)")]
    [PropertyTooltip("Quantidade máxima de andares do prédio.")]
    [Range(1, 50)] public int maxBuildingFloors = 4;

    // --- CONSTRUÇÕES LEGADAS ---
    [TabGroup("Construções (Perímetro)")]
    [Title("Parâmetros Legados (Primitivas)", "Usados quando o modo for LegacyPrimitives")]
    [LabelText("Largura Mínima da Casa")]
    public float minBuildingWidth = 4f;
    
    [TabGroup("Construções (Perímetro)")]
    [LabelText("Largura Máxima da Casa")]
    public float maxBuildingWidth = 15f;
    
    [TabGroup("Construções (Perímetro)")]
    [LabelText("Profundidade Mínima")]
    public float minBuildingDepth = 8f;
    
    [TabGroup("Construções (Perímetro)")]
    [LabelText("Profundidade Máxima")]
    public float maxBuildingDepth = 20f;
    
    [TabGroup("Construções (Perímetro)")]
    [LabelText("Distância da Esquina")]
    [PropertyTooltip("Evita que as casas se atravessem nas pontas das quadras.")]
    public float blockCornerMargin = 2f;
    
    [TabGroup("Construções (Perímetro)")]
    [LabelText("Espaçamento Mínimo entre Casas")]
    public float buildingGap = 0.5f;

    [TabGroup("Construções (Perímetro)")]
    [LabelText("Margem Interna de Segurança")]
    [PropertyTooltip("Distância mínima entre prédios de arestas opostas. Evita sobreposição em quadras estreitas.")]
    public float buildingInnerSafetyMargin = 1.5f;

    [TabGroup("Construções (Perímetro)")]
    [LabelText("Altura Mínima (Prédios)")]
    public float minBuildingHeight = 5f;

    [TabGroup("Construções (Perímetro)")]
    [LabelText("Altura Máxima (Prédios)")]
    public float maxBuildingHeight = 30f;


    // --- PROGRESSÃO ---
    [TabGroup("Progressão e Zonas")]
    [InfoBox("O novo sistema divide o mapa em camadas. Perto do centro é seguro, longe é perigoso.")]
    [LabelText("Pizzaria na Quadra")]
    [PropertyTooltip("Se ativado, coloca a Pizzaria incorporada em uma das quadras perimetrais.")]
    public bool pizzariaInsideBlock = true;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Inimigos Máximos por Zona")]
    [Range(0, 5)] public int maxEnemySpawnPointsPerZone = 3;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Entregas por Quadra (Mín)")]
    public int minDeliveryPointsPerBlock = 0;
    
    [TabGroup("Progressão e Zonas")]
    [LabelText("Entregas por Quadra (Máx)")]
    public int maxDeliveryPointsPerBlock = 3;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Densidade de Edifícios com Delivery")]
    [Range(0.05f, 0.5f)] public float deliveryBuildingDensity = 0.18f;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Pontos Mínimos de Entrega")]
    public int minimumDeliveryPoints = 4;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Pontos Máximos de Entrega")]
    public int maximumDeliveryPoints = 50;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Separação Mínima entre Entregas")]
    public float minDeliverySeparation = 8f;

    [TabGroup("Progressão e Zonas")]
    [LabelText("Distribuir Entregas por Zonas")]
    public bool preferZoneDistribution = true;


    // --- POPULAÇÃO E PEDESTRES ---
    [TabGroup("População e Pedestres")]
    [LabelText("Ativar Pedestres e Cidade Viva")]
    public bool enablePedestrians = true;

    [TabGroup("População e Pedestres")]
    [LabelText("População Base de Pedestres")]
    public int populationBase = 8;

    [TabGroup("População e Pedestres")]
    [LabelText("Densidade de Pedestres (por Prédio)")]
    [Range(0.05f, 0.5f)] public float populationDensity = 0.15f;

    [TabGroup("População e Pedestres")]
    [LabelText("Teto Máximo de Pedestres Ativos")]
    public int maxActivePedestrians = 40;

    [TabGroup("População e Pedestres")]
    [LabelText("Tempo de Travessia Verde (s)")]
    public float pedestrianCrossingTime = 8f;

    [TabGroup("População e Pedestres")]
    [LabelText("Pedestres Respeitam Semáforo")]
    public bool pedestrianWaitTrafficLight = true;


    // --- TRÂNSITO ---
    [TabGroup("Trânsito e Eventos")]
    [LabelText("Máximo de Carros (NPC)")]
    [Range(0, 30)] public int maxTrafficVehicles = 10;

    [TabGroup("Trânsito e Eventos")]
    [LabelText("Velocidade do Trânsito")]
    public float trafficBaseSpeed = 8f;

    [TabGroup("Trânsito e Eventos")]
    [LabelText("Chance de Semáforo")]
    [Range(0f, 1f)] public float trafficLightProbability = 0.5f;

    [TabGroup("Trânsito e Eventos")]
    [LabelText("Eventos: Intervalo Mínimo (s)")]
    public float minEventInterval = 30f;

    [TabGroup("Trânsito e Eventos")]
    [LabelText("Eventos: Intervalo Máximo (s)")]
    public float maxEventInterval = 90f;

    [TabGroup("Trânsito e Eventos")]
    [LabelText("Duração de Ruas Bloqueadas (s)")]
    public float blockadeDuration = 45f;


    private void OnValidate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);
        streetWidth = Mathf.Max(0f, streetWidth);
        sidewalkWidth = Mathf.Max(0f, sidewalkWidth);

        minBuildingHeight = Mathf.Max(1f, minBuildingHeight);
        maxBuildingHeight = Mathf.Max(minBuildingHeight, maxBuildingHeight);

        minDeliveryPointsPerBlock = Mathf.Max(0, minDeliveryPointsPerBlock);
        maxDeliveryPointsPerBlock = Mathf.Max(minDeliveryPointsPerBlock, maxDeliveryPointsPerBlock);
        deliveryBuildingDensity = Mathf.Clamp(deliveryBuildingDensity, 0.05f, 0.5f);
        minimumDeliveryPoints = Mathf.Max(1, minimumDeliveryPoints);
        maximumDeliveryPoints = Mathf.Max(minimumDeliveryPoints, maximumDeliveryPoints);
        minDeliverySeparation = Mathf.Max(1f, minDeliverySeparation);

        populationBase = Mathf.Max(0, populationBase);
        populationDensity = Mathf.Clamp(populationDensity, 0.01f, 1f);
        maxActivePedestrians = Mathf.Max(1, maxActivePedestrians);
        pedestrianCrossingTime = Mathf.Max(3f, pedestrianCrossingTime);

        minBuildingWidth = Mathf.Max(2f, minBuildingWidth);
        maxBuildingWidth = Mathf.Max(minBuildingWidth, maxBuildingWidth);
        minBuildingDepth = Mathf.Max(2f, minBuildingDepth);
        maxBuildingDepth = Mathf.Max(minBuildingDepth, maxBuildingDepth);
        blockCornerMargin = Mathf.Max(0f, blockCornerMargin);

        minBuildingModulesWidth = Mathf.Max(2, minBuildingModulesWidth);
        maxBuildingModulesWidth = Mathf.Max(minBuildingModulesWidth, maxBuildingModulesWidth);
        minBuildingModulesDepth = Mathf.Max(2, minBuildingModulesDepth);
        maxBuildingModulesDepth = Mathf.Max(minBuildingModulesDepth, maxBuildingModulesDepth);
        minBuildingFloors = Mathf.Max(1, minBuildingFloors);
        maxBuildingFloors = Mathf.Max(minBuildingFloors, maxBuildingFloors);

        minStreetBranchLength = Mathf.Max(10f, minStreetBranchLength);
        maxStreetBranchLength = Mathf.Max(minStreetBranchLength, maxStreetBranchLength);

        trafficBaseSpeed = Mathf.Max(1f, trafficBaseSpeed);
        minEventInterval = Mathf.Max(5f, minEventInterval);
        maxEventInterval = Mathf.Max(minEventInterval, maxEventInterval);
        blockadeDuration = Mathf.Max(5f, blockadeDuration);

        // Notifica o CityGenerator para reconstruir em tempo real diretamente
#if UNITY_EDITOR
        if (gerarEmTempoReal && !Application.isPlaying)
        {
            CityGenerator generator = Object.FindFirstObjectByType<CityGenerator>();
            if (generator != null)
            {
                UnityEditor.EditorApplication.delayCall -= generator.GenerateCity;
                UnityEditor.EditorApplication.delayCall += generator.GenerateCity;
            }
        }
#endif
        OnConfigChanged?.Invoke(); // Mantém o evento para outros usos
    }
}
