using System.Numerics;
using System.Buffers;

namespace SomeSimpleConsoleGame.Core.Physics
{
    using SomeSimpleConsoleGame.Core.World;

    public sealed class PhysicsSystem : IUpdateSystem, IDisposable
    {
        public IReadOnlyCollection<IForce> GlobalForces => _globalForces;

        private readonly HashSet<IForce> _globalForces = [];
        private readonly World _world;
        private readonly SpatialGrid<Entity> _broadphase;
        private float _maxBroadphaseRadius = 0;

        private readonly float _timeStep;
        private float _accumulator = 0;
        private Entity[] _candidateBuffer = Array.Empty<Entity>();

        public PhysicsSystem(World world, in int updatesPerSecond, float broadphaseCellSize = 0.5f)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(updatesPerSecond);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(broadphaseCellSize);
            _timeStep = 1f / updatesPerSecond;
            _broadphase = new(broadphaseCellSize);
        }

        public void AddGlobalForce(in IForce force) => _globalForces.Add(force);
        public void RemoveGlobalForce(in IForce force) => _globalForces.Remove(force);

        public void Update(float deltaTime)
        {
            _accumulator += deltaTime;
            while (_accumulator >= _timeStep)
            {
                FixedUpdate();
                _accumulator -= _timeStep;
            }
        }

        private void FixedUpdate()
        {
            var world = _world;

            foreach (var entity in world.Entities)
            {
                if (!world.TryGet(entity, out PhysicsBody? body) || !world.TryGet(entity, out Transform transform)) continue;

                body!.UpdateKinematics(body.KinematicParameters with { Position = transform.Position, Rotation = transform.Rotation });
                if (!body.IsStatic) UpdateKinematics(body);

                transform.Position = body.KinematicParameters.Position;
                transform.Rotation = body.KinematicParameters.Rotation;
                world.TrySet(entity, in transform);
            }

            RebuildBroadphase(world.Entities);
            ResolveAllCollisions(world.Entities);
        }

        private void RebuildBroadphase(IEnumerable<Entity> entities)
        {
            _maxBroadphaseRadius = 0;
            _broadphase.Clear();

            foreach (var entity in entities)
            {
                if (!_world.TryGet(entity, out PhysicsBody? body)) continue;

                var pos = body!.KinematicParameters.Position;
                _broadphase.Add(entity, body.Bounds.GetWorldCenter(pos));
                _maxBroadphaseRadius = MathF.Max(_maxBroadphaseRadius, body.Bounds.Radius);
            }
        }

        private void ResolveAllCollisions(IEnumerable<Entity> entities)
        {
            var pool = ArrayPool<Entity>.Shared;
            foreach (var aEntity in entities)
            {
                if (!_world.TryGet(aEntity, out PhysicsBody? a)) continue;

                var aPos = a!.KinematicParameters.Position;
                var aCenter = a.Bounds.GetWorldCenter(aPos);
                float radius = a.Bounds.Radius + _maxBroadphaseRadius;

                if (_candidateBuffer.Length == 0) _candidateBuffer = pool.Rent(128);

                int candidateCount = _broadphase.GetItemsInRadius(aCenter, radius, _candidateBuffer);
                if (candidateCount >= _candidateBuffer.Length)
                {
                    pool.Return(_candidateBuffer);
                    _candidateBuffer = pool.Rent(_candidateBuffer.Length * 2);
                    candidateCount = _broadphase.GetItemsInRadius(aCenter, radius, _candidateBuffer);
                }

                for (int ci = 0; ci < candidateCount; ci++)
                {
                    var bEntity = _candidateBuffer[ci];
                    if (bEntity.Id <= aEntity.Id) continue;
                    if (!_world.TryGet(bEntity, out PhysicsBody? b)) continue;
                    ResolveCollision(aEntity, a, bEntity, b!);
                }
            }
            if (_candidateBuffer.Length != 0)
            {
                pool.Return(_candidateBuffer);
                _candidateBuffer = Array.Empty<Entity>();
            }
        }

        private void ResolveCollision(Entity aEntity, PhysicsBody a, Entity bEntity, PhysicsBody b)
        {
            if (a.IsStatic && b.IsStatic) return;

            var aKin = a.KinematicParameters;
            var bKin = b.KinematicParameters;

            if (!a.Bounds.TryComputeContact(aKin.Position, b.Bounds, bKin.Position, out var contact)) return;

            float invMassA = a.IsStatic ? 0f : 1f / a.Mass;
            float invMassB = b.IsStatic ? 0f : 1f / b.Mass;
            float invMassSum = invMassA + invMassB;
            if (invMassSum <= 0) return;

            const float slop = 0.0005f;
            const float percent = 0.8f;
            float correctionMag = MathF.Max(contact.Penetration - slop, 0f) / invMassSum * percent;
            Vector3 correction = correctionMag * contact.Normal;

            Vector3 aPos = aKin.Position - correction * invMassA;
            Vector3 bPos = bKin.Position + correction * invMassB;

            Vector3 rv = bKin.LinearVelocity - aKin.LinearVelocity;
            float velAlongNormal = Vector3.Dot(rv, contact.Normal);
            if (velAlongNormal < 0)
            {
                float restitution = MathF.Min(a.Restitution, b.Restitution);
                float j = -(1f + restitution) * velAlongNormal / invMassSum;
                Vector3 impulse = j * contact.Normal;

                Vector3 aVel = aKin.LinearVelocity - impulse * invMassA;
                Vector3 bVel = bKin.LinearVelocity + impulse * invMassB;

                a.UpdateKinematics(aKin with { Position = aPos, LinearVelocity = aVel });
                b.UpdateKinematics(bKin with { Position = bPos, LinearVelocity = bVel });
            }
            else
            {
                a.UpdateKinematics(aKin with { Position = aPos });
                b.UpdateKinematics(bKin with { Position = bPos });
            }

            if (_world.TryGet(aEntity, out Transform aTransform))
            {
                aTransform.Position = a.KinematicParameters.Position;
                _world.TrySet(aEntity, in aTransform);
            }

            if (_world.TryGet(bEntity, out Transform bTransform))
            {
                bTransform.Position = b.KinematicParameters.Position;
                _world.TrySet(bEntity, in bTransform);
            }

            _broadphase.Move(aEntity, a.Bounds.GetWorldCenter(a.KinematicParameters.Position));
            _broadphase.Move(bEntity, b.Bounds.GetWorldCenter(b.KinematicParameters.Position));
        }

        private void UpdateKinematics(PhysicsBody body)
        {
            var kinematics = body.KinematicParameters;
            var (position, linearVelocity, linearAcceleration, rotation, angularVelocity) = kinematics;

            position += _timeStep * (linearVelocity + (0.5f * linearAcceleration * _timeStep));

            var newAcceleration = ComputeForces(body) / body.Mass;

            linearVelocity += 0.5f * (linearAcceleration + newAcceleration) * _timeStep;

            body.UpdateKinematics(new(position, linearVelocity, newAcceleration, rotation, angularVelocity));
        }

        private Vector3 ComputeForces(PhysicsBody body)
        {
            if (body.IsStatic) return Vector3.Zero;
            Vector3 forces = Vector3.Zero;
            foreach (var force in GlobalForces)
            {
                forces += force.GetForce(body);
            }

            forces += body.ConsumeAccumulatedForces();
            return forces;
        }

        public void Dispose()
        {
            _globalForces.Clear();
            _broadphase.Clear();
        }
    }
}
