using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public static class LateBindingCalculateMethodManagerInit
    {
        public static void Init(ILateBindingCalculateMethodManager calcManager)
        {
            InitMath<sbyte>(calcManager);
            InitMath<byte>(calcManager);
            InitMath<short>(calcManager);
            InitMath<ushort>(calcManager);
            InitMath<int>(calcManager);
            InitMath<uint>(calcManager);
            InitMath<long>(calcManager);
            InitMath<ulong>(calcManager);
            InitMath<float>(calcManager);
            InitMath<double>(calcManager);
            InitMath<decimal>(calcManager);

            InitString(calcManager);

            InitEnumerable(calcManager);

            InitDateTime(calcManager);
        }

        public static void InitMath<T>(ILateBindingCalculateMethodManager calcManager)
        {
            var argsT1 = new[] { typeof(T) };
            var argsT2 = new[] { typeof(T), typeof(T) };
            var argsT3 = new[] { typeof(T), typeof(T), typeof(T) };

            calcManager
                .Define("+", argExprs => Expression.Add(argExprs[0], argExprs[1]), argsT2, true)
                .Define("-", argExprs => Expression.Subtract(argExprs[0], argExprs[1]), argsT2, true)
                .Define("*", argExprs => Expression.Multiply(argExprs[0], argExprs[1]), argsT2, true)
                .Define("/", argExprs => Expression.Divide(argExprs[0], argExprs[1]), argsT2, true)
                .Define("%", argExprs => Expression.Modulo(argExprs[0], argExprs[1]), argsT2, true)
                .Define("==", argExprs => Expression.Equal(argExprs[0], argExprs[1]), argsT2, true)
                .Define("!=", argExprs => Expression.NotEqual(argExprs[0], argExprs[1]), argsT2, true)
                .Define(">", argExprs => Expression.GreaterThan(argExprs[0], argExprs[1]), argsT2, true)
                .Define(">=", argExprs => Expression.GreaterThanOrEqual(argExprs[0], argExprs[1]), argsT2, true)
                .Define("<", argExprs => Expression.LessThan(argExprs[0], argExprs[1]), argsT2, true)
                .Define("<=", argExprs => Expression.LessThanOrEqual(argExprs[0], argExprs[1]), argsT2, true);

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
                calcManager.Define("abs", argExprs => Expression.Call(null, absMethod, argExprs), argsT1, true);
            var signMethod = GetMathMethod(nameof(Math.Sign), argsT1);
            if (absMethod is not null)
                calcManager.Define("sign", argExprs => Expression.Call(null, signMethod, argExprs), argsT1, true);
            var roundMethod = GetMathMethod(nameof(Math.Round), argsT1);
            if (roundMethod is not null)
                calcManager.Define("round", argExprs => Expression.Call(null, roundMethod, argExprs), argsT1, true);
            var floorMethod = GetMathMethod(nameof(Math.Floor), argsT1);
            if (floorMethod is not null)
                calcManager.Define("floor", argExprs => Expression.Call(null, floorMethod, argExprs), argsT1, true);
            var ceilingMethod = GetMathMethod(nameof(Math.Ceiling), argsT1);
            if (ceilingMethod is not null)
                calcManager.Define("ceiling", argExprs => Expression.Call(null, ceilingMethod, argExprs), argsT1, true);

            var powMethod = GetMathMethod(nameof(Math.Pow), argsT2);
            if (powMethod is not null)
                calcManager.Define("pow", argExprs => Expression.Call(null, powMethod, argExprs), argsT2, true);
            var minMethod = GetMathMethod(nameof(Math.Min), argsT2);
            if (minMethod is not null)
                calcManager.Define("min", argExprs => Expression.Call(null, minMethod, argExprs), argsT2, true);
            var maxMethod = GetMathMethod(nameof(Math.Max), argsT2);
            if (maxMethod is not null)
                calcManager.Define("max", argExprs => Expression.Call(null, maxMethod, argExprs), argsT2, true);
            var logMethod = GetMathMethod(nameof(Math.Log), argsT2);
            if (logMethod is not null)
                calcManager.Define("log", argExprs => Expression.Call(null, logMethod, argExprs), argsT2, true);

            var clampMethod = GetMathMethod(nameof(Math.Clamp), argsT3);
            if (clampMethod is not null)
                calcManager.Define("clamp", argExprs => Expression.Call(null, clampMethod, argExprs), argsT3, true);
        }

        public static void InitString(ILateBindingCalculateMethodManager calcManager)
        {
            calcManager
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

        public static void InitEnumerable(ILateBindingCalculateMethodManager calcManager)
        {
            calcManager
                .Define("contains", argExprs =>
                    {
                        var enumerableExpr = argExprs[0];
                        var enumerableInterfaces = LateBindingHelpers
                            .GetIEnumerableInterfaces(argExprs[0].Type, true);

                        var itemExpr = argExprs[1];
                        foreach (var enumerableInterface in enumerableInterfaces)
                        {
                            var itemType = enumerableInterface.GetGenericArguments().Single();
                            if (itemExpr.Type.CanCastTo(itemType, implicitOnly: true))
                            {
                                var method = LateBindingHelpers.GetIEnumerableMethod((IEnumerable<object> x) => x.Contains(null), itemType);
                                itemExpr = itemExpr.Type == itemType ?
                                    itemExpr :
                                    Expression.Convert(itemExpr, itemType);
                                return Expression.Call(null, method, new[] { enumerableExpr, itemExpr });
                            }
                        }

                        return null;
                    },
                    new[] { typeof(IEnumerable), typeof(object) }, false)
                .Define("count", argExprs =>
                    {
                        var enumerableExpr = argExprs[0];
                        var enumerableInterface = LateBindingHelpers
                            .GetIEnumerableInterfaces(argExprs[0].Type, true)
                            .FirstOrDefault();

                        if (enumerableInterface is null)
                            return null;

                        var itemType = enumerableInterface.GetGenericArguments().Single();
                        var method = LateBindingHelpers.GetIEnumerableMethod((IEnumerable<object> x) => x.Count(), itemType);

                        return Expression.Call(null, method, new[] { enumerableExpr });
                    },
                    new[] { typeof(IEnumerable) }, false);
        }

        public static void InitBool(ILateBindingCalculateMethodManager calcManager)
        {
            calcManager
                .Define("!", (bool b) => !b)
                .Define("==", (bool left, bool right) => left == right)
                .Define("!=", (bool left, bool right) => left != right)
                .Define("and", (bool left, bool right) => left && right)
                .Define("or", (bool left, bool right) => left || right)
                .Define("xor", (bool left, bool right) => left ^ right);
        }

        public static void InitDateTime(ILateBindingCalculateMethodManager calcManager)
        {
            calcManager
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
    }
}