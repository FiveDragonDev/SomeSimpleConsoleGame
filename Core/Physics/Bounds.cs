using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SomeSimpleConsoleGame.Core.Physics
{
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = SizeInBytes)]
    public readonly struct Bounds
    {
        public const int SizeInBytes = sizeof(float) * 2;

        public Vector3 Min { get; }
        public Vector3 Max { get; }

        public Vector3 Center => (Min + Max) / 2f;
        public Vector3 Size => Max - Min;

        public static readonly Bounds Zero = new();
        public static readonly Bounds NDC = new(new(-1, -1, 0), new(1, 1, 1));

        public Bounds(Vector3 min, Vector3 max)
        {
            if (max.X < min.X || max.Y < min.Y || max.Z < min.Z)
                throw new ArgumentException("Max must be greater than or equal to Min.");

            Min = min;
            Max = max;
        }
        public static Bounds CreateFromCenterAndSize(in Vector3 center, in Vector3 size) => new(center - size / 2f, center + size / 2f);
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

        public bool Contains(in Vector3 point) =>
            point.X >= Min.X && point.X <= Max.X &&
            point.Y >= Min.Y && point.Y <= Max.Y &&
            point.Z >= Min.Z && point.Z <= Max.Z;
        public bool Intersects(in Bounds other)
        {
            return Min.X <= other.Max.X && Max.X >= other.Min.X &&
                   Min.Y <= other.Max.Y && Max.Y >= other.Min.Y &&
                   Min.Z <= other.Max.Z && Max.Z >= other.Min.Z;
        }

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
    }
}
