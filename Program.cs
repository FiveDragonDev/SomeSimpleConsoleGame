using System.Runtime.InteropServices;

namespace SomeSimpleConsoleGame
{
    internal sealed class Program
    {
        private static SystemsUpdater? _systems;

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        private static void Main()
        {
            const int width = 120, height = 120;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.SetWindowSize(width, height);
                Console.SetBufferSize(width, height);
                EnableAnsiCodes();
            }

            _systems = new();
            GLContext context = new(width, height);

            Mesh mesh = new([
                new(-0.5f, -0.5f, -0.5f),
                new(0.5f, -0.5f, -0.5f),
                new(0.5f, 0.5f, -0.5f),
                new(-0.5f, 0.5f, -0.5f),
                new(-0.5f, -0.5f, 0.5f),
                new(0.5f, -0.5f, 0.5f),
                new(0.5f, 0.5f, 0.5f),
                new(-0.5f, 0.5f, 0.5f)
                ],
                [
                    0, 1, 2, 0, 2, 3,
                    4, 6, 5, 4, 7, 6,
                    0, 3, 7, 0, 7, 4,
                    1, 5, 6, 1, 6, 2,
                    0, 4, 5, 0, 5, 1,
                    3, 2, 6, 3, 6, 7,
                ]);

            _systems.AddSystem(new TestRotateSystem(mesh, context), 2);
            _systems.AddSystem(new RenderSystem(width, height, 60, context), 1);

            while (true)
            {
                _systems.Update();
            }
        }

        private static void EnableAnsiCodes()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                _ = GetConsoleMode(handle, out uint mode);
                _ = SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
