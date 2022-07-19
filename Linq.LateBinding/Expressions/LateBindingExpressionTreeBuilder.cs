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

        private ILateBindingFunctionCollection Functions { get; }

        public LateBindingExpressionTreeBuilder(ILogger<LateBindingExpressionTreeBuilder> logger, ILateBindingFunctionCollection functions)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Functions = functions ?? throw new ArgumentNullException(nameof(functions));
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

        public Expression Build(Expression targetExpr, ILateBinding bind)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (bind is null)
                throw new ArgumentNullException(nameof(bind));

            if (!TryBuildWithOptionalType(targetExpr, bind, null, out var expr))
                throw new InvalidOperationException();

            return expr;
        }

        public Expression BuildAs(Expression targetExpr, ILateBinding bind, Type type)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (bind is null)
                throw new ArgumentNullException(nameof(bind));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (!TryBuildWithOptionalType(targetExpr, bind, type, out var expr))
                throw new InvalidOperationException();

            return expr;
        }

        public bool TryBuildAs(Expression targetExpr, ILateBinding bind,
            Type type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            if (targetExpr is null)
                throw new ArgumentNullException(nameof(targetExpr));
            if (bind is null)
                throw new ArgumentNullException(nameof(bind));
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            return TryBuildWithOptionalType(targetExpr, bind, type, out resultExpr);
        }

        private bool TryBuildWithOptionalType(Expression targetExpr, ILateBinding bind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            switch (bind.Form)
            {
                case LateBindingForm.Const when bind is ILateBindingToConstant constantBind:
                    return TryBuildConstantExpression(targetExpr, constantBind, type, out resultExpr);
                case LateBindingForm.Entity when bind is ILateBindingToEntity entityBind:
                    return TryBuildEntityExpression(targetExpr, entityBind, type, out resultExpr);
                case LateBindingForm.Call when bind is ILateBindingToCall callBind:
                    return TryBuildCallExpression(targetExpr, callBind, type, out resultExpr);
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

        private bool TryBuildConstantExpression(Expression targetExpr, ILateBindingToConstant constantBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            if (type is null)
            {
                var value = constantBind.GetValue();
                resultExpr = Expression.Constant(value);
                return true;
            }
            else
            {
                if (constantBind.TryGetValueAs(type, out var value))
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

        private bool TryBuildEntityExpression(Expression targetExpr, ILateBindingToEntity entityBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            var split = entityBind
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

        private bool TryBuildCallExpression(Expression targetExpr, ILateBindingToCall callBind,
            Type? type, [NotNullWhen(true)] out Expression? resultExpr)
        {
            var context = new BuildContext(this, targetExpr, callBind);
            var builders = Functions.GetBuilders(callBind.Method);
            var parameterExprs = new Expression[callBind.Arguments.Count];
            foreach (var builder in builders)
            {
                var callExpr = builder.Build(context);
                if (callExpr is null)
                    continue;

                if (!TryBuildConvertIfNeeded(callExpr, type, out resultExpr))
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

        private sealed class BuildContext : ILateBindingCallBuilderContext
        {
            public ILateBindingExpressionTreeBuilder Builder { get; }

            public Expression TargetExpr { get; }

            public ILateBindingToCall Call { get; }

            public BuildContext(ILateBindingExpressionTreeBuilder builder, Expression targetExpr, ILateBindingToCall call)
            {
                Builder = builder ?? throw new ArgumentNullException(nameof(builder));
                TargetExpr = targetExpr ?? throw new ArgumentNullException(nameof(targetExpr));
                Call = call ?? throw new ArgumentNullException(nameof(call));
            }
        }
    }
}