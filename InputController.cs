using System.Diagnostics;

namespace SomeSimpleConsoleGame
{
    public sealed class InputController : IDisposable
    {
        private record struct KeyState(long FirstPressTimestamp, long LastPressTimestamp, bool IsPressed);

        private readonly Dictionary<int, KeyState> _keyStates = [];

        private readonly HashSet<int> _pressedThisFrame = [];
        private readonly HashSet<int> _releasedThisFrame = [];
        private readonly HashSet<int> _downKeys = [];

        private readonly List<int> _releaseCheckScratch = [];

        private readonly long _repeatThresholdTicks;
        private readonly long _releaseThresholdTicks;
        private readonly long _frequency = Stopwatch.Frequency;

        public InputController(TimeSpan? repeatThreshold = null, TimeSpan? releaseThreshold = null)
        {
            repeatThreshold ??= TimeSpan.FromMilliseconds(100);
            releaseThreshold ??= TimeSpan.FromMilliseconds(200);

            _repeatThresholdTicks = (long)(repeatThreshold.Value.TotalSeconds * _frequency);
            _releaseThresholdTicks = (long)(releaseThreshold.Value.TotalSeconds * _frequency);
        }

        private static int EncodeKey(ConsoleKeyInfo keyInfo) => EncodeKey(keyInfo.Key, keyInfo.Modifiers);
        private static int EncodeKey(ConsoleKey key, ConsoleModifiers modifiers = 0)
        {
            int code = (int)key;
            if (modifiers.HasFlag(ConsoleModifiers.Shift)) code |= 1 << 16;
            if (modifiers.HasFlag(ConsoleModifiers.Alt)) code |= 1 << 17;
            if (modifiers.HasFlag(ConsoleModifiers.Control)) code |= 1 << 18;
            return code;
        }

        public void PollEvents()
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            long now = Stopwatch.GetTimestamp();

            while (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(true);
                int key = EncodeKey(keyInfo);

                if (_keyStates.TryGetValue(key, out var state))
                {
                    long timeSinceLast = now - state.LastPressTimestamp;
                    if (timeSinceLast > _repeatThresholdTicks)
                    {
                        _pressedThisFrame.Add(key);
                    }

                    state.LastPressTimestamp = now;
                    state.IsPressed = true;
                }
                else
                {
                    state = new(now, now, true);
                    _pressedThisFrame.Add(key);
                }
                _keyStates[key] = state;
                _downKeys.Add(key);
            }

            _releaseCheckScratch.Clear();
            foreach (var key in _downKeys)
            {
                var state = _keyStates[key];
                if (state.IsPressed && (now - state.LastPressTimestamp) > _releaseThresholdTicks)
                {
                    _releaseCheckScratch.Add(key);
                }
            }

            for (int i = 0; i < _releaseCheckScratch.Count; i++)
            {
                int key = _releaseCheckScratch[i];
                var state = _keyStates[key];
                state.IsPressed = false;
                _keyStates[key] = state;
                _downKeys.Remove(key);
                _releasedThisFrame.Add(key);
            }
        }

        public bool IsKeyDown(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _keyStates.TryGetValue(EncodeKey(key, modifiers), out var state) && state.IsPressed;
        public bool IsKeyUp(ConsoleKey key, ConsoleModifiers modifiers = 0) => !IsKeyDown(key, modifiers);
        public bool IsKeyPressed(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _pressedThisFrame.Contains(EncodeKey(key, modifiers));
        public bool IsKeyReleased(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _releasedThisFrame.Contains(EncodeKey(key, modifiers));

        public TimeSpan GetKeyHoldDuration(ConsoleKey key, ConsoleModifiers modifiers = 0)
        {
            int code = EncodeKey(key, modifiers);
            if (_keyStates.TryGetValue(code, out var state) && state.IsPressed)
            {
                long now = Stopwatch.GetTimestamp();
                long durationTicks = now - state.FirstPressTimestamp;
                return TimeSpan.FromSeconds((double)durationTicks / _frequency);
            }
            return TimeSpan.Zero;
        }

        public long GetKeyLastPressTimestamp(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _keyStates.TryGetValue(EncodeKey(key, modifiers), out var state) ? state.LastPressTimestamp : 0;

        public IEnumerable<ConsoleKey> GetPressedKeys()
        {
            foreach (var kv in _keyStates)
                if (kv.Value.IsPressed)
                    yield return (ConsoleKey)(kv.Key & 0xFFFF);
        }

        public IEnumerable<(ConsoleKey Key, ConsoleModifiers Modifiers)> GetPressedKeysWithModifiers()
        {
            foreach (var kv in _keyStates)
            {
                if (kv.Value.IsPressed)
                {
                    int code = kv.Key;
                    ConsoleKey key = (ConsoleKey)(code & 0xFFFF);
                    ConsoleModifiers modifiers = 0;
                    if ((code & (1 << 16)) != 0) modifiers |= ConsoleModifiers.Shift;
                    if ((code & (1 << 17)) != 0) modifiers |= ConsoleModifiers.Alt;
                    if ((code & (1 << 18)) != 0) modifiers |= ConsoleModifiers.Control;
                    yield return (key, modifiers);
                }
            }
        }

        public void Dispose()
        {
            _keyStates.Clear();
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            _downKeys.Clear();
            _releaseCheckScratch.Clear();
        }
    }
}
