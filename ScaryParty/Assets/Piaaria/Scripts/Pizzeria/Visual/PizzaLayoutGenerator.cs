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
    /// Respeita bordas cornicione, previne z-fighting e adiciona sutilezas orgânicas nas inclinações.
    /// </summary>
    public static class PizzaLayoutGenerator
    {
        private struct OccupiedCircle
        {
            public Vector2 Center;
            public float Radius;
        }

        private struct PieceCandidate
        {
            public NetIngredientDto Ingredient;
            public PizzaIngredientVisualProfile Profile;
            public int PieceIndex;
            public int TotalPieces;
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

            // Métricas calculadas dinamicamente a partir da malha da massa
            float usableRadius = comp.SurfaceMetrics.UsableRadius > 0.02f ? comp.SurfaceMetrics.UsableRadius : comp.UsableRadius;
            if (usableRadius > 0.20f) usableRadius *= 0.5f;
            if (usableRadius <= 0.05f) usableRadius = 0.145f;

            float floorY = comp.SurfaceMetrics.FloorY;

            // Ordenação determinística: Total (Molho e Queijo) -> Central -> Specific -> Ring -> Sliced -> Scatter
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

            // 1. Processar Total (Molho e Queijo Fundido) - Escala e apoia dinamicamente na malha
            float autoCoatingScale = usableRadius / 0.155f;
            float totalLayerY = floorY + 0.002f;
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Total) continue;

                float cScale = profile.coatingScale;
                if (cScale < 0.5f && cScale > 0.1f) cScale = 1.0f;
                if (cScale <= 0f) cScale = 1.0f;
                float finalScale = cScale * autoCoatingScale;
                float yThick = (profile.thickness > 0.05f) ? profile.thickness : 1.0f;

                result.Placements.Add(new PizzaPiecePlacement
                {
                    IngredientId = ing.DefinitionId,
                    Prefab = profile.unitPrefab,
                    MaterialOverride = profile.overrideMaterial,
                    LocalPosition = new Vector3(0f, totalLayerY, 0f),
                    LocalRotation = Quaternion.identity,
                    LocalScale = (profile.unitPrefab != null)
                        ? new Vector3(finalScale, profile.thickness, finalScale)
                        : new Vector3(finalScale, yThick, finalScale),
                    TintColor = GetBakeTintColor(profile.tintColor, profile.bakedTintColor, profile.burnedTintColor, comp),
                    Mode = PizzaPlacementMode.Total,
                    IsCoating = true
                });
                totalLayerY += Mathf.Max(0.002f, profile.layerHeight);
            }

            float baseToppingSupportY = totalLayerY;

            // 2. Processar Specific (Poses Manuais e Fixas) - SOBERANAS: reservadas primeiro no espaço
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

                        Vector3 adjustedPos = pose.localPosition;
                        if (adjustedPos.y <= 0.001f) adjustedPos.y = baseToppingSupportY + 0.001f;

                        result.Placements.Add(new PizzaPiecePlacement
                        {
                            IngredientId = ing.DefinitionId,
                            Prefab = profile.unitPrefab,
                            MaterialOverride = profile.overrideMaterial,
                            LocalPosition = adjustedPos,
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

            // 3. Processar Central (Manjericão, Decoração Central)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Central) continue;

                float centralY = baseToppingSupportY + profile.centralYOffset;
                Vector3 pos = profile.centerOffset + new Vector3(0f, centralY, 0f);
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

            // 4. Processar Ring (Distribuição em Anel)
            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null || profile.placementMode != PizzaPlacementMode.Ring) continue;

                int ringCount = Mathf.Max(1, profile.piecesInRing);
                float angleStep = 360f / ringCount;
                float baseAngle = profile.startAngleDegrees;
                float clampedRingRadius = Mathf.Clamp(profile.ringRadius * autoCoatingScale, 0.03f, usableRadius - 0.025f);

                for (int i = 0; i < ringCount; i++)
                {
                    float jitter = ((float)rng.NextDouble() * 2f - 1f) * profile.angleJitterDegrees;
                    float angleDeg = baseAngle + i * angleStep + jitter;
                    float angleRad = angleDeg * Mathf.Deg2Rad;

                    Vector2 circlePos = new Vector2(Mathf.Cos(angleRad) * clampedRingRadius, Mathf.Sin(angleRad) * clampedRingRadius);
                    float microY = baseToppingSupportY + profile.ringYOffset + ((float)rng.NextDouble() * 0.001f);
                    Vector3 pos = new Vector3(circlePos.x, microY, circlePos.y);

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

            // 5. Processar Sliced & Scatter com Intercalação Justa de Toppings Concorrentes
            var multiToppingLists = new List<List<PieceCandidate>>();

            foreach (var ing in sortedIngredients)
            {
                var profile = catalog != null ? catalog.GetProfile(ing.DefinitionId, (ProcessingStage)ing.Stage) : null;
                if (profile == null) continue;
                if (profile.placementMode != PizzaPlacementMode.Sliced && profile.placementMode != PizzaPlacementMode.Scatter) continue;

                int portionCount = Mathf.Max(1, (int)ing.Count);
                int targetPieces = profile.placementMode == PizzaPlacementMode.Sliced
                    ? profile.piecesPerPortion * portionCount
                    : Mathf.Min(profile.scatterDensity * portionCount, profile.maxPiecesBudget);

                var pieceList = new List<PieceCandidate>();
                for (int p = 0; p < targetPieces; p++)
                {
                    pieceList.Add(new PieceCandidate
                    {
                        Ingredient = ing,
                        Profile = profile,
                        PieceIndex = p,
                        TotalPieces = targetPieces
                    });
                }
                if (pieceList.Count > 0) multiToppingLists.Add(pieceList);
            }

            // Intercalação determinística entre ingredientes (ex: Calabresa 1, Cogumelo 1, Tomate 1, Calabresa 2...)
            var interleavedPieces = new List<PieceCandidate>();
            int maxListCount = 0;
            for (int l = 0; l < multiToppingLists.Count; l++)
            {
                if (multiToppingLists[l].Count > maxListCount) maxListCount = multiToppingLists[l].Count;
            }

            for (int step = 0; step < maxListCount; step++)
            {
                for (int l = 0; l < multiToppingLists.Count; l++)
                {
                    if (step < multiToppingLists[l].Count)
                    {
                        interleavedPieces.Add(multiToppingLists[l][step]);
                    }
                }
            }

            // Sorteio com detecção de colisão contra ocupações existentes (incluindo poses específicas e centrais)
            for (int q = 0; q < interleavedPieces.Count; q++)
            {
                var candPiece = interleavedPieces[q];
                var profile = candPiece.Profile;
                var ing = candPiece.Ingredient;

                float occRadius = profile.placementMode == PizzaPlacementMode.Sliced
                    ? (profile.occupancyRadius > 0.01f && profile.occupancyRadius < 0.08f ? profile.occupancyRadius : 0.028f)
                    : profile.minSpacing * 0.5f;
                float minSpacing = profile.placementMode == PizzaPlacementMode.Sliced
                    ? (profile.minDistanceBetweenPieces > 0.01f && profile.minDistanceBetweenPieces < 0.12f ? profile.minDistanceBetweenPieces : 0.050f)
                    : profile.minSpacing;
                float yOffset = profile.placementMode == PizzaPlacementMode.Sliced
                    ? (profile.yOffset > 0.005f && profile.yOffset < 0.035f ? profile.yOffset : 0.015f)
                    : profile.scatterYOffset;

                bool placed = false;
                for (int attempt = 0; attempt < 35; attempt++)
                {
                    float borderClearance = 0.015f;
                    float maxAllowedR = usableRadius - occRadius - borderClearance;
                    float minR = profile.placementMode == PizzaPlacementMode.Sliced
                        ? Mathf.Min(profile.minRadius > 0.01f && profile.minRadius < 0.08f ? profile.minRadius : 0.035f, maxAllowedR * 0.4f)
                        : 0.02f;
                    float maxR = profile.placementMode == PizzaPlacementMode.Sliced
                        ? Mathf.Min(profile.maxRadius > 0.05f && profile.maxRadius < 0.16f ? profile.maxRadius : maxAllowedR, maxAllowedR)
                        : maxAllowedR;

                    float u = (float)rng.NextDouble();
                    float r = Mathf.Lerp(minR, maxR, Mathf.Sqrt(u));
                    float theta = (float)rng.NextDouble() * Mathf.PI * 2f;

                    Vector2 candidate = new Vector2(Mathf.Cos(theta) * r, Mathf.Sin(theta) * r);

                    // Testar colisão contra todas as ocupações existentes (poses manuais Específicas, Centrais e outras fatias)
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

                        float microY = baseToppingSupportY + yOffset + ((float)rng.NextDouble() * 0.0016f);
                        float tiltX = ((float)rng.NextDouble() * 4f - 2f);
                        float tiltZ = ((float)rng.NextDouble() * 4f - 2f);
                        Quaternion pieceRot = Quaternion.Euler(tiltX, rotY, tiltZ);

                        result.Placements.Add(new PizzaPiecePlacement
                        {
                            IngredientId = ing.DefinitionId,
                            Prefab = profile.unitPrefab,
                            MaterialOverride = profile.overrideMaterial,
                            LocalPosition = new Vector3(candidate.x, microY, candidate.y),
                            LocalRotation = pieceRot,
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
                        break;
                    }
                }

                if (!placed)
                {
                    result.Warnings.Add($"Aviso: Não foi possível acomodar a peça #{candPiece.PieceIndex + 1} de '{profile.displayName}' sem sobreposição. Área útil ocupada.");
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
