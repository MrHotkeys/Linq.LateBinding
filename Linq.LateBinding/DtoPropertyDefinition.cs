using System;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class DtoPropertyDefinition
    {
        public string Name { get; }

        public Type Type { get; }

        public DtoPropertyDefinition(string name, Type type)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }
    }
}