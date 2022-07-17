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

        public ILateBindingToCalculate CalculateLateBind { get; }

        public Expression BuildArgument(int argumentIndex) =>
            Builder.Build(TargetExpr, CalculateLateBind.Arguments[argumentIndex]);

        public Expression BuildArgumentAs(int argumentIndex, Type type) =>
            Builder.BuildAs(TargetExpr, CalculateLateBind.Arguments[argumentIndex], type);

        public bool TryBuildArgumentAs(int argumentIndex, Type type, [NotNullWhen(true)] out Expression? expression) =>
            Builder.TryBuildAs(TargetExpr, CalculateLateBind.Arguments[argumentIndex], type, out expression);
    }
}