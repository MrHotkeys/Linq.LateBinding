using System;
using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

namespace MrHotkeys.Linq.LateBinding.EntityFramework.Sql
{
    public static class LateBindingInitEntityFrameworkSql
    {
        public static void AddToStatic()
        {
            LateBindingInit.DefaultCalculateMethodsConstructing += (sender, args) => InitializeCalculate(args.CalculateMethods);
        }

        public static void InitializeCalculate(ILateBindingCalculateBuilderCollection calcs)
        {
            InitializeCalculateString(calcs);

            InitializeCalculateDateTime(calcs);
        }

        public static void InitializeCalculateString(ILateBindingCalculateBuilderCollection calcs)
        {
            calcs
                .Define("like", (string str, string pattern) => EF.Functions.Like(str, pattern));
        }

        public static void InitializeCalculateDateTime(ILateBindingCalculateBuilderCollection calcs)
        {
            calcs
                .Define("diff_milliseconds", (DateTime left, DateTime right) => EF.Functions.DateDiffMillisecond(right, left))
                .Define("diff_seconds", (DateTime left, DateTime right) => EF.Functions.DateDiffSecond(right, left))
                .Define("diff_minutes", (DateTime left, DateTime right) => EF.Functions.DateDiffMinute(right, left))
                .Define("diff_hours", (DateTime left, DateTime right) => EF.Functions.DateDiffHour(right, left))
                .Define("diff_days", (DateTime left, DateTime right) => EF.Functions.DateDiffDay(right, left))
                .Define("diff_months", new[] { typeof(DateTime), typeof(DateTime) }, argExprs => MakeDateDiffMonthsExpression(argExprs[0], argExprs[1]))
                .Define("diff_years", new[] { typeof(DateTime), typeof(DateTime) }, argExprs => MakeDateDiffYearsExpression(argExprs[0], argExprs[1]));
        }

        private static Expression MakeDateDiffMonthsExpression(Expression left, Expression right)
        {
            return Expression.Condition(
                test: Expression.GreaterThanOrEqual(left, right),
                ifTrue: MakeCompareExpression(right, left, 1),
                ifFalse: MakeCompareExpression(left, right, -1));

            static Expression MakeCompareExpression(Expression start, Expression end, int sign)
            {
                var monthsFromYearsExpr = Expression.Multiply(
                    left: Expression.Subtract(
                        left: MakeYearAccess(end),
                        right: MakeYearAccess(start)
                    ),
                    right: Expression.Constant(12)
                );

                var monthsFromMonthsExpr = Expression.Subtract(
                    left: Expression.Subtract(
                        left: MakeMonthAccess(end),
                        right: MakeMonthAccess(start)
                    ),
                    right: Expression.Constant(1)
                );

                var monthsFromDaysExpr = Expression.Condition(
                    test: Expression.GreaterThanOrEqual(
                        left: MakeDayAccess(end),
                        right: MakeDayAccess(start)
                    ),
                    ifTrue: Expression.Constant(1),
                    ifFalse: Expression.Constant(0)
                );

                return Expression.Add(
                    left: monthsFromYearsExpr,
                    right: Expression.Add(
                        left: monthsFromMonthsExpr,
                        right: monthsFromDaysExpr
                    )
                );
            }

            static Expression MakeDayAccess(Expression expr) =>
                Expression.MakeMemberAccess(expr, typeof(DateTime).GetProperty(nameof(DateTime.Day)));

            static Expression MakeMonthAccess(Expression expr) =>
                Expression.MakeMemberAccess(expr, typeof(DateTime).GetProperty(nameof(DateTime.Month)));

            static Expression MakeYearAccess(Expression expr) =>
                Expression.MakeMemberAccess(expr, typeof(DateTime).GetProperty(nameof(DateTime.Year)));
        }

        private static Expression MakeDateDiffYearsExpression(Expression left, Expression right)
        {
            return Expression.Divide(
                left: MakeDateDiffMonthsExpression(left, right),
                right: Expression.Constant(12)
            );
        }
    }
}