using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public interface IForce
    {
        Vector3 GetForce(PhysicsBody body);
    }

    public readonly struct LinearForce : IForce
    {
        public Vector3 Force { get; }

        public LinearForce(Vector3 force) => Force = force;

        public Vector3 GetForce(PhysicsBody body) => Force;
    }

    public readonly struct GravityForce : IForce
    {
        public Vector3 Acceleration { get; }

        public GravityForce(Vector3 acceleration) => Acceleration = acceleration;

        public Vector3 GetForce(PhysicsBody body) => body.Mass * Acceleration;
    }

    public readonly struct DragForce : IForce
    {
        public float LinearCoefficient { get; }
        public float QuadraticCoefficient { get; }

        public DragForce(float linearCoefficient, float quadraticCoefficient)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(linearCoefficient);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quadraticCoefficient);

            LinearCoefficient = linearCoefficient;
            QuadraticCoefficient = quadraticCoefficient;
        }

        public Vector3 GetForce(PhysicsBody body)
        {
            Vector3 velocity = body.KinematicParameters.Velocity;
            float speedSq = velocity.LengthSquared();
            if (speedSq == 0) return Vector3.Zero;

            return -velocity * (LinearCoefficient + QuadraticCoefficient * MathUtils.QSqrt(speedSq));
        }
    }
}
