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

        public LateBindingCalculateMethodManager()
        { }

        private void AddBuilder(CalculateExpressionBuilder builder)
        {
            if (!Builders.TryGetValue(builder.Method, out var list))
            {
                list = new List<CalculateExpressionBuilder>();
                Builders[builder.Method] = list;
            }

            list.Add(builder);
        }

        public LateBindingCalculateMethodManager Define(string method, Func<IReadOnlyList<Expression>, Expression> builderFunc, Type[] parameterTypes)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderFunc is null)
                throw new ArgumentNullException(nameof(builderFunc));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            var builder = new CalculateExpressionBuilder(method, builderFunc, parameterTypes);
            AddBuilder(builder);

            return this;
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

            var builderFunc = (IReadOnlyList<Expression> expressions) =>
            {
                var visitor = new ParameterExpressionReplaceVisitor();

                for (var i = 0; i < parameterTypes.Length; i++)
                {
                    visitor.Add(builderExpr.Parameters[i], expressions[i]);
                }

                return visitor.Visit(builderExpr.Body);
            };

            var builder = new CalculateExpressionBuilder(method, builderFunc, parameterTypes);
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

            if (!Builders.TryGetValue(method, out var list))
                throw new KeyNotFoundException($"No builders defined for method \"{method}\"!");

            var expressionReTyped = new Expression[expressions.Count];
            foreach (var builder in list)
            {
                if (expressions.Count != builder.ParameterTypes.Count)
                    continue;

                var incompatibilityFound = false;
                for (var i = 0; i < builder.ParameterTypes.Count; i++)
                {
                    var expressionType = expressions[i].Type;
                    var parameterType = builder.ParameterTypes[i];

                    if (!expressionType.CanCastTo(parameterType, implicitOnly: true))
                    {
                        incompatibilityFound = true;
                        break;
                    }

                    expressionReTyped[i] = expressions[i].Type == parameterType ?
                        expressions[i] :
                        Expression.Convert(expressions[i], parameterType);
                }

                if (incompatibilityFound)
                    continue;

                return builder.Build(expressionReTyped);
            }

            throw new InvalidOperationException($"No suitable candidate builders found!");
        }

        private sealed class CalculateExpressionBuilder
        {
            public string Method { get; }
            private Func<IReadOnlyList<Expression>, Expression> BuilderFunc { get; }

            public IReadOnlyList<Type> ParameterTypes { get; }

            public CalculateExpressionBuilder(string method, Func<IReadOnlyList<Expression>, Expression> builderFunc, IList<Type> parameterTypes)
            {
                Method = method ?? throw new ArgumentNullException(nameof(method));
                BuilderFunc = builderFunc ?? throw new ArgumentNullException(nameof(builderFunc));
                ParameterTypes = parameterTypes is not null ?
                    new ReadOnlyCollection<Type>(parameterTypes) :
                    throw new ArgumentNullException(nameof(parameterTypes));
            }

            public Expression Build(IReadOnlyList<Expression> expressions) =>
                BuilderFunc(expressions);

            public override string ToString() =>
                $"{Method}({string.Join(", ", ParameterTypes.Select(t => t.Name))})";
        }
    }
}