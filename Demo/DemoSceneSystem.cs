using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Rendering;
using System.Diagnostics;
using System.Numerics;

namespace SomeSimpleConsoleGame.Demo
{
    public sealed class DemoSceneSystem : IUpdateSystem
    {
        private readonly GLContext _gl;
        private readonly Shader _shader;
        private readonly InputController _input;
        private readonly DemoState _state;

        private readonly Mesh _torus;
        private readonly Mesh _sphere;
        private readonly Mesh _cube;
        private readonly Mesh _plane;

        private readonly (float angle, float radius, float height, float scale)[] _asteroids;

        public DemoSceneSystem(GLContext gl, Shader shader, InputController input, DemoState state)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _shader = shader ?? throw new ArgumentNullException(nameof(shader));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _state = state ?? throw new ArgumentNullException(nameof(state));

            _torus = MeshPrimitives.CreateTorus();
            _sphere = MeshPrimitives.CreateUvSphere();
            _cube = MeshPrimitives.CreateCube();
            _plane = MeshPrimitives.CreateDoubleSidedPlane();

            _asteroids = CreateAsteroids(count: 120, seed: (int)Stopwatch.GetTimestamp());
        }

        public void Update(double deltaTime)
        {
            float dt = (float)deltaTime;
            HandleInput(dt);

            _state.Time += dt;
            if (!_state.Paused) _state.InGameTime += dt;
            if (_state.LockCamera) AutoPilotCamera();

            if (_state.LightFollowsCamera) _state.LightDirectionWorld = Vector3.Normalize(_state.Camera.GetForward() * -1f + new Vector3(-0.2f, 0.45f, 0.1f));
            else _state.LightDirectionWorld = Vector3.Normalize(new(
                    MathF.Sin(_state.InGameTime * 0.35f) * 0.6f - 0.25f,
                    0.85f,
                    MathF.Cos(_state.InGameTime * 0.35f) * 0.6f - 0.15f
                ));

            var viewProj = _state.GetViewProjection();
            var camPos = _state.Camera.Position;
            var lightDir = _state.LightDirectionWorld;

            _shader.Use();
            _shader.Uniform("uViewProj", viewProj);
            _shader.Uniform("uCameraPos", camPos.X, camPos.Y, camPos.Z);
            _shader.Uniform("uLightDir", lightDir.X, lightDir.Y, lightDir.Z);
            _shader.Uniform("uAmbient", _state.Ambient);
            _shader.Uniform("uDiffuse", _state.Diffuse);
            _shader.Uniform("uSpecular", _state.Specular);
            _shader.Uniform("uShininess", _state.Shininess);
            _shader.Uniform("uFog", 2.5f, 12.5f);
            _shader.Uniform("uTime", _state.Time);

            DrawScene();
        }

        private void AutoPilotCamera()
        {
            float t = _state.InGameTime;
            _state.Camera.Position = new Vector3(
                MathF.Sin(t * 0.25f) * 3.2f,
                0.7f + MathF.Sin(t * 0.18f) * 0.35f,
                MathF.Cos(t * 0.25f) * 3.2f
            );

            Vector3 toCenter = Vector3.Normalize(-_state.Camera.Position);
            _state.Camera.Yaw = MathF.Atan2(toCenter.X, toCenter.Z);
            _state.Camera.Pitch = MathF.Asin(toCenter.Y);
        }

        private void HandleInput(float dt)
        {
            if (_input.IsKeyPressed(ConsoleKey.P) || _input.IsKeyPressed(ConsoleKey.Spacebar)) _state.Paused = !_state.Paused;
            if (_input.IsKeyPressed(ConsoleKey.C)) _state.LockCamera = !_state.LockCamera;
            if (_input.IsKeyPressed(ConsoleKey.L)) _state.LightFollowsCamera = !_state.LightFollowsCamera;

            if (_input.IsKeyPressed(ConsoleKey.D1)) _state.SceneIndex = 0;
            if (_input.IsKeyPressed(ConsoleKey.D2)) _state.SceneIndex = 1;

            if (_input.IsKeyPressed(ConsoleKey.B))
            {
                _state.BackgroundRgb = _state.BackgroundRgb switch
                {
                    (10, 7, 15) => (13, 6, 9),
                    (13, 6, 9) => (10, 7, 15),
                    _ => throw new NotImplementedException(),
                };
                _state.ForegroundRgb = _state.ForegroundRgb switch
                {
                    (255, 255, 235) => (255, 235, 210),
                    (255, 235, 210) => (255, 255, 235),
                    _ => throw new NotImplementedException(),
                };
            }

            if (_state.LockCamera) return;

            const float lookSpeed = 1.7f;
            if (_input.IsKeyDown(ConsoleKey.LeftArrow)) _state.Camera.Yaw += lookSpeed * dt;
            if (_input.IsKeyDown(ConsoleKey.RightArrow)) _state.Camera.Yaw -= lookSpeed * dt;
            if (_input.IsKeyDown(ConsoleKey.UpArrow)) _state.Camera.Pitch += lookSpeed * dt;
            if (_input.IsKeyDown(ConsoleKey.DownArrow)) _state.Camera.Pitch -= lookSpeed * dt;

            _state.Camera.Pitch = Math.Clamp(_state.Camera.Pitch, -MathF.PI / 3, MathF.PI / 3);

            float speed = _input.HasModifiers(ConsoleModifiers.Shift) ? 4 : 2;
            Vector3 forward = _state.Camera.GetForward();
            Vector3 right = _state.Camera.GetRight();
            Vector3 up = Vector3.UnitY;

            Vector3 move = Vector3.Zero;
            if (_input.IsKeyDown(ConsoleKey.W)) move += forward;
            if (_input.IsKeyDown(ConsoleKey.S)) move -= forward;
            if (_input.IsKeyDown(ConsoleKey.D)) move += right;
            if (_input.IsKeyDown(ConsoleKey.A)) move -= right;
            if (_input.IsKeyDown(ConsoleKey.E)) move += up;
            if (_input.IsKeyDown(ConsoleKey.Q)) move -= up;

            if (move != Vector3.Zero) _state.Camera.Position += Vector3.Normalize(move) * (speed * dt);
        }

        private void DrawScene()
        {
            float t = _state.InGameTime;

            if (_state.SceneIndex == 1)
            {
                DrawSpiral(t);
                return;
            }

            DrawOrrery(t);
        }

        private void DrawOrrery(float t)
        {
            DrawMesh(_sphere, Matrix4x4.CreateScale(0.62f), albedo: 1);

            var ringModel =
                Matrix4x4.CreateFromYawPitchRoll(t * 0.45f, t * 0.22f, 0) *
                Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f);
            DrawMesh(_torus, in ringModel, albedo: 0.95f);

            const float orbitRadius = 1.55f;
            Vector3 planetPos = new(MathF.Sin(t * 0.9f) * orbitRadius, 0.15f, MathF.Cos(t * 0.9f) * orbitRadius);
            var planetModel = Matrix4x4.CreateScale(0.22f) * Matrix4x4.CreateTranslation(planetPos);
            DrawMesh(_sphere, in planetModel, albedo: 0.75f);

            for (int i = 0; i < _asteroids.Length; i++)
            {
                var (angle, radius, height, scale) = _asteroids[i];
                float aTime = t * 0.25f + angle;
                Vector3 p = new(MathF.Sin(aTime) * radius, height, MathF.Cos(aTime) * radius);

                float spin = t * 1.4f + i * 0.23f;
                var model =
                    Matrix4x4.CreateScale(scale) *
                    Matrix4x4.CreateFromYawPitchRoll(spin, spin * 0.7f, spin * 0.5f) *
                    Matrix4x4.CreateTranslation(p);

                DrawMesh(_cube, model, albedo: 0.55f);
            }
        }

        private void DrawSpiral(float t)
        {
            const int spiralLength = 35;
            for (int i = 0; i < spiralLength; i++)
            {
                float k = i / (float)(spiralLength - 1);
                float a = t * 0.9f + k * 8.5f;
                float r = 0.25f + k * 1.6f;
                float y = (k - 0.5f) * 1.8f;

                Vector3 p = new(MathF.Sin(a) * r, y, MathF.Cos(a) * r);
                float s = 0.12f + (1f - k) * 0.06f;
                var model =
                    Matrix4x4.CreateScale(s) *
                    Matrix4x4.CreateFromYawPitchRoll(a * 1.1f, a * 0.6f, a * 0.9f) *
                    Matrix4x4.CreateTranslation(p);

                float albedo = 0.5f + k * 0.5f;
                DrawMesh(_cube, in model, albedo);
            }

            var coreModel = Matrix4x4.CreateFromYawPitchRoll(t * 0.4f, t * 0.55f, 0);
            DrawMesh(_torus, in coreModel, albedo: 0.8f);

            var subCoreModel = Matrix4x4.CreateScale(0.6f);
            DrawMesh(_sphere, in subCoreModel, albedo: 1);
        }

        private void DrawMesh(Mesh mesh, in Matrix4x4 model, float albedo)
        {
            var verts = mesh.GetVertices();
            var tris = mesh.GetTriangles();

            Span<(float x, float y, float z, float intensity)> triVerts = stackalloc (float, float, float, float)[3];

            for (int i = 0; i < tris.Length; i += 3)
            {
                var a0 = verts[tris[i]];
                var b0 = verts[tris[i + 1]];
                var c0 = verts[tris[i + 2]];

                Vector3 a = Vector3.Transform(new Vector3(a0.X, a0.Y, a0.Z), model);
                Vector3 b = Vector3.Transform(new Vector3(b0.X, b0.Y, b0.Z), model);
                Vector3 c = Vector3.Transform(new Vector3(c0.X, c0.Y, c0.Z), model);

                triVerts[0] = (a.X, a.Y, a.Z, albedo);
                triVerts[1] = (b.X, b.Y, b.Z, albedo);
                triVerts[2] = (c.X, c.Y, c.Z, albedo);

                _gl.DrawPrimitive(triVerts);
            }
        }

        private static (float angle, float radius, float height, float scale)[] CreateAsteroids(int count, int seed)
        {
            var rng = new Random(seed);
            var asteroids = new (float angle, float radius, float height, float scale)[count];
            for (int i = 0; i < asteroids.Length; i++)
            {
                float angle = (float)rng.NextDouble() * MathF.Tau;
                float radius = 0.95f + (float)rng.NextDouble() * 0.55f;
                float height = ((float)rng.NextDouble() * 2 - 1) * 0.15f;
                float scale = 0.05f + (float)rng.NextDouble() * 0.07f;
                asteroids[i] = (angle, radius, height, scale);
            }
            return asteroids;
        }
    }
}
