using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using ScaryParty.Pizzeria.Domain.Types;
using UnityEngine;

namespace ScaryParty.Pizzeria.Presentation.Feedback
{
    [Serializable]
    public struct GameFeelRecipe
    {
        public GameFeelEventId EventId;
        public GameFeelMaterialFamily MaterialFamily;
        public GameFeelChannel Channels;
        [Min(0.02f)] public float Duration;
        [Range(0f, 0.35f)] public float AnticipationFraction;
        [Range(0.05f, 0.7f)] public float ImpactFraction;
        [Range(0.2f, 0.9f)] public float ReboundFraction;
        public Vector3 AnticipationScale;
        public Vector3 SquashScale;
        public Vector3 ReboundScale;
        [Min(0f)] public float ArcHeight;
        [Range(0f, 15f)] public float MaxTiltDegrees;
        [Range(0f, 0.25f)] public float AsymmetryRatio;
        [Min(0f)] public float Cooldown;
        [Range(0, 100)] public int Priority;
        [Range(0f, 0.15f)] public float PitchVariance;
        [Range(0f, 1.5f)] public float VolumeScale;
        public MMF_Player OptionalFeelPrefab;

        public static GameFeelRecipe CreateDefault(GameFeelEventId eventId, GameFeelMaterialFamily family = GameFeelMaterialFamily.Default)
        {
            return new GameFeelRecipe
            {
                EventId = eventId,
                MaterialFamily = family,
                Channels = GameFeelChannel.Movement | GameFeelChannel.Audio,
                Duration = 0.24f,
                AnticipationFraction = 0f,
                ImpactFraction = 0.33f,
                ReboundFraction = 0.60f,
                AnticipationScale = Vector3.one,
                SquashScale = new Vector3(1.06f, 0.88f, 1.06f),
                ReboundScale = new Vector3(0.98f, 1.05f, 0.98f),
                ArcHeight = 0.03f,
                MaxTiltDegrees = 5f,
                AsymmetryRatio = 0.08f,
                Cooldown = 0.06f,
                Priority = 50,
                PitchVariance = 0.04f,
                VolumeScale = 1f
            };
        }
    }

    public static class GameFeelMaterialClassifier
    {
        public static GameFeelMaterialFamily ClassifyItem(byte categoryByte, int definitionId, bool isBoxed)
        {
            if (isBoxed) return GameFeelMaterialFamily.CardboardPaper;

            switch ((ItemCategory)categoryByte)
            {
                case ItemCategory.Dough:
                case ItemCategory.PizzaBase:
                case ItemCategory.Pizza:
                    return GameFeelMaterialFamily.Dough;
                case ItemCategory.Sauce:
                case ItemCategory.Cheese:
                    return GameFeelMaterialFamily.FluidGranular;
                case ItemCategory.Topping:
                case ItemCategory.Ingredient:
                    return GameFeelMaterialFamily.SmallBounceIngredient;
                case ItemCategory.Tool:
                    return GameFeelMaterialFamily.RigidMetal;
                case ItemCategory.Box:
                    return GameFeelMaterialFamily.CardboardPaper;
                default:
                    // Mushroom (8/9) ou tomate/calabresa entram em SmallBounceIngredient por padrão
                    if (definitionId == 1 || definitionId == 2) return GameFeelMaterialFamily.Dough;
                    if (definitionId == 3 || definitionId == 4 || definitionId == 5) return GameFeelMaterialFamily.FluidGranular;
                    return GameFeelMaterialFamily.Default;
            }
        }

        public static GameFeelMaterialFamily ClassifyProcess(int processId)
        {
            switch (processId)
            {
                case 1: return GameFeelMaterialFamily.Dough;
                case 2: return GameFeelMaterialFamily.FluidGranular;
                case 3:
                case 4: return GameFeelMaterialFamily.SmallBounceIngredient;
                default: return GameFeelMaterialFamily.Default;
            }
        }
    }

    [CreateAssetMenu(menuName = "ScaryParty/Game Feel/Game Feel Profile", fileName = "GameFeelDefaultProfile")]
    public sealed class GameFeelProfile : ScriptableObject
    {
        public List<GameFeelRecipe> Recipes = new List<GameFeelRecipe>();

        private Dictionary<(GameFeelEventId, GameFeelMaterialFamily), GameFeelRecipe> _lookup;

        private void OnEnable()
        {
            InvalidateLookup();
        }

        public void InvalidateLookup()
        {
            _lookup = null;
        }

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<(GameFeelEventId, GameFeelMaterialFamily), GameFeelRecipe>();
            if (Recipes == null) return;
            for (int i = 0; i < Recipes.Count; i++)
            {
                var r = Recipes[i];
                _lookup[(r.EventId, r.MaterialFamily)] = r;
            }
        }

        public bool TryGetRecipe(GameFeelEventId eventId, GameFeelMaterialFamily family, out GameFeelRecipe recipe)
        {
            EnsureLookup();
            if (_lookup.TryGetValue((eventId, family), out recipe))
                return true;

            if (family != GameFeelMaterialFamily.Default && _lookup.TryGetValue((eventId, GameFeelMaterialFamily.Default), out recipe))
                return true;

            recipe = GameFeelRecipe.CreateDefault(eventId, family);
            return false;
        }

        public GameFeelRecipe GetRecipeOrFallback(GameFeelEventId eventId, GameFeelMaterialFamily family)
        {
            TryGetRecipe(eventId, family, out var recipe);
            return recipe;
        }

        public static GameFeelProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<GameFeelProfile>();
            profile.name = "GameFeelRuntimeDefault";
            profile.hideFlags = HideFlags.DontSave;
            profile.PopulateDefaultRecipes();
            return profile;
        }

        public void PopulateDefaultRecipes()
        {
            Recipes.Clear();

            // Receita A — Pouso de Ingrediente na Bancada / Mão (240ms: 80ms arco+impacto, squash Y*0.88 XZ*1.06, rebound Y*1.05 XZ*0.98)
            var placeDefault = GameFeelRecipe.CreateDefault(GameFeelEventId.ItemPlaced, GameFeelMaterialFamily.Default);
            placeDefault.Duration = 0.24f;
            placeDefault.ImpactFraction = 0.333f;
            placeDefault.ReboundFraction = 0.604f;
            placeDefault.SquashScale = new Vector3(1.06f, 0.88f, 1.06f);
            placeDefault.ReboundScale = new Vector3(0.98f, 1.05f, 0.98f);
            placeDefault.ArcHeight = 0.045f;
            placeDefault.MaxTiltDegrees = 5.5f;
            placeDefault.AsymmetryRatio = 0.09f;
            Recipes.Add(placeDefault);

            // Variante Massa (macia, pesada, compressão larga)
            var placeDough = placeDefault;
            placeDough.MaterialFamily = GameFeelMaterialFamily.Dough;
            placeDough.Duration = 0.28f;
            placeDough.SquashScale = new Vector3(1.11f, 0.81f, 1.11f);
            placeDough.ReboundScale = new Vector3(0.97f, 1.06f, 0.97f);
            placeDough.ArcHeight = 0.03f;
            placeDough.Channels |= GameFeelChannel.Particles;
            Recipes.Add(placeDough);

            // Variante Metal/Utensílio (rígido, vibração curta sem virar gelatina)
            var placeMetal = placeDefault;
            placeMetal.MaterialFamily = GameFeelMaterialFamily.RigidMetal;
            placeMetal.Duration = 0.16f;
            placeMetal.SquashScale = new Vector3(1.015f, 0.985f, 1.015f);
            placeMetal.ReboundScale = Vector3.one;
            placeMetal.MaxTiltDegrees = 7f;
            placeMetal.AsymmetryRatio = 0.03f;
            Recipes.Add(placeMetal);

            // Variante Caixa/Papel (seco, estalo e flutter)
            var boxPlace = placeDefault;
            boxPlace.EventId = GameFeelEventId.BoxPlaced;
            boxPlace.MaterialFamily = GameFeelMaterialFamily.CardboardPaper;
            boxPlace.Duration = 0.22f;
            boxPlace.SquashScale = new Vector3(1.045f, 0.90f, 1.045f);
            boxPlace.MaxTiltDegrees = 6.5f;
            Recipes.Add(boxPlace);

            // Pickup (comprimir rapidamente e acomodar na mão)
            var pickup = GameFeelRecipe.CreateDefault(GameFeelEventId.ItemPickedUp, GameFeelMaterialFamily.Default);
            pickup.Duration = 0.18f;
            pickup.AnticipationFraction = 0.12f;
            pickup.ImpactFraction = 0.35f;
            pickup.ReboundFraction = 0.68f;
            pickup.AnticipationScale = new Vector3(1.05f, 0.92f, 1.05f);
            pickup.SquashScale = new Vector3(0.93f, 1.10f, 0.93f);
            pickup.ReboundScale = new Vector3(1.02f, 0.98f, 1.02f);
            pickup.ArcHeight = 0.035f;
            pickup.MaxTiltDegrees = 6f;
            Recipes.Add(pickup);

            var pickupTool = pickup;
            pickupTool.EventId = GameFeelEventId.ToolPickedUp;
            pickupTool.MaterialFamily = GameFeelMaterialFamily.RigidMetal;
            pickupTool.Duration = 0.15f;
            pickupTool.SquashScale = new Vector3(0.985f, 1.025f, 0.985f);
            pickupTool.ReboundScale = Vector3.one;
            pickupTool.MaxTiltDegrees = 8f;
            Recipes.Add(pickupTool);

            // Foco e rejeição
            var focusEnter = GameFeelRecipe.CreateDefault(GameFeelEventId.FocusEnter);
            focusEnter.Channels = GameFeelChannel.Movement;
            focusEnter.Duration = 0.12f;
            focusEnter.SquashScale = new Vector3(1.035f, 1.035f, 1.035f);
            focusEnter.ReboundScale = new Vector3(1.015f, 1.015f, 1.015f);
            focusEnter.ArcHeight = 0.01f;
            focusEnter.MaxTiltDegrees = 0f;
            focusEnter.Cooldown = 0.08f;
            Recipes.Add(focusEnter);

            var rejected = GameFeelRecipe.CreateDefault(GameFeelEventId.ActionRejected);
            rejected.Duration = 0.20f;
            rejected.Channels = GameFeelChannel.Movement | GameFeelChannel.Audio | GameFeelChannel.UI;
            rejected.SquashScale = new Vector3(0.96f, 1.02f, 0.96f);
            rejected.MaxTiltDegrees = 8f;
            rejected.Cooldown = 0.25f;
            rejected.Priority = 80;
            Recipes.Add(rejected);

            // Receita B — Ciclo de Corte (320ms)
            var slice = GameFeelRecipe.CreateDefault(GameFeelEventId.SliceTomatoPepperoniStep, GameFeelMaterialFamily.SmallBounceIngredient);
            slice.Duration = 0.32f;
            slice.AnticipationFraction = 0.17f;
            slice.ImpactFraction = 0.30f;
            slice.ReboundFraction = 0.65f;
            slice.AnticipationScale = new Vector3(0.96f, 1.06f, 0.96f);
            slice.SquashScale = new Vector3(1.12f, 0.82f, 1.12f);
            slice.ReboundScale = new Vector3(0.98f, 1.05f, 0.98f);
            slice.ArcHeight = 0.04f;
            slice.MaxTiltDegrees = 8f;
            slice.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio;
            Recipes.Add(slice);

            var sliceMushroom = slice;
            sliceMushroom.EventId = GameFeelEventId.SliceMushroomStep;
            sliceMushroom.SquashScale = new Vector3(1.09f, 0.86f, 1.09f);
            sliceMushroom.MaxTiltDegrees = 10f;
            Recipes.Add(sliceMushroom);

            var rollDough = slice;
            rollDough.EventId = GameFeelEventId.RollDoughStep;
            rollDough.MaterialFamily = GameFeelMaterialFamily.Dough;
            rollDough.Duration = 0.38f;
            rollDough.SquashScale = new Vector3(1.15f, 0.79f, 1.15f);
            Recipes.Add(rollDough);

            var grateCheese = slice;
            grateCheese.EventId = GameFeelEventId.GrateCheeseStep;
            grateCheese.MaterialFamily = GameFeelMaterialFamily.FluidGranular;
            grateCheese.Duration = 0.26f;
            grateCheese.SquashScale = new Vector3(1.03f, 0.95f, 1.03f);
            Recipes.Add(grateCheese);

            var procComplete = GameFeelRecipe.CreateDefault(GameFeelEventId.ProcessCompleted);
            procComplete.Duration = 0.26f;
            procComplete.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio;
            procComplete.SquashScale = new Vector3(1.10f, 0.88f, 1.10f);
            procComplete.ReboundScale = new Vector3(0.96f, 1.08f, 0.96f);
            procComplete.ArcHeight = 0.055f;
            procComplete.Priority = 75;
            Recipes.Add(procComplete);

            // Forno
            var ovenIn = GameFeelRecipe.CreateDefault(GameFeelEventId.OvenInserted, GameFeelMaterialFamily.RigidMetal);
            ovenIn.Duration = 0.25f;
            ovenIn.SquashScale = new Vector3(1.04f, 0.92f, 1.04f);
            ovenIn.Priority = 70;
            Recipes.Add(ovenIn);

            var ovenReady = GameFeelRecipe.CreateDefault(GameFeelEventId.OvenReadyDing, GameFeelMaterialFamily.RigidMetal);
            ovenReady.Duration = 0.38f;
            ovenReady.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio | GameFeelChannel.UI;
            ovenReady.SquashScale = new Vector3(1.08f, 0.90f, 1.08f);
            ovenReady.ReboundScale = new Vector3(0.97f, 1.06f, 0.97f);
            ovenReady.ArcHeight = 0.04f;
            ovenReady.Priority = 95;
            Recipes.Add(ovenReady);

            var ovenWarn = GameFeelRecipe.CreateDefault(GameFeelEventId.OvenBurningWarning, GameFeelMaterialFamily.RigidMetal);
            ovenWarn.Duration = 0.30f;
            ovenWarn.Cooldown = 2.0f;
            ovenWarn.Priority = 90;
            ovenWarn.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio;
            Recipes.Add(ovenWarn);

            // Receita C — Fechamento de Caixa (520ms)
            var boxPack = GameFeelRecipe.CreateDefault(GameFeelEventId.BoxPackaged, GameFeelMaterialFamily.CardboardPaper);
            boxPack.Duration = 0.52f;
            boxPack.AnticipationFraction = 0.14f;
            boxPack.ImpactFraction = 0.38f;
            boxPack.ReboundFraction = 0.70f;
            boxPack.SquashScale = new Vector3(1.07f, 0.86f, 1.07f);
            boxPack.ReboundScale = new Vector3(0.97f, 1.05f, 0.97f);
            boxPack.ArcHeight = 0.05f;
            boxPack.MaxTiltDegrees = 7f;
            boxPack.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio;
            boxPack.Priority = 80;
            Recipes.Add(boxPack);

            // Mochila
            var bagStore = GameFeelRecipe.CreateDefault(GameFeelEventId.BackpackStored, GameFeelMaterialFamily.FabricCargo);
            bagStore.Duration = 0.26f;
            bagStore.SquashScale = new Vector3(1.08f, 0.89f, 1.08f);
            bagStore.ReboundScale = new Vector3(0.97f, 1.04f, 0.97f);
            bagStore.MaxTiltDegrees = 6f;
            Recipes.Add(bagStore);

            // Receita D — Entrega (700ms)
            var delivSuccess = GameFeelRecipe.CreateDefault(GameFeelEventId.DeliverySuccess, GameFeelMaterialFamily.CharacterBody);
            delivSuccess.Duration = 0.70f;
            delivSuccess.Channels = GameFeelChannel.Movement | GameFeelChannel.Particles | GameFeelChannel.Audio | GameFeelChannel.UI;
            delivSuccess.SquashScale = new Vector3(1.05f, 0.95f, 1.05f);
            delivSuccess.ReboundScale = new Vector3(0.97f, 1.05f, 0.97f);
            delivSuccess.Priority = 100;
            Recipes.Add(delivSuccess);

            var delivPartial = delivSuccess;
            delivPartial.EventId = GameFeelEventId.DeliveryPartial;
            delivPartial.Duration = 0.50f;
            delivPartial.Priority = 90;
            Recipes.Add(delivPartial);

            var delivFail = delivSuccess;
            delivFail.EventId = GameFeelEventId.DeliveryFailed;
            delivFail.Duration = 0.55f;
            delivFail.MaxTiltDegrees = 9f;
            delivFail.Priority = 95;
            Recipes.Add(delivFail);

            InvalidateLookup();
        }
    }
}
