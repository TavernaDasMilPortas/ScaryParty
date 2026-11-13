#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using ScaryParty.City.Modular;
using ScaryParty.Pizzeria.Authoring;
using ScaryParty.Pizzeria.Composition;
using ScaryParty.VisualStyles;

namespace ScaryParty.Pizzeria.Editor.Authoring
{
    /// <summary>
    /// Utilitário idempotente para gerar o teto modular da Pizzaria usando o módulo de teto canônico da cidade
    /// (CityModuleId.E / cobertura_plana.fbx) cobrindo a planta em L, além de adicionar múltiplos focos de luz
    /// distribuídos estrategicamente (salão, mesas, balcão, cozinha, forno, estoque/logística)
    /// para testar a iluminação e cel shading / shader toon.
    /// </summary>
    public static class PizzeriaCeilingAndLightingBuilder
    {
        public const string ManualPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_Manual.prefab";
        public const string CelShadedPrefabPath = "Assets/Piaaria/Prefabs/Pizzeria/PizzeriaModular_CelShaded.prefab";
        public const string SoftToonVariantPrefabPath = "Assets/Piaaria/Prefabs/VisualStyles/soft_toon_vibrant/PizzeriaModular_soft_toon_vibrant.prefab";

        public const string CityCatalogPath = "Assets/Piaaria/ScriptableObjects/City/Modular/CityModuleCatalog.asset";
        public const string CoberturaFbxPath = "Assets/Piaaria/Prefabs/City/Modules/cobertura_plana.fbx";

        public const float CanonicalCeilingY = 3.235f;

        [MenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Adicionar Teto e Luzes de Teste na Pizzaria", false, 155)]
        public static void BuildCeilingAndLightingManualPrefab()
        {
            int updated = 0;
            if (File.Exists(ManualPrefabPath))
            {
                if (ApplyCeilingAndLightsToPrefab(ManualPrefabPath, out string reportM))
                {
                    Debug.Log($"<b>[PizzeriaCeilingAndLighting]</b> Manual Prefab: {reportM}");
                    updated++;
                }
            }

            if (File.Exists(CelShadedPrefabPath))
            {
                if (ApplyCeilingAndLightsToPrefab(CelShadedPrefabPath, out string reportC))
                {
                    Debug.Log($"<b>[PizzeriaCeilingAndLighting]</b> CelShaded Prefab: {reportC}");
                    updated++;
                }
            }

            if (File.Exists(SoftToonVariantPrefabPath))
            {
                if (ApplyCeilingAndLightsToPrefab(SoftToonVariantPrefabPath, out string reportS))
                {
                    Debug.Log($"<b>[PizzeriaCeilingAndLighting]</b> SoftToon Variant Prefab: {reportS}");
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Teto e Luzes da Pizzaria",
                $"Processamento concluído com sucesso em {updated} prefab(s) da pizzaria!\n\n" +
                $"• Teto modular da cidade (Peça E - 2x2m) cobrindo toda a planta em L.\n" +
                $"• Focos de luz pontuais e spots distribuídos por Salão, Balcão, Mesas, Cozinha, Forno e Logística.\n" +
                $"• Organizado sob 'Arquitetura/Tetos' e 'LightingRig' preservando a estrutura de autoria.",
                "OK"
            );
        }

        /// <summary>
        /// Aplica teto e luzes diretamente no asset de prefab especificado.
        /// </summary>
        public static bool ApplyCeilingAndLightsToPrefab(string prefabPath, out string report)
        {
            report = string.Empty;
            if (string.IsNullOrEmpty(prefabPath) || !File.Exists(prefabPath))
            {
                report = $"Arquivo não encontrado: {prefabPath}";
                return false;
            }

            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (root == null)
                {
                    report = "Falha ao carregar PrefabContents.";
                    return false;
                }

                int tiles = BuildCeilingGrid(root.transform, CanonicalCeilingY);
                int lights = BuildInteriorLightingRig(root.transform, CanonicalCeilingY);

                // Aplica estilo visual aos novos módulos de teto se um gerenciador de estilo estiver presente
                ApplyVisualStyleToCeiling(root);

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                report = $"Sucesso! {tiles} módulos de teto montados e {lights} focos de luz configurados.";
                return true;
            }
            catch (Exception ex)
            {
                report = $"Exceção ao processar '{prefabPath}': {ex.Message}";
                Debug.LogException(ex);
                return false;
            }
            finally
            {
                if (root != null)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        /// <summary>
        /// Monta a grade de módulos de teto cobrindo o Salão (Região 1) e Cozinha/Logística (Região 2).
        /// </summary>
        public static int BuildCeilingGrid(Transform rootT, float ceilingY)
        {
            if (rootT == null) return 0;

            CityModuleCatalog catalog = AssetDatabase.LoadAssetAtPath<CityModuleCatalog>(CityCatalogPath);
            CityModuleDefinition defE = catalog != null ? catalog.GetDefinition(CityModuleId.E) : null;
            GameObject roofPrefab = defE?.prefab ?? AssetDatabase.LoadAssetAtPath<GameObject>(CoberturaFbxPath);

            if (roofPrefab == null)
            {
                Debug.LogError($"[PizzeriaCeilingAndLighting] Prefab da cobertura (Peça E) não encontrado em {CoberturaFbxPath} ou CityModuleCatalog!");
                return 0;
            }

            Transform tetosContainer = PizzeriaHierarchyOrganizer.GetOrCreateCategoryContainer(
                rootT,
                PizzeriaHierarchyCategory.Architecture_Ceilings);

            Transform tetosGerados = tetosContainer.Find("TetosGerados");
            if (tetosGerados == null)
            {
                var go = new GameObject("TetosGerados");
                Undo.RegisterCreatedObjectUndo(go, "Criar TetosGerados");
                tetosGerados = go.transform;
                tetosGerados.SetParent(tetosContainer, false);
                tetosGerados.localPosition = Vector3.zero;
                tetosGerados.localRotation = Quaternion.identity;
                tetosGerados.localScale = Vector3.one;
            }

            // Limpa peças antigas de TetosGerados
            for (int i = tetosGerados.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(tetosGerados.GetChild(i).gameObject);
            }

            // Regiões da planta em L (grade canônica de 2 em 2 metros)
            // Região 1: Salão e Atendimento (X: 0 a 32m, Z: -12 a 8m) -> passos de 2m
            // Região 2: Cozinha, Estoque e Logística (X: 16 a 32m, Z: -26 a -14m)
            var occupiedCoords = new HashSet<Vector2Int>();
            int createdCount = 0;

            void SpawnRoofTile(int gx, int gz)
            {
                var coord = new Vector2Int(gx, gz);
                if (occupiedCoords.Contains(coord)) return;
                occupiedCoords.Add(coord);

                float posX = gx * 2.0f + 1.0f;
                float posZ = gz * 2.0f + 1.0f;

                GameObject tile = UnityEngine.Object.Instantiate(roofPrefab, tetosGerados);
                tile.name = $"Roof_X{gx}_Z{gz}";
                tile.transform.localPosition = new Vector3(posX, ceilingY, posZ);
                tile.transform.localRotation = Quaternion.identity;
                tile.transform.localScale = Vector3.one;

                CityModuleInstance rInst = tile.GetComponent<CityModuleInstance>() ?? tile.AddComponent<CityModuleInstance>();
                rInst.moduleId = CityModuleId.E;
                rInst.side = FacadeSide.Roof;
                rInst.buildingId = "Pizzeria";
                rInst.AutoWireHierarchy();

                if (defE != null)
                {
                    ModularBuildingAssembler.ApplyVisualOffsets(tile, defE, rInst);
                }

                var identity = tile.GetComponent<PizzeriaNodeIdentity>() ?? tile.AddComponent<PizzeriaNodeIdentity>();
                identity.EnsureGuid();
                identity.originalNodeName = tile.name;
                identity.category = PizzeriaHierarchyCategory.Architecture_Ceilings;
                identity.manualAssociationLocked = true;

                createdCount++;
            }

            // Região 1: Salão / Atendimento / Fachada (X: 0..32, Z: -14..8)
            // gx: 0 a 16 (X: 1m a 33m)
            // gz: -7 a 4 (Z: -13m a +9m)
            for (int gx = 0; gx <= 16; gx++)
            {
                for (int gz = -7; gz <= 4; gz++)
                {
                    SpawnRoofTile(gx, gz);
                }
            }

            // Região 2: Cozinha, Despensa e Logística (X: 16..32, Z: -28..-14)
            // gx: 8 a 16 (X: 17m a 33m)
            // gz: -14 a -8 (Z: -27m a -15m)
            for (int gx = 8; gx <= 16; gx++)
            {
                for (int gz = -14; gz <= -8; gz++)
                {
                    SpawnRoofTile(gx, gz);
                }
            }

            return createdCount;
        }

        /// <summary>
        /// Monta o conjunto completo de focos de luz (LightingRig) internos na Pizzaria
        /// com intensidades, alcances e tonalidades balanceadas para realçar o cel shading.
        /// </summary>
        public static int BuildInteriorLightingRig(Transform rootT, float ceilingY)
        {
            if (rootT == null) return 0;

            Transform lightingRig = rootT.Find("LightingRig");
            if (lightingRig == null)
            {
                var go = new GameObject("LightingRig");
                Undo.RegisterCreatedObjectUndo(go, "Criar LightingRig");
                lightingRig = go.transform;
                lightingRig.SetParent(rootT, false);
                lightingRig.localPosition = Vector3.zero;
                lightingRig.localRotation = Quaternion.identity;
                lightingRig.localScale = Vector3.one;
            }

            // Limpa luzes antigas geradas para idempotência
            for (int i = lightingRig.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(lightingRig.GetChild(i).gameObject);
            }

            int lightCount = 0;
            float lightHeight = ceilingY - 0.45f; // ligeiramente abaixo das placas de teto

            // --- 1. SALÃO DE MESAS (Luzes quentes acolhedoras) ---
            CreateLight(lightingRig, "Light_Salao_Centro_01", new Vector3(8.0f, lightHeight, -2.0f),
                LightType.Point, new Color(1.0f, 0.94f, 0.85f), 1.8f, 12f, LightShadows.Soft);
            lightCount++;

            CreateLight(lightingRig, "Light_Salao_Centro_02", new Vector3(8.0f, lightHeight, 4.0f),
                LightType.Point, new Color(1.0f, 0.94f, 0.85f), 1.8f, 12f, LightShadows.Soft);
            lightCount++;

            CreateLight(lightingRig, "Light_Salao_Janelas", new Vector3(2.5f, lightHeight, -6.5f),
                LightType.Point, new Color(1.0f, 0.92f, 0.82f), 1.6f, 10f, LightShadows.None);
            lightCount++;

            CreateLight(lightingRig, "Light_Mesas_Norte", new Vector3(4.0f, lightHeight, 2.5f),
                LightType.Point, new Color(1.0f, 0.90f, 0.80f), 1.7f, 9f, LightShadows.None);
            lightCount++;

            CreateLight(lightingRig, "Light_Mesas_Sul", new Vector3(4.0f, lightHeight, -8.5f),
                LightType.Point, new Color(1.0f, 0.90f, 0.80f), 1.7f, 9f, LightShadows.None);
            lightCount++;

            // --- 2. BALCÃO DE ATENDIMENTO E CAIXA ---
            CreateLight(lightingRig, "Light_Balcao_Atendimento", new Vector3(12.5f, lightHeight, -3.5f),
                LightType.Point, new Color(1.0f, 0.98f, 0.92f), 2.2f, 9f, LightShadows.Soft);
            lightCount++;

            CreateLight(lightingRig, "Light_Caixa_Registradora", new Vector3(12.5f, lightHeight, 1.5f),
                LightType.Point, new Color(1.0f, 0.98f, 0.92f), 2.0f, 8f, LightShadows.None);
            lightCount++;

            // Spot focado no balcão de pedidos
            CreateSpotLight(lightingRig, "Spot_Balcao_Destaque", new Vector3(11.5f, lightHeight + 0.1f, -1.0f),
                new Vector3(70f, 90f, 0f), new Color(1.0f, 0.96f, 0.88f), 2.5f, 8f, 55f, LightShadows.Soft);
            lightCount++;

            // --- 3. COZINHA E BANCADAS OPERACIONAIS (Luz branca/neutra limpa) ---
            CreateLight(lightingRig, "Light_Cozinha_Principal", new Vector3(20.5f, lightHeight, -5.0f),
                LightType.Point, new Color(0.95f, 0.98f, 1.0f), 2.2f, 14f, LightShadows.Soft);
            lightCount++;

            CreateLight(lightingRig, "Light_Bancada_Preparo_Massa", new Vector3(17.5f, lightHeight, 2.0f),
                LightType.Point, new Color(0.96f, 0.98f, 1.0f), 2.0f, 10f, LightShadows.None);
            lightCount++;

            CreateLight(lightingRig, "Light_Bancada_Montagem", new Vector3(23.5f, lightHeight, 2.0f),
                LightType.Point, new Color(0.96f, 0.98f, 1.0f), 2.0f, 10f, LightShadows.None);
            lightCount++;

            // --- 4. FORNO A LENHA (Destaque alaranjado/fogo) ---
            CreateLight(lightingRig, "Light_Forno_Realce_Fogo", new Vector3(28.0f, 1.8f, -13.5f),
                LightType.Point, new Color(1.0f, 0.55f, 0.15f), 2.8f, 8.5f, LightShadows.Soft);
            lightCount++;

            CreateSpotLight(lightingRig, "Spot_Forno_Boca", new Vector3(28.0f, lightHeight, -11.5f),
                new Vector3(60f, 180f, 0f), new Color(1.0f, 0.65f, 0.25f), 3.0f, 9f, 60f, LightShadows.Soft);
            lightCount++;

            // --- 5. LOGÍSTICA, ESTOQUE E DESPENSA (Ala Sul da planta em L) ---
            CreateLight(lightingRig, "Light_Logistica_Embalagem", new Vector3(20.5f, lightHeight, -18.5f),
                LightType.Point, new Color(0.95f, 0.97f, 1.0f), 2.0f, 12f, LightShadows.Soft);
            lightCount++;

            CreateLight(lightingRig, "Light_Estoque_Ingredientes", new Vector3(26.5f, lightHeight, -20.5f),
                LightType.Point, new Color(0.92f, 0.95f, 1.0f), 1.9f, 11f, LightShadows.None);
            lightCount++;

            CreateLight(lightingRig, "Light_Saida_Entrega_Delivery", new Vector3(20.5f, lightHeight, -25.5f),
                LightType.Point, new Color(1.0f, 0.95f, 0.88f), 2.2f, 10f, LightShadows.Soft);
            lightCount++;

            return lightCount;
        }

        private static GameObject CreateLight(Transform parent, string name, Vector3 localPos, LightType type, Color color, float intensity, float range, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows;

            var identity = go.AddComponent<PizzeriaNodeIdentity>();
            identity.EnsureGuid();
            identity.originalNodeName = name;
            identity.category = PizzeriaHierarchyCategory.Decoration;
            identity.manualAssociationLocked = true;

            return go;
        }

        private static GameObject CreateSpotLight(Transform parent, string name, Vector3 localPos, Vector3 eulerRot, Color color, float intensity, float range, float spotAngle, LightShadows shadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(eulerRot);

            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.spotAngle = spotAngle;
            l.shadows = shadows;

            var identity = go.AddComponent<PizzeriaNodeIdentity>();
            identity.EnsureGuid();
            identity.originalNodeName = name;
            identity.category = PizzeriaHierarchyCategory.Decoration;
            identity.manualAssociationLocked = true;

            return go;
        }

        private static void ApplyVisualStyleToCeiling(GameObject root)
        {
            try
            {
                var mgr = UnityEngine.Object.FindObjectOfType<VisualStyleManager>();
                if (mgr != null && mgr.activeProfile != null)
                {
                    VisualStyleManager.ApplyHierarchyOrFallback(root, VisualSemanticRole.BuildingFacade, VisualCategoryIds.City);
                }
            }
            catch (Exception)
            {
                // Silencioso se não houver gerenciador ativo no momento de salvamento do prefab
            }
        }
    }
}
#endif
