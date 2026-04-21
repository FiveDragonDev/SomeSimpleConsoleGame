using SomeSimpleConsoleGame.Core;
using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public readonly record struct Contact(Vector3 Normal, float Penetration, Vector3 Point);

    internal static class Collision
    {
        public static bool TryComputeContact(in IBounds a, in Vector3 positionA, in IBounds b, in Vector3 positionB, out Contact contact)
        {
            contact = default;
            return (a, b) switch
            {
                (AABB boxA, AABB boxB) => TryBoxBox(in boxA, in positionA, in boxB, in positionB, out contact),
                (SphereBounds sphereA, SphereBounds sphereB) => TrySphereSphere(in sphereA, in positionA, in sphereB, in positionB, out contact),
                (SphereBounds sphereA, AABB boxB) => TrySphereBox(in sphereA, in positionA, in boxB, in positionB, out contact),
                (AABB boxA, SphereBounds sphereB) => TryBoxSphere(in boxA, in positionA, in sphereB, in positionB, out contact),
                _ => false
            };
        }

        private static bool TryBoxBox(in AABB a, in Vector3 positionA, in AABB b, in Vector3 positionB, out Contact contact)
        {
            var aCenter = a.GetWorldCenter(positionA);
            var bCenter = b.GetWorldCenter(positionB);

            var aHe = a.HalfSize;
            var bHe = b.HalfSize;

            var delta = bCenter - aCenter;
            float overlapX = (aHe.X + bHe.X) - MathF.Abs(delta.X);
            if (overlapX <= 0) { contact = default; return false; }

            float overlapY = (aHe.Y + bHe.Y) - MathF.Abs(delta.Y);
            if (overlapY <= 0) { contact = default; return false; }

            float overlapZ = (aHe.Z + bHe.Z) - MathF.Abs(delta.Z);
            if (overlapZ <= 0) { contact = default; return false; }

            float penetration = overlapX;
            Vector3 normal = delta.X >= 0 ? Vector3.UnitX : -Vector3.UnitX;

            if (overlapY < penetration)
            {
                penetration = overlapY;
                normal = delta.Y >= 0 ? Vector3.UnitY : -Vector3.UnitY;
            }

            if (overlapZ < penetration)
            {
                penetration = overlapZ;
                normal = delta.Z >= 0 ? Vector3.UnitZ : -Vector3.UnitZ;
            }

            var point = aCenter + normal * new Vector3(aHe.X * MathF.Abs(normal.X), aHe.Y * MathF.Abs(normal.Y), aHe.Z * MathF.Abs(normal.Z));
            contact = new Contact(normal, penetration, point);
            return true;
        }

        private static bool TrySphereSphere(in SphereBounds a, in Vector3 positionA, in SphereBounds b, in Vector3 positionB, out Contact contact)
        {
            var aCenter = a.GetWorldCenter(positionA);
            var bCenter = b.GetWorldCenter(positionB);

            var delta = bCenter - aCenter;
            float distSq = delta.LengthSquared();
            float radius = a.Radius + b.Radius;
            float radiusSq = radius * radius;
            if (distSq >= radiusSq) { contact = default; return false; }

            float dist = distSq > 1e-12f ? MathF.Sqrt(distSq) : 0f;
            Vector3 normal = dist > 1e-6f ? delta / dist : Vector3.UnitY;
            float penetration = radius - dist;
            var point = aCenter + normal * (a.Radius - penetration * 0.5f);
            contact = new Contact(normal, penetration, point);
            return true;
        }

        private static bool TrySphereBox(in SphereBounds sphere, in Vector3 spherePos, in AABB box, in Vector3 boxPos, out Contact contact)
        {
            var sphereCenter = sphere.GetWorldCenter(spherePos);
            var boxCenter = box.GetWorldCenter(boxPos);
            var he = box.HalfSize;
            var boxMin = boxCenter - he;
            var boxMax = boxCenter + he;

            var closest = Clamp(sphereCenter, boxMin, boxMax);
            var d = sphereCenter - closest;
            float distSq = d.LengthSquared();
            float r = sphere.Radius;
            float rSq = r * r;
            if (distSq > rSq) { contact = default; return false; }

            Vector3 normal;
            float penetration;
            if (distSq > 1e-12f)
            {
                float dist = MathF.Sqrt(distSq);
                normal = -(d / dist);
                penetration = r - dist;
            }
            else
            {
                float dxMin = sphereCenter.X - boxMin.X;
                float dxMax = boxMax.X - sphereCenter.X;
                float dyMin = sphereCenter.Y - boxMin.Y;
                float dyMax = boxMax.Y - sphereCenter.Y;
                float dzMin = sphereCenter.Z - boxMin.Z;
                float dzMax = boxMax.Z - sphereCenter.Z;

                float min = dxMin;
                normal = -Vector3.UnitX;
                if (dxMax < min) { min = dxMax; normal = Vector3.UnitX; }
                if (dyMin < min) { min = dyMin; normal = -Vector3.UnitY; }
                if (dyMax < min) { min = dyMax; normal = Vector3.UnitY; }
                if (dzMin < min) { min = dzMin; normal = -Vector3.UnitZ; }
                if (dzMax < min) { min = dzMax; normal = Vector3.UnitZ; }

                penetration = r + min;
            }

            contact = new Contact(normal, penetration, closest);
            return true;
        }

        private static bool TryBoxSphere(in AABB box, in Vector3 boxPos, in SphereBounds sphere, in Vector3 spherePos, out Contact contact)
        {
            if (!TrySphereBox(in sphere, in spherePos, in box, in boxPos, out contact)) return false;
            contact = contact with { Normal = -contact.Normal };
            return true;
        }

        private static Vector3 Clamp(in Vector3 value, in Vector3 min, in Vector3 max)
        {
            return new Vector3(
                Math.Clamp(value.X, min.X, max.X),
                Math.Clamp(value.Y, min.Y, max.Y),
                Math.Clamp(value.Z, min.Z, max.Z)
            );
        }
    }
}
