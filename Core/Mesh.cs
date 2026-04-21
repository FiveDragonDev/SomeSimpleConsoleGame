using Assimp;

namespace SomeSimpleConsoleGame.Core
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

            for (int i = 0; i < triangles.Length; i++)
            {
                if (triangles[i] < 0 || triangles[i] >= vertices.Length)
                    throw new ArgumentException("Triangle index out of range", nameof(triangles));
            }

            _vertices = [.. vertices];
            _originalVertices = [.. vertices];
            _triangles = [.. triangles];
            _primitiveVertices = new Vertex[triangles.Length];

            VertexCount = vertices.Length;
            TriangleCount = triangles.Length / 3;
        }
        public Mesh(ReadOnlySpan<Vertex> vertices, ReadOnlySpan<int> triangles)
        {
            if (vertices.IsEmpty) throw new ArgumentNullException(nameof(vertices));
            if (triangles.IsEmpty) throw new ArgumentNullException(nameof(triangles));

            if (triangles.Length % 3 != 0)
                throw new ArgumentException("Triangles array length must be a multiple of 3", nameof(triangles));
            for (int i = 0; i < triangles.Length; i++)
            {
                if (triangles[i] < 0 || triangles[i] >= vertices.Length)
                    throw new ArgumentException("Triangle index out of range", nameof(triangles));
            }

            _vertices = [.. vertices];
            _originalVertices = [.. vertices];
            _triangles = [.. triangles];
            _primitiveVertices = new Vertex[triangles.Length];

            VertexCount = vertices.Length;
            TriangleCount = triangles.Length / 3;
        }

        public static Mesh FromFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("Path cannot be null or empty.", nameof(path));

            AssimpContext importer = new();
            var scene = importer.ImportFile(path, PostProcessSteps.Triangulate | PostProcessSteps.GenerateNormals | PostProcessSteps.GenerateUVCoords);

            if (scene == null || scene.MeshCount == 0)
                throw new InvalidOperationException("No meshes found in the file.");

            var assimpMesh = scene.Meshes[0];

            Span<Vertex> vertices = stackalloc Vertex[assimpMesh.VertexCount];

            for (int i = 0; i < assimpMesh.VertexCount; i++)
            {
                var vertex = assimpMesh.Vertices[i];
                vertices[i] = new(vertex.X, vertex.Y, vertex.Z);
            }

            Span<int> triangles = new int[assimpMesh.FaceCount * 3];
            for (int i = 0; i < assimpMesh.FaceCount; i++)
            {
                var face = assimpMesh.Faces[i];
                if (face.IndexCount != 3)
                    throw new NotSupportedException("Only triangular faces are supported.");

                triangles[i * 3] = face.Indices[0];
                triangles[i * 3 + 1] = face.Indices[1];
                triangles[i * 3 + 2] = face.Indices[2];
            }

            return new Mesh(vertices, triangles);
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
            if (_originalVertices.AsSpan().SequenceEqual(_vertices)) return;
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
