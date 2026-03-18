using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public sealed class PhysicsBody
    {
        public record struct Kinematics(Vector3 Position, Vector3 Velocity, Vector3 Acceleration);

        public Bounds Bounds { get; }
        public Kinematics KinematicParameters { get; private set; }
        public float Mass
        {
            get => _mass;
            set => _mass = value <= 0 ? throw new ArgumentOutOfRangeException(nameof(Mass)) : value;
        }

        private float _mass;

        public PhysicsBody(Bounds bounds, Vector3 position, Vector3 velocity)
        {
            Bounds = bounds;
            KinematicParameters = new(position, velocity, Vector3.Zero);
        }

        public void ApplyLocalForce(in IForce force)
        {
            var acceleration = force.GetForce(this) / Mass;
            KinematicParameters = KinematicParameters with { Acceleration = KinematicParameters.Acceleration + acceleration };
        }

        public void UpdateKinematics(in Kinematics kinematics) => KinematicParameters = kinematics;
        public void Deconstruct(out Vector3 position, out Vector3 velocity, out Vector3 acceleration, out float mass, out Bounds bounds)
        {
            KinematicParameters.Deconstruct(out position, out velocity, out acceleration);
            mass = _mass;
            bounds = Bounds;
        }
    }
}
