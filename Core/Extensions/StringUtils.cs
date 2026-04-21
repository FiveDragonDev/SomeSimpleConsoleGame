using System.Runtime.CompilerServices;

namespace SomeSimpleConsoleGame.Core.Extensions
{
    public static class StringUtils
    {
        public static void WriteProgressBar(float percent, Span<char> destination, char filledChar = '#', char emptyChar = ' ')
        {
            if (destination.Length < 3) return;
            var length = destination.Length;
            percent = Math.Clamp(percent, 0, 1);
            int filled = MathUtils.RoundToInt(percent * (length - 1));
            if (filled != 0) destination[1..filled].Fill(filledChar);
            destination[filled..(length - 1)].Fill(emptyChar);
            destination[0] = '[';
            destination[length - 1] = ']';
        }

        public static void WriteTickAnimation(uint tick, ReadOnlySpan<char> animation, Span<char> destination)
        {
            if (destination.IsEmpty || animation.IsEmpty) return;
            if (animation.Length == 1)
            {
                destination[0] = animation[0];
                return;
            }

            destination[0] = animation[(int)(tick % animation.Length)];
        }
        public static void WriteTickAnimation(uint tick, ReadOnlySpan<string> animation, Span<char> destination)
        {
            if (destination.IsEmpty || animation.IsEmpty) return;
            if (animation.Length == 1)
            {
                animation[0].TryCopyTo(destination);
                return;
            }

            animation[(int)(tick % animation.Length)].TryCopyTo(destination);
        }

        public static void WriteTimer(TimeSpan time, Span<char> destination)
        {
            if (destination.IsEmpty) return;
            string format = time.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss";

            Span<char> buffer = stackalloc char[16];
            time.TryFormat(buffer, out int written, format);
            ReadOnlySpan<char> formatted = buffer[..written];

            if (destination.Length < formatted.Length)
            {
                formatted[..(destination.Length - 3)].CopyTo(destination);
                destination[(destination.Length - 2)..].Fill('.');
            }
            else
            {
                formatted.CopyTo(destination);
                if (destination.Length > formatted.Length) destination[formatted.Length..].Fill(' ');
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteLeftAligned(ReadOnlySpan<char> text, Span<char> destination, char padChar = ' ')
        {
            if (destination.IsEmpty) return;
            int copyLen = Math.Min(text.Length, destination.Length);
            text[..copyLen].CopyTo(destination);
            if (copyLen < destination.Length) destination[copyLen..].Fill(padChar);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WriteRightAligned(ReadOnlySpan<char> text, Span<char> destination, char padChar = ' ')
        {
            if (destination.IsEmpty) return;
            int copyLen = Math.Min(text.Length, destination.Length);
            int padLen = destination.Length - copyLen;
            if (padLen > 0) destination[..padLen].Fill(padChar);
            text[..copyLen].CopyTo(destination[padLen..]);
        }
        public static void WriteCentered(ReadOnlySpan<char> text, Span<char> destination, char padChar = ' ')
        {
            if (destination.IsEmpty) return;
            int copyLen = Math.Min(text.Length, destination.Length);
            int totalPad = destination.Length - copyLen;
            int leftPad = totalPad / 2;
            int rightPad = totalPad - leftPad;

            var leftSlice = destination[..leftPad];
            var textSlice = destination.Slice(leftPad, copyLen);
            var rightSlice = destination.Slice(leftPad + copyLen, rightPad);

            leftSlice.Fill(padChar);
            text[..copyLen].CopyTo(textSlice);
            rightSlice.Fill(padChar);
        }

        public static void WriteTruncated(ReadOnlySpan<char> text, Span<char> destination, string ellipsis = "...")
        {
            if (destination.IsEmpty) return;
            if (text.Length <= destination.Length)
            {
                WriteLeftAligned(text, destination);
                return;
            }

            int ellipsisLen = ellipsis.Length;
            if (destination.Length <= ellipsisLen)
            {
                ellipsis.AsSpan(0, Math.Min(ellipsisLen, destination.Length)).CopyTo(destination);
                return;
            }

            int keepLen = destination.Length - ellipsisLen;
            text[..keepLen].CopyTo(destination);
            ellipsis.AsSpan().CopyTo(destination[keepLen..]);
        }
        public static void WriteMarquee(ReadOnlySpan<char> text, int offset, Span<char> destination)
        {
            if (destination.IsEmpty || text.IsEmpty) return;

            offset %= text.Length;
            int remaining = destination.Length;
            int pos = 0;

            while (remaining > 0)
            {
                int copyCount = Math.Min(remaining, text.Length - offset);
                text.Slice(offset, copyCount).CopyTo(destination[pos..]);
                pos += copyCount;
                remaining -= copyCount;
                offset = 0;
            }
        }
    }
}
