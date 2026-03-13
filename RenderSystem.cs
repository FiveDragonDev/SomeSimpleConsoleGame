using System.Diagnostics;

namespace SomeSimpleConsoleGame
{
    public sealed class RenderSystem : IUpdateSystem
    {
        private readonly ConsoleRenderer _renderer;
        private readonly IRenderContext _renderContext;

        private readonly Stopwatch _frameTimer;
        private int _fps = 0;
        private int _frameCount = 0;

        private readonly float _targetFrameTime;
        private long _nextFrameTicks;

        public RenderSystem(int width, int height, int targetFPS, IRenderContext renderContext)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFPS);

            _renderer = new(width, height);
            _renderContext = renderContext;

            _targetFrameTime = 1f / targetFPS;
            _frameTimer = Stopwatch.StartNew();
        }

        public void Update(double deltaTime)
        {
            var (startIndex, data) = _renderContext.Render();
            _renderer.SetData(startIndex, data);

#if DEBUG
            _renderer.SetCharsBatch(1, 1, $"{(deltaTime * 1000):f4} ms");
            _renderer.SetCharsBatch(1, 2, _fps.ToString() + " fps");
#endif

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
