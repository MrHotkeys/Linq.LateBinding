using System;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingToConstant : ILateBinding
    {
        public object? GetValue();
        public bool TryGetValueAs(Type type, out object? value);
    }
}