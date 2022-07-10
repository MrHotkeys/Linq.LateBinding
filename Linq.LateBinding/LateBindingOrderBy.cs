using System;
using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingOrderBy
    {
        public bool Ascending { get; }

        public ILateBindingExpression Expression { get; }

        public LateBindingOrderBy(bool ascending, ILateBindingExpression expression)
        {
            Ascending = ascending;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public override string ToString() =>
            Expression.ToString() + (Ascending ? " ascending" : " descending");
    }
}