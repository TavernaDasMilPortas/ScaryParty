using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScaryParty.Pizzeria.Visual
{
    /// <summary>
    /// Fábrica procedural de malhas gastronômicas 3D de alta qualidade para pizzas modulares.
    /// Geometria anatômica com normais verificadas matematicamente (orientação positiva para cima e sem culling indevido):
    /// - Massa com borda cornicione elevada e reservatório rebaixado (sólido fechado)
    /// - Camada de molho orgânico com borda ondulada natural
    /// - Camada de queijo fundido com espessura e ondulação
    /// - Fatias de calabresa / pepperoni com efeito 'cupping' (curvatura de borda)
    /// - Fatias de cogumelo com silhueta de chapéu, lamelas e talo
    /// - Tiras/fiapos de queijo ralado arqueados
    /// - Folha de manjericão orgânica com dobra da nervura central
    /// </summary>
    public static class PizzaModularGeometryFactory
    {
        private static Mesh _doughMesh;
        private static Mesh _sauceMesh;
        private static Mesh _cheeseCoatingMesh;
        private static Mesh _pepperoniSliceMesh;
        private static Mesh _mushroomSliceMesh;
        private static Mesh _cheeseStrandMesh;
        private static Mesh _basilLeafMesh;

        public static Mesh GetDoughMesh()
        {
            if (_doughMesh == null) _doughMesh = BuildDoughMesh();
            return _doughMesh;
        }

        public static Mesh GetSauceMesh()
        {
            if (_sauceMesh == null) _sauceMesh = BuildSauceMesh();
            return _sauceMesh;
        }

        public static Mesh GetCheeseCoatingMesh()
        {
            if (_cheeseCoatingMesh == null) _cheeseCoatingMesh = BuildCheeseCoatingMesh();
            return _cheeseCoatingMesh;
        }

        public static Mesh GetPepperoniSliceMesh()
        {
            if (_pepperoniSliceMesh == null) _pepperoniSliceMesh = BuildPepperoniSliceMesh();
            return _pepperoniSliceMesh;
        }

        public static Mesh GetMushroomSliceMesh()
        {
            if (_mushroomSliceMesh == null) _mushroomSliceMesh = BuildMushroomSliceMesh();
            return _mushroomSliceMesh;
        }

        public static Mesh GetCheeseStrandMesh()
        {
            if (_cheeseStrandMesh == null) _cheeseStrandMesh = BuildCheeseStrandMesh();
            return _cheeseStrandMesh;
        }

        public static Mesh GetBasilLeafMesh()
        {
            if (_basilLeafMesh == null) _basilLeafMesh = BuildBasilLeafMesh();
            return _basilLeafMesh;
        }

        #region Dough Base (Cornicione)
        private static Mesh BuildDoughMesh()
        {
            var mesh = new Mesh { name = "Mesh_PizzaDough_Cornicione" };
            const int segments = 36;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Vértice 0: Centro da face superior
            vertices.Add(new Vector3(0f, 0.008f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Anéis superiores: (raio, altura Y)
            var rings = new (float r, float y)[]
            {
                (0.090f, 0.009f), // Anel 1: Leito intermediário
                (0.145f, 0.010f), // Anel 2: Limite do molho
                (0.168f, 0.018f), // Anel 3: Início da subida do cornicione
                (0.182f, 0.024f), // Anel 4: Crista do cornicione
                (0.190f, 0.014f), // Anel 5: Encosta externa
                (0.192f, 0.000f)  // Anel 6: Borda tocando o piso
            };

            for (int r = 0; r < rings.Length; r++)
            {
                float radius = rings[r].r;
                float y = rings[r].y;

                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    float rJitter = (r >= 2 && r <= 4) ? Mathf.Sin(angle * 7f) * 0.002f : 0f;
                    float curR = radius + rJitter;
                    float x = Mathf.Cos(angle) * curR;
                    float z = Mathf.Sin(angle) * curR;

                    vertices.Add(new Vector3(x, y, z));
                    uvs.Add(new Vector2((x + 0.20f) / 0.40f, (z + 0.20f) / 0.40f));
                }
            }

            // Triângulos do centro para o Anel 1 (Clockwise from above -> normal +Y)
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + s);
            }

            // Triângulos entre anéis concêntricos superiores
            for (int r = 0; r < rings.Length - 1; r++)
            {
                int rStart = 1 + r * segments;
                int nextRStart = 1 + (r + 1) * segments;

                for (int s = 0; s < segments; s++)
                {
                    int next = (s + 1) % segments;
                    int in_s = rStart + s;
                    int in_next = rStart + next;
                    int out_s = nextRStart + s;
                    int out_next = nextRStart + next;

                    // Normal +Y / outward comprovado
                    triangles.Add(in_s);
                    triangles.Add(in_next);
                    triangles.Add(out_s);

                    triangles.Add(in_next);
                    triangles.Add(out_next);
                    triangles.Add(out_s);
                }
            }

            // Fundo inferior plano (fechamento estanque com normal -Y)
            int bottomCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int rimStartIndex = 1 + (rings.Length - 1) * segments;
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles.Add(bottomCenterIndex);
                triangles.Add(rimStartIndex + s);
                triangles.Add(rimStartIndex + next);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Sauce Layer
        private static Mesh BuildSauceMesh()
        {
            var mesh = new Mesh { name = "Mesh_PizzaSauce_Organic" };
            const int segments = 32;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Vértice central
            vertices.Add(new Vector3(0f, 0.0125f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Anel 1 (intermediário)
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float r = 0.095f;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0128f, z));
                uvs.Add(new Vector2(0.5f + x * 2.5f, 0.5f + z * 2.5f));
            }

            // Anel 2 (borda ondulada orgânica)
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 6f) * 0.005f + Mathf.Cos(angle * 10f) * 0.003f;
                float r = 0.155f + wave;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0135f, z));
                uvs.Add(new Vector2(0.5f + x * 2.5f, 0.5f + z * 2.5f));
            }

            // Anel 3 (rebaixo da borda sobre a massa)
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 6f) * 0.005f + Mathf.Cos(angle * 10f) * 0.003f;
                float r = 0.157f + wave;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0105f, z));
                uvs.Add(new Vector2(0.5f + x * 2.5f, 0.5f + z * 2.5f));
            }

            // Centro -> Anel 1 (Clockwise from above)
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + s);
            }

            int midOffset = 1;
            int outerOffset = 1 + segments;
            int rimOffset = 1 + segments * 2;

            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int in_s = midOffset + s;
                int in_next = midOffset + next;
                int out_s = outerOffset + s;
                int out_next = outerOffset + next;

                triangles.Add(in_s);
                triangles.Add(in_next);
                triangles.Add(out_s);

                triangles.Add(in_next);
                triangles.Add(out_next);
                triangles.Add(out_s);

                // Rebaixo da borda
                int r_s = rimOffset + s;
                int r_next = rimOffset + next;
                triangles.Add(out_s);
                triangles.Add(out_next);
                triangles.Add(r_s);

                triangles.Add(out_next);
                triangles.Add(r_next);
                triangles.Add(r_s);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Cheese Coating Layer
        private static Mesh BuildCheeseCoatingMesh()
        {
            var mesh = new Mesh { name = "Mesh_PizzaCheese_Melted" };
            const int segments = 32;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            vertices.Add(new Vector3(0f, 0.0160f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float r = 0.088f;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0165f, z));
                uvs.Add(new Vector2(0.5f + x * 2.8f, 0.5f + z * 2.8f));
            }

            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 5f) * 0.006f + Mathf.Cos(angle * 8f) * 0.004f;
                float r = 0.148f + wave;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0160f, z));
                uvs.Add(new Vector2(0.5f + x * 2.8f, 0.5f + z * 2.8f));
            }

            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                float wave = Mathf.Sin(angle * 5f) * 0.006f + Mathf.Cos(angle * 8f) * 0.004f;
                float r = 0.150f + wave;
                float x = Mathf.Cos(angle) * r;
                float z = Mathf.Sin(angle) * r;
                vertices.Add(new Vector3(x, 0.0130f, z));
                uvs.Add(new Vector2(0.5f + x * 2.8f, 0.5f + z * 2.8f));
            }

            // Centro -> Anel 1
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + s);
            }

            int midOffset = 1;
            int outerOffset = 1 + segments;
            int rimOffset = 1 + segments * 2;

            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int in_s = midOffset + s;
                int in_next = midOffset + next;
                int out_s = outerOffset + s;
                int out_next = outerOffset + next;

                triangles.Add(in_s);
                triangles.Add(in_next);
                triangles.Add(out_s);

                triangles.Add(in_next);
                triangles.Add(out_next);
                triangles.Add(out_s);

                int r_s = rimOffset + s;
                int r_next = rimOffset + next;
                triangles.Add(out_s);
                triangles.Add(out_next);
                triangles.Add(r_s);

                triangles.Add(out_next);
                triangles.Add(r_next);
                triangles.Add(r_s);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Pepperoni Slice (Cupped)
        private static Mesh BuildPepperoniSliceMesh()
        {
            var mesh = new Mesh { name = "Mesh_Pepperoni_Cupped" };
            const int segments = 20;
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Vértice 0: Centro côncavo superior
            vertices.Add(new Vector3(0f, 0f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Anel 1: Leito interno (r = 0.016m, y = 0.0006m)
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                float r = 0.016f;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0.0006f, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.32f, 0.5f + Mathf.Sin(a) * 0.32f));
            }

            // Anel 2: Borda curvada (cupping r = 0.025m, y = 0.0022m)
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                float r = 0.025f;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0.0022f, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }

            // Anel 3: Canto lateral inferior da fatia (r = 0.0248m, y = 0.0004m)
            for (int s = 0; s < segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                float r = 0.0248f;
                vertices.Add(new Vector3(Mathf.Cos(a) * r, 0.0004f, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
            }

            // Vértice de fundo central inferior
            int bottomCenterIndex = vertices.Count;
            vertices.Add(new Vector3(0f, -0.0010f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Face superior côncava (Clockwise from above)
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + next);
                triangles.Add(1 + s);
            }

            int midOff = 1;
            int rimOff = 1 + segments;
            int botRimOff = 1 + segments * 2;

            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int in_s = midOff + s;
                int in_next = midOff + next;
                int out_s = rimOff + s;
                int out_next = rimOff + next;

                triangles.Add(in_s);
                triangles.Add(in_next);
                triangles.Add(out_s);

                triangles.Add(in_next);
                triangles.Add(out_next);
                triangles.Add(out_s);

                // Bevel lateral
                int b_s = botRimOff + s;
                int b_next = botRimOff + next;
                triangles.Add(out_s);
                triangles.Add(out_next);
                triangles.Add(b_s);

                triangles.Add(out_next);
                triangles.Add(b_next);
                triangles.Add(b_s);

                // Fundo inferior
                triangles.Add(bottomCenterIndex);
                triangles.Add(b_s);
                triangles.Add(b_next);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Mushroom Slice (Silhouette)
        private static Mesh BuildMushroomSliceMesh()
        {
            var mesh = new Mesh { name = "Mesh_Mushroom_Slice" };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            var contour = new Vector2[]
            {
                new Vector2(0.000f,  0.020f),  // Topo do chapéu
                new Vector2(0.012f,  0.018f),  // Chapéu superior direito
                new Vector2(0.021f,  0.011f),  // Lateral direita do chapéu
                new Vector2(0.022f,  0.003f),  // Canto inferior direito do chapéu
                new Vector2(0.013f,  0.003f),  // Lamela direita
                new Vector2(0.006f,  0.001f),  // Junção talo/chapéu direito
                new Vector2(0.005f, -0.016f),  // Talo inferior direito
                new Vector2(0.000f, -0.018f),  // Base do talo centro
                new Vector2(-0.005f, -0.016f), // Talo inferior esquerdo
                new Vector2(-0.006f,  0.001f), // Junção talo/chapéu esquerdo
                new Vector2(-0.013f,  0.003f), // Lamela esquerda
                new Vector2(-0.022f,  0.003f), // Canto inferior esquerdo do chapéu
                new Vector2(-0.021f,  0.011f), // Lateral esquerda do chapéu
                new Vector2(-0.012f,  0.018f)  // Chapéu superior esquerdo
            };

            int n = contour.Length;
            float thickness = 0.0032f;

            // Face superior (y = thickness)
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(contour[i].x, thickness, contour[i].y));
                uvs.Add(new Vector2(contour[i].x * 20f + 0.5f, contour[i].y * 20f + 0.5f));
            }

            // Face inferior (y = 0)
            int botOff = vertices.Count;
            for (int i = 0; i < n; i++)
            {
                vertices.Add(new Vector3(contour[i].x, 0f, contour[i].y));
                uvs.Add(new Vector2(contour[i].x * 20f + 0.5f, contour[i].y * 20f + 0.5f));
            }

            // Face superior (Clockwise from above, Y > 0)
            for (int i = 1; i < n - 1; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            // Face inferior (Clockwise from below, Y < 0)
            for (int i = 1; i < n - 1; i++)
            {
                triangles.Add(botOff);
                triangles.Add(botOff + i + 1);
                triangles.Add(botOff + i);
            }

            // Laterais (extrusão estanque externa)
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                int topA = i;
                int topB = next;
                int botA = botOff + i;
                int botB = botOff + next;

                triangles.Add(topA);
                triangles.Add(botA);
                triangles.Add(topB);

                triangles.Add(topB);
                triangles.Add(botA);
                triangles.Add(botB);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Cheese Strand (Grated Shred)
        private static Mesh BuildCheeseStrandMesh()
        {
            var mesh = new Mesh { name = "Mesh_Cheese_Strand" };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            const int segments = 6;
            float length = 0.026f;
            float halfWidth = 0.0022f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float x = Mathf.Lerp(-length * 0.5f, length * 0.5f, t);
                float arch = Mathf.Sin(t * Mathf.PI) * 0.0020f;
                float y = arch + 0.0010f;
                float w = halfWidth * Mathf.Sin(t * Mathf.PI * 0.9f + 0.1f);

                vertices.Add(new Vector3(x, y + 0.0012f, -w));
                vertices.Add(new Vector3(x, y + 0.0012f, w));
                vertices.Add(new Vector3(x, y, -w));
                vertices.Add(new Vector3(x, y, w));

                for (int c = 0; c < 4; c++)
                {
                    uvs.Add(new Vector2(t, (c % 2 == 0) ? 0f : 1f));
                }
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 4;
                int i1 = (i + 1) * 4;

                // Face superior
                triangles.Add(i0 + 0);
                triangles.Add(i0 + 1);
                triangles.Add(i1 + 0);

                triangles.Add(i1 + 0);
                triangles.Add(i0 + 1);
                triangles.Add(i1 + 1);

                // Lado esquerdo
                triangles.Add(i0 + 2);
                triangles.Add(i1 + 2);
                triangles.Add(i0 + 0);

                triangles.Add(i1 + 2);
                triangles.Add(i1 + 0);
                triangles.Add(i0 + 0);

                // Lado direito
                triangles.Add(i0 + 1);
                triangles.Add(i1 + 1);
                triangles.Add(i0 + 3);

                triangles.Add(i1 + 1);
                triangles.Add(i1 + 3);
                triangles.Add(i0 + 3);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion

        #region Basil Leaf
        private static Mesh BuildBasilLeafMesh()
        {
            var mesh = new Mesh { name = "Mesh_Basil_Leaf" };
            var vertices = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            const int segments = 8;
            float length = 0.046f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float z = Mathf.Lerp(-length * 0.4f, length * 0.6f, t);
                float w = Mathf.Sin(t * Mathf.PI) * 0.015f;
                float foldY = Mathf.Sin(t * Mathf.PI) * 0.0035f;

                vertices.Add(new Vector3(-w, foldY + 0.0025f, z));
                vertices.Add(new Vector3(0f, foldY, z));
                vertices.Add(new Vector3(w, foldY + 0.0025f, z));

                uvs.Add(new Vector2(0f, t));
                uvs.Add(new Vector2(0.5f, t));
                uvs.Add(new Vector2(1f, t));
            }

            for (int i = 0; i < segments; i++)
            {
                int r0 = i * 3;
                int r1 = (i + 1) * 3;

                // Lado esquerdo
                triangles.Add(r0 + 0);
                triangles.Add(r1 + 0);
                triangles.Add(r0 + 1);

                triangles.Add(r1 + 0);
                triangles.Add(r1 + 1);
                triangles.Add(r0 + 1);

                // Lado direito
                triangles.Add(r0 + 1);
                triangles.Add(r1 + 1);
                triangles.Add(r0 + 2);

                triangles.Add(r1 + 1);
                triangles.Add(r1 + 2);
                triangles.Add(r0 + 2);

                // Face reversa para visibilidade double-sided
                triangles.Add(r0 + 0);
                triangles.Add(r0 + 1);
                triangles.Add(r1 + 0);

                triangles.Add(r1 + 0);
                triangles.Add(r0 + 1);
                triangles.Add(r1 + 1);

                triangles.Add(r0 + 1);
                triangles.Add(r0 + 2);
                triangles.Add(r1 + 1);

                triangles.Add(r1 + 1);
                triangles.Add(r0 + 2);
                triangles.Add(r1 + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        #endregion
    }
}
