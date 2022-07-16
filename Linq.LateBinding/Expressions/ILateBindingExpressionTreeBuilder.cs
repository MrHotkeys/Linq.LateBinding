using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public interface ILateBindingExpressionTreeBuilder
    {
        public LateBindingExpressionTreeBuilder DefineMemberOverride(MemberInfo member, Func<Expression, Expression> buildOverrideFunc, bool onlyOnDirect);

        public LateBindingExpressionTreeBuilder DefineMemberOverride<TTarget, TMember, TOverride>(
            Expression<Func<TTarget, TMember>> memberGetterExpr, Expression<Func<TTarget, TOverride>> overrideExpr, bool onlyOnDirect);

        public Expression Build(Expression targetExpr, ILateBinding lateBinding);

        public Expression BuildAs(Expression targetExpr, ILateBinding lateBinding, Type type);

        public bool TryBuildAs(Expression targetExpr, ILateBinding lateBinding, Type type, [NotNullWhen(true)] out Expression? expression);
    }
}