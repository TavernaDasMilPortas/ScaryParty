using System;
using System.Collections.Generic;
using UnityEngine;
using ScaryParty.Pizzeria.Domain.Types;
using ScaryParty.Pizzeria.Network;

namespace ScaryParty.Pizzeria.Visual
{
    public struct PizzaPiecePlacement
    {
        public int IngredientId;
        public GameObject Prefab;
        public Material MaterialOverride;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;
        public Color TintColor;
        public PizzaPlacementMode Mode;
        public bool IsCoating;
    }

    public class PizzaLayoutResult
    {
        public List<PizzaPiecePlacement> Placements { get; } = new List<PizzaPiecePlacement>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();
        public bool HasFailures => Errors.Count > 0;
    }

    /// <summary>
    /// Gerador de layout determinístico de pizza.
    /// Totalmente desacoplado de UnityEditor, rede ou câmeras.
    /// </summary>
    public static class PizzaLayoutGenerator
    {
        private struct OccupiedCircle
        {
            public Vector2 Center;
            public float Radius;
        }

        public static PizzaLayoutResult Generate(PizzaVisualComposition comp, PizzaVisualCatalog catalog)
        {
            var result = new PizzaLayoutResult();
            if (comp == null)
            {
                result.Errors.Add("Composição nula.");
                return result;
            }

            var occupiedCircles = new List<OccupiedCircle>();
            var rng = new System.Random((int)(comp.VisualSeed & 0x7FFFFFFF));

            float usableRadius = comp.UsableRadius > 0.05f ? comp.UsableRadius : 0.36f;

            // Ordenação determinística dos ingredientes: Total primeiro, depois Central, Specific, Ring, Sliced, Scatter
            var sortedIngredients = new List<NetIngredientDto>(comp.Ingredients);
            sortedIngredients.Sort((a, b) =>
            {
                var profA = catalog != null ? catalog.GetProfile(a.DefinitionId, (ProcessingStage)a.Stage) : null;
                var profB = catalog != null ? catalog.GetProfile(b.DefinitionId, (ProcessingStage)b.Stage) : null;
                int modeA = profA != null ? (int)profA.placementMode : 0;
                int modeB = profB != null ? (int)profB.placementMode : 0;
                if (modeA != modeB) return modeA.CompareTo(modeB);
                return a.DefinitionId.CompareTo(b.DefinitionId);
            });

            // 1. Processar Total (Molho e Queijo Fundido)
            float totalLayerY = 0.012f;
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Total) continue;

                result.Placements.Add(new PizzaPiecePlacement
                {
                    IngredientId = ing.DefinitionId,
                    Prefab = profile.unitPrefab,
                    MaterialOverride = profile.overrideMaterial,
                    LocalPosition = new Vector3(0f, totalLayerY, 0f),
                    LocalRotation = Quaternion.identity,
                    LocalScale = new Vector3(profile.coatingScale, profile.thickness, profile.coatingScale),
                    TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                    Mode = PizzaPlacementMode.Total,
                    IsCoating = true
                });
                totalLayerY += profile.layerHeight;
            }

            // 2. Processar Central (Manjericão, Decoração Central)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Central) continue;

                Vector3 pos = profile.centerOffset + new Vector3(0f, profile.centralYOffset, 0f);
                result.Placements.Add(new PizzaPiecePlacement
                {
                    IngredientId = ing.DefinitionId,
                    Prefab = profile.unitPrefab,
                    MaterialOverride = profile.overrideMaterial,
                    LocalPosition = pos,
                    LocalRotation = Quaternion.identity,
                    LocalScale = profile.centerScale,
                    TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                    Mode = PizzaPlacementMode.Central,
                    IsCoating = false
                });

                occupiedCircles.Add(new OccupiedCircle
                {
                    Center = new Vector2(pos.x, pos.z),
                    Radius = profile.reservedCenterRadius
                });
            }

            // 3. Processar Specific (Poses Manuais e Fixas)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Specific) continue;

                if (profile.specificPoses != null && profile.specificPoses.Count > 0)
                {
                    for (int p = 0; p < profile.specificPoses.Count; p++)
                    {
                        var pose = profile.specificPoses[p];
                        Vector2 pos2D = new Vector2(pose.localPosition.x, pose.localPosition.z);
                        if (pos2D.magnitude > usableRadius)
                        {
                            result.Warnings.Add($"Pose manual #{p} de '{profile.displayName}' ultrapassa a borda da pizza ({pos2D.magnitude:F2} > {usableRadius:F2}).");
                        }

                        result.Placements.Add(new PizzaPiecePlacement
                        {
                            IngredientId = ing.DefinitionId,
                            Prefab = profile.unitPrefab,
                            MaterialOverride = profile.overrideMaterial,
                            LocalPosition = pose.localPosition,
                            LocalRotation = Quaternion.Euler(pose.localRotationEuler),
                            LocalScale = pose.localScale,
                            TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                            Mode = PizzaPlacementMode.Specific,
                            IsCoating = false
                        });

                        if (!profile.allowManualOverlap)
                        {
                            occupiedCircles.Add(new OccupiedCircle
                            {
                                Center = pos2D,
                                Radius = Mathf.Max(0.02f, profile.occupancyRadius)
                            });
                        }
                    }
                }
            }

            // 4. Processar Ring (Distribuição em Anel)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Ring) continue;

                int ringCount = Mathf.Max(1, profile.piecesInRing);
                float angleStep = 360f / ringCount;
                float baseAngle = profile.startAngleDegrees;

                for (int i = 0; i < ringCount; i++)
                {
                    float jitter = ((float)rng.NextDouble() * 2f - 1f) * profile.angleJitterDegrees;
                    float angleDeg = baseAngle + i * angleStep + jitter;
                    float angleRad = angleDeg * Mathf.Deg2Rad;

                    Vector2 circlePos = new Vector2(Mathf.Cos(angleRad) * profile.ringRadius, Mathf.Sin(angleRad) * profile.ringRadius);
                    Vector3 pos = new Vector3(circlePos.x, profile.ringYOffset, circlePos.y);

                    result.Placements.Add(new PizzaPiecePlacement
                    {
                        IngredientId = ing.DefinitionId,
                        Prefab = profile.unitPrefab,
                        MaterialOverride = profile.overrideMaterial,
                        LocalPosition = pos,
                        LocalRotation = Quaternion.Euler(0f, -angleDeg, 0f),
                        LocalScale = Vector3.one,
                        TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                        Mode = PizzaPlacementMode.Ring,
                        IsCoating = false
                    });

                    occupiedCircles.Add(new OccupiedCircle
                    {
                        Center = circlePos,
                        Radius = profile.ringOccupancyRadius
                    });
                }
            }

            // 5. Processar Sliced & Scatter (Distribuição Automática com Detecção de Sobreposição)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null) continue;
                if (profile.placementMode != PizzaPlacementMode.Sliced && profile.placementMode != PizzaPlacementMode.Scatter) continue;

                int portionCount = Mathf.Max(1, (int)ing.Count);
                int targetPieces = profile.placementMode == PizzaPlacementMode.Sliced
                    ? profile.piecesPerPortion * portionCount
                    : Mathf.Min(profile.scatterDensity * portionCount, profile.maxPiecesBudget);

                float occRadius = profile.placementMode == PizzaPlacementMode.Sliced ? profile.occupancyRadius : profile.minSpacing * 0.5f;
                float minSpacing = profile.placementMode == PizzaPlacementMode.Sliced ? profile.minDistanceBetweenPieces : profile.minSpacing;
                float yOffset = profile.placementMode == PizzaPlacementMode.Sliced ? profile.yOffset : profile.scatterYOffset;

                int placedCount = 0;
                for (int p = 0; p < targetPieces; p++)
                {
                    bool placed = false;
                    for (int attempt = 0; attempt < 30; attempt++)
                    {
                        // Sorteio com distribuição uniforme por área circular: r = R * sqrt(u)
                        float u = (float)rng.NextDouble();
                        float minR = profile.placementMode == PizzaPlacementMode.Sliced ? profile.minRadius : 0.04f;
                        float maxR = profile.placementMode == PizzaPlacementMode.Sliced ? Mathf.Min(profile.maxRadius, usableRadius - occRadius) : usableRadius - occRadius;
                        float r = Mathf.Lerp(minR, maxR, Mathf.Sqrt(u));
                        float theta = (float)rng.NextDouble() * Mathf.PI * 2f;

                        Vector2 candidate = new Vector2(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r);

                        // Teste contra todas as ocupações existentes
                        bool collision = false;
                        for (int o = 0; o < occupiedCircles.Count; o++)
                        {
                            float dist = Vector2.Distance(candidate, occupiedCircles[o].Center);
                            if (dist < (occRadius + occupiedCircles[o].Radius + minSpacing * 0.5f))
                            {
                                collision = true;
                                break;
                            }
                        }

                        if (!collision)
                        {
                            float rotY = (float)rng.NextDouble() * 360f;
                            float scaleMult = profile.placementMode == PizzaPlacementMode.Sliced
                                ? Mathf.Lerp(profile.scaleRange.x, profile.scaleRange.y, (float)rng.NextDouble())
                                : profile.scatterScale;

                            Vector3 localScale = profile.placementMode == PizzaPlacementMode.Sliced
                                ? Vector3.one * scaleMult
                                : new Vector3(scaleMult, scaleMult * 0.5f, scaleMult);

                            result.Placements.Add(new PizzaPiecePlacement
                            {
                                IngredientId = ing.DefinitionId,
                                Prefab = profile.unitPrefab,
                                MaterialOverride = profile.overrideMaterial,
                                LocalPosition = new Vector3(candidate.x, yOffset, candidate.y),
                                LocalRotation = Quaternion.Euler(0f, rotY, 0f),
                                LocalScale = localScale,
                                TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                                Mode = profile.placementMode,
                                IsCoating = false
                            });

                            occupiedCircles.Add(new OccupiedCircle
                            {
                                Center = candidate,
                                Radius = occRadius
                            });

                            placed = true;
                            placedCount++;
                            break;
                        }
                    }

                    if (!placed && profile.placementMode == PizzaPlacementMode.Sliced)
                    {
                        result.Warnings.Add($"Não foi possível acomodar todas as peças de '{profile.displayName}' (colocadas: {placedCount}/{targetPieces}). Espaço útil esgotado.");
                        break;
                    }
                }
            }

            return result;
        }

        private static Color GetBakeTintColor(Color rawColor, Color bakedColor, Color burnedColor, PizzaVisualComposition comp)
        {
            if (comp == null) return rawColor;

            if (comp.CookingStage == CookingStage.Burned)
            {
                return burnedColor;
            }
            if (comp.BurnProgress > 0f)
            {
                return Color.Lerp(bakedColor, burnedColor, Mathf.Clamp01(comp.BurnProgress));
            }
            if (comp.CookingStage == CookingStage.Baked)
            {
                return bakedColor;
            }
            return Color.Lerp(rawColor, bakedColor, Mathf.Clamp01(comp.CookProgress));
        }
    }
}
