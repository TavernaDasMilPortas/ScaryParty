using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Piaaria.TrailerStudio.Editor
{
    public static class TrailerStudioValidator
    {
        [MenuItem("Tools/P.I.A.R.I.A/Trailer Studio/Validate Scene", priority = 101)]
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

            int errors = 0;
            var seenIds = new HashSet<TrailerTakeId>();
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
            }

            if (director.profile.takes.Count != 15)
            {
                Debug.LogWarning($"[TrailerStudioValidator] Esperados 15 takes, encontrados {director.profile.takes.Count}.");
            }

            if (errors == 0)
                Debug.Log("[TrailerStudioValidator] Validação concluída sem erros críticos.");
            else
                Debug.LogError($"[TrailerStudioValidator] Validação falhou com {errors} erro(s).");
        }
    }
}
