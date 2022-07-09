using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class LateBindingExpressionTreeBuilder
    {
        public LateBindingExpressionTreeBuilder()
        { }

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
            switch (calculateLateBinding.Method.ToLower())
            {
                case "clamp":
                    {
                        if (calculateLateBinding.Expressions.Length != 3)
                            throw new InvalidOperationException();

                        var valExpr = Build(targetExpr, calculateLateBinding.Expressions[0]);
                        var minExpr = Expression.Convert(Build(targetExpr, calculateLateBinding.Expressions[1]), valExpr.Type);
                        var maxExpr = Expression.Convert(Build(targetExpr, calculateLateBinding.Expressions[2]), valExpr.Type);

                        var clampMethod = typeof(Math)
                            .GetMethods()
                            .Where(m =>
                            {
                                if (m.Name != nameof(Math.Clamp))
                                    return false;

                                var parameters = m.GetParameters();
                                return parameters.Length == 3 && parameters.All(p => p.ParameterType == valExpr.Type);
                            })
                            .Single();

                        return Expression.Call(clampMethod, valExpr, minExpr, maxExpr);
                    }
                default:
                    throw new NotImplementedException();
            }
        }
    }
}