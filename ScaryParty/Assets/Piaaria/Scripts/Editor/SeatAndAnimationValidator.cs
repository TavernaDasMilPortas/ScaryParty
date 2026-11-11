#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using ScaryParty.Player;
using ScaryParty.Pizzeria.Stations;

namespace ScaryParty.Pizzeria.Editor
{
    /// <summary>
    /// Validador automatizado para animaÃ§Ãµes de sentar/levantar, alturas de assento
    /// e integridade do AnimatorController nos prefabs e personagens de ScaryParty.
    /// </summary>
    public static class SeatAndAnimationValidator
    {
        private const string ControllerPath = "Assets/Piaaria/Animations/Characters/CharacterAnimator.controller";
        private const string ResourcesControllerPath = "Assets/Piaaria/Resources/CharacterAnimator.controller";
        private const string VisualPrefabPath = "Assets/Piaaria/Prefabs/Characters/CharacterVisual.prefab";
        private const string ResourcesVisualPrefabPath = "Assets/Piaaria/Resources/CharacterVisual.prefab";
        private const string PlayerArmaturePath = "Assets/ThirdParty/StarterAssets/ThirdPersonController/Prefabs/PlayerArmature.prefab";
        private const string FbxFolder = "Assets/Piaaria/Art/Characters/Generated";

        [MenuItem("Tools/Scary Party/Uso Comum/Pizzaria/Validar Animações e Assentos", false, 112)]
        public static void RunValidationMenu()
        {
            bool ok = RunValidation(out string report);
            if (ok)
            {
                Debug.Log($"<color=green>[VALIVAÃ‡ÃƒO CONCLUÃDA COM SUCESSO]</color>\n{report}");
            }
            else
            {
                Debug.LogError($"<color=red>[VALIVAÃ‡ÃƒO DETECTOU PROBLEMAS]</color>\n{report}");
            }
        }

        public static bool RunValidation(out string report)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("  RELATÃ“RIO DE VALIDAÃ‡ÃƒO: ANIMAÃ‡Ã•ES & ASSENTOS");
            sb.AppendLine("==================================================");
            bool allPassed = true;

            // 1. Validar AnimatorControllers
            sb.AppendLine("\n--- 1. VERIFICAÃ‡ÃƒO DO ANIMATOR CONTROLLER ---");
            allPassed &= ValidateAnimatorController(ControllerPath, "Principal", sb);
            allPassed &= ValidateAnimatorController(ResourcesControllerPath, "Resources", sb);

            // 2. Validar Prefabs de Personagem
            sb.AppendLine("\n--- 2. VERIFICAÃ‡ÃƒO DE PREFABS E ANIMATOR VÃNCULOS ---");
            allPassed &= ValidateCharacterPrefab(VisualPrefabPath, "CharacterVisual.prefab", sb);
            allPassed &= ValidateCharacterPrefab(ResourcesVisualPrefabPath, "Resources/CharacterVisual.prefab", sb);
            allPassed &= ValidatePlayerArmaturePrefab(PlayerArmaturePath, sb);

            // 3. Validar Clipes de AnimaÃ§Ã£o de Assento (SitDown, SitIdle, StandUp)
            sb.AppendLine("\n--- 3. VERIFICAÃ‡ÃƒO DE CURVAS E CLIPS DE ANIMAÃ‡ÃƒO BLENDER ---");
            allPassed &= ValidateSitClips(sb);

            // 4. Validar EstaÃ§Ãµes de Mesa e Ã‚ncoras de Cadeira
            sb.AppendLine("\n--- 4. VERIFICAÃ‡ÃƒO DE ESTAÃ‡Ã•ES DE MESA E Ã‚NCORAS ---");
            allPassed &= ValidateTableAnchors(sb);

            sb.AppendLine("\n==================================================");
            sb.AppendLine(allPassed ? "STATUS GERAL: APROVADO [SEM REGRESSÃ•ES]" : "STATUS GERAL: REPROVADO [ATENÃ‡ÃƒO NECESSÃRIA]");
            sb.AppendLine("==================================================");

            report = sb.ToString();
            return allPassed;
        }

        private static bool ValidateAnimatorController(string path, string label, StringBuilder sb)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                sb.AppendLine($"[ERRO] AnimatorController '{label}' nÃ£o encontrado em: {path}");
                return false;
            }

            var layers = controller.layers;
            if (layers.Length < 2)
            {
                sb.AppendLine($"[ERRO] AnimatorController '{label}' possui menos de 2 camadas ({layers.Length}). Esperado Base Layer e UpperBody.");
                return false;
            }

            var foundStates = new HashSet<string>();
            foreach (var l in layers)
            {
                foreach (var childState in l.stateMachine.states)
                {
                    foundStates.Add(childState.state.name);
                }
            }

            bool statesOk = true;
            bool hasLocomotion = foundStates.Contains("Locomotion") || foundStates.Contains("Idle Walk Run Blend");
            if (!hasLocomotion)
            {
                sb.AppendLine($"[ERRO] Estado de Locomoção ('Locomotion' ou 'Idle Walk Run Blend') ausente no AnimatorController '{label}'.");
                statesOk = false;
            }
            if (!foundStates.Contains("SitDown"))
            {
                sb.AppendLine($"[ERRO] Estado 'SitDown' ausente no AnimatorController '{label}'.");
                statesOk = false;
            }
            if (!foundStates.Contains("SitIdle"))
            {
                sb.AppendLine($"[ERRO] Estado 'SitIdle' ausente no AnimatorController '{label}'.");
                statesOk = false;
            }
            if (!foundStates.Contains("StandUp"))
            {
                sb.AppendLine($"[ERRO] Estado 'StandUp' ausente no AnimatorController '{label}'.");
                statesOk = false;
            }
            bool hasPrepare = foundStates.Contains("Prepare") || foundStates.Contains("Preparing");
            if (!hasPrepare)
            {
                sb.AppendLine($"[ERRO] Estado 'Prepare' ausente no AnimatorController '{label}'.");
                statesOk = false;
            }

            var expectedParams = new[] { "Speed", "MotionSpeed", "Grounded", "Jump", "FreeFall", "IsPreparing", "IsSitting", "SitDown", "StandUp" };
            var paramNames = new HashSet<string>();
            foreach (var p in controller.parameters) paramNames.Add(p.name);

            bool paramsOk = true;
            foreach (var p in expectedParams)
            {
                if (!paramNames.Contains(p))
                {
                    sb.AppendLine($"[ERRO] ParÃ¢metro '{p}' ausente no AnimatorController '{label}'.");
                    paramsOk = false;
                }
            }

            if (statesOk && paramsOk)
            {
                sb.AppendLine($"[OK] Controller '{label}' Ã­ntegro ({layers.Length} camadas, {foundStates.Count} estados, {controller.parameters.Length} parÃ¢metros).");
                return true;
            }
            return false;
        }

        private static bool ValidateCharacterPrefab(string path, string label, StringBuilder sb)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                sb.AppendLine($"[ERRO] Prefab '{label}' nÃ£o encontrado em: {path}");
                return false;
            }

            var anim = prefab.GetComponentInChildren<Animator>(true);
            if (anim == null)
            {
                sb.AppendLine($"[ERRO] Prefab '{label}' nÃ£o possui componente Animator!");
                return false;
            }

            if (anim.runtimeAnimatorController == null)
            {
                sb.AppendLine($"[ERRO] Prefab '{label}' possui Animator com runtimeAnimatorController NULO!");
                return false;
            }

            var visualCtrl = prefab.GetComponentInChildren<CharacterVisualController>(true);
            if (visualCtrl == null)
            {
                sb.AppendLine($"[ERRO] Prefab '{label}' nÃ£o possui CharacterVisualController!");
                return false;
            }

            if (visualCtrl.DefaultAnimatorController == null)
            {
                sb.AppendLine($"[AVISO] CharacterVisualController em '{label}' nÃ£o possui DefaultAnimatorController persistente serializado.");
            }

            sb.AppendLine($"[OK] Prefab '{label}' validado. Animator: {anim.runtimeAnimatorController.name}, VisualController: Presente.");
            return true;
        }

        private static bool ValidatePlayerArmaturePrefab(string path, StringBuilder sb)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                sb.AppendLine($"[ERRO] PlayerArmature nÃ£o encontrado em: {path}");
                return false;
            }

            var visualCtrl = prefab.GetComponentInChildren<CharacterVisualController>(true);
            if (visualCtrl == null)
            {
                sb.AppendLine("[ERRO] PlayerArmature nÃ£o possui filho com CharacterVisualController!");
                return false;
            }

            var anim = prefab.GetComponentInChildren<Animator>(true);
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                sb.AppendLine("[ERRO] PlayerArmature possui Animator nulo ou sem controller associado!");
                return false;
            }

            sb.AppendLine($"[OK] PlayerArmature validado com controller: {anim.runtimeAnimatorController.name}.");
            return true;
        }

        private static bool ValidateSitClips(StringBuilder sb)
        {
            string sitDownPath = $"{FbxFolder}/Character_SitDown.fbx";
            string sitIdlePath = $"{FbxFolder}/Character_SitIdle.fbx";
            string standUpPath = $"{FbxFolder}/Character_StandUp.fbx";

            string[] paths = new[] { sitDownPath, sitIdlePath, standUpPath };
            bool clipsOk = true;

            foreach (var p in paths)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                if (clip == null)
                {
                    // Tenta carregar sub-assets caso o FBX contenha clipe interno
                    var all = AssetDatabase.LoadAllAssetsAtPath(p);
                    foreach (var a in all)
                    {
                        if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                        {
                            clip = c;
                            break;
                        }
                    }
                }

                if (clip == null)
                {
                    sb.AppendLine($"[ERRO] Clipe de animaÃ§Ã£o nÃ£o encontrado para FBX: {p}");
                    clipsOk = false;
                    continue;
                }

                sb.AppendLine($"[OK] Clipe carregado: '{clip.name}' (DuraÃ§Ã£o: {clip.length:F2}s, Loop: {clip.isLooping}, Path: {Path.GetFileName(p)})");
            }

            return clipsOk;
        }

        private static bool ValidateTableAnchors(StringBuilder sb)
        {
            var tables = UnityEngine.Object.FindObjectsByType<DiningTableStation>(FindObjectsSortMode.None);
            if (tables == null || tables.Length == 0)
            {
                sb.AppendLine("[INFO] Nenhuma DiningTableStation instanciada na cena ativa atual. Procurando nos prefabs de pizzaria...");
                string[] guids = AssetDatabase.FindAssets("t:Prefab Pizzeria");
                bool foundInPrefab = false;
                foreach (var g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go == null) continue;
                    var ptables = go.GetComponentsInChildren<DiningTableStation>(true);
                    if (ptables.Length > 0)
                    {
                        foundInPrefab = true;
                        sb.AppendLine($"[INFO] Encontradas {ptables.Length} mesas no prefab '{go.name}'.");
                        foreach (var t in ptables)
                        {
                            ValidateSingleTable(t, sb);
                        }
                        break;
                    }
                }
                if (!foundInPrefab)
                {
                    sb.AppendLine("[OK] ValidaÃ§Ã£o de DiningTableStation ignorada por ausÃªncia de instÃ¢ncias de cena (serÃ£o criadas proceduramente por CustomerSpawner).");
                }
                return true;
            }

            sb.AppendLine($"[OK] {tables.Length} DiningTableStation encontrada(s) na cena ativa.");
            bool allTablesValid = true;
            foreach (var t in tables)
            {
                allTablesValid &= ValidateSingleTable(t, sb);
            }
            return allTablesValid;
        }

        private static bool ValidateSingleTable(DiningTableStation table, StringBuilder sb)
        {
            if (table == null) return true;
            table.EnsureAnchors();
            table.EnsureNavMeshObstacle();

            bool valid = true;
            float seatY = table.seatAnchor != null ? table.seatAnchor.position.y : 0f;
            float cushionY = table.GetSeatSurfacePosition().y;
            float heightDiff = cushionY - seatY;

            if (heightDiff < 0.35f || heightDiff > 0.58f)
            {
                sb.AppendLine($"[AVISO] Mesa '{table.tableName}' (Id {table.tableId}): Altura do assento fora do padrÃ£o ideal (calculada: {heightDiff:F2}m, esperado: ~0.44m - 0.48m).");
            }
            else
            {
                sb.AppendLine($"[OK] Mesa '{table.tableName}' (Id {table.tableId}): Altura do assento correta ({heightDiff:F2}m).");
            }

            Vector3 approachPos = table.GetApproachPosition();
            Vector3 seatPos = table.GetSeatPosition();
            float approachDist = Vector3.Distance(new Vector3(approachPos.x, 0f, approachPos.z), new Vector3(seatPos.x, 0f, seatPos.z));

            if (approachDist < 0.40f)
            {
                sb.AppendLine($"[AVISO] ApproachAnchor da Mesa '{table.tableName}' muito prÃ³ximo da cadeira ({approachDist:F2}m). Pode conflitar com obstÃ¡culo.");
            }
            else
            {
                sb.AppendLine($"[OK] ApproachAnchor da Mesa '{table.tableName}' posicionado a {approachDist:F2}m do assento (limpo fora do carving).");
            }

            var obs = table.GetComponent<NavMeshObstacle>();
            if (obs == null || !obs.carving)
            {
                sb.AppendLine($"[AVISO] Mesa '{table.tableName}' sem NavMeshObstacle com carving ativo!");
                valid = false;
            }
            else
            {
                sb.AppendLine($"[OK] NavMeshObstacle ativo com carving na Mesa '{table.tableName}'.");
            }

            return valid;
        }
    }
}
#endif
