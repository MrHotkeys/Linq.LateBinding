using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingCalculateBuilderFromCallback : ILateBindingCalculateMethodBuilder
    {
        public string Method { get; }

        public IReadOnlyList<Type> ParameterTypes { get; }

        private Func<ILateBindingCalculateBuilderContext, Expression?> Callback { get; }

        public LateBindingCalculateBuilderFromCallback(string method, IList<Type> parameterTypes, Func<ILateBindingCalculateBuilderContext, Expression?> callback)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));
            Callback = callback ?? throw new ArgumentNullException(nameof(callback));
            ParameterTypes = parameterTypes is not null ?
                new ReadOnlyCollection<Type>(parameterTypes) :
                throw new ArgumentNullException(nameof(parameterTypes));
        }

        public Expression? Build(ILateBindingCalculateBuilderContext context)
        {
            if (context is null)
                throw new ArgumentNullException(nameof(context));

            return Callback(context);
        }

        public override string ToString() =>
            $"{Method}({string.Join(", ", ParameterTypes.Select(t => t.Name))})";
    }
}