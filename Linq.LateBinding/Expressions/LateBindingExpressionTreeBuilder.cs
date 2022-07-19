using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Extensions.Logging;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class LateBindingExpressionTreeBuilder : ILateBindingExpressionTreeBuilder
    {
        private ILogger Logger { get; }

        private Dictionary<MemberInfo, MemberOverrideDefinition> MemberOverrides { get; set; } = new Dictionary<MemberInfo, MemberOverrideDefinition>();

        private ILateBindingCalculateBuilderCollection CalculateMethods { get; }

        public LateBindingExpressionTreeBuilder(ILogger<LateBindingExpressionTreeBuilder> logger, ILateBindingCalculateBuilderCollection calculateMethods)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            CalculateMethods = calculateMethods ?? throw new ArgumentNullException(nameof(calculateMethods));
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

        public Expression Build(Expression targetExpr, ILateBinding lateBinding)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (lateBinding is null)
                throw new ArgumentNullException(nameof(lateBinding));

            if (!TryBuildWithOptionalType(targetExpr, lateBinding, null, out var expr))
                throw new InvalidOperationException();

            return expr;
        }

        public Expression BuildAs(Expression targetExpr, ILateBinding lateBind, Type type)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (lateBind is null)
                throw new ArgumentNullException(nameof(lateBind));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (!TryBuildWithOptionalType(targetExpr, lateBind, type, out var expr))
                throw new InvalidOperationException();

            return expr;
        }

        public bool TryBuildAs(Expression targetExpr, ILateBinding lateBind,
            Type type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (lateBind is null)
                throw new ArgumentNullException(nameof(lateBind));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return TryBuildWithOptionalType(targetExpr, lateBind, type, out resultExpr);
        }

        private bool TryBuildWithOptionalType(Expression targetExpr, ILateBinding lateBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            switch (lateBind.Target)
            {
                case LateBindingTarget.Constant when lateBind is ILateBindingToConstant constantLateBind:
                    return TryBuildConstantExpression(targetExpr, constantLateBind, type, out resultExpr);
                case LateBindingTarget.Field when lateBind is ILateBindingToField fieldLateBind:
                    return TryBuildFieldExpression(targetExpr, fieldLateBind, type, out resultExpr);
                case LateBindingTarget.Calculate when lateBind is ILateBindingToCalculate calculateLateBind:
                    return TryBuildCalculateExpression(targetExpr, calculateLateBind, type, out resultExpr);
                default:
                    throw new InvalidOperationException();
            }
        }

        private bool TryBuildConvertIfNeeded(Expression expr, Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            if (type is null || expr.Type == type)
            {
                resultExpr = expr;
                return true;
            }
            else if (expr.Type.CanCastTo(type, implicitOnly: true))
            {
                resultExpr = Expression.Convert(expr, type);
                return true;
            }
            else
            {
                resultExpr = default;
                return false;
            }
        }

        private bool TryBuildConstantExpression(Expression targetExpr, ILateBindingToConstant constantLateBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            if (type is null)
            {
                var value = constantLateBind.GetValue();
                resultExpr = Expression.Constant(value);
                return true;
            }
            else
            {
                if (constantLateBind.TryGetValueAs(type, out var value))
                {
                    resultExpr = Expression.Constant(value, type); // Need to explicitly specify type in case it's null
                    return true;
                }
                else
                {
                    resultExpr = default;
                    return false;
                }
            }
        }

        private bool TryBuildFieldExpression(Expression targetExpr, ILateBindingToField fieldLateBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            var split = fieldLateBind
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
                    .Single(); // TODO: Catch if member not found and throw

                if (MemberOverrides.TryGetValue(member, out var memberOverride) && (memberOverride.OnlyOnDirect == false || i == split.Length - 1))
                {
                    currentExpr = memberOverride.BuildOverride(currentExpr);
                }
                else
                {
                    currentExpr = Expression.MakeMemberAccess(currentExpr, member);
                }
            }

            return TryBuildConvertIfNeeded(currentExpr, type, out resultExpr);
        }

        private bool TryBuildCalculateExpression(Expression targetExpr, ILateBindingToCalculate calculateLateBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            var context = new BuildContext(this, targetExpr, calculateLateBind);
            var builders = CalculateMethods.GetBuilders(calculateLateBind.Method);
            var parameterExprs = new Expression[calculateLateBind.Arguments.Count];
            foreach (var builder in builders)
            {
                var calculateExpr = builder.Build(context);
                if (calculateExpr is null)
                    continue;

                if (!TryBuildConvertIfNeeded(calculateExpr, type, out resultExpr))
                    continue;

                // If we made it this far, we were successful
                return true;
            }

            // If we made it this far, we failed to find builder compatible with the incoming parameters out required outgoing type
            resultExpr = default;
            return false;
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

        private sealed class BuildContext : ILateBindingCalculateBuilderContext
        {
            public ILateBindingExpressionTreeBuilder Builder { get; }

            public Expression TargetExpr { get; }

            public ILateBindingToCalculate CalculateLateBind { get; }

            public BuildContext(ILateBindingExpressionTreeBuilder builder, Expression targetExpr, ILateBindingToCalculate calculateLateBind)
            {
                Builder = builder ?? throw new ArgumentNullException(nameof(builder));
                TargetExpr = targetExpr ?? throw new ArgumentNullException(nameof(targetExpr));
                CalculateLateBind = calculateLateBind ?? throw new ArgumentNullException(nameof(calculateLateBind));
            }
        }
    }
}