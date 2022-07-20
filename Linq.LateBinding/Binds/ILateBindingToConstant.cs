using System;

namespace MrHotkeys.Linq.LateBinding.Binds
{
    public interface ILateBindingToConstant : ILateBinding
    {
        public object? GetValue();
        public bool TryGetValueAs(Type type, out object? value);
    }
}