using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

using MrHotkeys.Linq.LateBinding.Binds;
using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding.Calls
{
    public interface ILateBindingCallBuilderContext
    {
        public ILateBindingExpressionTreeBuilder Builder { get; }

        public Expression TargetExpr { get; }

        public ILateBindingToCall Call { get; }

        public Expression BuildArgument(int argumentIndex) =>
            Builder.Build(TargetExpr, Call.Arguments[argumentIndex]);

        public Expression BuildArgumentAs(int argumentIndex, Type type) =>
            Builder.BuildAs(TargetExpr, Call.Arguments[argumentIndex], type);

        public bool TryBuildArgumentAs(int argumentIndex, Type type, [NotNullWhen(true)] out Expression? expression) =>
            Builder.TryBuildAs(TargetExpr, Call.Arguments[argumentIndex], type, out expression);
    }
}