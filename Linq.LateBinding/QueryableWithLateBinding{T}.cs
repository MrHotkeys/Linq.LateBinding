using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using MrHotkeys.Linq.LateBinding.Expressions;

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

        public QueryableWithLateBinding<object?> Query(ILateBindingQuery query)
        {
            var queryable = query.Select is not null ?
                Select(query.Select) :
                SelectAsObjects();

            if (query.Where is not null)
            { }

            if (query.OrderBy is not null)
            { }

            if (query.Skip.HasValue)
            { }

            if (query.Take.HasValue)
            { }

            return queryable;
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


        public QueryableWithLateBinding<object?> Select(IDictionary<string, ILateBindingExpression> select)
        {
            if (select is null)
                throw new ArgumentNullException(nameof(select));

            var selectSourceParameterExpr = Expression.Parameter(typeof(T));
            var selectMemberExpressions = new Dictionary<string, Expression>();

            var dtoPropertyDefinitions = new List<DtoPropertyDefinition>();

            foreach (var (name, lateBindingExpression) in select)
            {
                var expression = QueryableWithLateBinding.ExpressionTreeBuilder.Build(selectSourceParameterExpr, lateBindingExpression);
                selectMemberExpressions[name] = expression;

                var dtoPropertyDefinition = new DtoPropertyDefinition(name, expression.Type);
                dtoPropertyDefinitions.Add(dtoPropertyDefinition);
            }

            var dtoType = QueryableWithLateBinding.DtoGenerator.Generate(dtoPropertyDefinitions);

            var dtoConstructor = dtoType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null);

            var selectMemberBindings = new List<MemberBinding>();
            foreach (var (name, expression) in selectMemberExpressions)
            {
                var property = dtoType.GetProperty(name) ?? throw new InvalidOperationException();
                var memberBinding = Expression.Bind(property, expression);
                selectMemberBindings.Add(memberBinding);
            }

            var selectNewExpr = Expression.New(dtoConstructor);
            var selectMemberInitExpr = Expression.MemberInit(selectNewExpr, selectMemberBindings);
            var selectObjectExpr = Expression.Convert(selectMemberInitExpr, typeof(object));
            var selectExpr = Expression.Lambda<Func<T, object>>(selectMemberInitExpr, selectSourceParameterExpr); // TODO: Add null for source parameter

            var entitiesSelected = Entities.Select(selectExpr);
            return new QueryableWithLateBinding<object?>(entitiesSelected);
        }

        private QueryableWithLateBinding<object?> SelectAsObjects()
        {
            var entitiesSelected = Entities.Select(x => (object?)x);
            return new QueryableWithLateBinding<object?>(entitiesSelected);
        }

        public IEnumerator<T> GetEnumerator() => Entities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
