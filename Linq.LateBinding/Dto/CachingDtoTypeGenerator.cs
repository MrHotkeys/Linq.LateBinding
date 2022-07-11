using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class CachingDtoTypeGenerator : IDtoTypeGenerator
    {
        private IDtoTypeGenerator Generator { get; }

        private Dictionary<CacheKey, WeakReference<Type>> DtoTypeCache { get; } = new Dictionary<CacheKey, WeakReference<Type>>();

        private object DtoTypeCacheLock { get; } = new object();

        public CachingDtoTypeGenerator(IDtoTypeGenerator generator)
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public Type Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            var key = new CacheKey(propertyDefintions);

            return TryGetDtoType(key, out var dtoType) ?
                dtoType :
                GenerateAndCache(key, propertyDefintions);
        }

        private bool TryGetDtoType(CacheKey key, [NotNullWhen(true)] out Type? dtoType)
        {
            if (DtoTypeCache.TryGetValue(key, out var dtoTypeReference) && dtoTypeReference.TryGetTarget(out dtoType))
            {
                return true;
            }
            else
            {
                dtoType = default;
                return false;
            }
        }

        private Type GenerateAndCache(CacheKey key, IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            lock (DtoTypeCacheLock)
            {
                // Now that we're inside the lock, we'll check the cache again, and the weak
                // reference returned, to see if something else refreshed the cache first
                if (TryGetDtoType(key, out var dtoType))
                {
                    return dtoType;
                }
                else
                {
                    dtoType = Generator.Generate(propertyDefintions);
                    DtoTypeCache[key] = new WeakReference<Type>(dtoType);

                    return dtoType;
                }
            }
        }

        public void Reset()
        {
            Generator.Reset();
        }

        private struct CacheKey
        {
            private HashSet<DtoPropertyDefinition> Definitions { get; }

            public int HashCode { get; }

            public CacheKey(IEnumerable<DtoPropertyDefinition> definitions)
            {
                if (definitions is null)
                    throw new ArgumentNullException(nameof(definitions));
                Definitions = new HashSet<DtoPropertyDefinition>(definitions);

                var hashCodeBuilder = new HashCode();
                foreach (var definition in definitions)
                    hashCodeBuilder.Add(definition);
                HashCode = hashCodeBuilder.ToHashCode();
            }

            public override bool Equals(object obj) =>
                obj is CacheKey other && Equals(other);

            public bool Equals(CacheKey other)
            {
                if (other.Definitions.Count != Definitions.Count)
                    return false;

                foreach (var definition in other.Definitions)
                {
                    if (!Definitions.Contains(definition))
                        return false;
                }

                return true;
            }

            public override int GetHashCode() =>
                HashCode;

            public override string ToString() =>
                $"{{ {string.Join(" ", Definitions.Select(d => $"{d};"))} }}";
        }
    }
}