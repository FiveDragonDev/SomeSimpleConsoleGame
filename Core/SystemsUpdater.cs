using System.Buffers;
using System.Diagnostics;

namespace SomeSimpleConsoleGame.Core
{
    public interface IUpdateSystem
    {
        bool IsReady() => true;

        void Update(float deltaTime);
    }
    public sealed class SystemsUpdater : IDisposable
    {
        private static readonly float TicksToSeconds = 1f / Stopwatch.Frequency;
        private static readonly Comparer<(IUpdateSystem system, byte priority, long lastCallTicks)> PriorityComparer =
            Comparer<(IUpdateSystem, byte priority, long)>.Create(static (a, b) => b.priority.CompareTo(a.priority));

        private int _systemCount;
        private (IUpdateSystem system, byte priority, long lastCallTicks)[] _systems;

        public SystemsUpdater() => _systems = ArrayPool<(IUpdateSystem, byte, long)>.Shared.Rent(4);

        public void Update()
        {
            var now = Stopwatch.GetTimestamp();
            for (int i = 0; i < _systemCount; i++)
            {
                var (system, _, lastCallTicks) = _systems[i];

                if (!system.IsReady()) continue;

                var deltaTime = (now - lastCallTicks) * TicksToSeconds;
                system.Update(deltaTime);

                _systems[i].lastCallTicks = now;
            }
        }

        public void AddSystem(IUpdateSystem system, byte priority = 0)
        {
            if (_systemCount >= _systems.Length)
            {
                var newArray = ArrayPool<(IUpdateSystem, byte, long)>.Shared.Rent(_systems.Length * 2);
                Array.Copy(_systems, newArray, _systems.Length);
                ArrayPool<(IUpdateSystem, byte, long)>.Shared.Return(_systems, true);
                _systems = newArray;
            }

            _systems[_systemCount++] = (system, priority, Stopwatch.GetTimestamp());

            Array.Sort(_systems, 0, _systemCount, PriorityComparer);
        }

        public void Dispose()
        {
            var systems = _systems;
            int count = _systemCount;
            _systems = [];
            _systemCount = 0;
            if (systems.Length != 0)
            {
                foreach (var (system, _, _) in systems.AsSpan(0, count))
                {
                    if (system is IDisposable disposable) disposable.Dispose();
                }
                ArrayPool<(IUpdateSystem, byte, long)>.Shared.Return(systems, true);
            }

            GC.SuppressFinalize(this);
        }
    }
}
