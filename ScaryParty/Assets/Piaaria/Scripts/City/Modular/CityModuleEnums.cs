using System;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Identificador canônico das 12 peças do kit modular da cidade.
    /// Contrato conforme Guia_Kit_Cidade_Modular.docx.
    /// </summary>
    public enum CityModuleId
    {
        A, // Parede lisa (2m larg x 3m alt x 0.20m esp)
        B, // Parede com janela (2m larg x 3m alt x 0.20m esp)
        C, // Parede com porta simples (2m larg x 3m alt x 0.20m esp, vão 1.0m x 2.20m)
        D, // Parede com vitrine (2m larg x 3m alt x 0.20m esp)
        E, // Cobertura plana (2m x 0.20m x 2m)
        F, // Platibanda (2m larg x 0.40m alt x 0.20m esp)
        G, // Janela com venezianas (2m larg x 3m alt x 0.20m esp)
        H, // Janela com floreira (2m larg x 3m alt x 0.20m esp, saliência externa 0.30m)
        I, // Parede com garagem (4m larg x 3m alt x 0.20m esp, ocupa 2 slots de 2m)
        J, // Parede com porta dupla (2m larg x 3m alt x 0.20m esp, vão 1.6m x 2.20m)
        K, // Toldo separado (2m larg x 0.40m alt x 0.80m prof, inclinado para fora e baixo)
        L  // Pilar de canto (0.20m larg x 3m alt x 0.20m prof)
    }

    /// <summary>
    /// Categorias funcionais de módulos para regras de montagem e validação.
    /// </summary>
    public enum CityModuleCategory
    {
        WallSolid,
        WallWindow,
        WallDoor,
        WallShopfront,
        RoofFlat,
        Parapet,
        WallShutters,
        WallPlanter,
        WallGarage,
        WallDoubleDoor,
        Awning,
        CornerPillar
    }

    /// <summary>
    /// Papel funcional dos materiais em submeshes e substituição visual.
    /// </summary>
    public enum CityModuleMaterialRole
    {
        Wall,
        Trim,
        Glass,
        Wood,
        Metal,
        Fabric
    }

    /// <summary>
    /// Lado da fachada para posicionamento das peças.
    /// </summary>
    public enum FacadeSide
    {
        Front,
        Back,
        Left,
        Right,
        Roof,
        Parapet,
        Pillar
    }

    /// <summary>
    /// Modo de geração dos edifícios no CityConfig.
    /// Permite migração segura mantendo modo legado funcional.
    /// </summary>
    public enum CityBuildingGenerationMode
    {
        LegacyPrimitives,
        ModularKit
    }

    /// <summary>
    /// Receitas fixas de validação descritas na Etapa 2 do plano.
    /// </summary>
    public enum ModularBuildingRecipe
    {
        Custom,
        HouseWithGarage, // Casa com garagem: 8m larg x 6m prof x 1 pav (I=4m + C=2m + G=2m)
        TwoStoryHouse,   // Sobrado: 6m larg x 6m prof x 2 pav (A + J + B)
        Shop,            // Loja: 6m larg x 8m prof x 1 pav (D + D + J, K acima das vitrines)
        MixedBuilding    // Prédio misto: 8m larg x 8m prof x 4 pav (D + D + J + D; janelas nos andares superiores)
    }
}
