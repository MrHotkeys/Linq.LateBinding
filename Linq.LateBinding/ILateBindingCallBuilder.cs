using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingCallBuilder
    {
        public string Method { get; }

        public IReadOnlyList<Type> ParameterTypes { get; }

        public Expression? Build(ILateBindingCallBuilderContext context);
    }
}