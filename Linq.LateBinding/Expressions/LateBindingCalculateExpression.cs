using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class CalculateLateBindingExpression : ILateBindingExpression
    {
        public LateBindingExpressionType ExpressionType => LateBindingExpressionType.Constant;

        public string Method { get; }

        public IReadOnlyList<ILateBindingExpression> Expressions { get; }

        public CalculateLateBindingExpression(string method, IEnumerable<ILateBindingExpression> expressions)
        {
            Method = method ?? throw new ArgumentNullException(nameof(method));

            if (expressions is null)
                throw new ArgumentNullException(nameof(expressions));
            Expressions = new ReadOnlyCollection<ILateBindingExpression>(expressions.ToArray());
        }

        public override string ToString() =>
            $"{Method}({string.Join(", ", Expressions)})";
    }
}