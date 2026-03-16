namespace SomeSimpleConsoleGame
{
    internal class TestRotateSystem : IUpdateSystem
    {
        private readonly Mesh _mesh;
        private readonly GLContext _context;
        private readonly float _speed;

        public TestRotateSystem(Mesh mesh, GLContext context, float speed = 1)
        {
            _mesh = mesh;
            _context = context;
            _speed = speed;
        }

        public void Update(double deltaTime)
        {
            var angle = (float)(deltaTime * _speed);

            if (angle != 0) _mesh.Rotate(angle, angle * 2, angle * 3);
            _context.DrawMesh(_mesh);
        }
    }
}
