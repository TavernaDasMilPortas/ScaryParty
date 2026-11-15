#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Visual;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Motor executor das etapas de criação e atualização da variante Cel Shading da Pizzeria.
    /// Garante isolamento absoluto do prefab original, criação de materiais dedicados por slot,
    /// inserção de personagens de demonstração e lighting rig condicional.
    /// </summary>
    public static class PizzeriaToonVariantBuilder
    {
        public const string SourcePrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string VariantPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab";
        public const string SoftToonProfilePath = "Assets/Piaaria/ScriptableObjects/Pizzeria/PizzeriaAppearanceProfile_SoftToon.asset";

        public class BuildResult
        {
            public bool success;
            public string message;
            public int renderersUpdated;
            public int slotsUpdated;
            public int preservedSlots;
            public int charactersAdded;
            public int lightsAdded;
            public List<string> errors = new List<string>();
            public List<string> warnings = new List<string>();
        }

        /// <summary>
        /// Clona o prefab original criando uma cópia independente com GUID próprio caso não exista.
        /// </summary>
        public static bool EnsureVariantPrefabExists(out string message)
        {
            if (!File.Exists(SourcePrefabPath))
            {
                message = $"Prefab original não encontrado em {SourcePrefabPath}";
                return false;
            }

            if (File.Exists(VariantPrefabPath))
            {
                message = $"Variante já existe em {VariantPrefabPath}. Pronta para atualizações seguras.";
                return true;
            }

            // Assegura pasta destino
            string dir = Path.GetDirectoryName(VariantPrefabPath).Replace("\\", "/");
            if (!AssetDatabase.IsValidFolder(dir))
            {
                Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            bool ok = AssetDatabase.CopyAsset(SourcePrefabPath, VariantPrefabPath);
            AssetDatabase.Refresh();

            if (ok)
            {
                message = $"Variante clonada com sucesso em {VariantPrefabPath}";
                return true;
            }
            else
            {
                message = $"Falha ao copiar asset de {SourcePrefabPath} para {VariantPrefabPath}";
                return false;
            }
        }

        /// <summary>
        /// Aplica os materiais toon a todos os renderers da cópia isolada.
        /// </summary>
        public static BuildResult ApplyToonMaterialsToVariant(PizzeriaAppearanceProfile profile)
        {
            var result = new BuildResult();

            if (profile == null)
            {
                result.errors.Add("Perfil de aparência Soft Toon não pode ser nulo.");
                result.success = false;
                return result;
            }

            if (!File.Exists(VariantPrefabPath))
            {
                result.errors.Add($"Variante não encontrada em {VariantPrefabPath}. Execute o clone primeiro.");
                result.success = false;
                return result;
            }

            // Trava de segurança: proíbe explicitamente caminho de produção
            if (string.Equals(VariantPrefabPath, SourcePrefabPath, StringComparison.OrdinalIgnoreCase))
            {
                result.errors.Add("Tentativa de sobrescrever o prefab original bloqueada por segurança.");
                result.success = false;
                return result;
            }

            var manifest = new ToonMaterialFactory.MaterialManifest
            {
                generatedAt = DateTime.UtcNow.ToString("o"),
                profileName = profile.name
            };

            string sourceGuid = AssetDatabase.AssetPathToGUID(SourcePrefabPath);
            string profileGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(profile));
            string sourceHash = PizzeriaToonAudit.ComputeFileMD5(SourcePrefabPath);

            GameObject variantRoot = PrefabUtility.LoadPrefabContents(VariantPrefabPath);
            try
            {
                // Anexa ou atualiza marcador visual exclusivamente na raiz da variante
                var marker = variantRoot.GetComponent<PizzeriaToonVisualMarker>();
                if (marker == null)
                {
                    marker = variantRoot.AddComponent<PizzeriaToonVisualMarker>();
                }
                marker.Initialize(sourceGuid, SourcePrefabPath, profileGuid, "1.0.0-softtoon", sourceHash, marker.HasShowcaseRig);

                Renderer[] renderers = variantRoot.GetComponentsInChildren<Renderer>(true);

                foreach (var r in renderers)
                {
                    if (r == null || ShouldIgnoreRenderer(r)) continue;

                    // Ignora elementos pertencentes ao rig de showcase
                    if (r.transform.IsChildOf(variantRoot.transform) && r.name.StartsWith("__ToonShowcase"))
                        continue;

                    Material[] currentMats = r.sharedMaterials;
                    if (currentMats == null || currentMats.Length == 0) continue;

                    Material[] newMats = new Material[currentMats.Length];
                    bool modified = false;

                    for (int s = 0; s < currentMats.Length; s++)
                    {
                        Material oldMat = currentMats[s];
                        MaterialPaletteRole role = MaterialPaletteRole.ParedeClara;
                        if (r is MeshRenderer mr)
                        {
                            role = PizzeriaMaterialApplicator.ResolveRole(mr, s, profile, mr.GetComponent<PizzeriaNodeIdentity>());
                        }
                        else
                        {
                            string matName = oldMat != null ? oldMat.name : "";
                            role = profile.SuggestRole(r.gameObject.name, matName);
                        }

                        if (role == MaterialPaletteRole.Preserve)
                        {
                            newMats[s] = oldMat;
                            result.preservedSlots++;
                            continue;
                        }

                        Material toonMat = ToonMaterialFactory.GetOrCreateToonMaterial(oldMat, role, profile, manifest);
                        if (toonMat != null)
                        {
                            newMats[s] = toonMat;
                            result.slotsUpdated++;
                            modified = true;
                        }
                        else
                        {
                            newMats[s] = oldMat;
                            result.preservedSlots++;
                        }
                    }

                    if (modified)
                    {
                        r.sharedMaterials = newMats;
                        result.renderersUpdated++;
                    }
                }

                // Salva exclusivamente na cópia
                PrefabUtility.SaveAsPrefabAsset(variantRoot, VariantPrefabPath);
                result.success = true;
                result.message = $"Sucesso: {result.renderersUpdated} renderers atualizados ({result.slotsUpdated} slots convertidos, {result.preservedSlots} preservados).";

                // Salva manifesto JSON
                string manifestDir = "Assets/Piaaria/Art/Pizzeria/Toon/Reports";
                if (!Directory.Exists(manifestDir)) Directory.CreateDirectory(manifestDir);
                string manifestJson = EditorJsonUtility.ToJson(manifest, true);
                File.WriteAllText($"{manifestDir}/ToonMaterialManifest.json", manifestJson, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                result.success = false;
                result.errors.Add($"Exceção durante aplicação de materiais toon: {ex.Message}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(variantRoot);
            }

            AssetDatabase.Refresh();
            return result;
        }

        private static bool ShouldIgnoreRenderer(Renderer r)
        {
            if (r == null) return true;
            if (r.GetComponent<Canvas>() != null || r.GetComponent<CanvasRenderer>() != null) return true;
            if (r.name.IndexOf("Collider", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (r.name.IndexOf("Trigger", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }
    }
}
#endif
