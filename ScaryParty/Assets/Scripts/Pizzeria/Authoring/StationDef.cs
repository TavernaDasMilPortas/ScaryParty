using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Authoring
{
    [Serializable]
    public class ProcessDurationOverride
    {
        public int processId;
        public float durationInSeconds;
    }

    [CreateAssetMenu(fileName = "Station_", menuName = "Scary Party/Pizzeria/Station Def")]
    public class StationDef : ScriptableObject
    {
        [Tooltip("ID numérico único e estável da estação")]
        public int id;
        public string stationName;
        [Tooltip("Contagem de slots de trabalho/espera da bancada")]
        public int slotCount = 1;
        [Tooltip("IDs dos ProcessDef aceitos nesta bancada")]
        public List<int> acceptedProcessIds = new List<int>();
        [Tooltip("Overrides locais de duração para processos específicos nesta bancada")]
        public List<ProcessDurationOverride> durationOverrides = new List<ProcessDurationOverride>();
    }
}
