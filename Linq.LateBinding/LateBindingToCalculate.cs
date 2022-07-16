using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingToCalculate : ILateBindingToCalculate
    {
        public LateBindingTarget Target => LateBindingTarget.Calculate;

        public string Method { get; }

        public IReadOnlyList<ILateBinding> Arguments { get; }

        public LateBindingToCalculate(string method, IEnumerable<ILateBinding> arguments)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));

            if (arguments is null)
                throw new ArgumentNullException(nameof(arguments));
            Arguments = new ReadOnlyCollection<ILateBinding>(arguments.ToArray());
        }

        public override string ToString() =>
            $"{Method}({string.Join(", ", Arguments)})";
    }
}