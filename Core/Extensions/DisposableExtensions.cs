namespace SomeSimpleConsoleGame.Core.Extensions
{
    public static class DisposableExtensions
    {
        public static void DisposeAll<T>(this IList<T> value) where T : IDisposable
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            for (int i = 0; i < value.Count; i++)
            {
                value[i].Dispose();
            }
        }
    }
}
