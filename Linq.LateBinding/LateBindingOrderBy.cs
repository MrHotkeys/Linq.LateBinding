using System;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingOrderBy
    {
        public bool Ascending { get; }

        public ILateBinding Expression { get; }

        public LateBindingOrderBy(bool ascending, ILateBinding expression)
        {
            Ascending = ascending;
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        }

        public override string ToString() =>
            Expression.ToString() + (Ascending ? " ascending" : " descending");
    }
}