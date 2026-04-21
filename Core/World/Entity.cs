namespace SomeSimpleConsoleGame.Core.World
{
    public readonly record struct Entity(uint Id) : IEquatable<Entity>, IComparable<Entity>
    {
        public int CompareTo(Entity other) => Id.CompareTo(other.Id);
        public override string ToString() => Id.ToString();
    }
}

