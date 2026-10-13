using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class ProcessDurationOverride
    {
        public ProcessDef process;
        public int processId;
        public float durationInSeconds;

        public int EffectiveProcessId => process != null ? process.id : processId;
    }

    [CreateAssetMenu(fileName = "Station_", menuName = "Scary Party/Pizzeria/Station Def")]
    public class StationDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável da estação")]
        public int id;
        public string stationName;
        [Tooltip("Contagem padrão de slots de trabalho/espera da bancada")]
        public int slotCount = 1;
        [Tooltip("Processos aceitos nesta bancada")]
        public List<ProcessDef> acceptedProcesses = new List<ProcessDef>();
        public List<int> acceptedProcessIds = new List<int>();
        [Tooltip("Overrides padrão de duração para processos específicos nesta bancada")]
        public List<ProcessDurationOverride> durationOverrides = new List<ProcessDurationOverride>();

        public List<int> GetEffectiveProcessIds()
        {
            var result = new List<int>();
            if (acceptedProcesses != null)
            {
                foreach (var p in acceptedProcesses)
                {
                    if (p != null && !result.Contains(p.id))
                        result.Add(p.id);
                }
            }
            if (acceptedProcessIds != null)
            {
                foreach (var id in acceptedProcessIds)
                {
                    if (id > 0 && !result.Contains(id))
                        result.Add(id);
                }
            }
            return result;
        }
    }
}
