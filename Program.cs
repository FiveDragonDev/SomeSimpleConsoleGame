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
            const int Width = 120, Height = 60;

            Console.SetWindowSize(Width, Height);
            Console.SetBufferSize(Width, Height);
            EnableAnsiCodes();

            _systems = new();

            _systems.AddSystem(new RenderSystem(Width, Height, 60), 1);

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
