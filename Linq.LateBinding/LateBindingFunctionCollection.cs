using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

using Microsoft.Extensions.Logging;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingFunctionCollection : ILateBindingFunctionCollection
    {
        private ILogger Logger { get; }

        private Dictionary<string, List<ILateBindingCallBuilder>> Builders =
            new Dictionary<string, List<ILateBindingCallBuilder>>(StringComparer.OrdinalIgnoreCase);

        public LateBindingFunctionCollection(ILogger<LateBindingFunctionCollection> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public ILateBindingFunctionCollection Add(ILateBindingCallBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (!Builders.TryGetValue(builder.Method, out var list))
            {
                list = new List<ILateBindingCallBuilder>();
                Builders[builder.Method] = list;
            }

            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].ParameterTypes.SequenceEqual(builder.ParameterTypes))
                {
                    if (Logger.IsEnabled(LogLevel.Debug))
                        Logger.LogDebug("Overwriting builder for function {method}({parameterTypes}).", builder.Method, GetParameterListStrign(builder.ParameterTypes));

                    list.RemoveAt(i);

                    break; // Since matches are removed on add, there shouldn't be more than one
                }
            }

            list.Add(builder);

            return this;
        }

        public ILateBindingFunctionCollection Remove(ILateBindingCallBuilder builder)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (Builders.TryGetValue(builder.Method, out var list))
                list.Remove(builder);

            return this;
        }

        public ILateBindingFunctionCollection Define(string method, Type[] parameterTypes, Func<IReadOnlyList<Expression>, Expression?> callback)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            Expression? CallbackFromLateBind(ILateBindingCallBuilderContext context)
            {
                if (context.Call.Arguments.Count != parameterTypes.Length)
                {
                    if (Logger.IsEnabled(LogLevel.Trace))
                    {
                        Logger.LogTrace("Soft-failing out of builder for function {method}({parameterTypes}): argument count mismatch (expected {expected}, got {actual}).",
                            method, GetParameterListStrign(parameterTypes), parameterTypes.Length, context.Call.Arguments.Count);
                    }

                    return null;
                }

                var argExprs = new Expression[context.Call.Arguments.Count];
                for (var i = 0; i < context.Call.Arguments.Count; i++)
                {
                    if (!context.TryBuildArgumentAs(i, parameterTypes[i], out var argExpr))
                    {
                        if (Logger.IsEnabled(LogLevel.Trace))
                        {
                            Logger.LogTrace("Soft-failing out of builder for function {method}({parameterTypes}): argument type mismatch (expected {expected}).",
                                method, GetParameterListStrign(parameterTypes), parameterTypes[i]);
                        }

                        return null;
                    }

                    argExprs[i] = argExpr;
                }

                return callback(argExprs);
            }

            var builder = new LateBindingCallBuilderFromCallback(method, parameterTypes, CallbackFromLateBind);
            return Add(builder);
        }

        public ILateBindingFunctionCollection Define(string method, Type[] parameterTypes, Func<ILateBindingCallBuilderContext, Expression?> callback)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (callback is null)
                throw new ArgumentNullException(nameof(callback));
            if (parameterTypes is null)
                throw new ArgumentNullException(nameof(parameterTypes));
            if (parameterTypes.Contains(null))
                throw new ArgumentException("Cannot contain null!", nameof(parameterTypes));

            Expression? CallbackWithGuard(ILateBindingCallBuilderContext context)
            {
                if (context.Call.Arguments.Count != parameterTypes.Length)
                    return null; // TODO: Log a trace/debug

                return callback(context);
            }

            var builder = new LateBindingCallBuilderFromCallback(method, parameterTypes, CallbackWithGuard);
            return Add(builder);
        }

        public ILateBindingFunctionCollection Define<TOut>(string method, Expression<Func<TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingFunctionCollection Define<T, TOut>(string method, Expression<Func<T, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingFunctionCollection Define<T0, T1, TOut>(string method, Expression<Func<T0, T1, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingFunctionCollection Define<T0, T1, T2, TOut>(string method, Expression<Func<T0, T1, T2, TOut>> builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            return Define(method, builderExpr as LambdaExpression);
        }

        public ILateBindingFunctionCollection Define(string method, LambdaExpression builderExpr)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));
            if (builderExpr is null)
                throw new ArgumentNullException(nameof(builderExpr));

            var parameterTypes = builderExpr
                .Parameters
                .Select(p => p.Type)
                .ToArray();

            Expression? Callback(IReadOnlyList<Expression> argExprs)
            {
                var visitor = new ParameterExpressionReplaceVisitor();

                for (var i = 0; i < parameterTypes.Length; i++)
                    visitor.Add(builderExpr.Parameters[i], argExprs[i]);

                return visitor.Visit(builderExpr.Body);
            }

            return Define(method, parameterTypes, Callback);
        }

        public ILateBindingFunctionCollection Undefine(string method)
        {
            if (method is null)
                throw new ArgumentNullException(nameof(method));

            if (Logger.IsEnabled(LogLevel.Trace))
                Logger.LogTrace("Undefining all methods named \"{method}\" ({count} total).", method, Builders.TryGetValue(method, out var list) ? list.Count : 0);

            Builders.Remove(method);

            return this;
        }

        public ILateBindingFunctionCollection Undefine(string method, Type[] parameterTypes)
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
                    var builder = list[i];
                    if (builder.ParameterTypes.SequenceEqual(parameterTypes))
                    {
                        if (Logger.IsEnabled(LogLevel.Trace))
                            Logger.LogTrace("Undefining method {method}({parameterTypes}).", method, GetParameterListStrign(parameterTypes), parameterTypes[i]);

                        list.RemoveAt(i);
                        i--;
                    }
                }
            }

            return this;
        }

        public IReadOnlyCollection<ILateBindingCallBuilder> GetBuilders(string method)
        {
            return Builders.TryGetValue(method, out var list) ?
                list :
                Array.Empty<ILateBindingCallBuilder>();
        }

        private string GetParameterListStrign(IReadOnlyList<Type> types) =>
            string.Join(", ", types.Select(t => t.Name));
    }
}