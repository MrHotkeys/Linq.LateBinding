using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public interface ILateBindingExpressionTreeBuilder
    {
        public LateBindingExpressionTreeBuilder DefineMemberOverride(MemberInfo member, Func<Expression, Expression> buildOverrideFunc, bool onlyOnDirect);

        public LateBindingExpressionTreeBuilder DefineMemberOverride<TTarget, TMember, TOverride>(
            Expression<Func<TTarget, TMember>> memberGetterExpr, Expression<Func<TTarget, TOverride>> overrideExpr, bool onlyOnDirect);

        public Expression Build(Expression targetExpr, ILateBindingExpression lateBinding);
    }
}