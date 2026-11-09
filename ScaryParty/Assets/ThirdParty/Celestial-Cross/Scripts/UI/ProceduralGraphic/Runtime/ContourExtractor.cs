using System.Collections.Generic;
using UnityEngine;

namespace ProceduralGraphicSystem
{
    public static class ContourExtractor
    {
        // Direction vectors for Moore neighborhood (clockwise)
        private static readonly int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };

        public static List<Vector2> ExtractContour(Texture2D tex, float alphaThreshold, int closeGapsRadius = 0)
        {
            return ExtractContour(tex.GetPixels(), tex.width, tex.height, alphaThreshold, closeGapsRadius);
        }

        public static List<Vector2> ExtractContour(Color[] pixels, int width, int height, float alphaThreshold, int closeGapsRadius = 0)
        {

            bool[,] solidGrid = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    solidGrid[x, y] = pixels[y * width + x].a >= alphaThreshold;
                }
            }

            if (closeGapsRadius > 0)
            {
                solidGrid = ApplyMorphologicalClosing(solidGrid, width, height, closeGapsRadius);
            }

            bool IsSolid(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) return false;
                return solidGrid[x, y];
            }

            // Find a starting point (first solid pixel from bottom-left)
            int startX = -1, startY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsSolid(x, y))
                    {
                        startX = x;
                        startY = y;
                        break;
                    }
                }
                if (startX != -1) break;
            }

            if (startX == -1) return new List<Vector2>(); // Completely transparent image

            List<Vector2> contour = new List<Vector2>();
            
            // Moore Neighborhood tracing
            int cx = startX;
            int cy = startY;
            int prevDir = 4; // Start looking upwards

            int failSafe = width * height; // Prevent infinite loop

            do
            {
                contour.Add(new Vector2(cx, cy));
                int dir = (prevDir + 2) % 8; // Look to the "left" of previous direction
                bool foundNext = false;

                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];

                    if (IsSolid(nx, ny))
                    {
                        cx = nx;
                        cy = ny;
                        prevDir = (dir + 4) % 8; // Opposite direction
                        foundNext = true;
                        break;
                    }
                    dir = (dir + 1) % 8;
                }

                if (!foundNext) break; // Isolated pixel
                failSafe--;

            } while ((cx != startX || cy != startY) && failSafe > 0);

            return contour;
        }

        private static bool[,] ApplyMorphologicalClosing(bool[,] grid, int width, int height, int radius)
        {
            // 1. Dilation (Expande os pixels sólidos)
            bool[,] dilated = new bool[width, height];
            int r2 = radius * radius;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (grid[x, y])
                    {
                        // Se for sólido, pinta um circulo ao redor no novo grid
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                if (dx * dx + dy * dy <= r2)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                    {
                                        dilated[nx, ny] = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // 2. Erosion (Encolhe os pixels sólidos de volta)
            bool[,] eroded = new bool[width, height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (dilated[x, y])
                    {
                        bool keep = true;
                        // Para manter um pixel, TODOS os vizinhos no raio precisam ser sólidos no dilated
                        for (int dy = -radius; dy <= radius; dy++)
                        {
                            for (int dx = -radius; dx <= radius; dx++)
                            {
                                if (dx * dx + dy * dy <= r2)
                                {
                                    int nx = x + dx;
                                    int ny = y + dy;
                                    if (nx < 0 || nx >= width || ny < 0 || ny >= height || !dilated[nx, ny])
                                    {
                                        keep = false;
                                        break;
                                    }
                                }
                            }
                            if (!keep) break;
                        }
                        eroded[x, y] = keep;
                    }
                }
            }

            return eroded;
        }

        public static List<Vector2> SimplifyContour(List<Vector2> points, int targetCount)
        {
            if (points.Count <= targetCount) return new List<Vector2>(points);

            float minTolerance = 0f;
            float maxTolerance = 100f; // Max pixel distance
            List<Vector2> bestResult = new List<Vector2>(points);

            // Binary search for tolerance
            for (int i = 0; i < 20; i++)
            {
                float midTolerance = (minTolerance + maxTolerance) / 2f;
                List<Vector2> currentResult = DouglasPeucker(points, midTolerance);

                if (Mathf.Abs(currentResult.Count - targetCount) < Mathf.Abs(bestResult.Count - targetCount))
                {
                    bestResult = currentResult;
                }

                if (currentResult.Count > targetCount)
                {
                    minTolerance = midTolerance;
                }
                else if (currentResult.Count < targetCount)
                {
                    maxTolerance = midTolerance;
                }
                else
                {
                    break; // Exact match found
                }
            }

            return bestResult;
        }

        private static List<Vector2> DouglasPeucker(List<Vector2> points, float tolerance)
        {
            if (points == null || points.Count < 3)
                return points;

            int firstPoint = 0;
            int lastPoint = points.Count - 1;
            List<int> pointIndexsToKeep = new List<int> { firstPoint, lastPoint };

            while (points[firstPoint] == points[lastPoint])
            {
                lastPoint--;
            }

            DouglasPeuckerReduction(points, firstPoint, lastPoint, tolerance, ref pointIndexsToKeep);

            pointIndexsToKeep.Sort();
            List<Vector2> returnPoints = new List<Vector2>();

            foreach (int index in pointIndexsToKeep)
            {
                returnPoints.Add(points[index]);
            }

            return returnPoints;
        }

        private static void DouglasPeuckerReduction(List<Vector2> points, int firstPoint, int lastPoint, float tolerance, ref List<int> pointIndexsToKeep)
        {
            float maxDistance = 0;
            int indexFarthest = 0;

            for (int index = firstPoint; index < lastPoint; index++)
            {
                float distance = PerpendicularDistance(points[firstPoint], points[lastPoint], points[index]);
                if (distance > maxDistance)
                {
                    maxDistance = distance;
                    indexFarthest = index;
                }
            }

            if (maxDistance > tolerance && indexFarthest != 0)
            {
                pointIndexsToKeep.Add(indexFarthest);
                DouglasPeuckerReduction(points, firstPoint, indexFarthest, tolerance, ref pointIndexsToKeep);
                DouglasPeuckerReduction(points, indexFarthest, lastPoint, tolerance, ref pointIndexsToKeep);
            }
        }

        private static float PerpendicularDistance(Vector2 Point1, Vector2 Point2, Vector2 Point)
        {
            float area = Mathf.Abs(.5f * (Point1.x * Point2.y + Point2.x * Point.y + Point.x * Point1.y - Point2.x * Point1.y - Point.x * Point2.y - Point1.x * Point.y));
            float bottom = Mathf.Sqrt(Mathf.Pow(Point1.x - Point2.x, 2) + Mathf.Pow(Point1.y - Point2.y, 2));
            if (bottom == 0) return 0f;
            return area / bottom * 2f;
        }

        public static List<Vector2> NormalizeContour(List<Vector2> points, int texWidth, int texHeight)
        {
            if (points.Count == 0) return new List<Vector2>();

            List<Vector2> normalized = new List<Vector2>();
            foreach (var p in points)
            {
                float nx = p.x / (float)texWidth;
                float ny = p.y / (float)texHeight;
                normalized.Add(new Vector2(nx, ny));
            }

            return normalized;
        }

        public static List<Vector2> ExpandContour(List<Vector2> points, float expansion)
        {
            if (points.Count < 3 || expansion == 0) return new List<Vector2>(points);

            List<Vector2> expanded = new List<Vector2>(points.Count);
            int count = points.Count;

            for (int i = 0; i < count; i++)
            {
                Vector2 prev = points[(i - 1 + count) % count];
                Vector2 curr = points[i];
                Vector2 next = points[(i + 1) % count];

                Vector2 dir1 = (curr - prev).normalized;
                Vector2 dir2 = (next - curr).normalized;

                Vector2 normal1 = new Vector2(dir1.y, -dir1.x); // Right normal (Outward for CCW tracing)
                Vector2 normal2 = new Vector2(dir2.y, -dir2.x);

                Vector2 averageNormal = (normal1 + normal2).normalized;

                // Dot product to adjust expansion based on angle to avoid pinching
                float dot = Vector2.Dot(normal1, averageNormal);
                float adjustedExpansion = expansion;
                if (dot > 0.1f) // Avoid division by zero or extreme expansions
                {
                    adjustedExpansion = expansion / dot;
                }

                // Clamp extreme expansions
                adjustedExpansion = Mathf.Clamp(adjustedExpansion, -Mathf.Abs(expansion) * 3f, Mathf.Abs(expansion) * 3f);

                expanded.Add(curr + averageNormal * adjustedExpansion);
            }

            return expanded;
        }

        public static List<Vector2> ApplyJitter(List<Vector2> points, float amount, int seed)
        {
            if (amount <= 0) return new List<Vector2>(points);

            Random.State oldState = Random.state;
            Random.InitState(seed);

            List<Vector2> jittered = new List<Vector2>(points.Count);
            foreach (var p in points)
            {
                Vector2 offset = Random.insideUnitCircle * amount;
                jittered.Add(p + offset);
            }

            Random.state = oldState;
            return jittered;
        }
        public static Color[] DownsamplePixels(Color[] pixels, int width, int height, out int newWidth, out int newHeight, int maxSize = 128)
        {
            if (width <= maxSize && height <= maxSize)
            {
                newWidth = width;
                newHeight = height;
                return pixels;
            }

            float ratio = Mathf.Min((float)maxSize / width, (float)maxSize / height);
            newWidth = Mathf.Max(1, Mathf.RoundToInt(width * ratio));
            newHeight = Mathf.Max(1, Mathf.RoundToInt(height * ratio));

            Color[] newPixels = new Color[newWidth * newHeight];
            
            for (int y = 0; y < newHeight; y++)
            {
                for (int x = 0; x < newWidth; x++)
                {
                    float u = newWidth > 1 ? (float)x / (newWidth - 1) : 0f;
                    float v = newHeight > 1 ? (float)y / (newHeight - 1) : 0f;

                    int origX = Mathf.Clamp(Mathf.RoundToInt(u * (width - 1)), 0, width - 1);
                    int origY = Mathf.Clamp(Mathf.RoundToInt(v * (height - 1)), 0, height - 1);

                    newPixels[y * newWidth + x] = pixels[origY * width + origX];
                }
            }

            return newPixels;
        }

        public static global::System.Collections.IEnumerator ExtractContourAsync(Color[] pixels, int width, int height, float alphaThreshold, int closeGapsRadius, global::System.Action<List<Vector2>> onComplete, int linesPerBatch = 64)
        {
            bool[,] solidGrid = new bool[width, height];
            
            int yStart = 0;
            while (yStart < height)
            {
                int yEnd = Mathf.Min(yStart + linesPerBatch, height);
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        solidGrid[x, y] = pixels[y * width + x].a >= alphaThreshold;
                    }
                }
                yStart = yEnd;
                yield return null;
            }

            if (closeGapsRadius > 0)
            {
                var closingRoutine = ApplyMorphologicalClosingAsync(solidGrid, width, height, closeGapsRadius, linesPerBatch, result => solidGrid = result);
                while (closingRoutine.MoveNext())
                {
                    yield return closingRoutine.Current;
                }
            }

            bool IsSolid(int x, int y)
            {
                if (x < 0 || x >= width || y < 0 || y >= height) return false;
                return solidGrid[x, y];
            }

            int startX = -1, startY = -1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (IsSolid(x, y))
                    {
                        startX = x;
                        startY = y;
                        break;
                    }
                }
                if (startX != -1) break;
            }

            if (startX == -1) 
            {
                onComplete?.Invoke(new List<Vector2>());
                yield break;
            }

            List<Vector2> contour = new List<Vector2>();
            int cx = startX;
            int cy = startY;
            int prevDir = 4;
            int failSafe = width * height;

            do
            {
                contour.Add(new Vector2(cx, cy));
                int dir = (prevDir + 2) % 8;
                bool foundNext = false;

                for (int i = 0; i < 8; i++)
                {
                    int nx = cx + dx[dir];
                    int ny = cy + dy[dir];

                    if (IsSolid(nx, ny))
                    {
                        cx = nx;
                        cy = ny;
                        prevDir = (dir + 4) % 8;
                        foundNext = true;
                        break;
                    }
                    dir = (dir + 1) % 8;
                }

                if (!foundNext) break;
                failSafe--;

            } while ((cx != startX || cy != startY) && failSafe > 0);

            onComplete?.Invoke(contour);
        }

        private static global::System.Collections.IEnumerator ApplyMorphologicalClosingAsync(bool[,] grid, int width, int height, int radius, int linesPerBatch, global::System.Action<bool[,]> onComplete)
        {
            // 1. Build SAT from original grid
            int[,] sat = new int[width, height];
            int yStart = 0;
            while (yStart < height)
            {
                int yEnd = Mathf.Min(yStart + linesPerBatch, height);
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int val = grid[x, y] ? 1 : 0;
                        int left = x > 0 ? sat[x - 1, y] : 0;
                        int top = y > 0 ? sat[x, y - 1] : 0;
                        int topLeft = (x > 0 && y > 0) ? sat[x - 1, y - 1] : 0;
                        sat[x, y] = val + left + top - topLeft;
                    }
                }
                yStart = yEnd;
                yield return null;
            }

            int GetSum(int[,] integral, int x1, int y1, int x2, int y2)
            {
                x1 = Mathf.Max(0, x1);
                y1 = Mathf.Max(0, y1);
                x2 = Mathf.Min(width - 1, x2);
                y2 = Mathf.Min(height - 1, y2);
                
                if (x1 > x2 || y1 > y2) return 0;

                int a = (x1 > 0 && y1 > 0) ? integral[x1 - 1, y1 - 1] : 0;
                int b = (y1 > 0) ? integral[x2, y1 - 1] : 0;
                int c = (x1 > 0) ? integral[x1 - 1, y2] : 0;
                int d = integral[x2, y2];

                return d - b - c + a;
            }

            // 2. Dilation
            bool[,] dilated = new bool[width, height];
            yStart = 0;
            while (yStart < height)
            {
                int yEnd = Mathf.Min(yStart + linesPerBatch, height);
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int sum = GetSum(sat, x - radius, y - radius, x + radius, y + radius);
                        dilated[x, y] = sum > 0;
                    }
                }
                yStart = yEnd;
                yield return null;
            }

            // 3. Build SAT from dilated grid
            int[,] dilatedSat = new int[width, height];
            yStart = 0;
            while (yStart < height)
            {
                int yEnd = Mathf.Min(yStart + linesPerBatch, height);
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int val = dilated[x, y] ? 1 : 0;
                        int left = x > 0 ? dilatedSat[x - 1, y] : 0;
                        int top = y > 0 ? dilatedSat[x, y - 1] : 0;
                        int topLeft = (x > 0 && y > 0) ? dilatedSat[x - 1, y - 1] : 0;
                        dilatedSat[x, y] = val + left + top - topLeft;
                    }
                }
                yStart = yEnd;
                yield return null;
            }

            // 4. Erosion
            bool[,] eroded = new bool[width, height];
            yStart = 0;
            while (yStart < height)
            {
                int yEnd = Mathf.Min(yStart + linesPerBatch, height);
                for (int y = yStart; y < yEnd; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int x1 = Mathf.Max(0, x - radius);
                        int y1 = Mathf.Max(0, y - radius);
                        int x2 = Mathf.Min(width - 1, x + radius);
                        int y2 = Mathf.Min(height - 1, y + radius);

                        int expectedArea = (x2 - x1 + 1) * (y2 - y1 + 1);
                        int sum = GetSum(dilatedSat, x - radius, y - radius, x + radius, y + radius);
                        
                        eroded[x, y] = sum == expectedArea;
                    }
                }
                yStart = yEnd;
                yield return null;
            }

            onComplete?.Invoke(eroded);
        }
    }
}
