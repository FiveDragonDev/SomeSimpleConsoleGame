namespace SomeSimpleConsoleGame
{
    internal class TestRotateSystem : IUpdateSystem
    {
        private readonly Mesh _mesh;
        private readonly GLContext _context;

        public TestRotateSystem(Mesh mesh, GLContext context)
        {
            _mesh = mesh;
            _context = context;
        }

        public void Update(double deltaTime)
        {
            float angle = (float)(deltaTime * Math.PI / 2);
            _mesh.TransformVertices(v => Transform(v, angle));
            _context.DrawMesh(_mesh);
        }

        private static void Rotate(ref float a, ref float b, float angle)
        {
            var (sin, cos) = MathF.SinCos(angle);
            (a, b) = (a * cos - b * sin, a * sin + b * cos);
        }
        private static Mesh.Vertex Transform(Mesh.Vertex vertex, float angle)
        {
            var (x, y, z) = vertex;
            Rotate(ref x, ref y, angle);
            Rotate(ref x, ref z, angle);
            Rotate(ref y, ref z, angle);
            return new(x, y, z);
        }
    }
}