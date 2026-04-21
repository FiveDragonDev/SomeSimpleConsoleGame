namespace SomeSimpleConsoleGame.Core.Extensions
{
    public static class ArrayExtensions
    {
        public static B[] MorphArray<A, B>(this ReadOnlySpan<A> value, Func<A, B> selector)
        {
            B[] result = new B[value.Length];
            for (int i = 0; i < value.Length; i++)
                result[i] = selector(value[i]);
            return result;
        }
    }
}
