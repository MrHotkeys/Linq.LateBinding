using System;

using MrHotkeys.Linq.LateBinding.Binds;

namespace MrHotkeys.Linq.LateBinding.Queries
{
    public sealed class LateBindingOrderBy
    {
        public bool Ascending { get; }

        public ILateBinding Binding { get; }

        public LateBindingOrderBy(bool ascending, ILateBinding binding)
        {
            Ascending = ascending;
            Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        }

        public override string ToString() =>
            Binding.ToString() + (Ascending ? " ascending" : " descending");
    }
}