using System.Collections.Generic;
using UnityEngine;

namespace Piaaria.TrailerStudio
{
    [CreateAssetMenu(fileName = "TrailerStudioProfile", menuName = "P.I.A.R.I.A/Trailer Studio Profile")]
    public class TrailerStudioProfile : ScriptableObject
    {
        [Header("Environment & Seed")]
        public int seed = 20261125;
        public float defaultLightingHour = 19f;
        public CityConfig cityConfig;
        public GameObject pizzeriaPrefab;

        [Header("Cast & Vehicles")]
        public GameObject characterVisualPrefab;
        public GameObject motorcyclePrefab;

        [Header("Props")]
        public GameObject doughPrefab;
        public GameObject pizzaWipPrefab;
        public GameObject pizzaBoxPrefab;
        public GameObject knifePrefab;
        public GameObject calabresaPrefab;
        public GameObject cheesePrefab;
        public Color[] actorColors = new Color[]
        {
            new Color(0.20f, 0.55f, 0.95f, 1f), // A: Azul
            new Color(0.92f, 0.25f, 0.25f, 1f), // B: Vermelho
            new Color(0.22f, 0.78f, 0.38f, 1f), // C: Verde
            new Color(0.95f, 0.72f, 0.18f, 1f)  // D: Amarelo/Laranja
        };

        [Header("Takes (T01 - T15)")]
        public List<TrailerTakeDefinition> takes = new List<TrailerTakeDefinition>();

        public TrailerTakeDefinition GetTake(TrailerTakeId id)
        {
            for (int i = 0; i < takes.Count; i++)
            {
                if (takes[i] != null && takes[i].id == id)
                    return takes[i];
            }
            return null;
        }
    }
}
