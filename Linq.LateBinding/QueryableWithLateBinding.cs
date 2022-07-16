using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using MrHotkeys.Linq.LateBinding.Dto;
using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class QueryableWithLateBinding<T> : IQueryable<T>
    {
        private IQueryable<T> Entities { get; }

        public Type ElementType => Entities.ElementType;

        public Expression Expression => Entities.Expression;

        public IQueryProvider Provider => Entities.Provider;

        private IDtoTypeGenerator DtoTypeGenerator { get; }

        private ILateBindingExpressionTreeBuilder ExpressionTreeBuilder { get; }

        public QueryableWithLateBinding(IQueryable<T> entities, IDtoTypeGenerator dtoTypeGenerator, ILateBindingExpressionTreeBuilder expressionTreeBuilder)
        {
            Entities = entities ?? throw new ArgumentNullException(nameof(entities));
            DtoTypeGenerator = dtoTypeGenerator ?? throw new ArgumentNullException(nameof(dtoTypeGenerator));
            ExpressionTreeBuilder = expressionTreeBuilder ?? throw new ArgumentNullException(nameof(expressionTreeBuilder));
        }

        public QueryableWithLateBinding<object?> Query(ILateBindingQuery query)
        {
            if (query is null)
                throw new ArgumentNullException(nameof(query));

            var queryable = this;

            if (query.Where is not null)
                queryable = queryable.Where(query.Where);

            if (query.OrderBy is not null)
                queryable = queryable.OrderBy(query.OrderBy);

            if (query.Skip.HasValue)
                queryable = queryable.Skip(query.Skip.Value);

            if (query.Take.HasValue)
                queryable = queryable.Take(query.Take.Value);

            // Select last since it upcasts everything as object (since the result will be a generated DTO type if a select
            // is defined) and that will prevent Where and OrderBy from seeing the correct object type to find members
            return query.Select is not null ?
                queryable.Select(query.Select) :
                queryable.SelectAsObjects();
        }

        public QueryableWithLateBinding<object?> Select(IDictionary<string, ILateBinding> select)
        {
            if (select is null)
                throw new ArgumentNullException(nameof(select));

            var selectTargetParameterExpr = Expression.Parameter(typeof(T));
            var selectMemberExpressions = new Dictionary<string, Expression>();

            var dtoPropertyDefinitions = new List<DtoPropertyDefinition>();

            foreach (var (name, lateBindingExpression) in select)
            {
                if (name is null)
                    throw new ArgumentException("Cannot contain null keys!", nameof(select));
                if (lateBindingExpression is null)
                    throw new ArgumentException("Cannot contain null values!", nameof(select));

                var expression = ExpressionTreeBuilder.Build(selectTargetParameterExpr, lateBindingExpression);
                selectMemberExpressions[name] = expression;

                var dtoPropertyDefinition = new DtoPropertyDefinition(name, expression.Type);
                dtoPropertyDefinitions.Add(dtoPropertyDefinition);
            }

            var dtoType = DtoTypeGenerator.Generate(dtoPropertyDefinitions);

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
            var selectExpr = Expression.Lambda<Func<T, object>>(selectObjectExpr, selectTargetParameterExpr);

            var entities = Entities.Select(selectExpr);
            return new QueryableWithLateBinding<object?>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        private QueryableWithLateBinding<object?> SelectAsObjects()
        {
            var entities = Entities.Select(x => (object?)x);
            return new QueryableWithLateBinding<object?>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        public QueryableWithLateBinding<T> Where(IEnumerable<ILateBinding> where)
        {
            if (where is null)
                throw new ArgumentNullException(nameof(where));

            var targetParameterExpr = Expression.Parameter(typeof(T));
            var whereBodyExpr = where
                .Select(w =>
                {
                    if (w is null)
                        throw new ArgumentException("Cannot contain null!", nameof(where));

                    var expr = ExpressionTreeBuilder.Build(targetParameterExpr, w);
                    if (expr.Type != typeof(bool))
                        throw new InvalidOperationException();
                    return expr;
                })
                .Aggregate((left, right) => Expression.And(left, right));

            var whereExpr = Expression.Lambda<Func<T, bool>>(whereBodyExpr, targetParameterExpr);

            var entities = Entities.Where(whereExpr);
            return new QueryableWithLateBinding<T>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        public QueryableWithLateBinding<T> OrderBy(IEnumerable<LateBindingOrderBy> orderBy)
        {
            if (orderBy is null)
                throw new ArgumentNullException(nameof(orderBy));

            var entities = Entities;
            foreach (var ob in orderBy)
            {
                if (ob is null)
                    throw new ArgumentException("Cannot contain null!", nameof(orderBy));

                var targetParameterExpr = Expression.Parameter(typeof(T));
                var bodyExpr = ExpressionTreeBuilder.Build(targetParameterExpr, ob.Expression);

                // Need to call another method to assemble and apply the lambda so we can
                // do it in a context where the type of the member is a type parameter
                entities = (IQueryable<T>)typeof(QueryableWithLateBinding<T>)
                    .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Where(m => m.Name == nameof(ApplyOrderBy) && m.ContainsGenericParameters)
                    .Single()
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(bodyExpr.Type)
                    .Invoke(this, new object[] { entities, bodyExpr, targetParameterExpr, ob.Ascending })!;
            }

            return new QueryableWithLateBinding<T>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        private IQueryable<T> ApplyOrderBy<TMember>(IQueryable<T> entities, Expression bodyExpr, ParameterExpression targetParameterExpr, bool ascending)
        {
            var orderByExpr = Expression.Lambda<Func<T, TMember>>(bodyExpr, targetParameterExpr);

            return ascending ?
                entities.OrderBy(orderByExpr) :
                entities.OrderByDescending(orderByExpr);
        }

        public QueryableWithLateBinding<T> Skip(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Must be >= 0!");

            var entities = Entities.Skip(count);
            return new QueryableWithLateBinding<T>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        public QueryableWithLateBinding<T> Take(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Must be >= 0!");

            var entities = Entities.Take(count);
            return new QueryableWithLateBinding<T>(entities, DtoTypeGenerator, ExpressionTreeBuilder);
        }

        public IEnumerator<T> GetEnumerator() => Entities.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
