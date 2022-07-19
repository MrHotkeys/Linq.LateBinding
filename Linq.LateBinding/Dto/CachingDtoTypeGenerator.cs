using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class CachingDtoTypeGenerator : IDtoTypeGenerator
    {
        private ILogger Logger { get; }

        public IDtoTypeGenerator Generator { get; }

        private Dictionary<CacheKey, WeakReference<DtoTypeInfo>> DtoTypeInfoCache { get; } = new Dictionary<CacheKey, WeakReference<DtoTypeInfo>>();

        private object DtoTypeInfoCacheLock { get; } = new object();

        public CachingDtoTypeGenerator(ILogger<CachingDtoTypeGenerator> logger, IDtoTypeGenerator generator)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            var key = new CacheKey(propertyDefintions);

            return TryGetDtoType(key, out var info) ?
                info :
                GenerateAndCache(key, propertyDefintions);
        }

        private bool TryGetDtoType(CacheKey key, [NotNullWhen(true)] out DtoTypeInfo? info)
        {
            if (DtoTypeInfoCache.TryGetValue(key, out var infoReference) && infoReference.TryGetTarget(out info))
            {
                return true;
            }
            else
            {
                info = default;
                return false;
            }
        }

        private DtoTypeInfo GenerateAndCache(CacheKey key, IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            lock (DtoTypeInfoCacheLock)
            {
                // Now that we're inside the lock, we'll check the cache again, and the weak
                // reference returned, to see if something else refreshed the cache first
                if (TryGetDtoType(key, out var dtoTypeInfo))
                {
                    return dtoTypeInfo;
                }
                else
                {
                    dtoTypeInfo = Generator.Generate(propertyDefintions);
                    DtoTypeInfoCache[key] = new WeakReference<DtoTypeInfo>(dtoTypeInfo);
                    dtoTypeInfo.Finalizing += HandleDtoTypeInfoFinalizing;

                    return dtoTypeInfo;
                }
            }
        }

        private void HandleDtoTypeInfoFinalizing(object sender, EventArgs args)
        {
            var dtoTypeInfo = (DtoTypeInfo)sender;
            var key = new CacheKey(dtoTypeInfo.PropertyDefinitions);

            // Lock so we don't remove a good reference immediately after finding a bad one because of a race
            lock (DtoTypeInfoCacheLock)
            {
                // Only remove if we find a dead reference or the reference we know is on its way out
                if (DtoTypeInfoCache.TryGetValue(key, out var infoReference) &&
                    (!infoReference.TryGetTarget(out var existingInfo) || ReferenceEquals(existingInfo, dtoTypeInfo)))
                {
                    DtoTypeInfoCache.Remove(key);
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