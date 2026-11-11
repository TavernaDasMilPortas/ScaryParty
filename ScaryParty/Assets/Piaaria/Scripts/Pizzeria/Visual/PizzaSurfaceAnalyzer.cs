using System;
using UnityEngine;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Métricas anatômicas da superfície da massa da pizza calculadas dinamicamente a partir da malha 3D real.
    /// Permite trocar o placeholder da massa por qualquer modelo 3D final sem necessidade de recalibrar posições manuais.
    /// </summary>
    public struct PizzaSurfaceMetrics
    {
        /// <summary>Raio máximo da borda externa da massa em metros (world/scaled).</summary>
        public float OuterRadius;

        /// <summary>Raio útil do reservatório interno onde molho, queijo e fatias repousam.</summary>
        public float UsableRadius;

        /// <summary>Altura Y da base central onde a camada de molho se apoia.</summary>
        public float FloorY;

        /// <summary>Altura Y do topo da crista do cornicione (borda elevada).</summary>
        public float CrestY;

        /// <summary>Raio radial da crista mais alta da borda.</summary>
        public float CrestRadius;

        /// <summary>Diâmetro total da pizza em centímetros.</summary>
        public float DiameterCm => OuterRadius * 200f;

        public static PizzaSurfaceMetrics Default => new PizzaSurfaceMetrics
        {
            OuterRadius = 0.182f,
            UsableRadius = 0.145f,
            FloorY = 0.009f,
            CrestY = 0.024f,
            CrestRadius = 0.180f
        };
    }

    /// <summary>
    /// Analisador dinâmico de geometria da massa de pizza.
    /// Inspeciona os vértices da malha real para extrair o leito interno e o cornicione automaticamente.
    /// </summary>
    public static class PizzaSurfaceAnalyzer
    {
        public static PizzaSurfaceMetrics Analyze(GameObject pizzaGo)
        {
            if (pizzaGo == null) return PizzaSurfaceMetrics.Default;

            // Tentar localizar a malha da massa
            MeshFilter mf = null;
            Transform baseDough = pizzaGo.transform.Find("IngredientLayerAnchor/ModularPizzaRoot/DoughRoot/BaseDough")
                               ?? pizzaGo.transform.Find("DoughRoot/BaseDough")
                               ?? pizzaGo.transform.Find("BaseDough");

            if (baseDough != null)
            {
                mf = baseDough.GetComponent<MeshFilter>();
            }

            if (mf == null)
            {
                // Buscar qualquer MeshFilter que não seja molho ou toppings
                var filters = pizzaGo.GetComponentsInChildren<MeshFilter>(true);
                for (int i = 0; i < filters.Length; i++)
                {
                    string name = filters[i].gameObject.name.ToLowerInvariant();
                    if (name.Contains("dough") || name.Contains("massa") || name.Contains("crust"))
                    {
                        mf = filters[i];
                        break;
                    }
                }
                if (mf == null && filters.Length > 0)
                {
                    mf = filters[0];
                }
            }

            if (mf == null || mf.sharedMesh == null)
            {
                return PizzaSurfaceMetrics.Default;
            }

            Vector3 worldScale = mf.transform.lossyScale;
            return AnalyzeMesh(mf.sharedMesh, worldScale);
        }

        public static PizzaSurfaceMetrics AnalyzeMesh(Mesh mesh, Vector3 scale)
        {
            if (mesh == null) return PizzaSurfaceMetrics.Default;

            Vector3[] vertices = mesh.vertices;
            if (vertices == null || vertices.Length == 0) return PizzaSurfaceMetrics.Default;

            float scaleX = Mathf.Abs(scale.x > 0.001f ? scale.x : 1f);
            float scaleY = Mathf.Abs(scale.y > 0.001f ? scale.y : 1f);
            float scaleZ = Mathf.Abs(scale.z > 0.001f ? scale.z : 1f);
            float horizScale = (scaleX + scaleZ) * 0.5f;

            float maxRadius = 0f;
            float maxCrestY = -9999f;
            float crestRadius = 0f;

            float centerFloorYSum = 0f;
            int centerFloorCount = 0;

            // Primeiro passe: determinar raio externo e altura máxima
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z) * horizScale;
                float y = v.y * scaleY;

                if (r > maxRadius) maxRadius = r;
                if (y > maxCrestY)
                {
                    maxCrestY = y;
                    crestRadius = r;
                }
            }

            if (maxRadius < 0.02f) return PizzaSurfaceMetrics.Default;

            // Segundo passe: calcular a altura da face central (r <= 35% do raio máximo)
            float centralZone = maxRadius * 0.35f;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                float r = Mathf.Sqrt(v.x * v.x + v.z * v.z) * horizScale;
                float y = v.y * scaleY;

                // Considerar apenas a face superior (y >= média de Y)
                if (r <= centralZone && y >= 0f)
                {
                    centerFloorYSum += y;
                    centerFloorCount++;
                }
            }

            float floorY = centerFloorCount > 0 ? (centerFloorYSum / centerFloorCount) : 0.009f;

            // Calcular o raio útil do reservatório interno:
            // Onde a bacia plana termina antes de começar a rampa de subida da borda (cornicione)
            float crestCandidate = crestRadius > centralZone ? crestRadius : (maxRadius * 0.92f);
            float usableRadius = Mathf.Clamp(crestCandidate * 0.84f, maxRadius * 0.70f, maxRadius - 0.018f);

            return new PizzaSurfaceMetrics
            {
                OuterRadius = maxRadius,
                UsableRadius = usableRadius,
                FloorY = floorY,
                CrestY = maxCrestY,
                CrestRadius = crestCandidate
            };
        }
    }
}
