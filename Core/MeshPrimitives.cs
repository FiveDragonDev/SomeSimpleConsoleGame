namespace SomeSimpleConsoleGame.Core
{
    public static class MeshPrimitives
    {
        public static Mesh CreateDoubleSidedPlane(float size = 1f)
        {
            float h = MathF.Abs(size / 2f);
            return new Mesh(
                [
                    new(-h, 0, -h),
                    new(h, 0, -h),
                    new(h, 0, h),
                    new(-h, 0, h),
                ],
                [
                    0, 1, 2, 0, 2, 3,
                    0, 2, 1, 0, 3, 2
                ]
            );
        }
        public static Mesh CreatePlane(float size = 1f)
        {
            float h = MathF.Abs(size / 2f);
            return new Mesh(
                [
                    new(-h, 0, -h),
                    new(h, 0, -h),
                    new(h, 0, h),
                    new(-h, 0, h),
                ],
                [
                    0, 1, 2, 0, 2, 3,
                ]
            );
        }
        public static Mesh CreateCube(float size = 1f)
        {
            float h = MathF.Abs(size / 2f);
            return new Mesh(
                [
                    new(-h, -h, -h),
                    new(h, -h, -h),
                    new(h, h, -h),
                    new(-h, h, -h),
                    new(-h, -h, h),
                    new(h, -h, h),
                    new(h, h, h),
                    new(-h, h, h),
            ],
                [
                    0, 1, 2, 0, 2, 3,
                    4, 6, 5, 4, 7, 6,
                    0, 3, 7, 0, 7, 4,
                    1, 5, 6, 1, 6, 2,
                    0, 4, 5, 0, 5, 1,
                    3, 2, 6, 3, 6, 7,
                ]
            );
        }

        public static Mesh CreateUvSphere(int segments = 24, int rings = 16, float radius = 0.5f)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
            ArgumentOutOfRangeException.ThrowIfLessThan(rings, 2);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

            int vertexCols = segments + 1;
            int vertexLength = (rings + 1) * vertexCols;
            Span<Mesh.Vertex> vertices = vertexLength < 256 ? stackalloc Mesh.Vertex[vertexLength] : new Mesh.Vertex[vertexLength];

            int vi = 0;
            for (int i = 0; i <= rings; i++)
            {
                float t = i / (float)rings;
                float theta = MathF.PI * t;
                float sinTheta = MathUtils.QSin(theta);
                float cosTheta = MathUtils.QCos(theta);

                for (int j = 0; j <= segments; j++)
                {
                    float s = j / (float)segments;
                    float phi = MathUtils.DoublePi * s;
                    float sinPhi = MathUtils.QSin(phi);
                    float cosPhi = MathUtils.QCos(phi);

                    float x = radius * sinTheta * cosPhi;
                    float y = radius * cosTheta;
                    float z = radius * sinTheta * sinPhi;
                    vertices[vi++] = new(x, y, z);
                }
            }

            int trianglesLength = segments * segments * 6;
            Span<int> triangles = trianglesLength < 256 ? stackalloc int[trianglesLength] : new int[trianglesLength];
            int ti = 0;
            for (int i = 0; i < rings; i++)
            {
                int row0 = i * vertexCols;
                int row1 = (i + 1) * vertexCols;
                for (int j = 0; j < segments; j++)
                {
                    int v0 = row0 + j;
                    int v1 = row1 + j;
                    int v2 = row1 + j + 1;
                    int v3 = row0 + j + 1;

                    triangles[ti++] = v0;
                    triangles[ti++] = v1;
                    triangles[ti++] = v2;

                    triangles[ti++] = v0;
                    triangles[ti++] = v2;
                    triangles[ti++] = v3;
                }
            }

            return new Mesh(vertices, triangles);
        }

        public static Mesh CreateTorus(int segments = 40, int sides = 14, float radius = 0.65f, float tubeRadius = 0.22f)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(segments, 3);
            ArgumentOutOfRangeException.ThrowIfLessThan(sides, 3);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tubeRadius);

            int vertexCols = sides + 1;
            int vertexLength = (segments + 1) * vertexCols;
            Span<Mesh.Vertex> vertices = vertexLength < 256 ? stackalloc Mesh.Vertex[vertexLength] : new Mesh.Vertex[vertexLength];

            int vi = 0;
            for (int i = 0; i <= segments; i++)
            {
                float u = i / (float)segments;
                float a = MathF.Tau * u;
                float sinA = MathF.Sin(a);
                float cosA = MathF.Cos(a);

                for (int j = 0; j <= sides; j++)
                {
                    float v = j / (float)sides;
                    float b = MathF.Tau * v;
                    float sinB = MathF.Sin(b);
                    float cosB = MathF.Cos(b);

                    float x = (radius + tubeRadius * cosB) * cosA;
                    float y = tubeRadius * sinB;
                    float z = (radius + tubeRadius * cosB) * sinA;

                    vertices[vi++] = new(x, y, z);
                }
            }

            int trianglesLength = segments * sides * 6;
            Span<int> triangles = trianglesLength < 256 ? stackalloc int[trianglesLength] : new int[trianglesLength];
            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int row0 = i * vertexCols;
                int row1 = (i + 1) * vertexCols;
                for (int j = 0; j < sides; j++)
                {
                    int v0 = row0 + j;
                    int v1 = row1 + j;
                    int v2 = row1 + j + 1;
                    int v3 = row0 + j + 1;

                    triangles[ti++] = v0;
                    triangles[ti++] = v1;
                    triangles[ti++] = v2;

                    triangles[ti++] = v0;
                    triangles[ti++] = v2;
                    triangles[ti++] = v3;
                }
            }

            return new Mesh(vertices, triangles);
        }
    }
}
