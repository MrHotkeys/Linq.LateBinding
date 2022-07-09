using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class CalculateLateBindingExpression : ILateBindingExpression
    {
        public LateBindingExpressionType Type => LateBindingExpressionType.Constant;

        public string Method { get; }

        public ImmutableArray<ILateBindingExpression> Expressions { get; }

        public CalculateLateBindingExpression(string method, IEnumerable<ILateBindingExpression> expressions)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));

            if (expressions is null)
                throw new ArgumentNullException(nameof(expressions));
            Expressions = expressions.ToImmutableArray();
        }

        public override string ToString() =>
            $"{Method}({string.Join(", ", Expressions)})";
    }
}