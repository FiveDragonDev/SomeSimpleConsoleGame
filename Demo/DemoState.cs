using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Extensions;
using System.Numerics;

namespace SomeSimpleConsoleGame.Demo
{
    public sealed class DemoState
    {
        public int BufferWidth { get; }
        public int BufferHeight { get; }

        public bool Paused { get; set; }
        public float FOV { get; set; } = MathUtils.HalfPi - MathUtils.Deg2Rad;
        public int SceneIndex { get; set; }

        public bool LockCamera { get; set; }
        public bool LightFollowsCamera { get; set; }

        public float InGameTime { get; set; }
        public float Time { get; set; }

        public (byte r, byte g, byte b) BackgroundRgb { get; set; } = (10, 7, 15);
        public (byte r, byte g, byte b) ForegroundRgb { get; set; } = (255, 255, 235);

        public Camera3D Camera;
        public Vector3 LightDirectionWorld = new Vector3(-0.4f, 0.85f, -0.2f).Normalized();

        public float Ambient = 0.1f;
        public float Diffuse = 0.88f;
        public float Specular = 0.25f;
        public float Shininess = 48f;

        public DemoState(int bufferWidth, int bufferHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferHeight);

            BufferWidth = bufferWidth;
            BufferHeight = bufferHeight;

            Camera = new Camera3D
            {
                Position = new(0, 0.35f, -3.3f),
                Yaw = 0f,
                Pitch = 0f,
                FovRadians = MathUtils.HalfPi - MathUtils.Deg2Rad,
                Near = 0.03f,
                Far = 20f,
                CharPixelAspect = 6f / 9,
            };
        }

        public string SceneName => SceneIndex switch
        {
            0 => "Orrery",
            1 => "Spiral",
            _ => "Unknown",
        };

        public Matrix4x4 GetViewProjection()
        {
            float aspect = (BufferWidth / (float)BufferHeight) * Camera.CharPixelAspect;
            var view = Camera.GetViewMatrix();
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(Camera.FovRadians, aspect, Camera.Near, Camera.Far);
            return view * proj;
        }
    }

    public struct Camera3D
    {
        public Vector3 Position;
        public float Yaw;
        public float Pitch;

        public float FovRadians;
        public float Near;
        public float Far;

        public float CharPixelAspect;

        public readonly Matrix4x4 GetViewMatrix() =>
            Matrix4x4.CreateLookAt(Position, Position + GetForward(), Vector3.UnitY);

        public readonly Vector3 GetForward()
        {
            float cp = MathF.Cos(Pitch);
            return Vector3.Normalize(new Vector3(
                MathF.Sin(Yaw) * cp,
                MathF.Sin(Pitch),
                MathF.Cos(Yaw) * cp
            ));
        }
        public readonly Vector3 GetRight() =>
            -Vector3.Normalize(Vector3.Cross(Vector3.UnitY, GetForward()));
    }
}
