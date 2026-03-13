using System;
using System.Text;

namespace SomeSimpleConsoleGame
{
    public sealed class ConsoleRenderer
    {
        public readonly int BufferWidth, BufferHeight, BufferArea;

        private int _frontBufferIndex;
        private readonly char[][] _charBuffers;

        private bool _fullRedrawNeeded = true;
        private readonly (int start, int length)?[] _dirtyLines;

        private readonly StringBuilder _outputBuilder;

        public ConsoleRenderer(int bufferWidth, int bufferHeight)
        {
            BufferWidth = bufferWidth;
            BufferHeight = bufferHeight;
            BufferArea = bufferWidth * bufferHeight;

            _charBuffers = [
                GC.AllocateUninitializedArray<char>(BufferArea, true),
                GC.AllocateUninitializedArray<char>(BufferArea, true)
                ];
            BackBuffer.AsSpan().Fill(' ');

            _dirtyLines = new (int, int)?[bufferHeight];

            _outputBuilder = new(BufferArea * 2);
        }

        public void Render() => RenderAsync().Wait();
        public async Task RenderAsync()
        {
            _outputBuilder.Clear();
            _outputBuilder.Append($"\x1b[1;1H");

            if (_fullRedrawNeeded) FullRedraw();
            else RedrawDirtyPixels();

            var renderTask = Console.Out.WriteAsync(_outputBuilder);

            _dirtyLines.AsSpan().Clear();
            _fullRedrawNeeded = false;

            await renderTask;
        }

        private void FullRedraw()
        {
            _outputBuilder.Append(FrontBuffer);
        }

        private void RedrawDirtyPixels()
        {
            for (int i = 0; i < BufferHeight; i++)
            {
                if (!_dirtyLines[i].HasValue) continue;
                var (start, length) = _dirtyLines[i]!.Value;
                _outputBuilder.Append($"\x1b[{i + 1};{start + 1}H");
                _outputBuilder.Append(FrontBuffer, GetBufferIndex(start, i), length);
            }
        }

        public void Clear()
        {
            BackBuffer.AsSpan().Fill(' ');
            MarkDirtyAll();
        }
        public void ClearLine(int line)
        {
            if (!CheckBounds(0, line)) return;
            BackBuffer.AsSpan(GetBufferIndex(0, line), BufferWidth).Fill(' ');
            MarkDityLine(line, 0, BufferWidth);
        }
        public void ClearLine(int line, int start, int length)
        {
            if (!CheckBounds(start, line)) return;
            BackBuffer.AsSpan(GetBufferIndex(start, line), length).Fill(' ');
            MarkDityLine(line, start, length);
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

            int startY = startIndex / BufferWidth;
            int startX = startIndex % BufferWidth;

            int offset = 0;
            int remaining = data.Length;

            int firstChunkLen = Math.Min(BufferWidth - startX, remaining);
            CheckAndMark(startY, startX, firstChunkLen, oldSpan, data, ref offset, ref remaining);

            int currentRow = startY + 1;
            while (remaining > BufferWidth)
            {
                CheckAndMark(currentRow, 0, BufferWidth, oldSpan, data, ref offset, ref remaining);
                currentRow++;
            }

            if (remaining > 0)
            {
                CheckAndMark(currentRow, 0, remaining, oldSpan, data, ref offset, ref remaining);
            }

            data.CopyTo(oldSpan);
        }

        public void SetChar(int x, int y, char c)
        {
            int index = GetBufferIndex(x, y);
            if (!CheckBounds(index)) return;
            if (BackBuffer[index] == c) return;
            BackBuffer[index] = c;
            MarkDityLine(y, x, 1);
        }
        public void SetCharsBatch(int x, int y, ReadOnlySpan<char> chars)
        {
            int startIndex = GetBufferIndex(x, y);
            if (!CheckBounds(startIndex)) return;

            Span<char> correctedChars = stackalloc char[chars.Length];
            int correctedIndex = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char ch = chars[i];
                if (!char.IsControl(ch))
                {
                    correctedChars[correctedIndex++] = ch;
                }
            }
            var filteredChars = correctedChars[..correctedIndex];
            if (!CheckRange(startIndex, filteredChars.Length)) return;

            var backSpan = BackBuffer.AsSpan(startIndex, filteredChars.Length);
            if (backSpan.SequenceEqual(filteredChars)) return;
            filteredChars.CopyTo(backSpan);
            MarkDityLine(y, x, filteredChars.Length);
        }

        private void MarkDityLine(int row, int start, int length)
        {
            if (!CheckBounds(start, row)) return;
            if (start + length > BufferWidth) length = BufferWidth - start;

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
        private void CheckAndMark(int row, int start, int length, Span<char> oldSpan, ReadOnlySpan<char> data, ref int offset, ref int remaining)
        {
            var oldSegment = oldSpan.Slice(offset, length);
            var newSegment = data.Slice(offset, length);

            if (!oldSegment.SequenceEqual(newSegment))
                MarkDityLine(row, start, length);

            offset += length;
            remaining -= length;
        }
        private void MarkDirtyAll()
        {
            _fullRedrawNeeded = true;
            Array.Clear(_dirtyLines, 0, BufferHeight);
        }

        private bool CheckBounds(int index) => index >= 0 && index < BufferArea;
        private bool CheckBounds(int x, int y) => x >= 0 && x < BufferWidth && y >= 0 && y < BufferHeight;
        private bool CheckRange(int startIndex, int length)
        {
            if (startIndex < 0 || length < 0) return false;
            return (long)startIndex + length <= BufferArea;
        }

        private int GetBufferIndex(int x, int y) => y * BufferWidth + x;
        private char[] BackBuffer => _charBuffers[1 - _frontBufferIndex];
        private char[] FrontBuffer => _charBuffers[_frontBufferIndex];
    }
}
