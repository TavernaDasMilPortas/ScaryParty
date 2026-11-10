using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace ScaryParty.City.Modular
{
    /// <summary>
    /// Catálogo central com as 12 peças do kit modular da cidade.
    /// Única fonte de verdade para prefabs, medidas e validação.
    /// </summary>
    [CreateAssetMenu(fileName = "CityModuleCatalog", menuName = "ScaryParty/City/Modular/Module Catalog")]
    public class CityModuleCatalog : ScriptableObject
    {
        [Tooltip("Lista estrita com as 12 definições do kit modular (A até L).")]
        public List<CityModuleDefinition> definitions = new List<CityModuleDefinition>();

        [NonSerialized]
        private Dictionary<CityModuleId, CityModuleDefinition> _lookup;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            _lookup = new Dictionary<CityModuleId, CityModuleDefinition>();
            if (definitions == null) return;

            // Ordena determinísticamente por ID
            definitions.Sort((a, b) => a.id.CompareTo(b.id));

            for (int i = 0; i < definitions.Count; i++)
            {
                var def = definitions[i];
                if (def != null && !_lookup.ContainsKey(def.id))
                {
                    _lookup[def.id] = def;
                }
            }
        }

        public CityModuleDefinition GetDefinition(CityModuleId id)
        {
            if (_lookup == null || _lookup.Count != definitions.Count)
            {
                RebuildLookup();
            }

            if (_lookup != null && _lookup.TryGetValue(id, out var def))
            {
                return def;
            }

            // Fallback busca linear
            for (int i = 0; i < definitions.Count; i++)
            {
                if (definitions[i] != null && definitions[i].id == id)
                    return definitions[i];
            }

            return null;
        }

        public bool TryGetDefinition(CityModuleId id, out CityModuleDefinition def)
        {
            def = GetDefinition(id);
            return def != null;
        }

        /// <summary>
        /// Garante que todos os 12 IDs canônicos (A-L) estejam presentes no catálogo.
        /// </summary>
        public void EnsureAllCanonicalDefinitions()
        {
            RebuildLookup();
            bool changed = false;

            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                if (!_lookup.ContainsKey(id))
                {
                    definitions.Add(CityModuleDefinition.CreateCanonical(id));
                    changed = true;
                }
            }

            definitions.Sort((a, b) => a.id.CompareTo(b.id));
            RebuildLookup();

#if UNITY_EDITOR
            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// Valida integridade do catálogo: duplicatas, ausências, prefabs nulos e dimensões.
        /// </summary>
        public bool ValidateCatalog(out List<string> errors)
        {
            errors = new List<string>();
            RebuildLookup();

            var seen = new HashSet<CityModuleId>();
            foreach (var def in definitions)
            {
                if (def == null)
                {
                    errors.Add("Definição nula encontrada no catálogo.");
                    continue;
                }

                if (seen.Contains(def.id))
                {
                    errors.Add($"Definição duplicada para o módulo {def.id}.");
                }
                seen.Add(def.id);

                if (def.prefab == null)
                {
                    errors.Add($"Módulo {def.id} ({def.displayName}) está sem prefab atribuído.");
                }

                if (def.width <= 0f || def.height <= 0f || def.depth <= 0f)
                {
                    errors.Add($"Módulo {def.id} possui dimensões inválidas: {def.width}x{def.height}x{def.depth}.");
                }

                if (def.id == CityModuleId.I && def.occupiedSlots != 2)
                {
                    errors.Add($"Módulo de garagem (I) DEVE ocupar exatamente 2 slots (4 metros). Valor atual: {def.occupiedSlots}.");
                }

                if (def.id != CityModuleId.I && def.id != CityModuleId.L && def.occupiedSlots != 1)
                {
                    errors.Add($"Módulo {def.id} deve ocupar 1 slot padrão (2 metros). Valor atual: {def.occupiedSlots}.");
                }
            }

            foreach (CityModuleId id in Enum.GetValues(typeof(CityModuleId)))
            {
                if (!seen.Contains(id))
                {
                    errors.Add($"Módulo obrigatório {id} está ausente do catálogo.");
                }
            }

            return errors.Count == 0;
        }

        /// <summary>
        /// Gera uma assinatura hash determinística do catálogo para validação entre host e cliente.
        /// </summary>
        public string GetCatalogSignature()
        {
            StringBuilder sb = new StringBuilder();
            definitions.Sort((a, b) => a.id.CompareTo(b.id));

            for (int i = 0; i < definitions.Count; i++)
            {
                var d = definitions[i];
                if (d == null) continue;
                sb.Append($"{d.id}:{d.width:F2}:{d.height:F2}:{d.depth:F2}:{d.occupiedSlots};");
            }

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }
    }
}
