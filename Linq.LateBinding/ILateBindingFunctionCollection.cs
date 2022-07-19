using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingFunctionCollection
    {
        public ILateBindingFunctionCollection Add(ILateBindingCallBuilder builder);

        public ILateBindingFunctionCollection Remove(ILateBindingCallBuilder builder);

        public ILateBindingFunctionCollection Define(string method, Type[] parameterTypes, Func<IReadOnlyList<Expression>, Expression?> buildFunc);

        public ILateBindingFunctionCollection Define(string method, Type[] parameterTypes, Func<ILateBindingCallBuilderContext, Expression?> buildFunc);

        public ILateBindingFunctionCollection Define<TOut>(string method, Expression<Func<TOut>> builderExpr);

        public ILateBindingFunctionCollection Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr);

        public ILateBindingFunctionCollection Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr);

        public ILateBindingFunctionCollection Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr);

        public ILateBindingFunctionCollection Define(string method, LambdaExpression builderExpr);

        public ILateBindingFunctionCollection Undefine(string method);

        public ILateBindingFunctionCollection Undefine(string method, Type[] parameterTypes);

        public IReadOnlyCollection<ILateBindingCallBuilder> GetBuilders(string method);
    }
}