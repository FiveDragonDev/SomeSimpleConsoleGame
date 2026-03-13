using System.Diagnostics;

namespace SomeSimpleConsoleGame
{
    public sealed class RenderSystem : IUpdateSystem
    {
        private readonly ConsoleRenderer _renderer;

        private readonly Shader _shader;
        private readonly GLContext _glContext;

        private Stopwatch _frameTimer;
        private int _fps = 0;
        private int _frameCount = 0;

        private readonly float _targetFrameTime;
        private long _nextFrameTicks;

        public RenderSystem(int width, int height, int targetFPS)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFPS);

            _renderer = new(width, height);

            _glContext = new(width, height);
            _shader = new(
@"
#version 440 core
layout(location = 0) in vec3 aPosition;
layout(location = 1) in float aColor;
out vec2 vCoord;
out float vColor;
void main() {
    gl_Position = vec4(aPosition, 1.0);
    vColor = aColor;
    vCoord = aPosition.xy;
}",
@"
#version 440 core
in vec2 vCoord;
in float vColor;
out float FragColor;
void main() {
    FragColor = pow(1.0 / (length(vCoord) + 1), 3);
}");
            _shader.Use();

            _targetFrameTime = 1f / targetFPS;
            _frameTimer = Stopwatch.StartNew();
        }

        public void Update(double deltaTime)
        {
            _glContext.DrawPrimitive([
                (-0.8f, -0.8f, 0f, 1),
                (0.8f,  0.8f, 0f, 1),
                (0.8f, -0.8f, 0f, 1),
            ]);
            _glContext.DrawPrimitive([
                (-0.8f, -0.8f, 0f, 1),
                (-0.8f, 0.8f, 0f, 1),
                (0.8f, 0.8f, 0f, 1),
            ]);

            _renderer.SetData(0, _glContext.Render());

            _renderer.SetCharsBatch(1, 1, $"{(deltaTime * 1000):f4} ms");
            _renderer.SetCharsBatch(1, 2, _fps.ToString());

            _renderer.SwapBuffers();
            var renderTask = _renderer.RenderAsync();

            _frameCount++;
            if (_frameTimer.Elapsed.TotalSeconds >= 1)
            {
                _fps = _frameCount;
                _frameCount = 0;
                _frameTimer.Restart();
            }

            long now = Stopwatch.GetTimestamp();
            long targetTicks = (long)(_targetFrameTime * Stopwatch.Frequency);
            _nextFrameTicks += targetTicks;

            renderTask.Wait();

            long sleepTicks = _nextFrameTicks - now;
            if (sleepTicks > 0)
            {
                int sleepMs = (int)(sleepTicks * 1000 / Stopwatch.Frequency);
                if (sleepMs > 0) Thread.Sleep(sleepMs);
            }
            else _nextFrameTicks = now;
        }
    }
}
