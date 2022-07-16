using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingCalculateMethodBuilder
    {
        public string Method { get; }

        public IReadOnlyList<Type> ParameterTypes { get; }

        public Expression? Build(ILateBindingCalculateBuilderContext context);
    }
}