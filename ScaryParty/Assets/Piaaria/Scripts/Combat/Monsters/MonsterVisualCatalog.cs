using System;
using System.Collections.Generic;
using ScaryParty.Pizzeria.Domain.Services;
using UnityEngine;

namespace ScaryParty.Combat.Monsters
{
    [Serializable]
    public class MonsterSpeciesVisualEntry
    {
        public NpcSpecies species;
        public GameObject visualPrefab;
        public Color baseTint = Color.white;
        public Color aggressiveTint = new Color(0.95f, 0.25f, 0.25f);
        public Vector3 visualLocalScale = Vector3.one;
        public float visualYOffset = 0f;
        public float colliderHeight = 1.8f;
        public float colliderRadius = 0.38f;
    }

    /// <summary>
    /// Catálogo persistente de visuais provisórios de monstros por espécie (P5).
    /// Utilizado pelo spawn real servidor e pela criação de proxies nos clientes,
    /// sem exibir nomes/labels reveladores de espécie no modo normal.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterVisualCatalog", menuName = "Scary Party/Playtest/Monster Visual Catalog")]
    public class MonsterVisualCatalog : ScriptableObject
    {
        public List<MonsterSpeciesVisualEntry> entries = new List<MonsterSpeciesVisualEntry>();

        public bool TryGetEntry(NpcSpecies species, out MonsterSpeciesVisualEntry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (entries[i] != null && entries[i].species == species)
                    {
                        entry = entries[i];
                        return true;
                    }
                }
            }
            entry = null;
            return false;
        }
    }
}
