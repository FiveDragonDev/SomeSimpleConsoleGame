using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Extensions;
using SomeSimpleConsoleGame.Core.Physics;
using SomeSimpleConsoleGame.Core.Rendering;
using SomeSimpleConsoleGame.Core.World;
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
        private readonly World _world;

        private readonly Mesh _torus;
        private readonly Mesh _sphere;
        private readonly Mesh _cube;
        private readonly Mesh _plane;

        private readonly Vector4[] _pointLights = new Vector4[16];
        private readonly Vector4[] _spotLights = new Vector4[16];
        private readonly Vector4[] _spotLightDirections = new Vector4[16];

        private readonly (float angle, float radius, float height, float scale)[] _asteroids;
        private readonly Entity _physicsDemoEntity;
        private readonly Entity _physicsBallEntity;
        private readonly Entity _physicsFloorEntity;

        public DemoSceneSystem(GLContext gl, Shader shader, InputController input, DemoState state, World world)
        {
            _gl = gl ?? throw new ArgumentNullException(nameof(gl));
            _shader = shader ?? throw new ArgumentNullException(nameof(shader));
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _world = world ?? throw new ArgumentNullException(nameof(world));

            _torus = MeshPrimitives.CreateTorus(80, 28);
            _sphere = MeshPrimitives.CreateUvSphere(96, 64);
            _cube = MeshPrimitives.CreateCube();
            _plane = MeshPrimitives.CreateDoubleSidedPlane();

            _asteroids = CreateAsteroids(count: 256, seed: (int)Stopwatch.GetTimestamp());

            _physicsFloorEntity = _world.CreateEntity();
            var floorPosition = new Vector3(0, -0.05f, 0);
            _world.TryAdd(_physicsFloorEntity, new Transform { Position = floorPosition });
            _world.TryAdd(
                _physicsFloorEntity,
                new PhysicsBody(
                    bounds: AABB.CreateFromCenterAndSize(Vector3.Zero, new Vector3(8f, 0.1f, 8f)),
                    position: floorPosition,
                    velocity: Vector3.Zero,
                    mass: 1f,
                    isStatic: true)
                { Restitution = 0 });

            _physicsDemoEntity = _world.CreateEntity();
            var startPosition = new Vector3(0, 0.9f, 0);
            _world.TryAdd(_physicsDemoEntity, new Transform { Position = startPosition });
            _world.TryAdd(
                _physicsDemoEntity,
                new PhysicsBody(
                    bounds: AABB.CreateFromCenterAndSize(Vector3.Zero, new Vector3(0.22f)),
                    position: startPosition,
                    velocity: Vector3.Zero,
                    mass: 1f));

            _physicsBallEntity = _world.CreateEntity();
            var ballStart = new Vector3(-0.5f, 1.5f, 0);
            _world.TryAdd(_physicsBallEntity, new Transform { Position = ballStart });
            _world.TryAdd(
                _physicsBallEntity,
                new PhysicsBody(
                    bounds: new SphereBounds(Vector3.Zero, radius: 0.11f),
                    position: ballStart,
                    velocity: new Vector3(0.9f, 0, 0),
                    mass: 0.65f)
                { Restitution = 0.25f });

            _pointLights[0] = new(1, 3, 1, 2);
            _pointLights[1] = new(-1, -3, -1, 1);

            _shader.Uniform("uScreenSize", _state.BufferWidth, _state.BufferHeight);
            _shader.Uniform("uAmbient", _state.Ambient);
            _shader.Uniform("uDiffuse", _state.Diffuse);
            _shader.Uniform("uSpecular", _state.Specular);
            _shader.Uniform("uShininess", _state.Shininess);
            _shader.Uniform("uFog", 5, 10);
        }

        public void Update(float deltaTime)
        {
            HandleInput(deltaTime);

            _state.Time += deltaTime;
            if (!_state.Paused) _state.InGameTime += deltaTime;
            if (_state.LockCamera) AutoPilotCamera();

            // if (_state.LightFollowsCamera) _state.LightDirectionWorld = Vector3.Normalize(_state.Camera.GetForward() * -1f + new Vector3(-0.2f, 0.45f, 0.1f));
            _state.LightDirectionWorld = Vector3.Normalize(new(
                    MathUtils.QSin(_state.InGameTime * 0.35f) * 0.6f - 0.25f,
                    0.85f,
                    MathUtils.QCos(_state.InGameTime * 0.35f) * 0.6f - 0.15f));

            _state.Camera.FovRadians = Math.Clamp(_state.FOV, MathUtils.Deg2Rad, MathUtils.Pi - MathUtils.Deg2Rad);

            var viewProj = _state.GetViewProjection();
            var camPos = _state.Camera.Position;
            var lightDir = _state.LightDirectionWorld;

            _shader.Use();
            _shader.Uniform("uViewProj", viewProj);
            _shader.Uniform("uCameraPos", camPos.X, camPos.Y, camPos.Z);
            _shader.Uniform("uLightDir", lightDir.X, lightDir.Y, lightDir.Z);
            _shader.Uniform("uTime", _state.Time);

            float light = 0;
            if (_state.LightFollowsCamera)
            {
                light = 6;
                _spotLightDirections[0] = new(_state.Camera.GetForward(), MathUtils.Pi / 5f);
            }
            _spotLights[0] = new(camPos, light);

            _shader.Uniform("uPointLights", 16, _pointLights);
            _shader.Uniform("uSpotLights", 16, _spotLights);
            _shader.Uniform("uSpotLightDirections", 16, _spotLightDirections);

            DrawScene();
            // DrawPhysicsDemo();
        }

        private void AutoPilotCamera()
        {
            float t = _state.InGameTime;
            _state.Camera.Position = new Vector3(
                MathUtils.QSin(t * 0.25f) * 3.2f,
                0.7f + MathUtils.QSin(t * 0.18f) * 0.35f,
                MathUtils.QCos(t * 0.25f) * 3.2f);

            Vector3 toCenter = Vector3.Normalize(-_state.Camera.Position);
            _state.Camera.Yaw = MathF.Atan2(toCenter.X, toCenter.Z);
            _state.Camera.Pitch = MathF.Asin(toCenter.Y);
        }

        private void HandleInput(float dt)
        {
            if (_input.IsKeyPressed(ConsoleKey.P) || _input.IsKeyPressed(ConsoleKey.Spacebar)) _state.Paused = !_state.Paused;
            if (_input.IsKeyPressed(ConsoleKey.C)) _state.LockCamera = !_state.LockCamera;
            if (_input.IsKeyPressed(ConsoleKey.L)) _state.LightFollowsCamera = !_state.LightFollowsCamera;

            if (_input.IsKeyPressed(ConsoleKey.F) && _world.TryGet(_physicsDemoEntity, out PhysicsBody? body))
                body!.ApplyImpulse(new Vector3(0, 1.25f, 0));

            if (_input.IsKeyPressed(ConsoleKey.G) && _world.TryGet(_physicsBallEntity, out PhysicsBody? ballBody))
                ballBody!.ApplyImpulse(new Vector3(0, 1.25f, 0));

            if (_input.IsKeyPressed(ConsoleKey.R)) ResetPhysicsDemo();

            if (_input.IsKeyPressed(ConsoleKey.D1)) _state.SceneIndex = 0;
            if (_input.IsKeyPressed(ConsoleKey.D2)) _state.SceneIndex = 1;

            if (_input.IsKeyPressed(ConsoleKey.B))
            {
                _state.BackgroundRgb = _state.BackgroundRgb switch
                {
                    (10, 7, 15) => (16, 9, 15),
                    (16, 9, 15) => (10, 7, 15),
                    _ => throw new NotImplementedException(),
                };
                _state.ForegroundRgb = _state.ForegroundRgb switch
                {
                    (255, 255, 235) => (255, 235, 215),
                    (255, 235, 215) => (255, 255, 235),
                    _ => throw new NotImplementedException(),
                };
            }

            const float zoomSpeed = 250;
            int zoomInput = 0;
            if (_input.IsKeyDown(ConsoleKey.OemMinus)) zoomInput = 1;
            if (_input.IsKeyDown(ConsoleKey.OemPlus)) zoomInput += -1;
            _state.FOV += zoomInput * dt * MathUtils.Deg2Rad * zoomSpeed;

            if (_state.LockCamera) return;

            const float lookSpeed = 1;
            Vector3 deltaView = Vector3.Zero;
            if (_input.IsKeyDown(ConsoleKey.LeftArrow)) deltaView.X = 1;
            if (_input.IsKeyDown(ConsoleKey.RightArrow)) deltaView.X -= 1;
            if (_input.IsKeyDown(ConsoleKey.UpArrow)) deltaView.Y = 1;
            if (_input.IsKeyDown(ConsoleKey.DownArrow)) deltaView.Y -= 1;
            deltaView.Normalize();
            deltaView *= lookSpeed * dt;

            _state.Camera.Yaw += deltaView.X;
            _state.Camera.Pitch = Math.Clamp(_state.Camera.Pitch + deltaView.Y,
                -MathUtils.Pi / 2 + MathUtils.Deg2Rad, MathUtils.Pi / 2 - MathUtils.Deg2Rad);

            float speed = MathUtils.Sqrt2;
            if (_input.HasModifiers(ConsoleModifiers.Shift)) speed *= 2;

            Vector3 moveInput = Vector3.Zero;
            if (_input.IsKeyDown(ConsoleKey.W)) moveInput.Z = 1;
            if (_input.IsKeyDown(ConsoleKey.S)) moveInput.Z -= 1;
            if (_input.IsKeyDown(ConsoleKey.D)) moveInput.X = 1;
            if (_input.IsKeyDown(ConsoleKey.A)) moveInput.X -= 1;
            if (_input.IsKeyDown(ConsoleKey.E)) moveInput.Y = 1;
            if (_input.IsKeyDown(ConsoleKey.Q)) moveInput.Y -= 1;

            if (moveInput != Vector3.Zero)
            {
                moveInput.Normalize();
                Vector3 forward = _state.Camera.GetForward() * moveInput.Z;
                Vector3 right = _state.Camera.GetRight() * moveInput.X;
                Vector3 up = Vector3.UnitY * moveInput.Y;

                _state.Camera.Position += (forward + right + up) * (speed * dt);
            }
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

        private void DrawPhysicsDemo()
        {
            if (!_world.TryGet(_physicsDemoEntity, out Transform transform)) return;

            var rotation = transform.Rotation;
            var model =
                Matrix4x4.CreateFromYawPitchRoll(rotation.Y, rotation.X, rotation.Z) *
                Matrix4x4.CreateTranslation(transform.Position);

            DrawMesh(_cube, in model, albedo: 1);

            if (_world.TryGet(_physicsBallEntity, out transform))
            {
                var ballModel =
                    Matrix4x4.CreateScale(0.22f) *
                    Matrix4x4.CreateTranslation(transform.Position);
                DrawMesh(_sphere, in ballModel, albedo: 0.85f);
            }

            if (_world.TryGet(_physicsFloorEntity, out transform))
            {
                var floorModel =
                    Matrix4x4.CreateScale(8f, 1f, 8f) *
                    Matrix4x4.CreateTranslation(transform.Position);
                DrawMesh(_plane, in floorModel, albedo: 0.3f);
            }
        }

        private void ResetPhysicsDemo()
        {
            if (!_world.TryGet(_physicsDemoEntity, out Transform transform)) return;
            if (!_world.TryGet(_physicsDemoEntity, out PhysicsBody? body)) return;

            transform.Position = new Vector3(0, 1, 0);
            transform.Rotation = new Vector3(0, 0, 0);
            _world.TrySet(_physicsDemoEntity, in transform);
            body!.UpdateKinematics(new PhysicsBody.Kinematics(transform.Position, Vector3.Zero, Vector3.Zero, transform.Rotation, Vector3.Zero));

            if (_world.TryGet(_physicsBallEntity, out transform) && _world.TryGet(_physicsBallEntity, out PhysicsBody? ballBody))
            {
                transform.Position = new Vector3(-0.5f, 1.5f, 0);
                transform.Rotation = Vector3.Zero;
                _world.TrySet(_physicsBallEntity, in transform);
                ballBody!.UpdateKinematics(new PhysicsBody.Kinematics(transform.Position, new Vector3(0.9f, 0, 0), Vector3.Zero, Vector3.Zero, Vector3.Zero));
            }
        }

        private void DrawOrrery(float t)
        {
            DrawMesh(_sphere, Matrix4x4.CreateScale(0.62f), albedo: 1f);

            var ringModel =
                Matrix4x4.CreateFromYawPitchRoll(t * 0.45f, t * 0.22f, 0) *
                Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, MathUtils.Pi / 2f);
            DrawMesh(_torus, in ringModel, albedo: 0.95f);

            const float orbitRadius = 1.55f;
            Vector3 planetPos = new(MathUtils.QSin(t * 0.9f) * orbitRadius, 0.15f, MathUtils.QCos(t * 0.9f) * orbitRadius);
            var planetModel = Matrix4x4.CreateScale(0.22f) * Matrix4x4.CreateTranslation(planetPos);
            DrawMesh(_sphere, in planetModel, albedo: 0.75f);

            for (int i = 0; i < _asteroids.Length; i++)
            {
                var (angle, radius, height, scale) = _asteroids[i];
                float aTime = t * 0.25f + angle;
                Vector3 p = new(MathUtils.QSin(aTime) * radius, height, MathUtils.QCos(aTime) * radius);

                float spin = t * 1.4f + i * 0.23f;
                var model =
                    Matrix4x4.CreateScale(scale) *
                    Matrix4x4.CreateFromYawPitchRoll(spin, spin * 0.7f, spin * 0.5f) *
                    Matrix4x4.CreateTranslation(p);

                DrawMesh(_cube, model, albedo: 0.5f);
            }
        }

        private void DrawSpiral(float t)
        {
            const int spiralLength = 48;
            for (int i = 0; i < spiralLength; i++)
            {
                float k = i / (float)(spiralLength - 1);
                float a = t * 0.9f + k * 8.5f;
                float r = 0.25f + k * 1.6f;
                float y = (k - 0.5f) * 1.8f;

                Vector3 p = new(MathUtils.QSin(a) * r, y, MathUtils.QCos(a) * r);
                float s = 0.12f + (1f - k) * 0.06f;
                var model =
                    Matrix4x4.CreateScale(s) *
                    Matrix4x4.CreateFromYawPitchRoll(a * 1.1f, a * 0.6f, a * 0.9f) *
                    Matrix4x4.CreateTranslation(p);

                float albedo = 0.35f + k * 0.5f;
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

                var a = Vector3.Transform(new(a0.X, a0.Y, a0.Z), model);
                var b = Vector3.Transform(new(b0.X, b0.Y, b0.Z), model);
                var c = Vector3.Transform(new(c0.X, c0.Y, c0.Z), model);

                triVerts[0] = (a.X, a.Y, a.Z, albedo);
                triVerts[1] = (b.X, b.Y, b.Z, albedo);
                triVerts[2] = (c.X, c.Y, c.Z, albedo);

                _gl.DrawTriangles(triVerts);
            }
        }

        private static (float angle, float radius, float height, float scale)[] CreateAsteroids(int count, int seed)
        {
            Random random = new(seed);
            var asteroids = new (float angle, float radius, float height, float scale)[count];
            for (int i = 0; i < asteroids.Length; i++)
            {
                float angle = (float)random.NextDouble() * MathUtils.DoublePi;
                float radius = 1f + (float)random.NextDouble();
                float height = ((float)random.NextDouble() * 2 - 1) * 0.3f;
                float scale = 0.05f + (float)random.NextDouble() * 0.15f;
                asteroids[i] = (angle, radius, height, scale);
            }
            return asteroids;
        }
    }
}
