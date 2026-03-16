namespace SomeSimpleConsoleGame
{
    public interface IRenderContext
    {
        (int startIndex, char[]) Render();
    }

    public interface IRenderContextLowLevel : IRenderContext
    {
        void Render(ICharRenderTarget target);
    }
}
