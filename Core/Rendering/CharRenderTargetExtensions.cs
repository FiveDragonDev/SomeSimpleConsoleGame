using System.Buffers;

namespace SomeSimpleConsoleGame.Core.Rendering
{
    public static class CharRenderTargetExtensions
    {
        public static bool TryGetIndex(this ICharRenderTarget target, int x, int y, out int index)
        {
            if (x < 0 || y < 0 || x >= target.Width || y >= target.Height)
            {
                index = -1;
                return false;
            }

            index = y * target.Width + x;
            return true;
        }

        public static Span<char> GetRowSpan(this ICharRenderTarget target, int y)
        {
            if (y < 0 || y >= target.Height) return [];
            int start = y * target.Width;
            return target.GetBackBuffer().Slice(start, target.Width);
        }

        public static Span<char> GetRowSpan(this ICharRenderTarget target, int x, int y, int length)
        {
            if (length <= 0) return [];
            if (y < 0 || y >= target.Height) return [];
            if (x >= target.Width) return [];

            int startX = Math.Max(x, 0);
            int endXExclusive = Math.Min(x + length, target.Width);
            int spanLen = endXExclusive - startX;
            if (spanLen <= 0) return [];

            return target.GetBackBuffer().Slice(y * target.Width + startX, spanLen);
        }

        public static void FillRect(this ICharRenderTarget target, int x, int y, int width, int height, char c, bool markDirty = true)
        {
            if (width <= 0 || height <= 0) return;
            if (x >= target.Width || y >= target.Height) return;
            if (x + width <= 0 || y + height <= 0) return;

            int startX = Math.Max(x, 0);
            int startY = Math.Max(y, 0);
            int endXExclusive = Math.Min(x + width, target.Width);
            int endYExclusive = Math.Min(y + height, target.Height);

            int fillWidth = endXExclusive - startX;
            int fillHeight = endYExclusive - startY;
            if (fillWidth <= 0 || fillHeight <= 0) return;

            var buffer = target.GetBackBuffer();
            for (int row = startY; row < endYExclusive; row++)
            {
                buffer.Slice(row * target.Width + startX, fillWidth).Fill(c);
            }

            if (markDirty)
                target.MarkDirtyRect(startX, startY, fillWidth, fillHeight);
        }

        public static void WriteRow(this ICharRenderTarget target, int x, int y, ReadOnlySpan<char> text, bool markDirty = true, bool filterControlChars = false)
        {
            if (text.Length == 0) return;
            if (y < 0 || y >= target.Height) return;
            if (x >= target.Width) return;

            int srcOffset = 0;
            if (x < 0)
            {
                srcOffset = -x;
                if (srcOffset >= text.Length) return;
                x = 0;
            }

            int maxLen = Math.Min(text.Length - srcOffset, target.Width - x);
            if (maxLen <= 0) return;

            var src = text.Slice(srcOffset, maxLen);
            var dst = target.GetRowSpan(x, y, maxLen);
            if (dst.Length == 0) return;

            if (!filterControlChars)
            {
                src.CopyTo(dst);
                if (markDirty) target.MarkDirtyRect(x, y, dst.Length, 1);
                return;
            }

            char[]? rented = null;
            Span<char> filtered = src.Length <= 256
                ? stackalloc char[256]
                : (rented = ArrayPool<char>.Shared.Rent(src.Length));

            try
            {
                int len = 0;
                for (int i = 0; i < src.Length; i++)
                {
                    char ch = src[i];
                    if (!char.IsControl(ch))
                        filtered[len++] = ch;
                }

                if (len <= 0) return;

                var toCopy = filtered[..Math.Min(len, dst.Length)];
                toCopy.CopyTo(dst);
                if (markDirty) target.MarkDirtyRect(x, y, toCopy.Length, 1);
            }
            finally
            {
                if (rented is not null) ArrayPool<char>.Shared.Return(rented);
            }
        }

        public static void WriteRow(this ICharRenderTarget target, int x, int y, string text, bool markDirty = true, bool filterControlChars = false)
        {
            if (text is null) return;
            target.WriteRow(x, y, text.AsSpan(), markDirty, filterControlChars);
        }
    }
}
