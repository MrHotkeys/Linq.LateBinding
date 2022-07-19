using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingToCall : ILateBinding
    {
        public string Method { get; }

        public IReadOnlyList<ILateBinding> Arguments { get; }
    }
}