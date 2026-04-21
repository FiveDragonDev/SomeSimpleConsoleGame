using SomeSimpleConsoleGame.Core;
using SomeSimpleConsoleGame.Core.Physics;
using SomeSimpleConsoleGame.Core.Rendering;
using SomeSimpleConsoleGame.Core.World;
using SomeSimpleConsoleGame.Demo;
using System.Numerics;

namespace SomeSimpleConsoleGame
{
    internal sealed class Program
    {
        private static void Main(string[] args)
        {
            int width = GetArgInt(args, "--w") ?? 120;
            int height = GetArgInt(args, "--h") ?? 60;

            bool? oldCursorVisible = null;
            if (OperatingSystem.IsWindows())
            {
                oldCursorVisible = Console.CursorVisible;
                Console.CursorVisible = false;
                Console.Title = "Console 3D Demo";
                ConsoleUtils.EnableAnsiCodes();
            }

            if (!ConsoleUtils.TrySetConsoleSize(width, height))
            {
                width = Console.WindowWidth;
                height = Console.WindowHeight;
            }

            try
            {
                using SystemsUpdater systems = new();
                using InputController inputs = new();

                DemoState state = new(width, height);
                DemoRenderContext context = new(state);

                using World world = new();

                var physics = new PhysicsSystem(world, 20);
                physics.AddGlobalForce(_ = new GravityForce(new Vector3(0, -2, 0)));
                physics.AddGlobalForce(_ = new DragForce(0.15f, 0));

                ScreenShakeSystem shaker = new(3, 6, 13);

                systems.AddSystem(shaker, 0);
                // systems.AddSystem(physics, 3);
                systems.AddSystem(_ = new DemoSceneSystem(context.GL, context.Shader, inputs, state, world), 2);
                systems.AddSystem(_ = new RenderSystem(width, height, 60, context, showStats: true), 1);

                bool running = true;
                while (running)
                {
                    inputs.PollEvents();

                    if (inputs.IsKeyPressed(ConsoleKey.Escape))
                    {
                        running = false;
                        continue;
                    }
                    if (inputs.IsKeyDown(ConsoleKey.J)) shaker.AddAmplitude(1);

                    systems.Update();
                }
            }
            finally
            {
                if (OperatingSystem.IsWindows() && oldCursorVisible.HasValue)
                    Console.CursorVisible = oldCursorVisible.Value;
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
    }
}
