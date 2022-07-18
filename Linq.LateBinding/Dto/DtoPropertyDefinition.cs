using System;

namespace MrHotkeys.Linq.LateBinding.Dto
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

        public override bool Equals(object obj) =>
            obj is DtoPropertyDefinition other && Equals(other);

        public bool Equals(DtoPropertyDefinition other) =>
            other.Name == Name && other.Type == Type;

        public override int GetHashCode() =>
            HashCode.Combine(Name, Type);

        public override string ToString() =>
            $"{Type.Name} {Name}";

        public void Deconstruct(out string name, out Type type)
        {
            name = Name;
            type = Type;
        }
    }
}