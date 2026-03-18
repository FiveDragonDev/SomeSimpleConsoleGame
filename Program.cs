using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Physics;
using SomeSimpleConsoleGame.Core.Rendering;
using SomeSimpleConsoleGame.Demo;
using System.Runtime.InteropServices;

namespace SomeSimpleConsoleGame
{
    internal sealed class Program
    {
        private const int StdOutputHandle = -11;
        private const uint EnableVirtualTerminalProcessing = 0x0004;

        private static void Main(string[] args)
        {
            int width = GetArgInt(args, "--w") ?? 120;
            int height = GetArgInt(args, "--h") ?? 60;

            bool? oldCursorVisible = null;
            if (OperatingSystem.IsWindows())
            {
                oldCursorVisible = Console.CursorVisible;
                Console.CursorVisible = false;
            }

            if (OperatingSystem.IsWindows() && TrySetConsoleSize(width, height))
            {
                Console.Title = "Console 3D Demo";
                EnableAnsiCodes();
            }
            else
            {
                width = Console.WindowWidth;
                height = Console.WindowHeight;
            }

            try
            {
                using var systems = new SystemsUpdater();
                using InputController inputs = new();

                DemoState state = new(width, height);
                DemoRenderContext context = new(state);

                systems.AddSystem(new PhysicsSystem(30), 3);
                systems.AddSystem(new DemoSceneSystem(context.GL, context.Shader, inputs, state), 2);
                systems.AddSystem(new RenderSystem(width, height, 60, context, showStats: true), 1);

                bool running = true;
                while (running)
                {
                    inputs.PollEvents();

                    if (inputs.IsKeyDown(ConsoleKey.Escape))
                    {
                        running = false;
                        continue;
                    }

                    systems.Update();
                }
            }
            finally
            {
                if (OperatingSystem.IsWindows() && oldCursorVisible.HasValue)
                    Console.CursorVisible = oldCursorVisible.Value;
            }
        }

        private static bool TrySetConsoleSize(int width, int height)
        {
            if (!OperatingSystem.IsWindows()) return false;

            try
            {
                Console.SetWindowSize(width, height);
                Console.SetBufferSize(width, height);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int? GetArgInt(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) continue;
                if (int.TryParse(args[i + 1], out int value)) return value;
            }
            return null;
        }

        private static void EnableAnsiCodes()
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = GetStdHandle(StdOutputHandle);
                _ = GetConsoleMode(handle, out uint mode);
                _ = SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
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
