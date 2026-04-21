using System.Runtime.CompilerServices;

namespace SomeSimpleConsoleGame.Core.Extensions
{
    public static class SpanExtensions
    {
        public static int GetSize<T>(this ReadOnlySpan<T> data) => checked(Unsafe.SizeOf<T>() * data.Length);

        public static int Concat<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> dest)
        {
            var total = a.Length + b.Length;

            if (dest.Length < total)
                throw new ArgumentException("Destination is too small", nameof(dest));

            a.CopyTo(dest);
            b.CopyTo(dest[a.Length..]);
            return total;
        }
        public static bool TryConcat<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b, Span<T> dest, out int writen)
        {
            var total = a.Length + b.Length;

            if (dest.Length < total)
            {
                writen = 0;
                return false;
            }

            a.CopyTo(dest);
            b.CopyTo(dest[a.Length..]);
            writen = total;
            return true;
        }
    }
}
