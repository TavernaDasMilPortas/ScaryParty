using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Composition
{
    /// <summary>
    /// Metadados explícitos da área construída, orientação da fachada, acessos e planta em L da Pizzaria.
    /// Substitui a dependência frágil de busca exclusiva por um objeto chamado "Floor".
    /// </summary>
    [DisallowMultipleComponent]
    public class PizzeriaFootprint : MonoBehaviour
    {
        [Header("Dimensões Totais da Área Construída")]
        [Tooltip("Bounds conservadores do prédio no espaço local da raiz da pizzaria.")]
        public Bounds localBounds = new Bounds(new Vector3(16.5f, 1.5f, -9.2f), new Vector3(34.5f, 6.0f, 38.5f));

        [Tooltip("Largura (X) e Profundidade (Z) totais da área ocupada.")]
        public Vector2 footprintSize = new Vector2(34.5f, 38.5f);

        [Tooltip("Centro (X, Z) do footprint no espaço local da raiz da pizzaria.")]
        public Vector2 footprintCenter = new Vector2(16.5f, -9.2f);

        [Header("Nível de Piso e Apoio")]
        [Tooltip("Altura Y local da superfície caminhável do piso. No prefab modular manual: -1.6m.")]
        public float floorSurfaceLocalY = -1.6f;

        [Tooltip("Âncora explícita de nível térreo ou piso (opcional).")]
        public Transform groundAnchor;

        [Header("Fachada e Acesso")]
        [Tooltip("Direção normalizada para a qual a fachada de entrada aponta no espaço local (para a rua). Ex: (-1, 0) aponta para -X.")]
        public Vector2 entranceDirectionLocal = new Vector2(-1f, 0f);

        [Header("Planta em L (Sub-retângulos de Piso e Ocupação)")]
        [Tooltip("Salão de Atendimento / Entrada (X: 0 a 32.8m, Z: -13.2 a 9.2m)")]
        public Rect diningHallRect = Rect.MinMaxRect(0f, -13.2f, 32.8f, 9.2f);

        [Tooltip("Cozinha e Área Operacional (X: 16.4 a 32.8m, Z: -27.6 a -13.2m)")]
        public Rect kitchenRect = Rect.MinMaxRect(16.4f, -27.6f, 32.8f, -13.2f);

        [Tooltip("Regiões estruturais explícitas. Se preenchido, tem prioridade sobre diningHallRect e kitchenRect.")]
        public Rect[] structuralRegions;

        [Header("Envelope de Saliências e Cobertura")]
        [Tooltip("Envelope 2D XZ de beirais e saliências no espaço local.")]
        public Rect overhangEnvelope = Rect.MinMaxRect(-0.85f, -28.45f, 33.65f, 10.05f);

        [Header("Corredores de Acesso Livres")]
        [Tooltip("Corredores que devem permanecer desobstruídos por outros prédios (ex: calçada em frente à entrada).")]
        public Rect[] accessCorridors;

        [Header("Âncoras de Acesso")]
        public Transform entranceAnchor;
        public Transform receivingAnchor;

        /// <summary>
        /// Retorna a lista de regiões estruturais locais (retângulos no plano XZ).
        /// </summary>
        public Rect[] GetStructuralRegions()
        {
            if (structuralRegions != null && structuralRegions.Length > 0)
                return structuralRegions;
            return new Rect[] { diningHallRect, kitchenRect };
        }

        /// <summary>
        /// Retorna a altura Y da superfície do piso em coordenadas de mundo.
        /// </summary>
        public float GetFloorSurfaceWorldY()
        {
            if (groundAnchor != null)
                return groundAnchor.position.y;
            return transform.TransformPoint(new Vector3(0f, floorSurfaceLocalY, 0f)).y;
        }

        /// <summary>
        /// Retorna a direção da fachada em coordenadas de mundo (apontando para a rua).
        /// </summary>
        public Vector3 GetEntranceDirectionWorld()
        {
            Vector3 localDir3 = new Vector3(entranceDirectionLocal.x, 0f, entranceDirectionLocal.y);
            if (localDir3.sqrMagnitude < 0.0001f) localDir3 = Vector3.back;
            return transform.TransformDirection(localDir3.normalized);
        }

        /// <summary>
        /// Retorna a direção local 3D da fachada.
        /// </summary>
        public Vector3 GetEntranceDirectionLocal3D()
        {
            Vector3 localDir3 = new Vector3(entranceDirectionLocal.x, 0f, entranceDirectionLocal.y);
            if (localDir3.sqrMagnitude < 0.0001f) return Vector3.back;
            return localDir3.normalized;
        }

        /// <summary>
        /// Retorna os bounds conservadores em coordenadas de mundo considerando a transformação atual,
        /// calculando a projeção correta de rotação arbitrária e escala (AABB exato do OBB).
        /// </summary>
        public Bounds GetWorldBounds()
        {
            return TransformBounds(localBounds, transform.localToWorldMatrix);
        }

        /// <summary>
        /// Transforma um Bounds local para um AABB de mundo via matriz 4x4 completa.
        /// </summary>
        public static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 worldExtents = new Vector3(
                Mathf.Abs(matrix.m00) * extents.x + Mathf.Abs(matrix.m01) * extents.y + Mathf.Abs(matrix.m02) * extents.z,
                Mathf.Abs(matrix.m10) * extents.x + Mathf.Abs(matrix.m11) * extents.y + Mathf.Abs(matrix.m12) * extents.z,
                Mathf.Abs(matrix.m20) * extents.x + Mathf.Abs(matrix.m21) * extents.y + Mathf.Abs(matrix.m22) * extents.z
            );
            return new Bounds(center, worldExtents * 2f);
        }

        /// <summary>
        /// Retorna os polígonos (4 vértices XZ em mundo) de cada sub-região estrutural.
        /// </summary>
        public List<Vector3[]> GetWorldStructuralPolygons()
        {
            Rect[] regions = GetStructuralRegions();
            List<Vector3[]> polys = new List<Vector3[]>(regions.Length);
            for (int i = 0; i < regions.Length; i++)
            {
                polys.Add(TransformRectToWorldPolygon(regions[i]));
            }
            return polys;
        }

        /// <summary>
        /// Converte um Rect local (XZ) para um polígono de 4 pontos em mundo.
        /// </summary>
        public Vector3[] TransformRectToWorldPolygon(Rect r)
        {
            Vector3 p0 = transform.TransformPoint(new Vector3(r.xMin, floorSurfaceLocalY, r.yMin));
            Vector3 p1 = transform.TransformPoint(new Vector3(r.xMax, floorSurfaceLocalY, r.yMin));
            Vector3 p2 = transform.TransformPoint(new Vector3(r.xMax, floorSurfaceLocalY, r.yMax));
            Vector3 p3 = transform.TransformPoint(new Vector3(r.xMin, floorSurfaceLocalY, r.yMax));
            return new Vector3[] { p0, p1, p2, p3 };
        }

        /// <summary>
        /// Verifica se uma coordenada local (X, Z) está contida no piso em L da pizzaria.
        /// </summary>
        public bool IsPointInsideFootprint(Vector2 localXZ)
        {
            Rect[] regions = GetStructuralRegions();
            for (int i = 0; i < regions.Length; i++)
            {
                if (regions[i].Contains(localXZ))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Verifica se um ponto de mundo está contido dentro de alguma região estrutural da pizzaria.
        /// </summary>
        public bool IsWorldPointInsideFootprint(Vector3 worldPoint)
        {
            Vector3 localP = transform.InverseTransformPoint(worldPoint);
            return IsPointInsideFootprint(new Vector2(localP.x, localP.z));
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);

            // Desenha sub-retângulos do piso em L
            Rect[] regions = GetStructuralRegions();
            for (int i = 0; i < regions.Length; i++)
            {
                Gizmos.color = i == 0 ? new Color(0.2f, 1.0f, 0.3f, 0.4f) : new Color(1.0f, 0.6f, 0.2f, 0.4f);
                DrawRectGizmo(regions[i], floorSurfaceLocalY);
            }

            // Envelope de saliências
            if (overhangEnvelope.width > 0 && overhangEnvelope.height > 0)
            {
                Gizmos.color = new Color(1.0f, 0.2f, 0.8f, 0.3f);
                DrawRectGizmo(overhangEnvelope, floorSurfaceLocalY + 0.1f);
            }

            // Direção de entrada
            Gizmos.color = Color.yellow;
            Vector3 entOrigin = new Vector3(footprintCenter.x, floorSurfaceLocalY + 0.5f, footprintCenter.y);
            Vector3 entDir3 = GetEntranceDirectionLocal3D();
            Gizmos.DrawRay(entOrigin, entDir3 * 5f);

            // Se houver EntranceAnchor, desenhar seu forward
            if (entranceAnchor != null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(entranceAnchor.position, 0.4f);
                Gizmos.DrawRay(entranceAnchor.position, entranceAnchor.forward * 3f);
            }
        }

        private void DrawRectGizmo(Rect r, float y)
        {
            Vector3 p0 = new Vector3(r.xMin, y, r.yMin);
            Vector3 p1 = new Vector3(r.xMax, y, r.yMin);
            Vector3 p2 = new Vector3(r.xMax, y, r.yMax);
            Vector3 p3 = new Vector3(r.xMin, y, r.yMax);

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }
    }
}
