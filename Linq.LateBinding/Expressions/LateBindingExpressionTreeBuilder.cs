using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class LateBindingExpressionTreeBuilder
    {
        private CalculateExpressionManager CalculateExpressionManager { get; }

        public LateBindingExpressionTreeBuilder(CalculateExpressionManager calculateExpressionManager)
        {
            CalculateExpressionManager = calculateExpressionManager ?? throw new ArgumentNullException(nameof(calculateExpressionManager));
        }

        public Expression Build(Expression targetExpr, ILateBindingExpression lateBinding)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (lateBinding is null)
                throw new ArgumentNullException(nameof(lateBinding));

            switch (lateBinding)
            {
                case ConstantLateBindingExpression constantLateBinding:
                    return BuildConstantExpression(targetExpr, constantLateBinding);
                case FieldLateBindingExpression fieldLateBinding:
                    return BuildFieldExpression(targetExpr, fieldLateBinding);
                case CalculateLateBindingExpression calculateLateBinding:
                    return BuildCalculateExpression(targetExpr, calculateLateBinding);
                default:
                    throw new InvalidOperationException();
            }
        }

        private Expression BuildConstantExpression(Expression targetExpr, ConstantLateBindingExpression constantLateBinding)
        {
            return Expression.Constant(constantLateBinding.Value);
        }

        private Expression BuildFieldExpression(Expression targetExpr, FieldLateBindingExpression fieldLateBinding)
        {
            var property = targetExpr
                .Type
                .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, fieldLateBinding.Field))
                .Single();

            return Expression.MakeMemberAccess(targetExpr, property);
        }

        private Expression BuildCalculateExpression(Expression targetExpr, CalculateLateBindingExpression calculateLateBinding)
        {
            var expressions = calculateLateBinding
                .Expressions
                .Select(lbe => Build(targetExpr, lbe))
                .ToArray();

            return CalculateExpressionManager.Build(calculateLateBinding.Method, expressions);
        }
    }
}