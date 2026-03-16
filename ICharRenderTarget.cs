namespace SomeSimpleConsoleGame
{
    public interface ICharRenderTarget
    {
        int Width { get; }
        int Height { get; }
        int Area { get; }

        Span<char> GetBackBuffer();

        void MarkDirty(int startIndex, int length);
        void MarkDirtyRect(int x, int y, int width, int height);
    }
}
