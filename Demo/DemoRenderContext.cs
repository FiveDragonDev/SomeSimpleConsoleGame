using SomeSimpleConsoleGame.Core.Rendering;
using System.Numerics;

namespace SomeSimpleConsoleGame.Demo
{
    public sealed class DemoRenderContext : IRenderContext, IDisposable
    {
        public GLContext GL { get; }
        public Shader Shader { get; }

        private readonly DemoState _state;
        private readonly Vector3[] _starsWorld;

        public DemoRenderContext(DemoState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            GL = new GLContext(state.BufferWidth, state.BufferHeight);
            Shader = new(
@"
#version 440 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in float aAlbedo;

uniform mat4 uViewProj;

out vec3 vWorldPos;
out float vAlbedo;

void main() {
    vWorldPos = aPosition;
    vAlbedo = aAlbedo;
    gl_Position = vec4(aPosition, 1.0) * uViewProj;
}",
@"
#version 440 core
in vec3 vWorldPos;
in float vAlbedo;

uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform float uAmbient;
uniform float uDiffuse;
uniform float uSpecular;
uniform float uShininess;
uniform vec2 uFog;
uniform float uTime;

out float FragColor;

void main() {
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 dx = dFdx(vWorldPos);
    vec3 dy = dFdy(vWorldPos);
    vec3 N = normalize(cross(dx, dy));
    N = faceforward(N, -V, N);

    vec3 L = normalize(uLightDir);
    float ndotl = max(dot(N, L), 0.0);

    vec3 H = normalize(L + V);
    float ndoth = max(dot(N, H), 0.0);
    float spec = pow(ndoth, uShininess) * uSpecular;

    float value = uAmbient + (uDiffuse * ndotl * vAlbedo) + spec;

    float dist = length(uCameraPos - vWorldPos);
    float fog = clamp((dist - uFog.x) / max(uFog.y - uFog.x, 0.0001), 0.0, 1.0);
    value *= 1.0 - fog * 0.55;
    value *= 1.0 + (sin(uTime / 2.0) * 0.01);

    FragColor = clamp(pow(value, 1.4), 0.0, 1.0);
}");
            GL.SetShader(Shader);

            _starsWorld = CreateStars(count: 3, seed: 0xC0FFEE);
        }

        public void Render(ICharRenderTarget target)
        {
            if (target is ConsoleRenderer renderer)
            {
                renderer.SetBackgroundColor(_state.BackgroundRgb.r, _state.BackgroundRgb.g, _state.BackgroundRgb.b);
                renderer.SetForegroundColor(_state.ForegroundRgb.r, _state.ForegroundRgb.g, _state.ForegroundRgb.b);
            }

            GL.Render(target);
            DrawStars(target);
            DrawOverlay(target);

            target.MarkDirty(0, target.Area);
        }

        private void DrawStars(ICharRenderTarget target)
        {
            Matrix4x4 viewProj = _state.GetViewProjection();
            var buffer = target.GetBackBuffer();

            for (int i = 0; i < _starsWorld.Length; i++)
            {
                Vector3 w = _starsWorld[i];
                w.Z += (MathF.Sin(_state.InGameTime * 0.12f + i) * 0.25f);

                if (!TryProjectToNdc(in w, in viewProj, out var ndc)) continue;
                if (ndc.X < -1 || ndc.X > 1 || ndc.Y < -1 || ndc.Y > 1) continue;

                int x = (int)MathF.Round((ndc.X * 0.5f + 0.5f) * (target.Width - 1));
                int y = (int)MathF.Round((1f - (ndc.Y * 0.5f + 0.5f)) * (target.Height - 1));

                if (!target.TryGetIndex(x, y, out int index)) continue;
                if (buffer[index] != ' ') continue;

                float depth01 = (ndc.Z * 0.5f + 0.5f);
                char ch = depth01 switch
                {
                    < 0.9f => '.',
                    < 0.99f => '+',
                    _ => '*',
                };

                buffer[index] = ch;
            }
        }

        private void DrawOverlay(ICharRenderTarget target)
        {
            target.WriteRow(1, 2, $"Console 3D Demo  |  Scene: {_state.SceneName}  |  Esc: quit", true);
            if (_state.Paused) target.WriteRow(1, 3, "[PAUSED]  (P/Space to resume)", true);
        }

        private static bool TryProjectToNdc(in Vector3 world, in Matrix4x4 viewProj, out Vector3 ndc)
        {
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), viewProj);
            if (!float.IsFinite(clip.W) || clip.W <= 0.0001f)
            {
                ndc = default;
                return false;
            }

            float invW = 1f / clip.W;
            ndc = new Vector3(clip.X * invW, clip.Y * invW, clip.Z * invW);
            return float.IsFinite(ndc.X) && float.IsFinite(ndc.Y) && float.IsFinite(ndc.Z);
        }

        private static Vector3[] CreateStars(int count, int seed)
        {
            var rng = new Random(seed);
            var stars = new Vector3[count];
            for (int i = 0; i < stars.Length; i++)
            {
                float x = (float)(rng.NextDouble() * 2 - 1) * 18f;
                float y = (float)(rng.NextDouble() * 2 - 1) * 10f;
                float z = (float)(rng.NextDouble() * 2 - 1) * 18f;

                z = MathF.Abs(z) + 3.5f;

                stars[i] = new Vector3(x, y, z);
            }
            return stars;
        }

        public void Dispose()
        {
            GL.MakeCurrent();
            Shader.Dispose();
            GL.Dispose();
        }
    }
}
