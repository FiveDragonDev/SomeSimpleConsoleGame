using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace SomeSimpleConsoleGame.Core.World
{
    public sealed class SpatialGrid<T> where T : notnull
    {
        private readonly record struct Vector3Int(int X, int Y, int Z)
        {
            public static Vector3Int Floor(Vector3 value) =>
                new((int)MathF.Floor(value.X),
                    (int)MathF.Floor(value.Y),
                    (int)MathF.Floor(value.Z));
        }

        public Vector3 this[T item]
        {
            get
            {
                if (TryGetPosition(item, out var position)) return position;
                throw new KeyNotFoundException();
            }
        }

        public float CellSize { get; }

        public int ItemCount => _locations.Count;
        public int CellCount => _grid.Count;

        public IReadOnlyCollection<T> AllItems => (IReadOnlyCollection<T>)_locations.Keys;

        private readonly float _inversedCellSize;

        private readonly ConcurrentDictionary<Vector3Int, ConcurrentDictionary<T, byte>> _grid = [];
        private readonly ConcurrentDictionary<T, Vector3> _locations = [];

        public SpatialGrid(float cellSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cellSize, nameof(cellSize));
            CellSize = cellSize;
            _inversedCellSize = 1f / cellSize;
        }

        public bool TryGetPosition(T item, out Vector3 position) => _locations.TryGetValue(item, out position);

        public bool Contains(T item) => _locations.ContainsKey(item);

        public int GetCellItemCount(Vector3 position) => _grid.TryGetValue(GetCell(position), out var bucket) ? bucket.Count : 0;

        public bool Add(T item, Vector3 position)
        {
            if (!_locations.TryAdd(item, position)) return false;

            var cell = GetCell(position);
            var bucket = GetOrCreateBucket(cell);
            bucket.TryAdd(item, 0);
            return true;
        }
        public bool Remove(T item)
        {
            if (!_locations.TryGetValue(item, out var record)) return false;

            lock (_grid)
            {
                _locations.TryRemove(item, out _);
                var cell = GetCell(record);
                if (_grid.TryGetValue(cell, out var bucket)) bucket.TryRemove(item, out _);
            }

            return true;
        }

        public bool Move(T item, Vector3 newPosition)
        {
            if (!_locations.TryGetValue(item, out var oldPosition)) return false;

            _locations[item] = newPosition;

            var oldCell = GetCell(oldPosition);
            var newCell = GetCell(newPosition);
            if (oldCell == newCell)
            {
                _locations[item] = newPosition;
                return true;
            }

            lock (_grid)
            {
                var newBucket = GetOrCreateBucket(newCell);
                newBucket.TryAdd(item, 0);
                if (_grid.TryGetValue(oldCell, out var oldBucket))
                    oldBucket.TryRemove(item, out _);
            }

            return true;
        }

        public IReadOnlyCollection<T> GetItemsInCell(Vector3 position) =>
            _grid.TryGetValue(GetCell(position), out var bucket) ? (IReadOnlyCollection<T>)bucket.Keys : Array.Empty<T>();

        public IReadOnlyCollection<T> GetItemsInRadius(Vector3 center, float radius)
        {
            if (radius <= 0 || !float.IsFinite(radius) || _grid.IsEmpty) return Array.Empty<T>();

            var items = new List<T>();
            float radiusSqr = radius * radius;

            var min = center - new Vector3(radius);
            var max = center + new Vector3(radius);

            var minCell = GetCell(min);
            var maxCell = GetCell(max);

            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        if (!_grid.TryGetValue(new(x, y, z), out var bucket)) continue;

                        foreach (var entity in bucket.Keys)
                        {
                            if (!_locations.TryGetValue(entity, out var pos)) continue;
                            if (Vector3.DistanceSquared(pos, center) <= radiusSqr) items.Add(entity);
                        }
                    }
                }
            }
            return items;
        }
        public int GetItemsInRadius(Vector3 center, float radius, Span<T> items)
        {
            if (radius <= 0 || !float.IsFinite(radius) || _grid.IsEmpty || items.Length == 0) return 0;

            var maxlength = items.Length;
            float radiusSqr = radius * radius;

            var min = center - new Vector3(radius);
            var max = center + new Vector3(radius);

            var minCell = GetCell(min);
            var maxCell = GetCell(max);

            int count = 0;
            for (int x = minCell.X; x <= maxCell.X; x++)
            {
                for (int y = minCell.Y; y <= maxCell.Y; y++)
                {
                    for (int z = minCell.Z; z <= maxCell.Z; z++)
                    {
                        if (!_grid.TryGetValue(new(x, y, z), out var bucket)) continue;

                        foreach (var (entity, _) in bucket)
                        {
                            if (!_locations.TryGetValue(entity, out var position)) continue;
                            if (Vector3.DistanceSquared(position, center) <= radiusSqr)
                            {
                                items[count++] = entity;
                                if (count >= maxlength) goto end;
                            }
                        }
                    }
                }
            }
        end: return count;
        }

        public void Clear()
        {
            _grid.Clear();
            _locations.Clear();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Vector3Int GetCell(Vector3 position) => Vector3Int.Floor(position * _inversedCellSize);
        private ConcurrentDictionary<T, byte> GetOrCreateBucket(Vector3Int cell) => _grid.GetOrAdd(cell, static _ => []);
    }
}
