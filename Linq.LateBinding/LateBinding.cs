using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MrHotkeys.Linq.LateBinding.Calls;
using MrHotkeys.Linq.LateBinding.Dto;
using MrHotkeys.Linq.LateBinding.Expressions;
using MrHotkeys.Linq.LateBinding.Queries;
using MrHotkeys.Linq.LateBinding.Utility;

namespace MrHotkeys.Linq.LateBinding
{
    public static class LateBinding
    {
        public static QueryableWithLateBinding<T> WithLateBinding<T>(this IQueryable<T> entities) =>
            new QueryableWithLateBinding<T>(entities, LateBinding.DtoTypeGenerator, LateBinding.ExpressionTreeBuilder);
        public static QueryableWithLateBinding<object?> WithLateBinding<T>(this IQueryable<T> entities, ILateBindingQuery query) =>
            new QueryableWithLateBinding<T>(entities, LateBinding.DtoTypeGenerator, LateBinding.ExpressionTreeBuilder)
                .Query(query);

        public static QueryableWithLateBinding<T> AsQueryableWithLateBinding<T>(this IEnumerable<T> entities) =>
            new QueryableWithLateBinding<T>(entities.AsQueryable(), LateBinding.DtoTypeGenerator, LateBinding.ExpressionTreeBuilder);
        public static QueryableWithLateBinding<object?> AsQueryableWithLateBinding<T>(this IEnumerable<T> entities, ILateBindingQuery query) =>
            new QueryableWithLateBinding<T>(entities.AsQueryable(), LateBinding.DtoTypeGenerator, LateBinding.ExpressionTreeBuilder)
                .Query(query);

        private static IServiceProvider? _serviceProvider;
        public static IServiceProvider ServiceProvider
        {
            get
            {
                if (_serviceProvider is null)
                {
                    var serviceCollection = new ServiceCollection()
                        .AddLogging(c => c.AddProvider(NullLoggerProvider.Instance))
                        .AddSingleton<IDtoTypeGenerator, CachingDtoTypeGenerator>(sp =>
                        {
                            var actualGeneratorLogger = sp.GetRequiredService<ILogger<DtoTypeGenerator>>();
                            var actualGenerator = new DtoTypeGenerator(actualGeneratorLogger);

                            var resettingWrapperLogger = sp.GetRequiredService<ILogger<SelfResettingDtoTypeGenerator>>();
                            var resettingWrapper = new SelfResettingDtoTypeGenerator(resettingWrapperLogger, actualGenerator);

                            var cachingWrapperLogger = sp.GetRequiredService<ILogger<CachingDtoTypeGenerator>>();
                            var cachingWrapper = new CachingDtoTypeGenerator(cachingWrapperLogger, resettingWrapper); // This needs to come after the resetting wrapper!

                            return cachingWrapper;
                        })
                        .AddSingleton<ILateBindingFunctionCollection, LateBindingFunctionCollection>(sp =>
                        {
                            var logger = sp.GetRequiredService<ILogger<LateBindingFunctionCollection>>();
                            var functions = new LateBindingFunctionCollection(logger);

                            if (DefaultFunctionsConstructing is not null)
                            {
                                var eventArgs = new FunctionsEventArgs(functions);
                                DefaultFunctionsConstructing.Invoke(null, eventArgs);
                            }

                            return functions;
                        })
                        .AddSingleton<ILateBindingExpressionTreeBuilder, LateBindingExpressionTreeBuilder>();

                    if (DefaultServiceProviderConstructing is not null)
                    {
                        var eventArgs = new ServiceCollectionEventArgs(serviceCollection);
                        DefaultServiceProviderConstructing.Invoke(null, eventArgs);
                    }

                    return serviceCollection.BuildServiceProvider();
                }

                return _serviceProvider;
            }
            set
            {
                _serviceProvider = value ?? throw new ArgumentNullException(nameof(ServiceProvider));
                _loggerFactory = null;
                _dtoTypeGenerator = null;
                _functions = null;
                _expressionTreeBuilder = null;
            }
        }

        private static ILoggerFactory? _loggerFactory;
        public static ILoggerFactory LoggerFactory
        {
            get
            {
                if (_loggerFactory is null)
                    _loggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();

                return _loggerFactory;
            }
        }

        private static IDtoTypeGenerator? _dtoTypeGenerator;
        public static IDtoTypeGenerator DtoTypeGenerator
        {
            get
            {
                if (_dtoTypeGenerator is null)
                    _dtoTypeGenerator = ServiceProvider.GetRequiredService<IDtoTypeGenerator>();

                return _dtoTypeGenerator;
            }
        }

        private static ILateBindingFunctionCollection? _functions;
        public static ILateBindingFunctionCollection Functions
        {
            get
            {
                if (_functions is null)
                    _functions = ServiceProvider.GetRequiredService<ILateBindingFunctionCollection>();

                return _functions;
            }
        }

        private static ILateBindingExpressionTreeBuilder? _expressionTreeBuilder;
        public static ILateBindingExpressionTreeBuilder ExpressionTreeBuilder
        {
            get
            {
                if (_expressionTreeBuilder is null)
                    _expressionTreeBuilder = ServiceProvider.GetRequiredService<ILateBindingExpressionTreeBuilder>();

                return _expressionTreeBuilder;
            }
        }

        public static event EventHandler<ServiceCollectionEventArgs>? DefaultServiceProviderConstructing;

        public static event EventHandler<FunctionsEventArgs>? DefaultFunctionsConstructing;

        static LateBinding()
        {
            DefaultFunctionsConstructing += (sender, args) => InitializeFunctions(args.Functions);
        }

        public static void InitializeFunctions(ILateBindingFunctionCollection functions)
        {
            InitializeMathFunctions<sbyte>(functions);
            InitializeMathFunctions<byte>(functions);
            InitializeMathFunctions<short>(functions);
            InitializeMathFunctions<ushort>(functions);
            InitializeMathFunctions<int>(functions);
            InitializeMathFunctions<uint>(functions);
            InitializeMathFunctions<long>(functions);
            InitializeMathFunctions<ulong>(functions);
            InitializeMathFunctions<float>(functions);
            InitializeMathFunctions<double>(functions);
            InitializeMathFunctions<decimal>(functions);

            InitializeStringFunctions(functions);

            InitializeEnumerableFunctions(functions);

            InitializeDateTimeFunctions(functions);
        }

        public static void InitializeMathFunctions<T>(ILateBindingFunctionCollection functions)
        {
            var argsT1 = new[] { typeof(T) };
            var argsT2 = new[] { typeof(T), typeof(T) };
            var argsT3 = new[] { typeof(T), typeof(T), typeof(T) };

            functions
                .Define("+", argsT2, argExprs => Expression.Add(argExprs[0], argExprs[1]))
                .Define("-", argsT2, argExprs => Expression.Subtract(argExprs[0], argExprs[1]))
                .Define("*", argsT2, argExprs => Expression.Multiply(argExprs[0], argExprs[1]))
                .Define("/", argsT2, argExprs => Expression.Divide(argExprs[0], argExprs[1]))
                .Define("%", argsT2, argExprs => Expression.Modulo(argExprs[0], argExprs[1]))
                .Define("==", argsT2, argExprs => Expression.Equal(argExprs[0], argExprs[1]))
                .Define("!=", argsT2, argExprs => Expression.NotEqual(argExprs[0], argExprs[1]))
                .Define(">", argsT2, argExprs => Expression.GreaterThan(argExprs[0], argExprs[1]))
                .Define(">=", argsT2, argExprs => Expression.GreaterThanOrEqual(argExprs[0], argExprs[1]))
                .Define("<", argsT2, argExprs => Expression.LessThan(argExprs[0], argExprs[1]))
                .Define("<=", argsT2, argExprs => Expression.LessThanOrEqual(argExprs[0], argExprs[1]));

            MethodInfo? GetMathMethod(string name, Type[] args) => typeof(Math)
                .GetMethods()
                .Where(m =>
                {
                    if (m.Name != name)
                        return false;

                    var parameters = m.GetParameters();
                    if (parameters.Length != args.Length)
                        return false;

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType != args[i])
                            return false;
                    }

                    return true;
                })
                .SingleOrDefault();

            var absMethod = GetMathMethod(nameof(Math.Abs), argsT1);
            if (absMethod is not null)
                functions.Define("abs", argsT1, argExprs => Expression.Call(null, absMethod, argExprs));
            var signMethod = GetMathMethod(nameof(Math.Sign), argsT1);
            if (absMethod is not null)
                functions.Define("sign", argsT1, argExprs => Expression.Call(null, signMethod, argExprs));
            var roundMethod = GetMathMethod(nameof(Math.Round), argsT1);
            if (roundMethod is not null)
                functions.Define("round", argsT1, argExprs => Expression.Call(null, roundMethod, argExprs));
            var floorMethod = GetMathMethod(nameof(Math.Floor), argsT1);
            if (floorMethod is not null)
                functions.Define("floor", argsT1, argExprs => Expression.Call(null, floorMethod, argExprs));
            var ceilingMethod = GetMathMethod(nameof(Math.Ceiling), argsT1);
            if (ceilingMethod is not null)
                functions.Define("ceiling", argsT1, argExprs => Expression.Call(null, ceilingMethod, argExprs));

            var powMethod = GetMathMethod(nameof(Math.Pow), argsT2);
            if (powMethod is not null)
                functions.Define("pow", argsT2, argExprs => Expression.Call(null, powMethod, argExprs));
            var minMethod = GetMathMethod(nameof(Math.Min), argsT2);
            if (minMethod is not null)
                functions.Define("min", argsT2, argExprs => Expression.Call(null, minMethod, argExprs));
            var maxMethod = GetMathMethod(nameof(Math.Max), argsT2);
            if (maxMethod is not null)
                functions.Define("max", argsT2, argExprs => Expression.Call(null, maxMethod, argExprs));
            var logMethod = GetMathMethod(nameof(Math.Log), argsT2);
            if (logMethod is not null)
                functions.Define("log", argsT2, argExprs => Expression.Call(null, logMethod, argExprs));

            var clampMethod = GetMathMethod(nameof(Math.Clamp), argsT3);
            if (clampMethod is not null)
                functions.Define("clamp", argsT3, argExprs => Expression.Call(null, clampMethod, argExprs));
        }

        public static void InitializeStringFunctions(ILateBindingFunctionCollection functions)
        {
            functions
                .Define("==", (string left, string right) => left == right)
                .Define("!=", (string left, string right) => left != right)
                .Define("length", (string str) => str.Length)
                .Define("charat", (string str, int index) => str[index])
                .Define("contains", (string left, string right) => left.Contains(right))
                .Define("indexof", (string outer, string inner) => outer.IndexOf(inner))
                .Define("startswith", (string left, string right) => left.StartsWith(right))
                .Define("substring", (string str, int startIndex, int length) => str.Substring(startIndex, length))
                .Define("substring", (string str, int startIndex) => str.Substring(startIndex))
                .Define("concat", (string left, string right) => left + right)
                .Define("replace", (string str, string oldValue, string newValue) => str.Replace(oldValue, newValue))
                .Define("insert", (string outer, int index, string inner) => outer.Insert(index, inner))
                .Define("trim", (string str) => str.Trim())
                .Define("ltrim", (string str) => str.TrimStart())
                .Define("rtrim", (string str) => str.TrimEnd())
                .Define("upper", (string str) => str.ToUpper())
                .Define("lower", (string str) => str.ToLower());
        }

        public static void InitializeEnumerableFunctions(ILateBindingFunctionCollection functions)
        {
            functions
                .Define("contains", new[] { typeof(IEnumerable), typeof(object) }, context =>
                    {
                        var enumerableExpr = context.BuildArgument(0);
                        var enumerableInterfaces = LateBindingHelpers
                            .GetIEnumerableInterfaces(enumerableExpr.Type, true);

                        foreach (var enumerableInterface in enumerableInterfaces)
                        {
                            var itemType = enumerableInterface.GetGenericArguments().Single();
                            if (context.TryBuildArgumentAs(1, itemType, out var itemExpr))
                            {
                                var method = LateBindingHelpers.GetIEnumerableMethod((IEnumerable<object> x) => x.Contains(null), itemType);
                                itemExpr = itemExpr.Type == itemType ?
                                    itemExpr :
                                    Expression.Convert(itemExpr, itemType);
                                return Expression.Call(null, method, new[] { enumerableExpr, itemExpr });
                            }
                        }

                        return null;
                    })
                .Define("count", new[] { typeof(IEnumerable) }, context =>
                    {
                        var enumerableExpr = context.BuildArgument(0);
                        var enumerableInterface = LateBindingHelpers
                            .GetIEnumerableInterfaces(enumerableExpr.Type, true)
                            .FirstOrDefault();

                        if (enumerableInterface is null)
                            return null;

                        var itemType = enumerableInterface.GetGenericArguments().Single();
                        var method = LateBindingHelpers.GetIEnumerableMethod((IEnumerable<object> x) => x.Count(), itemType);

                        return Expression.Call(null, method, new[] { enumerableExpr });
                    });
        }

        public static void InitializeBoolFunctions(ILateBindingFunctionCollection functions)
        {
            functions
                .Define("!", (bool b) => !b)
                .Define("==", (bool left, bool right) => left == right)
                .Define("!=", (bool left, bool right) => left != right)
                .Define("and", (bool left, bool right) => left && right)
                .Define("or", (bool left, bool right) => left || right)
                .Define("xor", (bool left, bool right) => left ^ right);
        }

        public static void InitializeDateTimeFunctions(ILateBindingFunctionCollection functions)
        {
            functions
                .Define("now", () => DateTime.Now)
                .Define("==", (DateTime left, DateTime right) => left == right)
                .Define("!=", (DateTime left, DateTime right) => left != right)
                .Define(">", (DateTime left, DateTime right) => left > right)
                .Define(">=", (DateTime left, DateTime right) => left >= right)
                .Define("<", (DateTime left, DateTime right) => left < right)
                .Define("<=", (DateTime left, DateTime right) => left <= right)
                .Define("add_milliseconds", (DateTime dt, double ms) => dt.AddMilliseconds(ms))
                .Define("add_seconds", (DateTime dt, double s) => dt.AddSeconds(s))
                .Define("add_minutes", (DateTime dt, double m) => dt.AddMinutes(m))
                .Define("add_hours", (DateTime dt, double hr) => dt.AddHours(hr))
                .Define("add_days", (DateTime dt, double d) => dt.AddDays(d))
                .Define("add_months", (DateTime dt, int mo) => dt.AddMonths(mo))
                .Define("add_years", (DateTime dt, int y) => dt.AddYears(y))
                .Define("diff_milliseconds", (DateTime left, DateTime right) => (left - right).Milliseconds)
                .Define("diff_seconds", (DateTime left, DateTime right) => (left - right).Seconds)
                .Define("diff_minutes", (DateTime left, DateTime right) => (left - right).Minutes)
                .Define("diff_hours", (DateTime left, DateTime right) => (left - right).Hours)
                .Define("diff_days", (DateTime left, DateTime right) => (left - right).Days)
                .Define("diff_months", (DateTime left, DateTime right) => DateDiffMonths(left, right))
                .Define("diff_years", (DateTime left, DateTime right) => DateDiffMonths(left, right) / 12);
        }

        private static int DateDiffMonths(DateTime left, DateTime right)
        {
            var (start, end, sign) = left >= right ?
                (right, left, 1) :
                (left, right, -1);

            var years = end.Year - start.Year;
            var months = (years * 12) + end.Month - start.Month - 1;

            if (end.Day >= start.Day)
                months++;

            return months * sign;
        }

        public sealed class FunctionsEventArgs : EventArgs
        {
            public ILateBindingFunctionCollection Functions { get; }

            public FunctionsEventArgs(ILateBindingFunctionCollection functions)
            {
                Functions = functions ?? throw new ArgumentNullException(nameof(functions));
            }
        }

        public sealed class ServiceCollectionEventArgs : EventArgs
        {
            public IServiceCollection ServiceCollection { get; }

            public ServiceCollectionEventArgs(IServiceCollection serviceCollection)
            {
                ServiceCollection = serviceCollection ?? throw new ArgumentNullException(nameof(serviceCollection));
            }
        }
    }
}