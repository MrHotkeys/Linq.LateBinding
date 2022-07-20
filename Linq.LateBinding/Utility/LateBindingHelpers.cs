using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Utility
{
    public static class LateBindingHelpers
    {
        public static IEnumerable<Type> GetIEnumerableInterfaces(Type type, bool genericOnly)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            var interfaces = type
                .GetInterfaces()
                .AsEnumerable();
            if (type.IsInterface)
                interfaces = interfaces.Append(type);

            return interfaces.Where(i => i.Name.StartsWith(nameof(IEnumerable)) && (!genericOnly || i.IsGenericType));
        }

        public static MethodInfo GetIEnumerableMethod(LambdaExpression expr, params Type[] typeArgs)
        {
            if (expr is null)
                throw new ArgumentNullException(nameof(expr));
            if (expr.Body is not MethodCallExpression callExpr)
                throw new ArgumentException();

            var method = callExpr.Method;

            if (typeArgs?.Length > 0)
            {
                method = method
                    .GetGenericMethodDefinition()
                    .MakeGenericMethod(typeArgs);
            }

            return method;
        }
    }
}