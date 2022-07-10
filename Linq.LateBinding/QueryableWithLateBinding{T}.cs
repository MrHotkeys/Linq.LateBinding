﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class QueryableWithLateBinding<T> : IQueryable<T>
    {
        private IQueryable<T> Entities { get; }

        public Type ElementType => Entities.ElementType;
        public Expression Expression => Entities.Expression;
        public IQueryProvider Provider => Entities.Provider;

        public QueryableWithLateBinding(IQueryable<T> entities)
        {
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
        }

        public QueryableWithLateBinding<T> OrderBy(string propertyName) =>
            OrderBy(propertyName, true);

        public QueryableWithLateBinding<T> OrderByDescending(string propertyName) =>
            OrderBy(propertyName, false);

        private QueryableWithLateBinding<T> OrderBy(string propertyName, bool ascending)
        {
            if (propertyName is null)
                throw new ArgumentNullException(nameof(propertyName));

            var property = typeof(T).GetProperty(propertyName);
            if (property is null)
                throw new ArgumentException($"Property {propertyName} not found on type {typeof(T).Name}!", nameof(propertyName));

            return (QueryableWithLateBinding<T>)typeof(QueryableWithLateBinding<T>)
                .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                .Where(m => m.Name == nameof(OrderBy) && m.ContainsGenericParameters)
                .Single()
                .GetGenericMethodDefinition()
                .MakeGenericMethod(property.PropertyType)
                .Invoke(this, new object[] { property, ascending })!;
        }

        private QueryableWithLateBinding<T> OrderBy<TProperty>(PropertyInfo property, bool ascending)
        {
            var entityExpr = Expression.Parameter(typeof(T));
            var memberExpr = Expression.MakeMemberAccess(entityExpr, property);
            var lambdaExpr = Expression.Lambda<Func<T, TProperty>>(memberExpr, entityExpr);

            var entitiesOrdered = ascending ?
                Entities.OrderBy(lambdaExpr) :
                Entities.OrderByDescending(lambdaExpr);

            return new QueryableWithLateBinding<T>(entitiesOrdered);
        }

        public QueryableWithLateBinding<TDto> Select<TDto>(params string[] propertyNames)
        {
            if (propertyNames is null)
                throw new ArgumentNullException(nameof(propertyNames));

            var dtoType = QueryableWithLateBinding.DtoGenerator.Generate<T, TDto>(propertyNames);

            var entityExpr = Expression.Parameter(typeof(T));

            var memberBindings = propertyNames
                .Select(pn =>
                {
                    var sourceProperty = typeof(T).GetProperty(pn)!;
                    var dtoProperty = dtoType.GetProperty(pn)!;

                    if (sourceProperty.GetMethod is null)
                        throw new ArgumentException();

                    var sourcePropertyExpr = Expression.MakeMemberAccess(entityExpr, sourceProperty);

                    return Expression.Bind(dtoProperty, sourcePropertyExpr);
                })
                .ToArray();

            var newDtoExpr = Expression.New(dtoType.GetConstructor(Type.EmptyTypes)!);
            var dtoInitExpr = Expression.MemberInit(newDtoExpr, memberBindings);
            var lambdaExpr = Expression.Lambda<Func<T, TDto>>(dtoInitExpr, entityExpr);

            var entitiesSelected = Entities.Select(lambdaExpr);

            return new QueryableWithLateBinding<TDto>(entitiesSelected);
        }

        public IEnumerator<T> GetEnumerator() => Entities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}