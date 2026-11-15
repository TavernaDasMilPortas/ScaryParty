using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Lighting
{
    [Serializable]
    public class InteriorZonePreset
    {
        public PizzeriaZoneId zoneId = PizzeriaZoneId.Kitchen;
        public Vector3 localCenter;
        public Vector3 localSize = new Vector3(12f, 5f, 12f);
        public Color fillColor = new Color(1f, 0.92f, 0.78f, 1f);
        [Range(0f, 1f)] public float fillWeight = 0.24f;
        public Color localPointLightTint = new Color(1f, 0.90f, 0.74f, 1f);
        [Range(0.2f, 2.5f)] public float intensityMultiplier = 1.0f;

        public Bounds ComputeWorldBounds(Transform root)
        {
            if (root == null) return new Bounds(localCenter, localSize);
            Vector3 worldCenter = root.TransformPoint(localCenter);
            return new Bounds(worldCenter, localSize);
        }
    }

    [Serializable]
    public class ZonePrecomputedSampleB2
    {
        public PizzeriaZoneId zoneId;
        public Color averageBounceColor = new Color(0.95f, 0.88f, 0.78f, 1f);
        public float bounceStrength = 0.22f;
    }

    /// <summary>
    /// Contrato B2 para futuros dados pre-calculados de ambientes fixos (ex.: pizzaria).
    /// Valida spaceId, assinatura de layout e rotacao; se houver divergencia, cai para A6 em tempo real.
    /// </summary>
    [CreateAssetMenu(fileName = "FixedInteriorLightingData", menuName = "Scary Party/Lighting/Fixed Interior Data (B2)")]
    public class FixedInteriorLightingData : ScriptableObject
    {
        public string spaceId = "pizzeria_modular_manual";
        public string layoutFingerprint = "L_shape_kitchen_counter_dining_v1";
        public Quaternion expectedRootRotation = Quaternion.identity;
        public float maxRotationDeviationDegrees = 2.5f;
        public bool isBakedDataValid = false;
        public List<ZonePrecomputedSampleB2> bakedZones = new List<ZonePrecomputedSampleB2>();

        public bool ValidateAgainstLayout(string runtimeFingerprint, Transform pizzeriaTransform, out string reason)
        {
            if (!isBakedDataValid || bakedZones == null || bakedZones.Count == 0)
            {
                reason = "Sem dados B2 assados/validados; utilizando preenchimento estilizado em tempo real (A6).";
                return false;
            }

            if (!string.Equals(layoutFingerprint, runtimeFingerprint, StringComparison.Ordinal))
            {
                reason = $"Fingerprint de layout divergente ('{runtimeFingerprint}' != '{layoutFingerprint}'); invalidando B2 com fallback para A6.";
                return false;
            }

            if (pizzeriaTransform != null && Quaternion.Angle(expectedRootRotation, pizzeriaTransform.rotation) > maxRotationDeviationDegrees)
            {
                reason = "Rotacao da pizzaria divergiu da orientacao esperada pelo dado B2; aplicando fallback seguro para A6.";
                return false;
            }

            reason = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Marca uma zona interna opcional na cena/prefab para sobrescrever cor e peso de preenchimento de sombra.
    /// </summary>
    public class InteriorLightingZone : MonoBehaviour
    {
        public PizzeriaZoneId zoneId = PizzeriaZoneId.Kitchen;
        public Vector3 localCenter = Vector3.zero;
        public Vector3 localSize = new Vector3(10f, 4.5f, 10f);
        public Color fillColor = new Color(1f, 0.93f, 0.80f, 1f);
        [Range(0f, 1f)] public float fillWeight = 0.24f;

        public Bounds WorldBounds => new Bounds(transform.TransformPoint(localCenter), localSize);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.35f);
            Gizmos.DrawWireCube(transform.TransformPoint(localCenter), localSize);
        }
    }
}
