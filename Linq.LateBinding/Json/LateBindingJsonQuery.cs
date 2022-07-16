using System.Collections.Generic;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding.Json
{
    public sealed class JsonQuery : ILateBindingQuery
    {
        public IDictionary<string, ILateBinding>? Select { get; set; }

        public ICollection<ILateBinding>? Where { get; set; }

        public IList<LateBindingOrderBy>? OrderBy { get; set; }

        public int? Skip { get; set; }

        public int? Take { get; set; }

        public JsonQuery()
        { }
    }
}