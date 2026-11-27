using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Piaaria.TrailerStudio.Editor
{
    public static class TrailerStudioValidator
    {
        [MenuItem("Tools/Scary Party/Uso Comum/Trailer Studio/Validar Cena", false, 193)]
        public static void ValidateScene()
        {
            TrailerDirector director = Object.FindFirstObjectByType<TrailerDirector>();
            if (director == null)
            {
                Debug.LogError("[TrailerStudioValidator] TrailerDirector não encontrado na cena atual.");
                return;
            }

            if (director.profile == null)
            {
                Debug.LogError("[TrailerStudioValidator] TrailerStudioProfile não atribuído no TrailerDirector.");
                return;
            }

            director.profile.EnsureRuntimeDefaults();

            int errors = 0;
            int warnings = 0;
            var seenIds = new HashSet<TrailerTakeId>();

            if (director.profile.pizzeriaPrefab == null) { Debug.LogError("[TrailerStudioValidator] pizzeriaPrefab ausente."); errors++; }
            if (director.profile.characterVisualPrefab == null) { Debug.LogError("[TrailerStudioValidator] characterVisualPrefab ausente."); errors++; }
            if (director.profile.motorcyclePrefab == null) { Debug.LogError("[TrailerStudioValidator] motorcyclePrefab ausente."); errors++; }
            if (director.profile.cityConfig == null) { Debug.LogError("[TrailerStudioValidator] cityConfig ausente."); errors++; }

            foreach (var def in director.profile.takes)
            {
                if (def == null) continue;
                if (!seenIds.Add(def.id))
                {
                    Debug.LogError($"[TrailerStudioValidator] ID duplicado: {def.id}");
                    errors++;
                }
                if (def.timelineAsset == null)
                {
                    Debug.LogError($"[TrailerStudioValidator] {def.id}: TimelineAsset ausente.");
                    errors++;
                }
                if (!Mathf.Approximately(def.preRoll, 0.75f) || !Mathf.Approximately(def.postRoll, 0.75f))
                {
                    Debug.LogWarning($"[TrailerStudioValidator] {def.id}: Margens recomendadas 0.75s/0.75s (atual {def.preRoll}/{def.postRoll}).");
                    warnings++;
                }
                if (def.cameraAStartOffset == Vector3.zero)
                {
                    Debug.LogWarning($"[TrailerStudioValidator] {def.id}: cameraAStartOffset está zerado.");
                    warnings++;
                }
                if (def.cues == null || def.cues.Count == 0)
                {
                    Debug.LogError($"[TrailerStudioValidator] {def.id}: Nenhum cue configurado.");
                    errors++;
                }
                else
                {
                    for (int c = 0; c < def.cues.Count; c++)
                    {
                        if (def.cues[c].time < 0f || def.cues[c].time > def.actionDuration + 0.001f)
                        {
                            Debug.LogError($"[TrailerStudioValidator] {def.id}: Cue[{c}] tempo {def.cues[c].time}s fora de [0, {def.actionDuration}].");
                            errors++;
                        }
                    }
                }
            }

            if (director.profile.takes.Count != 15)
            {
                Debug.LogWarning($"[TrailerStudioValidator] Esperados 15 takes, encontrados {director.profile.takes.Count}.");
                warnings++;
            }

            if (errors == 0)
                Debug.Log($"[TrailerStudioValidator] Validação concluída: 0 erros, {warnings} aviso(s).");
            else
                Debug.LogError($"[TrailerStudioValidator] Validação falhou com {errors} erro(s) e {warnings} aviso(s).");
        }
    }
}
