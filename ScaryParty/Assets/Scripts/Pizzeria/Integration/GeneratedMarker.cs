using System;
using UnityEngine;

namespace ScaryParty.Pizzeria.Integration
{
    [DisallowMultipleComponent]
    public class GeneratedMarker : MonoBehaviour
    {
        [SerializeField] private string _builderName;
        [SerializeField] private string _key;
        [SerializeField] private string _configHash;
        [SerializeField] private string _timestamp;

        public string BuilderName => _builderName;
        public string Key => _key;
        public string ConfigHash => _configHash;
        public string Timestamp => _timestamp;

        public void Mark(string builderName, string key, string configHash = "")
        {
            _builderName = builderName;
            _key = key;
            _configHash = configHash;
            _timestamp = DateTime.UtcNow.ToString("o");
        }
    }
}
