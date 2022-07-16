using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingQuery
    {
        public IDictionary<string, ILateBinding>? Select { get; }

        public ICollection<ILateBinding>? Where { get; }

        public IList<LateBindingOrderBy>? OrderBy { get; }

        public int? Skip { get; }

        public int? Take { get; }
    }
}