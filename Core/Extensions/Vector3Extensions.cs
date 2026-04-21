using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Extensions
{
    public static class Vector3Extensions
    {
        public static Vector3 Normalized(this in Vector3 value)
        {
            var lengthSqr = value.LengthSquared();
            if (lengthSqr == 0) return Vector3.Zero;
            return value / (float)Math.Sqrt(lengthSqr);
        }
        public static void Normalize(ref this Vector3 value) => value = value.Normalized();
    }
}
