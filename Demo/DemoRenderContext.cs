using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Extensions;
using SomeSimpleConsoleGame.Core.Rendering;
using System.Diagnostics;
using System.Numerics;

namespace SomeSimpleConsoleGame.Demo
{
    public sealed class DemoRenderContext : IRenderContext, IDisposable
    {
        public GLContext GL { get; }
        public Shader Shader { get; }

        private readonly DateTime _startTime;

        private readonly DemoState _state;
        private readonly Vector3[] _starsWorld;

        public DemoRenderContext(DemoState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            GL = new(state.BufferWidth, state.BufferHeight);
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

uniform vec2 uScreenSize;
uniform vec3 uCameraPos;
uniform vec3 uLightDir;
uniform float uAmbient;
uniform float uDiffuse;
uniform float uSpecular;
uniform float uShininess;
uniform vec2 uFog;
uniform float uTime;

uniform vec4 uPointLights[16];
uniform vec4 uSpotLights[16];
uniform vec4 uSpotLightDirections[16];

out float FragColor;

float calc_global_light(vec3 N, vec3 V) {
    vec3 L_dir = normalize(uLightDir);
    float ndotl_dir = max(dot(N, L_dir), 0.0);
    vec3 H_dir = normalize(L_dir + V);
    float ndoth_dir = max(dot(N, H_dir), 0.0);
    float spec_dir = pow(ndoth_dir, uShininess) * uSpecular;

    return (uDiffuse * ndotl_dir * vAlbedo) + spec_dir;
}
float calc_point_light(vec4 light, vec3 N, vec3 V) {
    if (light.w <= 0) return 0;

    vec3 toLight = light.xyz - vWorldPos;
    float dist = length(toLight);
    vec3 L_point = toLight / dist;
    float attenuation = 1.0 / (1.0 + dist * dist);
    float intensity = light.w;

    float ndotl_point = max(dot(N, L_point), 0.0);
    vec3 H_point = normalize(L_point + V);
    float ndoth_point = max(dot(N, H_point), 0.0);
    float spec_point = pow(ndoth_point, uShininess) * uSpecular;

    return (uDiffuse * ndotl_point * vAlbedo + spec_point) * intensity * attenuation;
}
float calc_spot_light(vec4 light, vec4 dir, vec3 N, vec3 V) {
    if (light.w <= 0) return 0;

    vec3 toLight = light.xyz - vWorldPos;
    float dist = length(toLight);
    vec3 L_point = toLight / dist;
    
    float attenuation = 1.0 / (1.0 + dist * dist);
    float intensity = light.w;

    vec3 spotDir = normalize(dir.xyz);
    float spotDot = dot(-L_point, spotDir);

    const float innerCutoff = cos(dir.w * 0.66);
    const float outerCutoff = cos(dir.w);
    float spotFactor = smoothstep(outerCutoff, innerCutoff, spotDot);

    float ndotl_point = max(dot(N, L_point), 0.0);
    vec3 H_point = normalize(L_point + V);
    float ndoth_point = max(dot(N, H_point), 0.0);
    float spec_point = pow(ndoth_point, uShininess) * uSpecular;

    float pointLight = (uDiffuse * ndotl_point * vAlbedo + spec_point) * intensity * attenuation;
    
    return pointLight * spotFactor;
}

float postprocessing(float value) {
    vec2 normUV = gl_FragCoord.xy / uScreenSize * 2 - 1;
    value *= 1.0 + (sin(uTime / 6.0) * 0.01);
    value = pow(value, 1.55);
    value += pow(value, 15);

    return value;
}

void main() {
    vec3 V = normalize(uCameraPos - vWorldPos);
    vec3 dx = dFdx(vWorldPos);
    vec3 dy = dFdy(vWorldPos);
    vec3 N = normalize(cross(dx, dy));
    N = faceforward(N, -V, N);
    
    float value = 0;
    
// GLOBAL LIGHT
    // value += calc_global_light(N, V);

// POINT LIGHT
    for (int i = 0; i < 16; i++) {
        // value += calc_point_light(uPointLights[i], N, V);
    }

// SPOT LIGHT
    for (int i = 0; i < 16; i++) {
        value += calc_spot_light(uSpotLights[i], uSpotLightDirections[i], N, V);
    }

    value += uAmbient;

    float distFrag = length(uCameraPos - vWorldPos);
    float fog = clamp((distFrag - uFog.x) / max(uFog.y - uFog.x, 0.0001), 0.0, 1.0);
    value *= 1.0 - fog;

    value = postprocessing(value);

    FragColor = clamp(value, 0.0, 1.0);
}");
            GL.SetShader(Shader);

            _starsWorld = CreateStars(count: 192, seed: (int)Stopwatch.GetTimestamp());
            _startTime = DateTime.Now;
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
                w.Z += MathUtils.QCos(_state.InGameTime * 0.1f + i * MathUtils.Sqrt2Over2);

                if (!TryProjectToNdc(in w, in viewProj, out var ndc)) continue;
                if (ndc.X < -1 || ndc.X > 1 || ndc.Y < -1 || ndc.Y > 1) continue;

                int x = MathUtils.RoundToInt((ndc.X * 0.5f + 0.5f) * (target.Width - 1));
                int y = MathUtils.RoundToInt((1f - (ndc.Y * 0.5f + 0.5f)) * (target.Height - 1));

                if (!target.TryGetIndex(x, y, out int index)) continue;
                if (buffer[index] != ' ') continue;

                float depth01 = (ndc.Z + 1) * 0.5f;
                char ch = depth01 switch
                {
                    < 0.75f => '*',
                    < 0.9f => '+',
                    _ => '.',
                };

                buffer[index] = ch;
            }
        }

        private void DrawOverlay(ICharRenderTarget target)
        {
            target.WriteRow(1, 2, $"Console 3D Demo  |  Scene: {_state.SceneName}  | {_state.BufferWidth}x{_state.BufferHeight} px |  Esc: quit", true);
            if (_state.Paused) target.WriteRow(1, 3, "[PAUSED]  (P/Space to resume)", true);

            Span<char> buffer = target.GetBackBuffer();

            StringUtils.WriteTimer((DateTime.Now - _startTime), buffer.Slice(GetBottomCenter(0, 6), 20));

            float percent = (MathUtils.QSin(_state.Time) + 1f) * 0.5f;
            StringUtils.WriteProgressBar(percent, buffer.Slice(GetBottomCenter(-14, 4), 25), '=', '.');
            string percentString = MathUtils.RoundToInt(percent * 100).ToString("000");
            for (int i = 0; i < 3; i++)
            {
                buffer[GetBottomCenter(12 + i, 4)] = percentString[i];
            }
            buffer[GetBottomCenter(15, 4)] = '%';

            DrawCrosshair(buffer);

            int GetCenterCenter(int x, int y) => (_state.BufferWidth * (_state.BufferHeight + 1) / 2) + (_state.BufferWidth * y) + x;
            int GetBottomCenter(int x, int y) => (_state.BufferWidth * _state.BufferHeight + _state.BufferWidth / 2) - (_state.BufferWidth * y) + x;

            void DrawCrosshair(Span<char> buffer)
            {
                buffer[GetCenterCenter(0, 0)] = '*';

                if (buffer[GetCenterCenter(-2, 0)] == ' ') buffer[GetCenterCenter(-2, 0)] = '|';
                if (buffer[GetCenterCenter(-2, 1)] == ' ') buffer[GetCenterCenter(-2, 1)] = '/';
                if (buffer[GetCenterCenter(0, 1)] == ' ') buffer[GetCenterCenter(0, 1)] = '—';
                if (buffer[GetCenterCenter(2, 1)] == ' ') buffer[GetCenterCenter(2, 1)] = '\\';

                if (buffer[GetCenterCenter(-2, -1)] == ' ') buffer[GetCenterCenter(-2, -1)] = '\\';
                if (buffer[GetCenterCenter(0, -1)] == ' ') buffer[GetCenterCenter(0, -1)] = '—';
                if (buffer[GetCenterCenter(2, -1)] == ' ') buffer[GetCenterCenter(2, -1)] = '/';
                if (buffer[GetCenterCenter(2, 0)] == ' ') buffer[GetCenterCenter(2, 0)] = '|';
            }
        }

        private static bool TryProjectToNdc(in Vector3 world, in Matrix4x4 viewProj, out Vector3 ndc)
        {
            Vector4 clip = Vector4.Transform(new Vector4(world, 1f), viewProj);
            if (!float.IsFinite(clip.W) || clip.W <= 1e-4f)
            {
                ndc = Vector3.NaN;
                return false;
            }

            float invW = 1f / clip.W;
            ndc = new Vector3(clip.X * invW, clip.Y * invW, clip.Z * invW);
            return float.IsFinite(ndc.X) && float.IsFinite(ndc.Y) && float.IsFinite(ndc.Z);
        }

        private static Vector3[] CreateStars(int count, int seed)
        {
            Random rng = new(seed);
            var stars = new Vector3[count];
            for (int i = 0; i < stars.Length; i++)
            {
                float x = (rng.NextSingle() * 2f) - 1f;
                float y = (rng.NextSingle() * 2f) - 1f;
                float z = (rng.NextSingle() * 2f) - 1f;

                stars[i] = new Vector3(x, y, z) * 20f;
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
