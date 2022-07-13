using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class LateBindingCalculateMethodManager : ILateBindingCalculateMethodManager
    {
        private Dictionary<string, List<CalculateExpressionBuilder>> Builders =
            new Dictionary<string, List<CalculateExpressionBuilder>>(StringComparer.OrdinalIgnoreCase);

        public LateBindingCalculateMethodManager(bool init)
        {
            if (init)
                LateBindingCalculateMethodManagerInit.Init(this);
        }

        private void AddBuilder(CalculateExpressionBuilder builder)
        {
            if (!Builders.TryGetValue(builder.Method, out var list))
            {
                list = new List<CalculateExpressionBuilder>();
                Builders[builder.Method] = list;
            }

            list.Add(builder);
        }

        public LateBindingCalculateMethodManager Define(string method, Func<IReadOnlyList<Expression>, Expression?> buildFunc, Type[] parameterTypes, bool convertArgs)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (buildFunc is null)
                throw new ArgumentNullException(nameof(buildFunc));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            var builder = new CalculateExpressionBuilder(method, buildFunc, parameterTypes, convertArgs);
            AddBuilder(builder);

            return this;
        }

        public LateBindingCalculateMethodManager Define<TOut>(string method, Expression<Func<TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public LateBindingCalculateMethodManager Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public LateBindingCalculateMethodManager Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public LateBindingCalculateMethodManager Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public LateBindingCalculateMethodManager Define(string method, LambdaExpression builderExpr)
        {
            var parameterTypes = builderExpr
                .Parameters
                .Select(p => p.Type)
                .ToArray();

            Expression BuildFuncFromLambda(IReadOnlyList<Expression> argExprs)
            {
                var visitor = new ParameterExpressionReplaceVisitor();

                for (var i = 0; i < parameterTypes.Length; i++)
                {
                    visitor.Add(builderExpr.Parameters[i], argExprs[i]);
                }

                return visitor.Visit(builderExpr.Body);
            }

            var builder = new CalculateExpressionBuilder(method, BuildFuncFromLambda, parameterTypes, true);
            AddBuilder(builder);

            return this;
        }

        public Expression Build(string method, IList<Expression> expressions)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (expressions is null)
                throw new ArgumentNullException(nameof(expressions));
            if (expressions.Contains(null!))
                throw new ArgumentException("Cannot contain null!", nameof(expressions));

            var candidateBuilders = FindCandidateBuilders(method, expressions);
            var expressionReTyped = new Expression[expressions.Count];
            foreach (var builder in candidateBuilders)
            {
                for (var i = 0; i < builder.ParameterTypes.Count; i++)
                {
                    var expression = expressions[i];
                    var parameterType = builder.ParameterTypes[i];

                    expressionReTyped[i] = !builder.RequireParameterRetype || expression.Type == parameterType ?
                        expression :
                        Expression.Convert(expression, parameterType);
                }

                var resultExpr = builder.BuildFunc(expressionReTyped);
                if (resultExpr != null)
                    return resultExpr;

                // TODO: Log that the builder soft failed
            }

            throw new InvalidOperationException($"No suitable candidate builders found!");
        }

        private List<CalculateExpressionBuilder> FindCandidateBuilders(string method, IList<Expression> expressions)
        {
            var candidates = new List<CalculateExpressionBuilder>();

            if (!Builders.TryGetValue(method, out var list))
                return candidates;

            foreach (var builder in list)
            {
                if (expressions.Count != builder.ParameterTypes.Count)
                    continue;

                var incompatibilityFound = false;
                var perfectMatch = true;
                for (var i = 0; i < builder.ParameterTypes.Count; i++)
                {
                    var expressionType = expressions[i].Type;
                    var parameterType = builder.ParameterTypes[i];

                    if (expressionType != parameterType)
                        perfectMatch = false;

                    if (!expressionType.CanCastTo(parameterType, implicitOnly: true) &&
                        !(expressions[i] is ConstantExpression constantExpr && constantExpr.Value is null && parameterType.CanBeSetToNull()))
                    {
                        incompatibilityFound = true;
                        break;
                    }
                }

                if (incompatibilityFound)
                    continue;

                // Put perfect matches first so they're prioritized
                if (perfectMatch)
                    candidates.Insert(0, builder);
                else
                    candidates.Add(builder);
            }

            return candidates;
        }

        private sealed class CalculateExpressionBuilder
        {
            public string Method { get; }
            public Func<IReadOnlyList<Expression>, Expression?> BuildFunc { get; }

            public IReadOnlyList<Type> ParameterTypes { get; }

            public bool RequireParameterRetype { get; }

            public CalculateExpressionBuilder(string method, Func<IReadOnlyList<Expression>, Expression?> buildFunc, IList<Type> parameterTypes, bool requireParameterRetype)
            {
                Method = method ?? throw new ArgumentNullException(nameof(method));
                BuildFunc = buildFunc ?? throw new ArgumentNullException(nameof(buildFunc));
                ParameterTypes = parameterTypes is not null ?
                    new ReadOnlyCollection<Type>(parameterTypes) :
                    throw new ArgumentNullException(nameof(parameterTypes));
                RequireParameterRetype = requireParameterRetype;
            }

            public override string ToString() =>
                $"{Method}({string.Join(", ", ParameterTypes.Select(t => t.Name))})";
        }
    }
}