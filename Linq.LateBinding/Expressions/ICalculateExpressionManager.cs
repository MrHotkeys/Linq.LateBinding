using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public interface ICalculateExpressionManager
    {
        public CalculateExpressionManager Define(string method, Func<IReadOnlyList<Expression>, Expression> builderFunc, Type[] parameterTypes);

        public CalculateExpressionManager Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr);

        public CalculateExpressionManager Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr);

        public CalculateExpressionManager Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr);

        public CalculateExpressionManager Define(string method, LambdaExpression builderExpr);

        public Expression Build(string method, IList<Expression> expressions);
    }
}