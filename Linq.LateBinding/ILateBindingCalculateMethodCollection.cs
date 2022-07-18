using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingCalculateBuilderCollection
    {
        public ILateBindingCalculateBuilderCollection Add(ILateBindingCalculateMethodBuilder builder);

        public ILateBindingCalculateBuilderCollection Remove(ILateBindingCalculateMethodBuilder builder);

        public ILateBindingCalculateBuilderCollection Define(string method, Type[] parameterTypes, Func<IReadOnlyList<Expression>, Expression?> buildFunc);

        public ILateBindingCalculateBuilderCollection Define(string method, Type[] parameterTypes, Func<ILateBindingCalculateBuilderContext, Expression?> buildFunc);

        public ILateBindingCalculateBuilderCollection Define<TOut>(string method, Expression<Func<TOut>> builderExpr);

        public ILateBindingCalculateBuilderCollection Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr);

        public ILateBindingCalculateBuilderCollection Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr);

        public ILateBindingCalculateBuilderCollection Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr);

        public ILateBindingCalculateBuilderCollection Define(string method, LambdaExpression builderExpr);

        public ILateBindingCalculateBuilderCollection Undefine(string method);

        public ILateBindingCalculateBuilderCollection Undefine(string method, Type[] parameterTypes);

        public IReadOnlyCollection<ILateBindingCalculateMethodBuilder> GetBuilders(string method);
    }
}