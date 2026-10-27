using System;
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

        [Header("Fachada e Acesso")]
        [Tooltip("Direção normalizada para a qual a fachada de entrada aponta no espaço local (para a rua).")]
        public Vector2 entranceDirectionLocal = new Vector2(-1f, 0f); // Aponta para -X (rua frontal)

        [Header("Planta em L (Sub-retângulos de Piso e Ocupação)")]
        [Tooltip("Salão de Atendimento / Entrada (X: 0 a 16.4m, Z: -13.2 a 9.2m)")]
        public Rect diningHallRect = Rect.MinMaxRect(0f, -13.2f, 16.4f, 9.2f);

        [Tooltip("Cozinha e Área Operacional (X: 16.4 a 33.0m, Z: -27.6 a 9.2m)")]
        public Rect kitchenRect = Rect.MinMaxRect(16.4f, -27.6f, 33.0f, 9.2f);

        [Header("Âncoras de Acesso")]
        public Transform entranceAnchor;
        public Transform receivingAnchor;

        /// <summary>
        /// Retorna os bounds conservadores em coordenadas de mundo considerando a transformação atual.
        /// </summary>
        public Bounds GetWorldBounds()
        {
            Vector3 worldCenter = transform.TransformPoint(localBounds.center);
            Vector3 worldExtents = transform.TransformVector(localBounds.extents);
            Vector3 absExtents = new Vector3(
                Mathf.Abs(worldExtents.x),
                Mathf.Abs(worldExtents.y),
                Mathf.Abs(worldExtents.z)
            );
            return new Bounds(worldCenter, absExtents * 2f);
        }

        /// <summary>
        /// Verifica se uma coordenada local (X, Z) está contida no piso em L da pizzaria.
        /// </summary>
        public bool IsPointInsideFootprint(Vector2 localXZ)
        {
            return diningHallRect.Contains(localXZ) || kitchenRect.Contains(localXZ);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.25f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(localBounds.center, localBounds.size);

            // Desenha sub-retângulos do piso em L
            Gizmos.color = new Color(0.2f, 1.0f, 0.3f, 0.4f);
            DrawRectGizmo(diningHallRect, 0.05f);
            Gizmos.color = new Color(1.0f, 0.6f, 0.2f, 0.4f);
            DrawRectGizmo(kitchenRect, 0.05f);

            // Direção de entrada
            Gizmos.color = Color.yellow;
            Vector3 entOrigin = new Vector3(footprintCenter.x, 0.5f, footprintCenter.y);
            Vector3 entDir3 = new Vector3(entranceDirectionLocal.x, 0f, entranceDirectionLocal.y);
            Gizmos.DrawRay(entOrigin, entDir3 * 5f);
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
