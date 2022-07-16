using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public interface ILateBindingCalculateMethodManager
    {
        public ILateBindingCalculateMethodManager Define(string method, Func<IReadOnlyList<Expression>, Expression?> buildFunc, Type[] parameterTypes, bool convertArgs);

        public ILateBindingCalculateMethodManager Define<TOut>(string method, Expression<Func<TOut>> builderExpr);

        public ILateBindingCalculateMethodManager Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr);

        public ILateBindingCalculateMethodManager Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr);

        public ILateBindingCalculateMethodManager Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr);

        public ILateBindingCalculateMethodManager Define(string method, LambdaExpression builderExpr);

        public Expression Build(string method, IList<Expression> expressions);
    }
}