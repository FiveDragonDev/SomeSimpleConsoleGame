namespace SomeSimpleConsoleGame
{
    public interface IRenderContext
    {
        (int startIndex, char[]) Render();
    }
}