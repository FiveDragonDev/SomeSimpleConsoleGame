using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public interface IBounds
    {
        float Radius { get; }

        Vector3 GetWorldCenter(in Vector3 position);
        AABB GetWorldAabb(in Vector3 position);

        bool Intersects(in Vector3 position, IBounds other, in Vector3 otherPosition) =>
            TryComputeContact(in position, other, in otherPosition, out _);

        bool TryComputeContact(in Vector3 position, IBounds other, in Vector3 otherPosition, out Contact contact) =>
            Collision.TryComputeContact(this, in position, other, in otherPosition, out contact);
    }

    public readonly struct SphereBounds : IBounds
    {
        public float Diameter => Radius * 2;
        public float SquareRadius => Radius * Radius;

        public Vector3 Center { get; }
        public float Radius { get; }

        public static SphereBounds One => new(Vector3.Zero, 1);

        public SphereBounds(Vector3 center, float radius)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);
            Center = center;
            Radius = radius;
        }

        public Vector3 GetWorldCenter(in Vector3 position) => Center + position;

        public AABB GetWorldAabb(in Vector3 position) =>
            AABB.CreateFromCenterAndSize(GetWorldCenter(in position), new Vector3(Diameter));
    }

    public readonly struct AABB : IBounds
    {
        public Vector3 Center => (Min + Max) * 0.5f;
        public Vector3 Size => Max - Min;
        public Vector3 HalfSize => Size * 0.5f;

        public Vector3 Min { get; }
        public Vector3 Max { get; }

        public float Radius => HalfSize.Length();

        public static readonly AABB Zero = new();
        public static readonly AABB One = new(-Vector3.One * 0.5f, Vector3.One * 0.5f);
        public static readonly AABB NDC = new(new(-1, -1, 0), new(1, 1, 1));

        public AABB(Vector3 min, Vector3 max)
        {
            if (max.X < min.X || max.Y < min.Y || max.Z < min.Z)
                throw new ArgumentException("Max must be greater than or equal to Min.");

            Min = min;
            Max = max;
        }
        public static AABB CreateFromCenterAndSize(in Vector3 center, in Vector3 size) => new(center - size * 0.5f, center + size * 0.5f);
        /*public static Bounds CreateFromPoints(in ReadOnlySpan<Vector3> points)
        {
            if (points.Length == 0)
                throw new ArgumentException(
                    $"[{nameof(Bounds)}] Requires at least one point", nameof(points));

            Vector3 min = Vector3.PositiveInfinity;
            Vector3 max = Vector3.NegativeInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                var point = points[i];

                if (point.X < min.X)
                    min = new(point.X, min.Y, min.Z);
                if (point.Y < min.Y)
                    min = new(min.X, point.Y, min.Z);
                if (point.Z < min.Z)
                    min = new(min.X, min.Y, point.Z);

                if (point.X > max.X)
                    max = new(point.X, max.Y, max.Z);
                if (point.Y > max.Y)
                    max = new(max.X, point.Y, max.Z);
                if (point.Z > max.Z)
                    max = new(max.X, max.Y, point.Z);
            }

            return new(min, max);
        }*/

        /*public Bounds Transform(Matrix4x4 matrix)
        {
            Span<Vector3> points = stackalloc Vector3[8];
            GetPoints(points);
            for (int i = 0; i < points.Length; i++)
                points[i] = Vector3.Transform(points[i], matrix);
            return CreateFromPoints(points);
        }

        public void GetPoints(Span<Vector3> points)
        {
            if (points.Length != 8) throw new Exception($"[{nameof(Bounds)}] Need 8 points to represent a box");

            Vector3 halfSize = Size / 2f;

            points[0] = Center + new Vector3(-halfSize.X, halfSize.Y, -halfSize.Z);
            points[1] = Center + new Vector3(halfSize.X, halfSize.Y, -halfSize.Z);
            points[2] = Center + new Vector3(halfSize.X, halfSize.Y, halfSize.Z);
            points[3] = Center + new Vector3(-halfSize.X, halfSize.Y, halfSize.Z);

            points[4] = Center + new Vector3(-halfSize.X, -halfSize.Y, -halfSize.Z);
            points[5] = Center + new Vector3(-halfSize.X, -halfSize.Y, halfSize.Z);
            points[6] = Center + new Vector3(halfSize.X, -halfSize.Y, halfSize.Z);
            points[7] = Center + new Vector3(halfSize.X, -halfSize.Y, -halfSize.Z);
        }*/

        public Vector3 GetWorldCenter(in Vector3 position) => Center + position;

        public AABB GetWorldAabb(in Vector3 position) => new(Min + position, Max + position);
    }
}
