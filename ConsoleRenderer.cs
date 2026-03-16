using System.Buffers;
using System.Text;

namespace SomeSimpleConsoleGame
{
    public sealed class ConsoleRenderer : ICharRenderTarget, IDisposable
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int Area { get; private set; }

        public Span<char> GetBackBuffer() => BackBuffer.AsSpan();

        private char[] BackBuffer => _charBuffers[1 - _frontBufferIndex];
        private char[] FrontBuffer => _charBuffers[_frontBufferIndex];

        private const string CursorHome = "\x1b[1;1H";
        private const string CursorMovePrefix = "\x1b[";
        private const string BackgroundRgbPrefix = "\x1b[48;2;";

        private int _frontBufferIndex;
        private readonly char[][] _charBuffers;

        private (byte, byte, byte) _currentBackgroundColor = (24, 8, 8);

        private bool _colorRedrawNeeded = true;
        private bool _fullRedrawNeeded = true;
        private readonly (int start, int length)?[] _dirtyLines;

        private readonly StringBuilder _outputBuilder;

        public ConsoleRenderer(int bufferWidth, int bufferHeight)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferWidth);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferHeight);

            Width = bufferWidth;
            Height = bufferHeight;
            Area = bufferWidth * bufferHeight;

            _charBuffers = [
                GC.AllocateUninitializedArray<char>(Area, true),
                GC.AllocateUninitializedArray<char>(Area, true)
                ];
            BackBuffer.AsSpan().Fill(' ');

            _dirtyLines = new (int, int)?[bufferHeight];

            _outputBuilder = new(Area * 2);
        }

        public void Render() => RenderAsync().GetAwaiter().GetResult();
        public async Task RenderAsync()
        {
            _outputBuilder.Clear();
            _outputBuilder.Append(CursorHome);
            if (_colorRedrawNeeded) RedrawColor();

            if (_fullRedrawNeeded) FullRedraw();
            else RedrawDirtyPixels();

            var renderTask = Console.Out.WriteAsync(_outputBuilder);

            _dirtyLines.AsSpan().Clear();
            _fullRedrawNeeded = false;

            await renderTask;

            void RedrawColor()
            {
                var (r, g, b) = _currentBackgroundColor;
                _outputBuilder.Append(BackgroundRgbPrefix);
                _outputBuilder.Append(r);
                _outputBuilder.Append(';');
                _outputBuilder.Append(g);
                _outputBuilder.Append(';');
                _outputBuilder.Append(b);
                _outputBuilder.Append('m');
                _colorRedrawNeeded = false;
            }
        }

        private void FullRedraw()
        {
            _outputBuilder.Append(FrontBuffer);
        }

        private void RedrawDirtyPixels()
        {
            for (int i = 0; i < Height; i++)
            {
                if (!_dirtyLines[i].HasValue) continue;
                var (start, length) = _dirtyLines[i]!.Value;
                AppendCursorPosition(row: i + 1, col: start + 1);
                _outputBuilder.Append(FrontBuffer, GetBufferIndex(start, i), length);
            }
        }

        public void ForceFullRedraw() => MarkDirtyAll();
        public void Clear()
        {
            BackBuffer.AsSpan().Fill(' ');
            MarkDirtyAll();
        }
        public void ClearLine(int line)
        {
            if (!CheckBounds(0, line)) return;
            BackBuffer.AsSpan(GetBufferIndex(0, line), Width).Fill(' ');
            MarkDirtyLine(line, 0, Width);
        }
        public void ClearLine(int line, int start, int length)
        {
            if (!CheckBounds(start, line)) return;
            if (length <= 0) return;
            if (start + length > Width) length = Width - start;
            if (length <= 0) return;
            BackBuffer.AsSpan(GetBufferIndex(start, line), length).Fill(' ');
            MarkDirtyLine(line, start, length);
        }

        public void SetBackgroundColor(byte r, byte g, byte b)
        {
            if (_currentBackgroundColor == (r, g, b)) return;
            _currentBackgroundColor = (r, g, b);
            _colorRedrawNeeded = true;
        }

        public void Fill(char c)
        {
            BackBuffer.AsSpan().Fill(c);
            MarkDirtyAll();
        }
        public void FillRect(int x, int y, int width, int height, char c)
        {
            if (width <= 0 || height <= 0) return;
            if (x >= Width || y >= Height) return;

            int startX = Math.Max(x, 0);
            int startY = Math.Max(y, 0);
            int endXExclusive = Math.Min(x + width, Width);
            int endYExclusive = Math.Min(y + height, Height);

            int fillWidth = endXExclusive - startX;
            if (fillWidth <= 0 || endYExclusive <= startY) return;

            for (int row = startY; row < endYExclusive; row++)
            {
                BackBuffer.AsSpan(GetBufferIndex(startX, row), fillWidth).Fill(c);
                MarkDirtyLine(row, startX, fillWidth);
            }
        }

        public void DrawHorizontalLine(int x, int y, int length, char c)
        {
            if (length <= 0) return;
            if (y < 0 || y >= Height) return;
            if (x >= Width) return;

            int startX = Math.Max(x, 0);
            int endXExclusive = Math.Min(x + length, Width);
            int drawLen = endXExclusive - startX;
            if (drawLen <= 0) return;

            BackBuffer.AsSpan(GetBufferIndex(startX, y), drawLen).Fill(c);
            MarkDirtyLine(y, startX, drawLen);
        }
        public void DrawVerticalLine(int x, int y, int length, char c)
        {
            if (length <= 0) return;
            if (x < 0 || x >= Width) return;
            if (y >= Height) return;

            int startY = Math.Max(y, 0);
            int endYExclusive = Math.Min(y + length, Height);
            if (endYExclusive <= startY) return;

            for (int row = startY; row < endYExclusive; row++)
                SetChar(x, row, c);
        }

        public void DrawBox(int x, int y, int width, int height, char border = '#', char? fill = null)
        {
            if (width <= 0 || height <= 0) return;

            DrawHorizontalLine(x, y, width, border);
            if (height > 1) DrawHorizontalLine(x, y + height - 1, width, border);

            if (height > 2)
            {
                DrawVerticalLine(x, y + 1, height - 2, border);
                if (width > 1) DrawVerticalLine(x + width - 1, y + 1, height - 2, border);
            }

            if (fill.HasValue && width > 2 && height > 2)
                FillRect(x + 1, y + 1, width - 2, height - 2, fill.Value);
        }

        public void SwapBuffers() => _frontBufferIndex = 1 - _frontBufferIndex;

        public char GetChar(int x, int y)
        {
            int index = GetBufferIndex(x, y);
            if (!CheckBounds(index)) return ' ';
            return BackBuffer[index];
        }

        public void SetData(int startIndex, ReadOnlySpan<char> data)
        {
            if (!CheckRange(startIndex, data.Length)) return;

            var oldSpan = BackBuffer.AsSpan(startIndex, data.Length);

            if (oldSpan.SequenceEqual(data)) return;

            int startY = startIndex / Width;
            int startX = startIndex % Width;

            int offset = 0;
            int remaining = data.Length;

            int firstChunkLen = Math.Min(Width - startX, remaining);
            CheckAndMark(this, startY, startX, firstChunkLen, oldSpan, data, ref offset, ref remaining);

            int currentRow = startY + 1;
            while (remaining > Width)
            {
                CheckAndMark(this, currentRow, 0, Width, oldSpan, data, ref offset, ref remaining);
                currentRow++;
            }

            if (remaining > 0)
            {
                CheckAndMark(this, currentRow, 0, remaining, oldSpan, data, ref offset, ref remaining);
            }

            data.CopyTo(oldSpan);

            static void CheckAndMark(ConsoleRenderer @this, int row, int start, int length, Span<char> oldSpan, ReadOnlySpan<char> data, ref int offset, ref int remaining)
            {
                var oldSegment = oldSpan.Slice(offset, length);
                var newSegment = data.Slice(offset, length);

                if (!oldSegment.SequenceEqual(newSegment))
                    @this.MarkDirtyLine(row, start, length);

                offset += length;
                remaining -= length;
            }
        }

        public void WriteSpan(int startIndex, ReadOnlySpan<char> data, bool markDirty = true)
        {
            if (data.Length == 0) return;
            if (!CheckRange(startIndex, data.Length)) return;

            data.CopyTo(BackBuffer.AsSpan(startIndex, data.Length));
            if (markDirty) MarkDirty(startIndex, data.Length);
        }

        public void MarkDirty(int startIndex, int length)
        {
            if (length <= 0) return;
            if (!CheckRange(startIndex, length)) return;

            int row = startIndex / Width;
            int col = startIndex % Width;
            int remaining = length;

            int firstChunkLen = Math.Min(Width - col, remaining);
            MarkDirtyLine(row, col, firstChunkLen);
            remaining -= firstChunkLen;
            row++;

            while (remaining > 0 && row < Height)
            {
                int chunkLen = Math.Min(Width, remaining);
                MarkDirtyLine(row, 0, chunkLen);
                remaining -= chunkLen;
                row++;
            }
        }

        public void MarkDirtyRect(int x, int y, int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (x >= Width || y >= Height) return;

            int startX = Math.Max(x, 0);
            int startY = Math.Max(y, 0);
            int endXExclusive = Math.Min(x + width, Width);
            int endYExclusive = Math.Min(y + height, Height);

            int rowLen = endXExclusive - startX;
            if (rowLen <= 0 || endYExclusive <= startY) return;

            for (int row = startY; row < endYExclusive; row++)
                MarkDirtyLine(row, startX, rowLen);
        }

        public void SetChar(int x, int y, char c)
        {
            int index = GetBufferIndex(x, y);
            if (!CheckBounds(index)) return;
            if (BackBuffer[index] == c) return;
            BackBuffer[index] = c;
            MarkDirtyLine(y, x, 1);
        }
        public void SetString(int x, int y, string text)
        {
            if (text is null) return;
            SetCharsBatch(x, y, text.AsSpan());
        }
        public void SetCharsBatch(int x, int y, ReadOnlySpan<char> chars)
        {
            if (chars.Length == 0) return;
            if (!CheckBounds(x, y)) return;

            int startIndex = GetBufferIndex(x, y);

            const int StackLimit = 256;
            char[]? rented = null;
            Span<char> correctedChars = chars.Length <= StackLimit
                ? stackalloc char[StackLimit]
                : (rented = ArrayPool<char>.Shared.Rent(chars.Length));

            try
            {
                int correctedIndex = 0;
                for (int i = 0; i < chars.Length; i++)
                {
                    char ch = chars[i];
                    if (!char.IsControl(ch))
                        correctedChars[correctedIndex++] = ch;
                }

                int maxLen = Math.Min(correctedIndex, Width - x);
                if (maxLen <= 0) return;

                var filteredChars = correctedChars[..maxLen];
                if (!CheckRange(startIndex, filteredChars.Length)) return;

                var backSpan = BackBuffer.AsSpan(startIndex, filteredChars.Length);
                if (backSpan.SequenceEqual(filteredChars)) return;
                filteredChars.CopyTo(backSpan);
                MarkDirtyLine(y, x, filteredChars.Length);
            }
            finally
            {
                if (rented is not null) ArrayPool<char>.Shared.Return(rented);
            }
        }

        private void AppendCursorPosition(int row, int col)
        {
            _outputBuilder.Append(CursorMovePrefix);
            _outputBuilder.Append(row);
            _outputBuilder.Append(';');
            _outputBuilder.Append(col);
            _outputBuilder.Append('H');
        }

        private void MarkDirtyLine(int row, int start, int length)
        {
            if (!CheckBounds(start, row)) return;
            if (start + length > Width) length = Width - start;

            ref var line = ref _dirtyLines[row];
            if (!line.HasValue)
            {
                line = (start, length);
                return;
            }

            var (oldStart, oldLen) = line.Value;
            int oldEnd = oldStart + oldLen;

            int newStart = Math.Min(oldStart, start);
            int newEnd = Math.Max(oldEnd, start + length);

            line = (newStart, newEnd - newStart);
        }
        private void MarkDirtyAll()
        {
            _fullRedrawNeeded = true;
            Array.Clear(_dirtyLines, 0, Height);
        }

        private bool CheckBounds(int index) => index >= 0 && index < Area;
        private bool CheckBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        private bool CheckRange(int startIndex, int length)
        {
            if (startIndex < 0 || length < 0) return false;
            return (long)startIndex + length <= Area;
        }

        private int GetBufferIndex(int x, int y) => y * Width + x;

        public void Dispose()
        {
            foreach (var buffer in _charBuffers)
            {
                Array.Clear(buffer, 0, Area);
            }
            Array.Clear(_charBuffers, 0, 2);

            Array.Clear(_dirtyLines, 0, Height);

            _outputBuilder.Clear();
        }
    }
}
