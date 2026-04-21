using System.Diagnostics.CodeAnalysis;

namespace SomeSimpleConsoleGame.Core.World
{
    public sealed class World : IDisposable
    {
        public IComponent[] this[Entity entity]
        {
            get => [.. _items[entity].Values];
        }
        public IComponent? this[Entity entity, Type componentType]
        {
            get
            {
                if (!_items.TryGetValue(entity, out var components) ||
                    !components.TryGetValue(componentType, out var component)) return null;
                return component;
            }
        }

        public IEnumerable<Entity> Entities => _items.Keys;
        public int Count => _items.Count;

        private readonly Dictionary<Entity, Dictionary<Type, IComponent>> _items = [];
        private uint _nextEntityValue = 1;

        public void Add(Entity entity, in IComponent component)
        {
            if (!_items.TryGetValue(entity, out var components))
            {
                components = [];
                _items[entity] = components;
            }

            components[component.GetType()] = component;
        }
        public bool Has(Entity entity) => _items.ContainsKey(entity);
        public bool Remove(Entity entity) => _items.Remove(entity);

        public Entity[] GetEntities<T>() where T : IComponent => [.. _items.Where(i => i.Value.ContainsKey(typeof(T))).Select(i => i.Key)];

        public bool Has<T>(Entity entity) where T : IComponent => TryGet<T>(entity, out _);
        public bool TryGet<T>(Entity entity, [MaybeNullWhen(false)] out T? component) where T : IComponent
        {
            component = default;
            if (_items.TryGetValue(entity, out var components) && components.TryGetValue(typeof(T), out var tComponent))
            {
                component = (T)tComponent!;
                return true;
            }
            return false;
        }
        public bool TryAdd<T>(Entity entity, in T component) where T : IComponent =>
            _items.TryGetValue(entity, out var components) && components.TryAdd(typeof(T), component);
        public bool TrySet<T>(Entity entity, in T component) where T : IComponent
        {
            if (Has<T>(entity))
            {
                _items[entity][typeof(T)] = component;
                return true;
            }
            return false;
        }

        public Entity CreateEntity()
        {
            Entity entity = new(_nextEntityValue++);
            _items.Add(entity, []);
            return entity;
        }

        public void Dispose()
        {
            foreach (var item in _items)
            {
                item.Value.Clear();
            }
            _items.Clear();
        }
    }
}
