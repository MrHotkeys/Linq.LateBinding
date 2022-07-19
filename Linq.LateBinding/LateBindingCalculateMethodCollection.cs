using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.Extensions.Logging;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingCalculateBuilderCollection : ILateBindingCalculateBuilderCollection
    {
        private ILogger Logger { get; }

        private Dictionary<string, List<ILateBindingCalculateMethodBuilder>> Builders =
            new Dictionary<string, List<ILateBindingCalculateMethodBuilder>>(StringComparer.OrdinalIgnoreCase);

        public LateBindingCalculateBuilderCollection(ILogger<LateBindingCalculateBuilderCollection> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILateBindingCalculateBuilderCollection Add(ILateBindingCalculateMethodBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (!Builders.TryGetValue(builder.Method, out var list))
            {
                list = new List<ILateBindingCalculateMethodBuilder>();
                Builders[builder.Method] = list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].ParameterTypes.SequenceEqual(builder.ParameterTypes))
                {
                    list.RemoveAt(i);
                    // TODO: Log removed builder

                    break; // Since matches are removed on add, there shouldn't be more than one
                }
            }

            list.Add(builder);

            return this;
        }

        public ILateBindingCalculateBuilderCollection Remove(ILateBindingCalculateMethodBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (Builders.TryGetValue(builder.Method, out var list))
            {
                list.Remove(builder);
            }

            return this;
        }

        public ILateBindingCalculateBuilderCollection Define(string method, Type[] parameterTypes, Func<IReadOnlyList<Expression>, Expression?> callback)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            Expression? CallbackFromLateBind(ILateBindingCalculateBuilderContext context)
            {
                if (context.CalculateLateBind.Arguments.Count != parameterTypes.Length)
                    return null; // TODO: Log a trace/debug

                var argExprs = new Expression[context.CalculateLateBind.Arguments.Count];
                for (var i = 0; i < context.CalculateLateBind.Arguments.Count; i++)
                {
                    if (!context.TryBuildArgumentAs(i, parameterTypes[i], out var argExpr))
                        return null; // TODO: Log a trace/debug

                    argExprs[i] = argExpr;
                }

                return callback(argExprs);
            }

            var builder = new LateBindingCalculateBuilderFromCallback(method, parameterTypes, CallbackFromLateBind);
            return Add(builder);
        }

        public ILateBindingCalculateBuilderCollection Define(string method, Type[] parameterTypes, Func<ILateBindingCalculateBuilderContext, Expression?> callback)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            Expression? CallbackWithGuard(ILateBindingCalculateBuilderContext context)
            {
                if (context.CalculateLateBind.Arguments.Count != parameterTypes.Length)
                    return null; // TODO: Log a trace/debug

                return callback(context);
            }

            var builder = new LateBindingCalculateBuilderFromCallback(method, parameterTypes, CallbackWithGuard);
            return Add(builder);
        }

        public ILateBindingCalculateBuilderCollection Define<TOut>(string method, Expression<Func<TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingCalculateBuilderCollection Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingCalculateBuilderCollection Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingCalculateBuilderCollection Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingCalculateBuilderCollection Define(string method, LambdaExpression builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            var parameterTypes = builderExpr
                .Parameters
                .Select(p => p.Type)
                .ToArray();

            Expression? Callback(ILateBindingCalculateBuilderContext context)
            {
                if (context.CalculateLateBind.Arguments.Count != builderExpr.Parameters.Count)
                    return null; // TODO: Log a trace/debug

                var visitor = new ParameterExpressionReplaceVisitor();

                for (var i = 0; i < parameterTypes.Length; i++)
                {
                    var parameterExpr = builderExpr.Parameters[i];

                    if (!context.TryBuildArgumentAs(i, parameterTypes[i], out var argExpr))
                        return null; // TODO: Log a trace/debug

                    visitor.Add(parameterExpr, argExpr);
                }

                return visitor.Visit(builderExpr.Body);
            }

            var builder = new LateBindingCalculateBuilderFromCallback(method, parameterTypes, Callback);
            return Add(builder);
        }

        public ILateBindingCalculateBuilderCollection Undefine(string method)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            Builders.Remove(method);

            return this;
        }

        public ILateBindingCalculateBuilderCollection Undefine(string method, Type[] parameterTypes)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("May not contain null!", nameof(parameterTypes));

            if (Builders.TryGetValue(method, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].ParameterTypes.SequenceEqual(parameterTypes))
                    {
                        list.RemoveAt(i);
                        i--;
                    }
                }
            }

            return this;
        }

        public IReadOnlyCollection<ILateBindingCalculateMethodBuilder> GetBuilders(string method)
        {
            return Builders.TryGetValue(method, out var list) ?
                list :
                Array.Empty<ILateBindingCalculateMethodBuilder>();
        }
    }
}