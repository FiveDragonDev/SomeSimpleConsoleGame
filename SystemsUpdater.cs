using System.Buffers;
using System.Diagnostics;

namespace SomeSimpleConsoleGame
{
    public interface IUpdateSystem
    {
        bool IsReady() => true;

        void Update(double deltaTime);
    }
    public sealed class SystemsUpdater
    {
        private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;

        private int _systemCount;
        private (IUpdateSystem system, byte priority, long lastCallTicks)[] _systems;

        public SystemsUpdater()
        {
            _systems = ArrayPool<(IUpdateSystem, byte, long)>.Shared.Rent(4);
        }
        ~SystemsUpdater()
        {
            ArrayPool<(IUpdateSystem, byte, long)>.Shared.Return(_systems, true);
        }

        public void Update()
        {
            var now = Stopwatch.GetTimestamp();
            for (int i = 0; i < _systemCount; i++)
            {
                var (system, _, lastCallTicks) = _systems[i];

                if (!system.IsReady()) continue;

                system.Update((now - lastCallTicks) * TicksToSeconds);

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

            Array.Sort(_systems, 0, _systemCount, Comparer<(IUpdateSystem, byte priority, long)>.Create(static (a, b) => b.priority.CompareTo(a.priority)));
        }
    }
}
