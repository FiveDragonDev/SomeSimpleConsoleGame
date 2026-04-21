namespace SomeSimpleConsoleGame.Core.Rendering
{
    public interface ICharRenderTarget
    {
        int Width { get; }
        int Height { get; }
        int Area { get; }

        Span<char> GetBackBuffer();
        /// <summary>
        /// Update the back buffer from the provided source characters. Implementations should
        /// compare the incoming data with the existing back buffer and mark dirty regions
        /// appropriately (per-line dirty tracking) to minimize console output.
        /// </summary>
        void UpdateBackBuffer(ReadOnlySpan<char> src);

        /// <summary>
        /// Returns true when the render target has pending changes that need to be flushed
        /// to the display (dirty regions or full redraw requested).
        /// </summary>
        bool HasPendingRedraw { get; }

        void MarkDirty(int startIndex, int length);
        void MarkDirtyRect(int x, int y, int width, int height);
    }
}
