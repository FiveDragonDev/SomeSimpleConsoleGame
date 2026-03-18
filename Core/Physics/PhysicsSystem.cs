using System.Numerics;

namespace SomeSimpleConsoleGame.Core.Physics
{
    public sealed class PhysicsSystem : IUpdateSystem
    {
        public IReadOnlyCollection<PhysicsBody> Bodies => _bodies;
        public IReadOnlyCollection<IForce> GlobalForces => _globalForces;

        private readonly HashSet<PhysicsBody> _bodies = [];
        private readonly HashSet<IForce> _globalForces = [];

        private readonly float _timeStep;
        private double _accumulator = 0;

        public PhysicsSystem(in int updatesPerSecond)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(updatesPerSecond);
            _timeStep = 1f / updatesPerSecond;
        }

        public void AddBody(PhysicsBody body) => _bodies.Add(body);
        public bool RemoveBody(PhysicsBody body) => _bodies.Remove(body);

        public void AddGlobalForce(in IForce force) => _globalForces.Add(force);
        public void RemoveGlobalForce(in IForce force) => _globalForces.Remove(force);

        public void Update(double deltaTime)
        {
            _accumulator += deltaTime;
            while (_accumulator >= _timeStep)
            {
                Update();
                _accumulator -= _timeStep;
            }
        }

        private void Update()
        {
            var bodies = Bodies.ToArray();
            for (int i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                UpdateKinematics(body);

                for (int j = i + 1; j < bodies.Length; j++)
                {
                    var other = bodies[j];
                    ResolveCollisions(body, other);
                }
            }
        }

        private void ResolveCollisions(PhysicsBody body, PhysicsBody other)
        {
            // im lazy mabe later
        }

        private void UpdateKinematics(PhysicsBody body)
        {
            var kinematics = body.KinematicParameters;
            var (position, velocity, acceleration) = kinematics;

            position += velocity * _timeStep + 0.5f * acceleration * _timeStep * _timeStep;

            var newAcceleration = ComputeForces(body) / body.Mass;

            velocity += 0.5f * (acceleration + newAcceleration) * _timeStep;

            body.UpdateKinematics(new(position, velocity, newAcceleration));
        }

        private Vector3 ComputeForces(PhysicsBody body)
        {
            Vector3 forces = Vector3.Zero;
            foreach (var force in GlobalForces)
            {
                forces += force.GetForce(body);
            }
            return forces;
        }
    }
}
