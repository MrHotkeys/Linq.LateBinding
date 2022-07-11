using System.Collections.Generic;

using MrHotkeys.Linq.LateBinding;

namespace System.Linq
{
    public static class LateBindingExtensions
    {
        public static QueryableWithLateBinding<T> WithLateBinding<T>(this IQueryable<T> entities) =>
            new QueryableWithLateBinding<T>(entities);
        public static QueryableWithLateBinding<object?> WithLateBinding<T>(this IQueryable<T> entities, ILateBindingQuery query) =>
            new QueryableWithLateBinding<T>(entities)
                .Query(query);

        public static QueryableWithLateBinding<T> AsQueryableWithLateBinding<T>(this IEnumerable<T> entities) =>
            new QueryableWithLateBinding<T>(entities.AsQueryable());
        public static QueryableWithLateBinding<object?> AsQueryableWithLateBinding<T>(this IEnumerable<T> entities, ILateBindingQuery query) =>
            new QueryableWithLateBinding<T>(entities.AsQueryable())
                .Query(query);
    }
}
