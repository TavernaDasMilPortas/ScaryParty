using System;

namespace ScaryParty.VisualStyles
{
    /// <summary>
    /// Built-in visual style presets shipped with the Global Cel Shading Wizard.
    /// </summary>
    public enum VisualStylePresetType
    {
        Custom = 0,
        SoftToonVibrant = 1,
        AnimeSoft = 2,
        ComicBold = 3,
        FlatGraphic = 4
    }

    /// <summary>
    /// Controls how cel-shading outlines are rendered for a style.
    /// </summary>
    public enum VisualStyleOutlineMode
    {
        Disabled = 0,
        ScreenSpace = 1,
        MaterialOutline = 2
    }

    /// <summary>
    /// Quality tier for outlines and distance culling.
    /// </summary>
    public enum VisualStyleOutlineQuality
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Target Distant Lands Illustrate shader family or preservation directive.
    /// </summary>
    public enum IllustrateShaderFamily
    {
        Opaque = 0,
        Simple = 1,
        Transparent = 2,
        AlphaClipped = 3,
        Outline = 4,
        PreserveOriginal = 5
    }

    /// <summary>
    /// Conversion action chosen by the classifier or user override.
    /// </summary>
    public enum VisualConversionAction
    {
        Convert = 0,
        Preserve = 1,
        Skip = 2,
        ManualReview = 3
    }

    /// <summary>
    /// Policy for newly imported or unknown materials discovered later.
    /// </summary>
    public enum VisualDiscoveryPolicy
    {
        Review = 0,
        Preserve = 1,
        AutoApplyIfConfident = 2
    }

    /// <summary>
    /// Discovery scan scope in the Wizard.
    /// </summary>
    public enum VisualDiscoveryScope
    {
        GameplayReachable = 0,
        SelectedFolders = 1,
        CurrentSelection = 2,
        FullProject = 3
    }

    /// <summary>
    /// Incremental sync status for tracked assets in the conversion manifest.
    /// </summary>
    public enum VisualAssetSyncStatus
    {
        UpToDate = 0,
        Unprocessed = 1,
        SourceModified = 2,
        ProfileOutdated = 3,
        Orphaned = 4,
        PromotedCustom = 5,
        ManualReview = 6
    }

    /// <summary>
    /// Preview lighting preset options matching gameplay environments.
    /// </summary>
    public enum VisualLightingPreviewMode
    {
        Interior = 0,
        Exterior = 1,
        Neutral = 2
    }

    /// <summary>
    /// Camera projection mode for Wizard 3D preview viewports.
    /// </summary>
    public enum VisualPreviewCameraMode
    {
        Perspective = 0,
        Isometric = 1,
        Orthographic = 2
    }

    /// <summary>
    /// Comparison mode inside the Wizard preview panel.
    /// </summary>
    public enum VisualPreviewComparisonMode
    {
        Toon = 0,
        Original = 1,
        SideBySideAB = 2,
        SplitAB = 3
    }

    /// <summary>
    /// Semantic role used for shared fallback materials and deterministic slot mapping.
    /// Note: extensible categories use string category IDs in VisualConversionRuleSet.
    /// </summary>
    public enum VisualSemanticRole
    {
        Unclassified = 0,
        Preserve = 1,

        // Architecture & Pizzeria
        PizzeriaWall = 10,
        PizzeriaAccentWall = 11,
        PizzeriaFloor = 12,
        PizzeriaKitchenFloor = 13,
        PizzeriaOven = 14,
        PizzeriaOvenDoor = 15,
        PizzeriaCounter = 16,
        PizzeriaFurniture = 17,
        PizzeriaChair = 18,
        PizzeriaCabinet = 19,
        PizzeriaFridge = 20,
        PizzeriaProp = 21,
        PizzeriaTrash = 22,

        // City, Streets & Procedural Modules
        Street = 40,
        Sidewalk = 41,
        Intersection = 42,
        RoadYellow = 43,
        RoadWhite = 44,
        Crosswalk = 45,
        RoadSign = 46,
        CityTerrain = 47,
        TrafficLightBody = 48,
        TrafficLightLens = 49,

        BuildingFacade = 60,
        BuildingRoof = 61,
        BuildingWindow = 62,
        BuildingDoor = 63,
        BuildingTrim = 64,
        BuildingAwning = 65,
        BuildingPillar = 66,

        // Characters & NPCs
        PlayerBody = 80,
        PlayerApron = 81,
        PlayerSkin = 82,
        PlayerEye = 83,
        PlayerPupil = 84,
        NpcBody = 85,

        // Food, Pizzas & Kitchen Tools
        PizzaDough = 100,
        PizzaSauce = 101,
        PizzaCheese = 102,
        PizzaCrust = 103,
        PizzaPepperoni = 104,
        PizzaMushroom = 105,
        PizzaBaked = 106,
        PizzaBox = 107,
        DeliveryBag = 108,
        KitchenTool = 109,
        FoodGeneric = 110,

        // Shared surface / special material roles
        GenericOpaque = 130,
        GenericSimple = 131,
        GenericTransparent = 132,
        GenericAlphaClipped = 133,
        Metal = 134,
        Wood = 135,
        Plastic = 136,
        Glass = 137,
        Decal = 138,
        Emission = 139,
        Particle = 140
    }

    /// <summary>
    /// Standard built-in string category IDs. User rulesets can define any additional IDs without editing C#.
    /// </summary>
    public static class VisualCategoryIds
    {
        public const string Architecture = "Architecture";
        public const string City = "City";
        public const string Streets = "Streets";
        public const string Characters = "Characters";
        public const string Food = "Food";
        public const string Props = "Props";
        public const string Interaction = "Interaction";
        public const string Effects = "Effects";
        public const string Unclassified = "Unclassified";
        public const string Preserve = "Preserve";

        public static readonly string[] BuiltInCategories =
        {
            Architecture,
            City,
            Streets,
            Characters,
            Food,
            Props,
            Interaction,
            Effects,
            Unclassified
        };
    }
}
