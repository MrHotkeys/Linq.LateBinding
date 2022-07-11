using System;
using System.Collections.Generic;
using System.Linq;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class CachingDtoTypeGenerator : IDtoTypeGenerator
    {
        private IDtoTypeGenerator Generator { get; }

        private Dictionary<CacheKey, Type> DtoTypeCache { get; } = new Dictionary<CacheKey, Type>();

        private object DtoTypeCacheLock { get; } = new object();

        public CachingDtoTypeGenerator(IDtoTypeGenerator generator)
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public Type Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            var key = new CacheKey(propertyDefintions);
            if (!DtoTypeCache.TryGetValue(key, out var dtoType))
            {
                lock (DtoTypeCacheLock)
                {
                    if (!DtoTypeCache.TryGetValue(key, out dtoType))
                    {
                        dtoType = Generator.Generate(propertyDefintions);
                        DtoTypeCache[key] = dtoType;
                    }
                }
            }

            return dtoType;
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