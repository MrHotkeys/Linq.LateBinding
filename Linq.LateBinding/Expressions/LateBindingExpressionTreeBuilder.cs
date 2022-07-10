using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class LateBindingExpressionTreeBuilder
    {
        private Dictionary<MemberInfo, MemberOverrideDefinition> MemberOverrides { get; set; } = new Dictionary<MemberInfo, MemberOverrideDefinition>();

        private CalculateExpressionManager CalculateExpressionManager { get; }

        public LateBindingExpressionTreeBuilder(CalculateExpressionManager calculateExpressionManager)
        {
            CalculateExpressionManager = calculateExpressionManager ?? throw new ArgumentNullException(nameof(calculateExpressionManager));
        }

        public LateBindingExpressionTreeBuilder DefineMemberOverride(MemberInfo member, Func<Expression, Expression> buildOverrideFunc, bool onlyOnDirect)
        {
            if (member is null)
                throw new ArgumentNullException(nameof(member));
            if (buildOverrideFunc is null)
                throw new ArgumentNullException(nameof(buildOverrideFunc));
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                throw new ArgumentException("Must be a field or property!", nameof(member));

            MemberOverrides[member] = new MemberOverrideDefinition(member, buildOverrideFunc, onlyOnDirect);

            return this;
        }

        public LateBindingExpressionTreeBuilder DefineMemberOverride<TTarget, TMember, TOverride>(
            Expression<Func<TTarget, TMember>> memberGetterExpr, Expression<Func<TTarget, TOverride>> overrideExpr, bool onlyOnDirect)
        {
            if (memberGetterExpr is null)
                throw new ArgumentNullException(nameof(memberGetterExpr));
            if (overrideExpr is null)
                throw new ArgumentNullException(nameof(overrideExpr));

            if (memberGetterExpr.Body is not MemberExpression memberExpr)
                throw new ArgumentException("Must be a simple field or property getter (e.g. (User u) => u.Email)!", nameof(memberGetterExpr));
            if (memberExpr.Expression != memberGetterExpr.Parameters[0])
                throw new ArgumentException("The field or property must be coming from the parameter (e.g. (User u) => u.Email)!", nameof(memberGetterExpr));

            var member = memberExpr.Member;
            if (member.MemberType != MemberTypes.Field && member.MemberType != MemberTypes.Property)
                throw new ArgumentException("Must be getting a field or property!", nameof(memberGetterExpr));

            var buildOverrideFunc = (Expression expression) =>
            {
                var visitor = new ParameterExpressionReplaceVisitor();
                visitor.Add(overrideExpr.Parameters[0], expression);
                return visitor.Visit(overrideExpr.Body);
            };

            MemberOverrides[member] = new MemberOverrideDefinition(member, buildOverrideFunc, onlyOnDirect);

            return this;
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
            var split = fieldLateBinding
                .Field
                .Split(".");
            var currentExpr = targetExpr;
            for (var i = 0; i < split.Length; i++)
            {
                var name = split[i];
                var member = currentExpr
                    .Type
                    .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Where(m => (m.MemberType == MemberTypes.Field || m.MemberType == MemberTypes.Property) &&
                        StringComparer.OrdinalIgnoreCase.Equals(m.Name, name))
                    .Single();

                if (MemberOverrides.TryGetValue(member, out var memberOverride) && (memberOverride.OnlyOnDirect == false || i == split.Length - 1))
                {
                    currentExpr = memberOverride.BuildOverride(currentExpr);
                }
                else
                {
                    currentExpr = Expression.MakeMemberAccess(currentExpr, member);
                }
            }

            return currentExpr;
        }

        private Expression BuildCalculateExpression(Expression targetExpr, CalculateLateBindingExpression calculateLateBinding)
        {
            var expressions = calculateLateBinding
                .Expressions
                .Select(lbe => Build(targetExpr, lbe))
                .ToArray();

            return CalculateExpressionManager.Build(calculateLateBinding.Method, expressions);
        }

        private sealed class MemberOverrideDefinition
        {
            public MemberInfo Member { get; }

            private Func<Expression, Expression> BuildOverrideFunc { get; }

            public bool OnlyOnDirect { get; }

            public MemberOverrideDefinition(MemberInfo member, Func<Expression, Expression> buildOverrideFunc, bool onlyOnDirect)
            {
                Member = member ?? throw new ArgumentNullException(nameof(member));
                BuildOverrideFunc = buildOverrideFunc ?? throw new ArgumentNullException(nameof(buildOverrideFunc));
                OnlyOnDirect = onlyOnDirect;
            }

            public Expression BuildOverride(Expression expr) => BuildOverrideFunc(expr);
        }
    }
}