using System.Collections.Generic;
using System.Linq.Expressions;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    internal sealed class ParameterExpressionReplaceVisitor : ExpressionVisitor
    {
        private Dictionary<ParameterExpression, Expression> Replacements { get; } = new Dictionary<ParameterExpression, Expression>();
        public ParameterExpressionReplaceVisitor()
        { }

        public void Add(ParameterExpression parameterExpr, Expression replacementExpr)
        {
            Replacements.Add(parameterExpr, replacementExpr);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return Replacements.TryGetValue(node, out var replacementExpr) ?
                replacementExpr :
                base.VisitParameter(node);
        }
    }
}