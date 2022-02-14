using System.Collections.Generic;

using MrHotkeys.Linq.LateBinding;

namespace System.Linq
{
    public static class LinqExtensions
    {
        public static QueryableWithLateBinding<T> WithLateBinding<T>(this IQueryable<T> entities) => new QueryableWithLateBinding<T>(entities);
    }

    public static class IEnumerableExtensions
    {
        public static QueryableWithLateBinding<T> AsQueryableWithLateBinding<T>(this IEnumerable<T> entities) => new QueryableWithLateBinding<T>(entities.AsQueryable());
    }
}
