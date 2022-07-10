using System;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public interface ILateBindingExpression
    {
        public LateBindingExpressionType ExpressionType { get; }
    }
}