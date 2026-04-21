using System.Diagnostics;
using System.Runtime.InteropServices;

namespace SomeSimpleConsoleGame.Core
{
    public sealed partial class InputController : IDisposable
    {
        private struct KeyState
        {
            public long FirstPressTimestamp;
            public long LastPressTimestamp;
            public bool IsPressed;

            public KeyState(long firstPressTimestamp, long lastPressTimestamp, bool isPressed)
            {
                FirstPressTimestamp = firstPressTimestamp;
                LastPressTimestamp = lastPressTimestamp;
                IsPressed = isPressed;
            }
        }

        private static readonly ConsoleKey[] _allKeys = Enum.GetValues<ConsoleKey>();

        private readonly Dictionary<ConsoleKey, KeyState> _keyStates = [];

        private readonly HashSet<ConsoleKey> _pressedThisFrame = [];
        private readonly HashSet<ConsoleKey> _releasedThisFrame = [];

        private readonly List<ConsoleKey> _releaseCheckScratch = [];

        private readonly long _repeatThresholdTicks;
        private readonly long _releaseThresholdTicks;
        private readonly long _frequency = Stopwatch.Frequency;

        private ConsoleModifiers _currentModifiers;

        public InputController(TimeSpan? repeatThreshold = null, TimeSpan? releaseThreshold = null)
        {
            repeatThreshold ??= TimeSpan.FromMilliseconds(100);
            releaseThreshold ??= TimeSpan.FromMilliseconds(200);

            _repeatThresholdTicks = (long)(repeatThreshold.Value.TotalSeconds * _frequency);
            _releaseThresholdTicks = (long)(releaseThreshold.Value.TotalSeconds * _frequency);
        }

        public void PollEvents()
        {
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            long now = Stopwatch.GetTimestamp();

            if (OperatingSystem.IsWindows() && !Console.IsInputRedirected)
            {
                _currentModifiers = QueryCurrentModifiersWindows();

                for (int i = 0; i < _allKeys.Length; i++)
                {
                    var key = _allKeys[i];
                    int vk = (int)key;
                    if ((uint)vk > 0xFF) continue;

                    bool downNow = IsVirtualKeyDownWindows(vk);
                    bool wasDown = _keyStates.TryGetValue(key, out var state) && state.IsPressed;

                    if (downNow)
                    {
                        if (!wasDown)
                        {
                            state = new(now, now, true);
                            _pressedThisFrame.Add(key);
                        }
                        else
                        {
                            state.IsPressed = true;
                        }
                        _keyStates[key] = state;
                    }
                    else if (wasDown)
                    {
                        state.IsPressed = false;
                        _keyStates[key] = state;
                        _releasedThisFrame.Add(key);
                    }
                }

                while (Console.KeyAvailable) _ = Console.ReadKey(true);

                return;
            }

            _currentModifiers = 0;
            while (Console.KeyAvailable)
            {
                var keyInfo = Console.ReadKey(intercept: true);
                _currentModifiers = keyInfo.Modifiers;

                var key = keyInfo.Key;
                if (_keyStates.TryGetValue(key, out var state))
                {
                    long timeSinceLast = now - state.LastPressTimestamp;
                    if (timeSinceLast > _repeatThresholdTicks) _pressedThisFrame.Add(key);

                    if (!state.IsPressed) state.FirstPressTimestamp = now;
                    state.LastPressTimestamp = now;
                    state.IsPressed = true;
                }
                else
                {
                    state = new(now, now, true);
                    _pressedThisFrame.Add(key);
                }
                _keyStates[key] = state;
            }

            _releaseCheckScratch.Clear();
            foreach (var kv in _keyStates)
            {
                if (!kv.Value.IsPressed) continue;
                if ((now - kv.Value.LastPressTimestamp) > _releaseThresholdTicks) _releaseCheckScratch.Add(kv.Key);
            }

            for (int i = 0; i < _releaseCheckScratch.Count; i++)
            {
                var key = _releaseCheckScratch[i];
                var state = _keyStates[key];
                state.IsPressed = false;
                _keyStates[key] = state;
                _releasedThisFrame.Add(key);
            }
        }

        public bool IsKeyDown(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _keyStates.TryGetValue(key, out var state) && state.IsPressed && HasModifiers(modifiers);
        public bool IsKeyUp(ConsoleKey key, ConsoleModifiers modifiers = 0) => !IsKeyDown(key, modifiers);
        public bool IsKeyPressed(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _pressedThisFrame.Contains(key) && HasModifiers(modifiers);
        public bool IsKeyReleased(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _releasedThisFrame.Contains(key) && HasModifiers(modifiers);

        public bool HasModifiers(ConsoleModifiers required) => (_currentModifiers & required) == required;

        public TimeSpan GetKeyHoldDuration(ConsoleKey key, ConsoleModifiers modifiers = 0)
        {
            if (_keyStates.TryGetValue(key, out var state) && state.IsPressed && HasModifiers(modifiers))
            {
                long now = Stopwatch.GetTimestamp();
                long durationTicks = now - state.FirstPressTimestamp;
                return TimeSpan.FromSeconds((double)durationTicks / _frequency);
            }
            return TimeSpan.Zero;
        }

        public long GetKeyLastPressTimestamp(ConsoleKey key, ConsoleModifiers modifiers = 0) =>
            _keyStates.TryGetValue(key, out var state) && HasModifiers(modifiers) ? state.LastPressTimestamp : 0;

        public IEnumerable<ConsoleKey> GetPressedKeys()
        {
            foreach (var kv in _keyStates)
                if (kv.Value.IsPressed)
                    yield return kv.Key;
        }

        public IEnumerable<(ConsoleKey Key, ConsoleModifiers Modifiers)> GetPressedKeysWithModifiers()
        {
            foreach (var kv in _keyStates)
            {
                if (kv.Value.IsPressed)
                {
                    yield return (kv.Key, _currentModifiers);
                }
            }
        }

        public void Dispose()
        {
            _keyStates.Clear();
            _pressedThisFrame.Clear();
            _releasedThisFrame.Clear();
            _releaseCheckScratch.Clear();
        }

        private static ConsoleModifiers QueryCurrentModifiersWindows()
        {
            ConsoleModifiers mods = 0;
            if (IsVirtualKeyDownWindows(0x10)) mods |= ConsoleModifiers.Shift;
            if (IsVirtualKeyDownWindows(0x11)) mods |= ConsoleModifiers.Control;
            if (IsVirtualKeyDownWindows(0x12)) mods |= ConsoleModifiers.Alt;
            return mods;
        }

        private static bool IsVirtualKeyDownWindows(int vKey) =>
            (GetAsyncKeyState(vKey) & 0x8000) != 0;

        [LibraryImport("user32.dll")]
        private static partial short GetAsyncKeyState(int vKey);
    }
}
