using System.Runtime.InteropServices;

namespace SomeSimpleConsoleGame
{
    public static partial class ConsoleUtils
    {
        private static readonly IntPtr _hwnd = GetConsoleWindow();

        public static bool TrySetConsoleSize(int width, int height)
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

        public static void EnableAnsiCodes()
        {
            const int StdOutputHandle = -11;
            const uint EnableVirtualTerminalProcessing = 0x0004;

            if (!OperatingSystem.IsWindows()) return;

            var handle = GetStdHandle(StdOutputHandle);
            _ = GetConsoleMode(handle, out uint mode);
            _ = SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
        }

        public static (int Width, int Height) GetScreenSize()
        {
            if (!OperatingSystem.IsWindows()) return (0, 0);
            return (GetSystemMetrics(0), GetSystemMetrics(1));
        }

        public static (int Left, int Top) GetCenteredConsolePosition()
        {
            if (!OperatingSystem.IsWindows()) return (0, 0);

            GetWindowRect(_hwnd, out var rect);
            int consoleWidth = rect.Right - rect.Left;
            int consoleHeight = rect.Bottom - rect.Top;

            var (screenWidth, screenHeight) = GetScreenSize();

            int left = (screenWidth - consoleWidth) / 2;
            int top = (screenHeight - consoleHeight) / 2;

            return (left, top);
        }

        public static void CenterConsoleWindow()
        {
            if (!OperatingSystem.IsWindows()) return;

            var (left, top) = GetCenteredConsolePosition();
            SetConsolePosition(left, top);
        }
        public static void SetConsolePosition(int left, int top)
        {
            const uint SwpNoSize = 1;
            const uint SwpNoZOrder = 4;

            if (!OperatingSystem.IsWindows()) return;
            SetWindowPos(_hwnd, 0, left, top, 0, 0, SwpNoSize | SwpNoZOrder);
        }

        public static (int Left, int Top) GetConsolePosition()
        {
            if (OperatingSystem.IsWindows() && GetWindowRect(_hwnd, out var rect)) return (rect.Left, rect.Top);
            return (0, 0);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [LibraryImport("user32.dll")]
        private static partial int GetSystemMetrics(int nIndex);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowPos")]
        private static partial IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int y, int cx, int cy, uint wFlags);

        [LibraryImport("kernel32.dll")]
        private static partial IntPtr GetConsoleWindow();

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static partial IntPtr GetStdHandle(int nStdHandle);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
    }
}
