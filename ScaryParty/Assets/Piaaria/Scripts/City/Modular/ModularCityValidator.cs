using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Validador arquitetural do kit modular de 12 peças.
    /// Valida contratos, dimensões, colisores de portas, âncoras e receitas.
    /// </summary>
    public static class ModularCityValidator
    {
        public struct ValidationResult
        {
            public bool isValid;
            public List<string> errors;
            public List<string> warnings;
            public List<string> passes;

            public string Summary => $"Validação: {(isValid ? "APROVADO" : "COM FALHAS")} ({passes.Count} verificações OK, {warnings.Count} avisos, {errors.Count} erros)";
        }

        /// <summary>
        /// Valida integralmente o kit de 12 peças e o catálogo.
        /// </summary>
        public static ValidationResult ValidateKit(CityModuleCatalog catalog)
        {
            ValidationResult res = new ValidationResult
            {
                isValid = true,
                errors = new List<string>(),
                warnings = new List<string>(),
                passes = new List<string>()
            };

            if (catalog == null)
            {
                res.isValid = false;
                res.errors.Add("Catálogo nulo.");
                return res;
            }

            // 1. Verifica presença de todos os 12 IDs
            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                CityModuleDefinition def = catalog.GetDefinition(id);
                if (def == null)
                {
                    res.isValid = false;
                    res.errors.Add($"Módulo {id} ausente no catálogo.");
                    continue;
                }

                if (def.prefab == null)
                {
                    res.isValid = false;
                    res.errors.Add($"Módulo {id} ({def.displayName}) sem prefab atribuído.");
                    continue;
                }

                res.passes.Add($"Módulo {id} ({def.displayName}) presente no catálogo.");

                // 2. Valida dimensões nominais contratuais
                ValidateDimensionsContract(def, ref res);

                // 3. Valida hierarquia padrão do prefab
                ValidatePrefabHierarchy(def, ref res);

                // 4. Valida clearance de passagem para portas C e J
                if (def.isEntrance)
                {
                    ValidateDoorPassageClearance(def, ref res);
                }

                // 5. Valida regra de 2 slots para garagem I
                if (def.id == CityModuleId.I)
                {
                    if (def.occupiedSlots != 2 || Mathf.Abs(def.width - 4.0f) > 0.01f)
                    {
                        res.isValid = false;
                        res.errors.Add("Módulo de garagem I deve ter exatamente 4m de largura e 2 slots ocupados.");
                    }
                    else
                    {
                        res.passes.Add("Módulo de garagem I configurado com 4m e 2 slots.");
                    }
                }

                // 6. Valida materiais e normais da malha procedural/visual
                ValidateMeshNormalsAndMaterials(def, ref res);
            }

            return res;
        }

        private static void ValidateMeshNormalsAndMaterials(CityModuleDefinition def, ref ValidationResult res)
        {
            if (def.materialRoles == null || def.materialRoles.Length == 0)
            {
                res.warnings.Add($"Módulo {def.id} não possui papéis funcionais de material (materialRoles) configurados.");
            }
            else
            {
                res.passes.Add($"Módulo {def.id} possui {def.materialRoles.Length} papéis de material configurados.");
            }

            if (def.prefab != null)
            {
                MeshFilter mf = def.prefab.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh mesh = mf.sharedMesh;
                    Vector3[] normals = mesh.normals;
                    if (normals != null && normals.Length > 0)
                    {
                        bool hasZeroNormal = false;
                        for (int i = 0; i < normals.Length; i++)
                        {
                            if (normals[i].sqrMagnitude < 0.001f)
                            {
                                hasZeroNormal = true;
                                break;
                            }
                        }
                        if (hasZeroNormal)
                        {
                            res.warnings.Add($"Módulo {def.id} possui vértices com vetor normal nulo/degenerado.");
                        }
                        else
                        {
                            res.passes.Add($"Normais da malha do módulo {def.id} validadas.");
                        }
                    }
                }
            }
        }

        private static void ValidateDimensionsContract(CityModuleDefinition def, ref ValidationResult res)
        {
            switch (def.id)
            {
                case CityModuleId.A:
                case CityModuleId.B:
                case CityModuleId.C:
                case CityModuleId.D:
                case CityModuleId.G:
                case CityModuleId.H:
                case CityModuleId.J:
                    if (Mathf.Abs(def.width - 2.0f) > 0.05f || Mathf.Abs(def.height - 3.0f) > 0.05f || Mathf.Abs(def.depth - 0.2f) > 0.05f)
                    {
                        res.isValid = false;
                        res.errors.Add($"Módulo {def.id} fora das dimensões canônicas (2.0 x 3.0 x 0.2m): {def.width}x{def.height}x{def.depth}");
                    }
                    else
                    {
                        res.passes.Add($"Dimensões nominais do módulo {def.id} conformes (2x3x0.2m).");
                    }
                    break;

                case CityModuleId.E:
                    if (Mathf.Abs(def.width - 2.0f) > 0.05f || Mathf.Abs(def.height - 0.2f) > 0.05f || Mathf.Abs(def.depth - 2.0f) > 0.05f)
                    {
                        res.isValid = false;
                        res.errors.Add($"Módulo de cobertura E fora das dimensões canônicas (2.0 x 0.2 x 2.0m): {def.width}x{def.height}x{def.depth}");
                    }
                    else
                    {
                        res.passes.Add("Dimensões da cobertura E conformes (2x0.2x2m).");
                    }
                    break;

                case CityModuleId.F:
                    if (Mathf.Abs(def.width - 2.0f) > 0.05f || Mathf.Abs(def.height - 0.4f) > 0.05f || Mathf.Abs(def.depth - 0.2f) > 0.05f)
                    {
                        res.isValid = false;
                        res.errors.Add($"Módulo de platibanda F fora das dimensões canônicas (2.0 x 0.4 x 0.2m): {def.width}x{def.height}x{def.depth}");
                    }
                    else
                    {
                        res.passes.Add("Dimensões da platibanda F conformes (2x0.4x0.2m).");
                    }
                    break;

                case CityModuleId.K:
                    if (Mathf.Abs(def.width - 2.0f) > 0.05f || Mathf.Abs(def.height - 0.4f) > 0.05f || Mathf.Abs(def.depth - 0.8f) > 0.05f)
                    {
                        res.isValid = false;
                        res.errors.Add($"Módulo de toldo K fora das dimensões canônicas (2.0 x 0.4 x 0.8m): {def.width}x{def.height}x{def.depth}");
                    }
                    else
                    {
                        res.passes.Add("Dimensões do toldo K conformes (2x0.4x0.8m).");
                    }
                    break;

                case CityModuleId.L:
                    if (Mathf.Abs(def.width - 0.2f) > 0.05f || Mathf.Abs(def.height - 3.0f) > 0.05f || Mathf.Abs(def.depth - 0.2f) > 0.05f)
                    {
                        res.isValid = false;
                        res.errors.Add($"Módulo de pilar L fora das dimensões canônicas (0.2 x 3.0 x 0.2m): {def.width}x{def.height}x{def.depth}");
                    }
                    else
                    {
                        res.passes.Add("Dimensões do pilar L conformes (0.2x3x0.2m).");
                    }
                    break;
            }
        }

        private static void ValidatePrefabHierarchy(CityModuleDefinition def, ref ValidationResult res)
        {
            if (def.prefab == null) return;

            Transform vis = def.prefab.transform.Find("Visual");
            if (vis == null)
            {
                res.warnings.Add($"Prefab do módulo {def.id} não possui nó 'Visual' filho.");
            }

            Transform col = def.prefab.transform.Find("Collision");
            if (col == null && def.prefab.GetComponent<Collider>() == null)
            {
                res.warnings.Add($"Prefab do módulo {def.id} não possui nó 'Collision' nem colisor próprio.");
            }

            Transform anc = def.prefab.transform.Find("Anchors");
            if (anc == null)
            {
                res.warnings.Add($"Prefab do módulo {def.id} não possui nó 'Anchors'.");
            }
        }

        /// <summary>
        /// Valida se as portas C e J possuem o vão livre desobstruído por colliders maciços.
        /// </summary>
        private static void ValidateDoorPassageClearance(CityModuleDefinition def, ref ValidationResult res)
        {
            if (def.prefab == null) return;

            // Vão livre esperado: centro X = 0, Y entre 0.1 e 2.0m, Z entre -0.05 e -0.15m
            BoxCollider[] colliders = def.prefab.GetComponentsInChildren<BoxCollider>();
            bool passageBlocked = false;
            Transform movingParts = def.prefab.transform.Find("MovingParts");

            float passageHalfW = (def.id == CityModuleId.J ? 1.6f : 1.0f) * 0.45f; // Margem de segurança de passagem

            foreach (var col in colliders)
            {
                // Ignora triggers
                if (col.isTrigger) continue;

                // Ignora colisores em folhas móveis ou filhos de MovingParts
                if (movingParts != null && col.transform.IsChildOf(movingParts))
                    continue;

                // Transforma ponto central do vão para o espaço local do colisor
                Vector3 passageCenter = new Vector3(0f, 1.0f, -0.1f);
                Vector3 localInCol = col.transform.InverseTransformPoint(def.prefab.transform.TransformPoint(passageCenter));

                Bounds b = new Bounds(col.center, col.size);
                if (b.Contains(localInCol))
                {
                    passageBlocked = true;
                    break;
                }
            }

            if (passageBlocked)
            {
                res.isValid = false;
                res.errors.Add($"CRÍTICO: A porta {def.id} possui um BoxCollider bloqueando o vão central de passagem!");
            }
            else
            {
                res.passes.Add($"Passagem da porta {def.id} livre e desobstruída.");
            }
        }
    }
}
