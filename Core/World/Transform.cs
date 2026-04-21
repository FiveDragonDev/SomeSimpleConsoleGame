using System.Numerics;

namespace SomeSimpleConsoleGame.Core.World
{
    public struct Transform : IComponent
    {
        public Vector3 Position;
        public Vector3 Rotation;
    }
}

