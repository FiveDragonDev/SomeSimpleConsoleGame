using SomeSimpleConsoleGame.Core.World;
using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public sealed class PhysicsBody : IComponent
    {
        public record struct Kinematics(
            Vector3 Position, Vector3 LinearVelocity, Vector3 LinearAcceleration,
            Vector3 Rotation, Vector3 AngularVelocity);

        public IBounds Bounds { get; }
        public bool IsStatic { get; }
        public float Restitution
        {
            get => _restitution;
            set => _restitution = float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : throw new ArgumentOutOfRangeException(nameof(Restitution));
        }
        public Kinematics KinematicParameters { get; private set; }
        public Vector3 AccumulatedForces { get; private set; }
        public float Mass
        {
            get => _mass;
            set => _mass = (value <= 0 || !float.IsFinite(value)) ? throw new ArgumentOutOfRangeException(nameof(Mass)) : value;
        }

        private float _mass = 1;
        private float _restitution = 0.1f;

        public PhysicsBody(IBounds bounds, Vector3 position, Vector3 velocity, float mass = 1f, bool isStatic = false)
        {
            Bounds = bounds;
            KinematicParameters = new(position, velocity, Vector3.Zero, Vector3.Zero, Vector3.Zero);
            Mass = mass;
            IsStatic = isStatic;
        }

        public void ApplyForce(in IForce force) => ApplyForce(force.GetForce(this));
        public void ApplyForce(in Vector3 force)
        {
            if (IsStatic) return;
            AccumulatedForces += force;
        }
        public void ApplyImpulse(in Vector3 impulse)
        {
            if (IsStatic) return;
            var kinematics = KinematicParameters;
            var velocity = kinematics.LinearVelocity + impulse / Mass;
            KinematicParameters = kinematics with { LinearVelocity = velocity };
        }

        internal Vector3 ConsumeAccumulatedForces()
        {
            var forces = AccumulatedForces;
            AccumulatedForces = Vector3.Zero;
            return forces;
        }

        public void UpdateKinematics(in Kinematics kinematics) => KinematicParameters = kinematics;
        public void Deconstruct(out Vector3 position, out Vector3 linearVelocity, out Vector3 linearAcceleration,
            out Vector3 rotation, out Vector3 angularVelocity, out float mass, out IBounds bounds)
        {
            KinematicParameters.Deconstruct(out position, out linearVelocity, out linearAcceleration, out rotation, out angularVelocity);
            mass = _mass;
            bounds = Bounds;
        }
    }
}
