using System.Collections.Generic;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingQuery
    {
        public IDictionary<string, ILateBindingExpression>? Select { get; }

        public ICollection<ILateBindingExpression>? Where { get; }

        public IList<LateBindingOrderBy>? OrderBy { get; }

        public int? Skip { get; }

        public int? Take { get; }
    }
}