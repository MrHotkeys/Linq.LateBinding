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

        private Dictionary<CacheKey, DtoTypeInfo.Weak> DtoTypeInfoCache { get; } = new Dictionary<CacheKey, DtoTypeInfo.Weak>();

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
            if (DtoTypeInfoCache.TryGetValue(key, out var infoWeak) && infoWeak.TryGetDtoTypeInfo(out info))
            {
                return true;
            }
            else
            {
                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    if (infoWeak is null)
                        Logger.LogTrace("No DTO Type found in cache for {key}.", key);
                    else
                        Logger.LogTrace("Found dead DTO Type reference in cache for {key}.", key);
                }

                info = default;
                return false;
            }
        }

        private DtoTypeInfo GenerateAndCache(CacheKey key, IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("Entering lock to generate DTO Type for {key}.", key);

            lock (DtoTypeInfoCacheLock)
            {
                // Now that we're inside the lock, we'll check the cache again, and the weak
                // reference returned, to see if something else refreshed the cache first
                if (TryGetDtoType(key, out var info))
                {
                    if (Logger.IsEnabled(LogLevel.Trace))
                        Logger.LogTrace("Aborted generating DTO Type (exists after lock) for {key}.", key);

                    return info;
                }
                else
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                        Logger.LogDebug("Generating DTO Type for {key}.", key);

                    info = Generator.Generate(propertyDefintions);
                    var infoWeak = info.ToWeak();
                    DtoTypeInfoCache[key] = infoWeak;
                    infoWeak.DtoTypeFinalizing += HandleDtoTypeInfoFinalizing;

                    return info;
                }
            }
        }

        private void HandleDtoTypeInfoFinalizing(object sender, EventArgs args)
        {
            var infoWeak = (DtoTypeInfo.Weak)sender;
            var key = new CacheKey(infoWeak.PropertyDefinitions);

            // Lock so we don't remove a good reference immediately after finding a bad one because of a race
            lock (DtoTypeInfoCacheLock)
            {
                // Only remove if we find a dead reference or the reference we know is on its way out
                if (DtoTypeInfoCache.TryGetValue(key, out var existingInfoWeak) && ReferenceEquals(existingInfoWeak, infoWeak))
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                        Logger.LogDebug("Removing dead reference to DTO Type \"{dtoTypeFullName}\" for {key}.", infoWeak.DtoTypeFullName, key);

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