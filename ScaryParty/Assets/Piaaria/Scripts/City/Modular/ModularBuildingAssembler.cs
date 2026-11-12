using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Construtor determinístico que transforma um BuildingLayout em hierarquia física de GameObjects
    /// instanciando os prefabs do CityModuleCatalog.
    /// Não consome novos números aleatórios durante a montagem.
    /// </summary>
    public static class ModularBuildingAssembler
    {
        /// <summary>
        /// Monta o edifício completo na cena a partir de um layout previamente planejado.
        /// </summary>
        public static GameObject AssembleBuilding(BuildingLayout layout, CityModuleCatalog catalog, Transform parentTransform)
        {
            if (layout == null || catalog == null)
            {
                Debug.LogError("[ModularBuildingAssembler] Layout ou catálogo nulos.");
                return null;
            }

            string bName = string.IsNullOrEmpty(layout.buildingId) ? $"Building_{Guid.NewGuid().ToString().Substring(0, 8)}" : layout.buildingId;
            GameObject rootObj = new GameObject(bName);
            if (parentTransform != null) rootObj.transform.SetParent(parentTransform);

            rootObj.transform.position = layout.worldPosition;
            rootObj.transform.rotation = layout.worldRotation;
            rootObj.transform.localScale = Vector3.one;

            // Pastas organizadoras internas
            GameObject facadesGroup = new GameObject("Facades");
            facadesGroup.transform.SetParent(rootObj.transform, false);

            GameObject roofGroup = new GameObject("Roof");
            roofGroup.transform.SetParent(rootObj.transform, false);

            GameObject parapetGroup = new GameObject("Parapet");
            parapetGroup.transform.SetParent(rootObj.transform, false);

            GameObject pillarsGroup = new GameObject("Pillars");
            pillarsGroup.transform.SetParent(rootObj.transform, false);

            GameObject awningsGroup = new GameObject("Awnings");
            awningsGroup.transform.SetParent(rootObj.transform, false);

            float halfW = layout.WidthMeters * 0.5f;
            float depth = layout.DepthMeters;

            // 1. Instancia as 4 fachadas para cada pavimento
            for (int f = 0; f < layout.floors.Count; f++)
            {
                FloorLayout fl = layout.floors[f];
                float floorY = f * 3.0f;

                // Fachada Frontal (Z = 0, frente +Z)
                AssembleFacadeLine(fl.frontModules, fl.frontAwnings, f, FacadeSide.Front, floorY,
                    layout.widthModules, halfW, depth, catalog, facadesGroup.transform, awningsGroup.transform, layout.buildingId);

                // Fachada Traseira (Z = -depth, frente -Z)
                AssembleFacadeLine(fl.backModules, null, f, FacadeSide.Back, floorY,
                    layout.widthModules, halfW, depth, catalog, facadesGroup.transform, awningsGroup.transform, layout.buildingId);

                // Fachada Esquerda (X = -halfW, frente -X)
                AssembleFacadeLine(fl.leftModules, null, f, FacadeSide.Left, floorY,
                    layout.depthModules, halfW, depth, catalog, facadesGroup.transform, awningsGroup.transform, layout.buildingId);

                // Fachada Direita (X = +halfW, frente +X)
                AssembleFacadeLine(fl.rightModules, null, f, FacadeSide.Right, floorY,
                    layout.depthModules, halfW, depth, catalog, facadesGroup.transform, awningsGroup.transform, layout.buildingId);

                // 2. Pilares de canto (Peça L) para cada pavimento
                AssembleCornerPillars(f, floorY, halfW, depth, catalog, pillarsGroup.transform, layout.buildingId);
            }

            // 3. Cobertura Plana (Peça E) cobrindo toda a grade em floorCount * 3m
            float roofY = layout.floorCount * 3.0f;
            AssembleRoofGrid(layout.widthModules, layout.depthModules, halfW, depth, roofY, catalog, roofGroup.transform, layout.buildingId);

            // 4. Platibanda (Peça F) no topo da cobertura (roofY + 0.2m)
            float parapetY = roofY + 0.2f;
            AssembleParapetPerimeter(layout.widthModules, layout.depthModules, halfW, depth, parapetY, catalog, parapetGroup.transform, layout.buildingId);

            // 5. Aplica estilo global (se ativo) e cores nos renderers dos módulos instanciados
            ScaryParty.VisualStyles.VisualStyleManager.ApplyHierarchyOrFallback(
                rootObj,
                ScaryParty.VisualStyles.VisualSemanticRole.BuildingFacade,
                ScaryParty.VisualStyles.VisualCategoryIds.City);
            ApplyBuildingPalette(rootObj, layout.buildingColor, catalog);

            // 6. Configura o componente CityBuilding na raiz
            CityBuilding cb = rootObj.AddComponent<CityBuilding>();
            cb.buildingId = layout.buildingId;
            cb.blockId = layout.blockId;
            cb.footprint = new Vector2(layout.WidthMeters, layout.DepthMeters);
            cb.floorCount = layout.floorCount;
            cb.zone = layout.zone;
            cb.buildingColor = layout.buildingColor;
            cb.canHaveDeliveryPoint = (layout.zone != ZoneType.Industrial);
            cb.canAdmitPedestrians = true;

            // Configura âncoras no mundo
            cb.entrancePosition = layout.entranceWorldPos;
            cb.entranceDirection = layout.entranceForwardWorld;
            cb.EnsurePortal();

            // Configura âncoras adicionais para compatibilidade
            SetupCityBuildingAnchors(cb, layout);

            return rootObj;
        }

        private static void AssembleFacadeLine(List<CityModuleId> moduleIds, List<bool> awnings, int floor,
            FacadeSide side, float floorY, int totalSlots, float halfW, float depth,
            CityModuleCatalog catalog, Transform facadeParent, Transform awningParent, string buildingId)
        {
            if (moduleIds == null || moduleIds.Count == 0) return;

            int slot = 0;
            for (int i = 0; i < moduleIds.Count && slot < totalSlots; i++)
            {
                CityModuleId modId = moduleIds[i];
                CityModuleDefinition def = catalog.GetDefinition(modId);
                if (def == null || def.prefab == null)
                {
                    slot++;
                    continue;
                }

                int occupied = Mathf.Max(1, def.occupiedSlots);
                float moduleWidth = def.width;

                Vector3 localPos = Vector3.zero;
                Quaternion localRot = Quaternion.identity;

                switch (side)
                {
                    case FacadeSide.Front:
                        // Frente em Z = 0, X de -halfW a +halfW, voltada para fora (+Z)
                        float xFront = -halfW + (slot * 2.0f) + (moduleWidth * 0.5f);
                        localPos = new Vector3(xFront, floorY, 0f);
                        localRot = Quaternion.identity;
                        break;

                    case FacadeSide.Back:
                        // Traseira em Z = -depth, X de +halfW a -halfW, voltada para fora (-Z)
                        float xBack = halfW - (slot * 2.0f) - (moduleWidth * 0.5f);
                        localPos = new Vector3(xBack, floorY, -depth);
                        localRot = Quaternion.Euler(0f, 180f, 0f);
                        break;

                    case FacadeSide.Left:
                        // Lateral esquerda em X = -halfW, Z de 0 a -depth, voltada para fora (-X)
                        float zLeft = -(slot * 2.0f) - (moduleWidth * 0.5f);
                        localPos = new Vector3(-halfW, floorY, zLeft);
                        localRot = Quaternion.Euler(0f, -90f, 0f);
                        break;

                    case FacadeSide.Right:
                        // Lateral direita em X = +halfW, Z de -depth a 0, voltada para fora (+X)
                        float zRight = -depth + (slot * 2.0f) + (moduleWidth * 0.5f);
                        localPos = new Vector3(halfW, floorY, zRight);
                        localRot = Quaternion.Euler(0f, 90f, 0f);
                        break;
                }

                GameObject instance = UnityEngine.Object.Instantiate(def.prefab, facadeParent);
                instance.name = $"{side}_{modId}_F{floor}_S{slot}";
                instance.transform.localPosition = localPos;
                instance.transform.localRotation = localRot;
                instance.transform.localScale = Vector3.one;

                CityModuleInstance modInst = instance.GetComponent<CityModuleInstance>() ?? instance.AddComponent<CityModuleInstance>();
                modInst.moduleId = modId;
                modInst.buildingId = buildingId;
                modInst.floorIndex = floor;
                modInst.slotIndex = slot;
                modInst.side = side;
                modInst.AutoWireHierarchy();

                // Aplica visualPositionOffset e visualRotationOffset no nó Visual (se configurados na definição)
                ApplyVisualOffsets(instance, def, modInst);

                // Montagem do toldo K se especificado
                if (awnings != null && i < awnings.Count && awnings[i])
                {
                    CityModuleDefinition defK = catalog.GetDefinition(CityModuleId.K);
                    if (defK != null && defK.prefab != null)
                    {
                        Vector3 awningLocalPos = localPos + localRot * (def.awningAnchorLocal != Vector3.zero ? def.awningAnchorLocal : new Vector3(0f, 2.7f, 0f));
                        GameObject awningObj = UnityEngine.Object.Instantiate(defK.prefab, awningParent);
                        awningObj.name = $"Awning_F{floor}_S{slot}";
                        awningObj.transform.localPosition = awningLocalPos;
                        awningObj.transform.localRotation = localRot;
                        awningObj.transform.localScale = Vector3.one;

                        CityModuleInstance awnInst = awningObj.GetComponent<CityModuleInstance>() ?? awningObj.AddComponent<CityModuleInstance>();
                        awnInst.moduleId = CityModuleId.K;
                        awnInst.buildingId = buildingId;
                        awnInst.floorIndex = floor;
                        awnInst.slotIndex = slot;
                        awnInst.side = side;
                        awnInst.AutoWireHierarchy();

                        ApplyVisualOffsets(awningObj, defK, awnInst);
                    }
                }

                slot += occupied;
            }
        }

        private static void AssembleCornerPillars(int floor, float floorY, float halfW, float depth,
            CityModuleCatalog catalog, Transform pillarsParent, string buildingId)
        {
            CityModuleDefinition defL = catalog.GetDefinition(CityModuleId.L);
            if (defL == null || defL.prefab == null) return;

            // Os 4 cantos externos
            // O pilar é 0.2m x 0.2m, centrado em sua base
            Vector3[] cornerPositions = new Vector3[4]
            {
                new Vector3(-halfW + 0.1f, floorY, -0.1f),          // Front-Left
                new Vector3(halfW - 0.1f, floorY, -0.1f),           // Front-Right
                new Vector3(halfW - 0.1f, floorY, -depth + 0.1f),   // Back-Right
                new Vector3(-halfW + 0.1f, floorY, -depth + 0.1f)   // Back-Left
            };

            for (int c = 0; c < 4; c++)
            {
                GameObject pillarObj = UnityEngine.Object.Instantiate(defL.prefab, pillarsParent);
                pillarObj.name = $"Pillar_C{c}_F{floor}";
                pillarObj.transform.localPosition = cornerPositions[c];
                pillarObj.transform.localRotation = Quaternion.identity;
                pillarObj.transform.localScale = Vector3.one;

                CityModuleInstance pInst = pillarObj.GetComponent<CityModuleInstance>() ?? pillarObj.AddComponent<CityModuleInstance>();
                pInst.moduleId = CityModuleId.L;
                pInst.buildingId = buildingId;
                pInst.floorIndex = floor;
                pInst.side = FacadeSide.Pillar;
                pInst.AutoWireHierarchy();

                ApplyVisualOffsets(pillarObj, defL, pInst);
            }
        }

        private static void AssembleRoofGrid(int widthModules, int depthModules, float halfW, float depth, float roofY,
            CityModuleCatalog catalog, Transform roofParent, string buildingId)
        {
            CityModuleDefinition defE = catalog.GetDefinition(CityModuleId.E);
            if (defE == null || defE.prefab == null) return;

            for (int wx = 0; wx < widthModules; wx++)
            {
                float x = -halfW + (wx * 2.0f) + 1.0f;
                for (int dz = 0; dz < depthModules; dz++)
                {
                    float z = -(dz * 2.0f) - 1.0f;

                    GameObject roofObj = UnityEngine.Object.Instantiate(defE.prefab, roofParent);
                    roofObj.name = $"Roof_X{wx}_Z{dz}";
                    roofObj.transform.localPosition = new Vector3(x, roofY, z);
                    roofObj.transform.localRotation = Quaternion.identity;
                    roofObj.transform.localScale = Vector3.one;

                    CityModuleInstance rInst = roofObj.GetComponent<CityModuleInstance>() ?? roofObj.AddComponent<CityModuleInstance>();
                    rInst.moduleId = CityModuleId.E;
                    rInst.buildingId = buildingId;
                    rInst.side = FacadeSide.Roof;
                    rInst.AutoWireHierarchy();

                    ApplyVisualOffsets(roofObj, defE, rInst);
                }
            }
        }

        private static void AssembleParapetPerimeter(int widthModules, int depthModules, float halfW, float depth, float parapetY,
            CityModuleCatalog catalog, Transform parapetParent, string buildingId)
        {
            CityModuleDefinition defF = catalog.GetDefinition(CityModuleId.F);
            if (defF == null || defF.prefab == null) return;

            // Front Edge (Z = 0, olha +Z)
            for (int wx = 0; wx < widthModules; wx++)
            {
                float x = -halfW + (wx * 2.0f) + 1.0f;
                SpawnParapetPiece(defF, new Vector3(x, parapetY, 0f), Quaternion.identity, parapetParent, $"Parapet_Front_{wx}", buildingId);
            }

            // Back Edge (Z = -depth, olha -Z)
            for (int wx = 0; wx < widthModules; wx++)
            {
                float x = halfW - (wx * 2.0f) - 1.0f;
                SpawnParapetPiece(defF, new Vector3(x, parapetY, -depth), Quaternion.Euler(0f, 180f, 0f), parapetParent, $"Parapet_Back_{wx}", buildingId);
            }

            // Left Edge (X = -halfW, olha -X)
            for (int dz = 0; dz < depthModules; dz++)
            {
                float z = -(dz * 2.0f) - 1.0f;
                SpawnParapetPiece(defF, new Vector3(-halfW, parapetY, z), Quaternion.Euler(0f, -90f, 0f), parapetParent, $"Parapet_Left_{dz}", buildingId);
            }

            // Right Edge (X = +halfW, olha +X)
            for (int dz = 0; dz < depthModules; dz++)
            {
                float z = -depth + (dz * 2.0f) + 1.0f;
                SpawnParapetPiece(defF, new Vector3(halfW, parapetY, z), Quaternion.Euler(0f, 90f, 0f), parapetParent, $"Parapet_Right_{dz}", buildingId);
            }
        }

        private static void SpawnParapetPiece(CityModuleDefinition defF, Vector3 pos, Quaternion rot, Transform parent, string name, string bId)
        {
            GameObject obj = UnityEngine.Object.Instantiate(defF.prefab, parent);
            obj.name = name;
            obj.transform.localPosition = pos;
            obj.transform.localRotation = rot;
            obj.transform.localScale = Vector3.one;

            CityModuleInstance inst = obj.GetComponent<CityModuleInstance>() ?? obj.AddComponent<CityModuleInstance>();
            inst.moduleId = CityModuleId.F;
            inst.buildingId = bId;
            inst.side = FacadeSide.Parapet;
            inst.AutoWireHierarchy();

            ApplyVisualOffsets(obj, defF, inst);
        }

        /// <summary>
        /// Aplica as compensações de posição e rotação visual aos filhos gráficos do módulo,
        /// garantindo alinhamento perfeito tanto de placeholders canônicos quanto de modelos autorais FBX importados do Blender.
        /// Se o prefab possuir o nó canônico "Visual", compensa nele.
        /// Se for um FBX direto (onde as meshes são filhas da raiz ou o visualRoot é um dos nós),
        /// aplica a compensação em TODOS os nós gráficos/filhos de malha para manter integridade.
        /// Se a definição estiver zerada na memória, aplica fallback automático para modelos conhecidos com eixos do Blender.
        /// </summary>
        public static void ApplyVisualOffsets(GameObject instance, CityModuleDefinition def, CityModuleInstance modInst)
        {
            if (instance == null || def == null) return;

            Vector3 posOffset = def.visualPositionOffset;
            Vector3 rotOffsetEuler = def.visualRotationOffset;

            // Se ambos forem zero, respeita o valor zerado sem forçar sobreposição
            if (posOffset == Vector3.zero && rotOffsetEuler == Vector3.zero) return;

            Quaternion rotOffset = Quaternion.Euler(rotOffsetEuler);

            // 1. Verifica se existe o nó canônico formal "Visual"
            Transform explicitVisual = instance.transform.Find("Visual");
            if (explicitVisual != null)
            {
                explicitVisual.localPosition = posOffset;
                explicitVisual.localRotation = rotOffset;
                return;
            }

            // 2. Se for um FBX com filhos (ex: parede_porta com folha, etc.)
            bool hasValidChildren = false;
            for (int i = 0; i < instance.transform.childCount; i++)
            {
                Transform child = instance.transform.GetChild(i);
                if (child.name != "Collision" && child.name != "Anchors")
                {
                    hasValidChildren = true;
                    break;
                }
            }

            if (hasValidChildren)
            {
                for (int i = 0; i < instance.transform.childCount; i++)
                {
                    Transform child = instance.transform.GetChild(i);
                    if (child.name == "Collision" || child.name == "Anchors") continue;

                    child.localPosition = posOffset;
                    child.localRotation = rotOffset;
                }
                return;
            }

            // 3. Se for um FBX direto cuja malha/renderer está na própria raiz (como platibanda.fbx, pilar_de_canto.fbx, etc.)
            // Não podemos mexer no transform raiz de 'instance' porque ele mantém o slot e orientação do edifício.
            // Solução robusta: criamos um filho intermediário "Visual", transferimos os componentes visuais para ele e aplicamos os offsets!
            MeshRenderer rootRenderer = instance.GetComponent<MeshRenderer>();
            MeshFilter rootFilter = instance.GetComponent<MeshFilter>();
            if (rootRenderer != null && rootFilter != null)
            {
                GameObject visualChild = new GameObject("Visual");
                visualChild.transform.SetParent(instance.transform, false);
                visualChild.layer = instance.layer;

                MeshFilter newFilter = visualChild.AddComponent<MeshFilter>();
                newFilter.sharedMesh = rootFilter.sharedMesh;

                MeshRenderer newRenderer = visualChild.AddComponent<MeshRenderer>();
                newRenderer.sharedMaterials = rootRenderer.sharedMaterials;

                // Destrói os componentes gráficos da raiz para não duplicar renderização
                UnityEngine.Object.DestroyImmediate(rootRenderer);
                UnityEngine.Object.DestroyImmediate(rootFilter);

                visualChild.transform.localPosition = posOffset;
                visualChild.transform.localRotation = rotOffset;

                if (modInst != null)
                {
                    modInst.visualRoot = visualChild.transform;
                }
            }
        }

        /// <summary>
        /// Gera a cor escura a partir da cor-base clara reduzindo o brilho (Value no HSV)
        /// pela intensidade especificada (padrão ~30%), preservando matiz (Hue) e saturação.
        /// </summary>
        public static Color GenerateTrimColor(Color baseColor, float darkenFactor = 0.30f)
        {
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            float darkV = Mathf.Clamp01(v * (1.0f - Mathf.Clamp01(darkenFactor)));
            Color trimColor = Color.HSVToRGB(h, s, darkV);
            trimColor.a = baseColor.a;
            return trimColor;
        }

        public static void ApplyBuildingPalette(GameObject rootObj, Color baseColor, CityModuleCatalog catalog = null, float darkenFactor = 0.30f)
        {
            Color trimColor = GenerateTrimColor(baseColor, darkenFactor);
            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();

            // Percorre todas as instâncias de módulos montadas na hierarquia do prédio
            CityModuleInstance[] instances = rootObj.GetComponentsInChildren<CityModuleInstance>(true);
            for (int i = 0; i < instances.Length; i++)
            {
                CityModuleInstance inst = instances[i];
                if (inst == null) continue;

                CityModuleDefinition def = catalog != null ? catalog.GetDefinition(inst.moduleId) : null;
                CityModuleMaterialRole[] roles = def?.materialRoles;

                Renderer[] renderers = inst.GetComponentsInChildren<Renderer>(true);
                for (int rIdx = 0; rIdx < renderers.Length; rIdx++)
                {
                    Renderer rend = renderers[rIdx];
                    if (rend == null) continue;

                    Material[] sharedMats = rend.sharedMaterials;
                    for (int mIdx = 0; mIdx < sharedMats.Length; mIdx++)
                    {
                        Material mat = sharedMats[mIdx];
                        if (mat == null) continue;

                        CityModuleMaterialRole role = CityModuleMaterialRole.Wall;
                        bool hasExplicitRole = false;

                        if (roles != null && mIdx < roles.Length)
                        {
                            role = roles[mIdx];
                            hasExplicitRole = true;
                        }

                        if (!hasExplicitRole)
                        {
                            // Inferência de fallback pelo nome do material ou tipo de peça
                            string matName = mat.name;
                            if (matName.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0)
                                role = CityModuleMaterialRole.Glass;
                            else if (matName.IndexOf("Fabric", StringComparison.OrdinalIgnoreCase) >= 0)
                                role = CityModuleMaterialRole.Fabric;
                            else if (matName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0)
                                role = CityModuleMaterialRole.Wood;
                            else if (matName.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
                                role = CityModuleMaterialRole.Metal;
                            else if (matName.IndexOf("Trim", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                     inst.moduleId == CityModuleId.L || inst.moduleId == CityModuleId.F)
                                role = CityModuleMaterialRole.Trim;
                            else
                                role = CityModuleMaterialRole.Wall;
                        }

                        // Aplica a tonalidade correspondente por índice de slot
                        switch (role)
                        {
                            case CityModuleMaterialRole.Wall:
                                ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, mIdx, baseColor, propBlock);
                                break;

                            case CityModuleMaterialRole.Trim:
                                ScaryParty.VisualStyles.VisualStyleShaderUtility.ApplyColorBlockToSlot(rend, mIdx, trimColor, propBlock);
                                break;

                            case CityModuleMaterialRole.Glass:
                            case CityModuleMaterialRole.Wood:
                            case CityModuleMaterialRole.Metal:
                            case CityModuleMaterialRole.Fabric:
                                // Preserva os materiais próprios sem alterar a cor
                                break;
                        }
                    }
                }
            }
        }

        private static void SetupCityBuildingAnchors(CityBuilding cb, BuildingLayout layout)
        {
            // Vincula âncoras adicionais ao CityBuilding
            if (cb.entranceAnchor == null)
            {
                GameObject ea = new GameObject("EntranceAnchor");
                ea.transform.SetParent(cb.transform, false);
                ea.transform.position = layout.entranceWorldPos;
                cb.entranceAnchor = ea.transform;
            }
            else
            {
                cb.entranceAnchor.position = layout.entranceWorldPos;
            }

            if (cb.sidewalkApproachAnchor == null)
            {
                GameObject aa = new GameObject("SidewalkApproachAnchor");
                aa.transform.SetParent(cb.transform, false);
                aa.transform.position = layout.approachWorldPos;
                cb.sidewalkApproachAnchor = aa.transform;
            }
            else
            {
                cb.sidewalkApproachAnchor.position = layout.approachWorldPos;
            }

            if (cb.doorAnchor == null)
            {
                GameObject da = new GameObject("DoorAnchor");
                da.transform.SetParent(cb.transform, false);
                da.transform.position = layout.entranceWorldPos;
                cb.doorAnchor = da.transform;
            }
            else
            {
                cb.doorAnchor.position = layout.entranceWorldPos;
            }

            if (cb.approachAnchor == null)
            {
                cb.approachAnchor = cb.sidewalkApproachAnchor;
            }

            if (cb.interiorAnchor == null)
            {
                GameObject ia = new GameObject("InteriorAnchor");
                ia.transform.SetParent(cb.transform, false);
                ia.transform.position = layout.interiorWorldPos;
                cb.interiorAnchor = ia.transform;
            }
            else
            {
                cb.interiorAnchor.position = layout.interiorWorldPos;
            }

            if (cb.deliveryAnchor == null)
            {
                GameObject dla = new GameObject("DeliveryAnchor");
                dla.transform.SetParent(cb.transform, false);
                dla.transform.position = layout.deliveryWorldPos;
                cb.deliveryAnchor = dla.transform;
            }
            else
            {
                cb.deliveryAnchor.position = layout.deliveryWorldPos;
            }
        }

        /// <summary>
        /// Gera uma das 4 receitas fixas da Seção 7 do plano.
        /// </summary>
        public static BuildingLayout CreateRecipeLayout(ModularBuildingRecipe recipe, Vector3 worldPos, Quaternion worldRot, int localSeed, string buildingId = null, int blockId = 0)
        {
            BuildingLayout layout = new BuildingLayout();
            layout.recipe = recipe;
            layout.worldPosition = worldPos;
            layout.worldRotation = worldRot;
            layout.localSeed = localSeed;
            layout.blockId = blockId;
            layout.buildingId = string.IsNullOrEmpty(buildingId) ? $"{recipe}_{localSeed}" : buildingId;

            System.Random rng = new System.Random(localSeed);
            float h = (float)rng.NextDouble();
            float sat = 0.4f + (float)rng.NextDouble() * 0.4f;
            float v = 0.5f + (float)rng.NextDouble() * 0.4f;
            layout.buildingColor = Color.HSVToRGB(h, sat, v);

            switch (recipe)
            {
                case ModularBuildingRecipe.HouseWithGarage:
                    // Casa com garagem: 8m larg x 6m prof x 1 pav (I=4m + C=2m + G=2m)
                    layout.widthModules = 4; // 8m
                    layout.depthModules = 3; // 6m
                    layout.floorCount = 1;
                    layout.zone = ZoneType.Residential;

                    FloorLayout f0 = new FloorLayout { floorIndex = 0 };
                    // Frente: I (ocupa 2 slots = 4m) + C (1 slot = 2m) + G (1 slot = 2m) -> total 4 slots
                    f0.frontModules.Add(CityModuleId.I);
                    f0.frontModules.Add(CityModuleId.C);
                    f0.frontModules.Add(CityModuleId.G);
                    f0.frontAwnings.AddRange(new[] { false, false, false });

                    // Traseira: 4 módulos de A e B
                    f0.backModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.B, CityModuleId.A });

                    // Laterais: 3 módulos de A e B
                    f0.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    f0.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });

                    layout.floors.Add(f0);

                    // Posição de entrada: Porta C está no slot 2 (X = -4m + 4m + 1m = +1m)
                    CalculateEntranceAnchors(layout, 1.0f);
                    break;

                case ModularBuildingRecipe.TwoStoryHouse:
                    // Sobrado: 6m larg x 6m prof x 2 pav (A + J + B térreo; andares superiores janelas/paredes)
                    layout.widthModules = 3; // 6m
                    layout.depthModules = 3; // 6m
                    layout.floorCount = 2;
                    layout.zone = ZoneType.Residential;

                    FloorLayout sh0 = new FloorLayout { floorIndex = 0 };
                    sh0.frontModules.AddRange(new[] { CityModuleId.A, CityModuleId.J, CityModuleId.B });
                    sh0.frontAwnings.AddRange(new[] { false, false, false });
                    sh0.backModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    sh0.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    sh0.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    layout.floors.Add(sh0);

                    FloorLayout sh1 = new FloorLayout { floorIndex = 1 };
                    sh1.frontModules.AddRange(new[] { CityModuleId.G, CityModuleId.H, CityModuleId.G });
                    sh1.frontAwnings.AddRange(new[] { false, false, false });
                    sh1.backModules.AddRange(new[] { CityModuleId.B, CityModuleId.B, CityModuleId.B });
                    sh1.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    sh1.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A });
                    layout.floors.Add(sh1);

                    // Entrada na porta dupla J centralizada no slot 1 (X = 0)
                    CalculateEntranceAnchors(layout, 0f);
                    break;

                case ModularBuildingRecipe.Shop:
                    // Loja: 6m larg x 8m prof x 1 pav (D + D + J, K acima das vitrines)
                    layout.widthModules = 3; // 6m
                    layout.depthModules = 4; // 8m
                    layout.floorCount = 1;
                    layout.zone = ZoneType.Commercial;

                    FloorLayout sp0 = new FloorLayout { floorIndex = 0 };
                    sp0.frontModules.AddRange(new[] { CityModuleId.D, CityModuleId.D, CityModuleId.J });
                    sp0.frontAwnings.AddRange(new[] { true, true, false }); // Toldo K acima das duas vitrines
                    sp0.backModules.AddRange(new[] { CityModuleId.A, CityModuleId.C, CityModuleId.A }); // Porta de serviço no fundo
                    sp0.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.A, CityModuleId.A, CityModuleId.A });
                    sp0.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.A, CityModuleId.A, CityModuleId.A });
                    layout.floors.Add(sp0);

                    // Entrada na porta dupla J no slot 2 (X = -3m + 4m + 1m = +2m)
                    CalculateEntranceAnchors(layout, 2.0f);
                    break;

                case ModularBuildingRecipe.MixedBuilding:
                default:
                    // Prédio misto: 8m larg x 8m prof x 4 pav (D + D + J + D térreo; janelas nos superiores)
                    layout.widthModules = 4; // 8m
                    layout.depthModules = 4; // 8m
                    layout.floorCount = 4;
                    layout.zone = ZoneType.Commercial;

                    FloorLayout mb0 = new FloorLayout { floorIndex = 0 };
                    mb0.frontModules.AddRange(new[] { CityModuleId.D, CityModuleId.D, CityModuleId.J, CityModuleId.D });
                    mb0.frontAwnings.AddRange(new[] { true, true, false, true });
                    mb0.backModules.AddRange(new[] { CityModuleId.A, CityModuleId.C, CityModuleId.B, CityModuleId.A });
                    mb0.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.A, CityModuleId.A, CityModuleId.A });
                    mb0.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.A, CityModuleId.A, CityModuleId.A });
                    layout.floors.Add(mb0);

                    for (int fl = 1; fl < 4; fl++)
                    {
                        FloorLayout upper = new FloorLayout { floorIndex = fl };
                        upper.frontModules.AddRange(new[] { CityModuleId.B, CityModuleId.H, CityModuleId.G, CityModuleId.B });
                        upper.frontAwnings.AddRange(new[] { false, false, false, false });
                        upper.backModules.AddRange(new[] { CityModuleId.B, CityModuleId.B, CityModuleId.B, CityModuleId.B });
                        upper.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.B, CityModuleId.A });
                        upper.rightModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.B, CityModuleId.A });
                        layout.floors.Add(upper);
                    }

                    // Entrada na porta J no slot 2 (X = -4m + 4m + 1m = +1m)
                    CalculateEntranceAnchors(layout, 1.0f);
                    break;

                case ModularBuildingRecipe.ShowcaseAll12Modules:
                    // Prédio Showcase: 8m larg x 8m prof x 2 pavimentos (4 x 4 módulos).
                    // Reúne explicitamente todos os 12 módulos canônicos:
                    // A (Parede Lisa), B (Janela), C (Porta), D (Vitrine), E (Cobertura Plana),
                    // F (Platibanda), G (Veneziana), H (Floreira), I (Garagem 4m), J (Porta Dupla),
                    // K (Toldo), L (Pilar de Canto)
                    layout.widthModules = 4; // 8m
                    layout.depthModules = 4; // 8m
                    layout.floorCount = 2;
                    layout.zone = ZoneType.Commercial;

                    // Pavimento 0 (Térreo)
                    FloorLayout sc0 = new FloorLayout { floorIndex = 0 };
                    // Frente (4 slots): I (Garagem = 2 slots) + D (Vitrine = 1 slot) + C (Porta Simples = 1 slot)
                    sc0.frontModules.AddRange(new[] { CityModuleId.I, CityModuleId.D, CityModuleId.C });
                    sc0.frontAwnings.AddRange(new[] { false, true, false }); // Toldo K acima da vitrine D!

                    // Traseira (4 slots): J (Porta Dupla = 1 slot) + A (Parede Lisa = 1 slot) + B (Janela = 1 slot) + A (Parede Lisa = 1 slot)
                    sc0.backModules.AddRange(new[] { CityModuleId.J, CityModuleId.A, CityModuleId.B, CityModuleId.A });

                    // Lateral Esquerda (4 slots): A, B, A, B
                    sc0.leftModules.AddRange(new[] { CityModuleId.A, CityModuleId.B, CityModuleId.A, CityModuleId.B });

                    // Lateral Direita (4 slots): D, D, A, A
                    sc0.rightModules.AddRange(new[] { CityModuleId.D, CityModuleId.D, CityModuleId.A, CityModuleId.A });
                    layout.floors.Add(sc0);

                    // Pavimento 1 (Superior)
                    FloorLayout sc1 = new FloorLayout { floorIndex = 1 };
                    // Frente: G (Veneziana) + H (Floreira) + B (Janela) + A (Parede Lisa)
                    sc1.frontModules.AddRange(new[] { CityModuleId.G, CityModuleId.H, CityModuleId.B, CityModuleId.A });
                    sc1.frontAwnings.AddRange(new[] { false, false, false, false });

                    // Traseira: H (Floreira) + G (Veneziana) + B (Janela) + A (Parede Lisa)
                    sc1.backModules.AddRange(new[] { CityModuleId.H, CityModuleId.G, CityModuleId.B, CityModuleId.A });

                    // Laterais: alternância de janelas G e H com paredes A
                    sc1.leftModules.AddRange(new[] { CityModuleId.G, CityModuleId.A, CityModuleId.H, CityModuleId.A });
                    sc1.rightModules.AddRange(new[] { CityModuleId.H, CityModuleId.A, CityModuleId.G, CityModuleId.A });
                    layout.floors.Add(sc1);

                    // Pilares L nos 4 cantos e Cobertura E + Platibanda F no topo são montados automaticamente!
                    CalculateEntranceAnchors(layout, 3.0f);
                    break;
            }

            return layout;
        }

        private static void CalculateEntranceAnchors(BuildingLayout layout, float doorLocalX)
        {
            Vector3 doorLocal = new Vector3(doorLocalX, 0f, 0f);
            layout.entranceForwardWorld = layout.worldRotation * Vector3.forward;
            layout.entranceWorldPos = layout.worldPosition + layout.worldRotation * doorLocal;
            layout.approachWorldPos = layout.entranceWorldPos + layout.entranceForwardWorld * 1.2f;
            layout.interiorWorldPos = layout.entranceWorldPos - layout.entranceForwardWorld * 1.0f;
            layout.deliveryWorldPos = layout.entranceWorldPos + layout.entranceForwardWorld * 0.4f;
        }

        /// <summary>
        /// Cria um layout procedural completo a partir do perfil configurado e seed determinística.
        /// </summary>
        public static BuildingLayout CreateProceduralLayout(int widthModules, int depthModules, int floorCount,
            ModularBuildingProfile profile, Vector3 worldPos, Quaternion worldRot, int localSeed, string buildingId = null, int blockId = 0)
        {
            BuildingLayout layout = new BuildingLayout();
            layout.recipe = ModularBuildingRecipe.Custom;
            layout.widthModules = widthModules;
            layout.depthModules = depthModules;
            layout.floorCount = floorCount;
            layout.worldPosition = worldPos;
            layout.worldRotation = worldRot;
            layout.localSeed = localSeed;
            layout.blockId = blockId;
            layout.zone = profile != null ? profile.zone : ZoneType.Commercial;
            layout.buildingId = string.IsNullOrEmpty(buildingId) ? $"Bldg_{blockId}_{localSeed}" : buildingId;

            System.Random rng = new System.Random(localSeed);

            // Cor orgânica determinística
            float h = (float)rng.NextDouble();
            float sat = 0.35f + (float)rng.NextDouble() * 0.45f;
            float v = 0.4f + (float)rng.NextDouble() * 0.5f;
            layout.buildingColor = Color.HSVToRGB(h, sat, v);

            // Pavimento Térreo (Floor 0)
            FloorLayout f0 = new FloorLayout { floorIndex = 0 };

            // 1. Reserva slot de entrada (C ou J)
            int doorSlot = rng.Next(0, widthModules);
            CityModuleId doorPiece = (profile != null && rng.NextDouble() < profile.doubleDoorRatio) ? CityModuleId.J : CityModuleId.C;

            // 2. Decide se haverá garagem (I) no térreo: precisa de 2 slots consecutivos e largura >= 4 módulos
            int garageSlot = -1;
            if (profile != null && widthModules >= 4 && rng.NextDouble() < profile.garageWeight)
            {
                // Escolhe um slot para a garagem que não colida com a porta
                List<int> validGarageSlots = new List<int>();
                for (int s = 0; s <= widthModules - 2; s++)
                {
                    if (s != doorSlot && (s + 1) != doorSlot)
                    {
                        validGarageSlots.Add(s);
                    }
                }
                if (validGarageSlots.Count > 0)
                {
                    garageSlot = validGarageSlots[rng.Next(validGarageSlots.Count)];
                }
            }

            // Preenche fachada térrea
            int currentSlot = 0;
            float entranceDoorLocalX = 0f;
            while (currentSlot < widthModules)
            {
                if (currentSlot == garageSlot)
                {
                    f0.frontModules.Add(CityModuleId.I);
                    f0.frontAwnings.Add(false);
                    currentSlot += 2; // Garagem ocupa 2 slots
                }
                else if (currentSlot == doorSlot)
                {
                    f0.frontModules.Add(doorPiece);
                    f0.frontAwnings.Add(false);
                    float halfW = layout.WidthMeters * 0.5f;
                    entranceDoorLocalX = -halfW + (currentSlot * 2.0f) + 1.0f;
                    currentSlot += 1;
                }
                else
                {
                    // Slot decorativo térreo
                    CityModuleId piece;
                    bool hasAwning = false;
                    double roll = rng.NextDouble();

                    if (profile != null && roll < profile.shopfrontWeight)
                    {
                        piece = CityModuleId.D; // Vitrine
                        hasAwning = rng.NextDouble() < profile.awningChance;
                    }
                    else if (profile != null && roll < profile.shopfrontWeight + profile.groundWindowWeight)
                    {
                        piece = CityModuleId.B; // Janela
                    }
                    else
                    {
                        piece = CityModuleId.A; // Parede sólida
                    }

                    f0.frontModules.Add(piece);
                    f0.frontAwnings.Add(hasAwning);
                    currentSlot += 1;
                }
            }

            // Traseira térreo: Paredes lisas A ou porta de serviço C
            for (int s = 0; s < widthModules; s++)
            {
                f0.backModules.Add(s == 1 ? CityModuleId.C : (rng.NextDouble() < 0.5 ? CityModuleId.A : CityModuleId.B));
            }

            // Laterais térreo: A e B
            for (int s = 0; s < depthModules; s++)
            {
                f0.leftModules.Add(rng.NextDouble() < 0.6 ? CityModuleId.A : CityModuleId.B);
                f0.rightModules.Add(rng.NextDouble() < 0.6 ? CityModuleId.A : CityModuleId.B);
            }

            layout.floors.Add(f0);

            // Pavimentos Superiores (Floor 1 até floorCount - 1)
            for (int fl = 1; fl < floorCount; fl++)
            {
                FloorLayout upper = new FloorLayout { floorIndex = fl };

                // Fachada frontal superior: B, G, H, A (NUNCA C, J ou I)
                for (int s = 0; s < widthModules; s++)
                {
                    double roll = rng.NextDouble();
                    CityModuleId piece;
                    if (profile != null && roll < profile.upperSolidWallWeight)
                        piece = CityModuleId.A;
                    else if (profile != null && roll < profile.upperSolidWallWeight + profile.shuttersWeight)
                        piece = CityModuleId.G;
                    else if (profile != null && roll < profile.upperSolidWallWeight + profile.shuttersWeight + profile.planterWeight)
                        piece = CityModuleId.H;
                    else
                        piece = CityModuleId.B;

                    upper.frontModules.Add(piece);
                    upper.frontAwnings.Add(false);
                }

                // Traseira e laterais superiores
                for (int s = 0; s < widthModules; s++)
                {
                    upper.backModules.Add(rng.NextDouble() < 0.4 ? CityModuleId.A : CityModuleId.B);
                }
                for (int s = 0; s < depthModules; s++)
                {
                    upper.leftModules.Add(rng.NextDouble() < 0.5 ? CityModuleId.A : CityModuleId.B);
                    upper.rightModules.Add(rng.NextDouble() < 0.5 ? CityModuleId.A : CityModuleId.B);
                }

                layout.floors.Add(upper);
            }

            // Calcula âncoras de entrada no mundo
            CalculateEntranceAnchors(layout, entranceDoorLocalX);

            return layout;
        }
    }
}
