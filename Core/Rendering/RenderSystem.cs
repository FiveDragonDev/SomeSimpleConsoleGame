using System.Diagnostics;

using System.Globalization;

namespace SomeSimpleConsoleGame.Core.Rendering
{
    public sealed class RenderSystem : IUpdateSystem, IDisposable
    {
        private readonly ConsoleRenderer _renderer;
        private readonly IRenderContext _renderContext;
        private Task? _renderTask;

        private readonly bool _showStats;

        private readonly Stopwatch _frameTimer;
        private readonly int _targetFps;
        private readonly long _targetTicks;
        private int _fps;
        private int _frameCount;

        private long _nextFrameTicks;

        public RenderSystem(int width, int height, int targetFPS, IRenderContext renderContext, bool showStats = true)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetFPS);

            _renderer = new(width, height);
            _renderContext = renderContext;
            _showStats = showStats;

            TargetDeltaTime = 1f / targetFPS;
            _targetFps = targetFPS;
            _targetTicks = Stopwatch.Frequency / _targetFps;
            _frameTimer = Stopwatch.StartNew();
            _nextFrameTicks = Stopwatch.GetTimestamp();
        }

        public void Update(double deltaTime)
        {
            _renderTask?.GetAwaiter().GetResult();

            _renderContext.Render(_renderer);

            if (_showStats)
            {
                int pos = 0;
                Span<char> buffer = stackalloc char[64];

                double ms = deltaTime * 1000;
                ms.TryFormat(buffer[pos..], out int written, "F4", CultureInfo.InvariantCulture);
                pos += written;
                " ms | ".AsSpan().CopyTo(buffer[pos..]);
                pos += 6;

                _fps.TryFormat(buffer[pos..], out written, provider: CultureInfo.InvariantCulture);
                pos += written;
                buffer[pos++] = '/';
                _targetFps.TryFormat(buffer[pos..], out written, provider: CultureInfo.InvariantCulture);
                pos += written;
                " fps".AsSpan().CopyTo(buffer[pos..]);
                pos += 4;

                _renderer.SetCharsBatch(1, 1, buffer[..pos]);
            }

            _renderer.SwapBuffers();
            _renderTask = _renderer.RenderAsync();

            _frameCount++;
            if (_frameTimer.Elapsed.TotalSeconds >= 1)
            {
                _fps = _frameCount;
                _frameCount = 0;
                _frameTimer.Restart();
            }

            _nextFrameTicks += _targetTicks;
            long afterWork = Stopwatch.GetTimestamp();
            long sleepTicks = _nextFrameTicks - afterWork;
            if (sleepTicks > 0)
            {
                int sleepMs = (int)(sleepTicks * 1000 / Stopwatch.Frequency);
                if (sleepMs > 1) Thread.Sleep(sleepMs - 1);
                while (Stopwatch.GetTimestamp() < _nextFrameTicks)
                    Thread.SpinWait(16);
            }
            else _nextFrameTicks = afterWork;
        }

        public void Dispose()
        {
            _renderTask?.GetAwaiter().GetResult();
            _renderTask?.Dispose();

            _frameTimer.Stop();
            _renderer.Dispose();

            if (_renderContext is IDisposable disposable) disposable.Dispose();
        }
    }
}
