namespace SomeSimpleConsoleGame
{
    public sealed class Mesh
    {
        public readonly record struct Vertex(float X, float Y, float Z);

        public int VertexCount { get; }
        public int TriangleCount { get; }

        private readonly Vertex[] _vertices;
        private readonly Vertex[] _originalVertices;
        private readonly int[] _triangles;

        private readonly Vertex[] _primitiveVertices;
        private bool _hasChanged = true;

        public Mesh(Vertex[] vertices, int[] triangles)
        {
            ArgumentNullException.ThrowIfNull(vertices);
            ArgumentNullException.ThrowIfNull(triangles);

            if (triangles.Length % 3 != 0)
                throw new ArgumentException("Triangles array length must be a multiple of 3", nameof(triangles));
            if (triangles.Any(index => index < 0 || index >= vertices.Length))
                throw new ArgumentException("Triangle index out of range", nameof(triangles));

            _vertices = [.. vertices];
            _originalVertices = [.. vertices];
            _triangles = [.. triangles];
            _primitiveVertices = new Vertex[triangles.Length];

            VertexCount = vertices.Length;
            TriangleCount = triangles.Length / 3;
        }

        public ReadOnlySpan<Vertex> GetVertices() => _vertices;
        public ReadOnlySpan<int> GetTriangles() => _triangles;

        public ReadOnlySpan<Vertex> GetTriangleVertices()
        {
            if (_hasChanged)
            {
                for (int i = 0; i < _triangles.Length; i++)
                {
                    _primitiveVertices[i] = _vertices[_triangles[i]];
                }
                _hasChanged = false;
            }
            return _primitiveVertices;
        }

        public void TransformVertices(Func<Vertex, Vertex> transform)
        {
            for (int i = 0; i < _vertices.Length; i++)
            {
                _vertices[i] = transform(_vertices[i]);
            }
            _hasChanged = true;
        }

        public void Locate(float x, float y, float z) => TransformVertices(v => new(v.X + x, v.Y + y, v.Z + z));
        public void Scale(float x, float y, float z) => TransformVertices(v => new(v.X * x, v.Y * y, v.Z * z));
        public void Rotate(float x, float y, float z)
        {
            TransformVertices(RotateVertex);

            Vertex RotateVertex(Vertex v)
            {
                var (vx, vy, vz) = v;
                Rotate2D(ref vy, ref vz, x);
                Rotate2D(ref vx, ref vz, y);
                Rotate2D(ref vx, ref vy, z);
                return new(vx, vy, vz);
            }
        }

        public void ResetVertices()
        {
            if (_originalVertices.Equals(_vertices)) return;
            Array.Copy(_originalVertices, _vertices, _vertices.Length);
            _hasChanged = true;
        }

        private static void Rotate2D(ref float a, ref float b, float angle)
        {
            var (sin, cos) = MathF.SinCos(angle);
            (a, b) = (a * cos - b * sin, a * sin + b * cos);
        }
    }
}
