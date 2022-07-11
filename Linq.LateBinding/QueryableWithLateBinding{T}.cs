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
            var queryable = this;

            if (query.Where is not null)
                queryable = queryable.Where(query.Where);

            if (query.OrderBy is not null)
            { }

            if (query.Skip.HasValue)
            { }

            if (query.Take.HasValue)
            { }

            // Select last since it upcasts everything as object (since the result will be a generated DTO type if a select
            // is defined) and that will prevent Where and OrderBy from seeing the correct object type to find members
            return query.Select is not null ?
                queryable.Select(query.Select) :
                queryable.SelectAsObjects();
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

            var dtoConstructor = dtoType.GetConstructor(BindingFlags.Public | BindingFlags.Instance, Type.DefaultBinder, Type.EmptyTypes, null) ?? throw new InvalidOperationException();

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

        public QueryableWithLateBinding<T> Where(IEnumerable<ILateBindingExpression> where)
        {
            var whereTargetParameterExpr = Expression.Parameter(typeof(T));
            var whereBodyExpr = where
                .Select(w =>
                {
                    var expression = QueryableWithLateBinding.ExpressionTreeBuilder.Build(whereTargetParameterExpr, w);
                    if (expression.Type != typeof(bool))
                        throw new InvalidOperationException();
                    return expression;
                })
                .Aggregate((left, right) => Expression.And(left, right));

            var whereExpr = Expression.Lambda<Func<T, bool>>(whereBodyExpr, whereTargetParameterExpr);

            var entitiesSelected = Entities.Where(whereExpr);
            return new QueryableWithLateBinding<T>(entitiesSelected);
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

        public IEnumerator<T> GetEnumerator() => Entities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
