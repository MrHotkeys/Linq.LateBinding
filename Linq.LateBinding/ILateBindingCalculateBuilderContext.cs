using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingCalculateBuilderContext
    {
        public ILateBindingExpressionTreeBuilder Builder { get; }

        public Expression TargetExpr { get; }

        public ILateBindingToCalculate Calculate { get; }

        public Expression BuildArgument(int argumentIndex) =>
            Builder.Build(TargetExpr, Calculate.Arguments[argumentIndex]);

        public Expression BuildArgumentAs(int argumentIndex, Type type) =>
            Builder.BuildAs(TargetExpr, Calculate.Arguments[argumentIndex], type);

        public bool TryBuildArgumentAs(int argumentIndex, Type type, [NotNullWhen(true)] out Expression? expression) =>
            Builder.TryBuildAs(TargetExpr, Calculate.Arguments[argumentIndex], type, out expression);
    }
}