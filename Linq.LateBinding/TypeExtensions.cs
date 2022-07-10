using System;
using System.Linq;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding
{
    internal static class TypeExtensions
    {
        /// <summary>
        /// Gets if the type can be cast to another given type.
        /// </summary>
        /// <param name="from">The type casting from.</param>
        /// <param name="to">The type casting to.</param>
        /// <param name="implicitOnly">If true, will only look for implicit casts.
        ///     If false, explicit casts will be included as well.</param>
        /// <returns>True if a matching cast exists, false if not.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="from"/> or <paramref name="to"/> is null.</exception>
        public static bool CanCastTo(this Type from, Type to, bool implicitOnly = false)
        {
            // Adapted from solution here https://stackoverflow.com/a/22031364

            if (from is null)
                throw new ArgumentNullException(nameof(from));
            if (to is null)
                throw new ArgumentNullException(nameof(to));

            if (from.IsAssignableTo(to))
                return true;

            if ((from.IsPrimitive || from.IsEnum) && (to.IsPrimitive || to.IsEnum))
            {
                // All primitives (except bool) can be explicitly cast to each other (narrowing)
                if (!implicitOnly)
                    return from != typeof(bool) && to != typeof(bool);

                // Implemented based on this chart https://docs.microsoft.com/en-us/dotnet/standard/base-types/conversion-tables
                var fromTypeCode = Type.GetTypeCode(from);
                var toTypeCode = Type.GetTypeCode(to);
                return fromTypeCode switch
                {
                    TypeCode.Byte => toTypeCode switch
                    {
                        TypeCode.UInt16 or TypeCode.Int16 or TypeCode.UInt32 or TypeCode.Int32 or
                        TypeCode.UInt64 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or
                        TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.SByte => toTypeCode switch
                    {
                        TypeCode.Int16 or TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or
                        TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.Int16 => toTypeCode switch
                    {
                        TypeCode.Int32 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or
                        TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.UInt16 => toTypeCode switch
                    {
                        TypeCode.UInt32 or TypeCode.Int32 or TypeCode.UInt64 or TypeCode.Int64 or
                        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.Char => toTypeCode switch
                    {
                        TypeCode.UInt16 or TypeCode.UInt32 or TypeCode.Int32 or TypeCode.UInt64 or
                        TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.Int32 => toTypeCode switch
                    {
                        TypeCode.Int64 or TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.UInt32 => toTypeCode switch
                    {
                        TypeCode.UInt64 or TypeCode.Int64 or TypeCode.Single or TypeCode.Double or
                        TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.Int64 => toTypeCode switch
                    {
                        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.UInt64 => toTypeCode switch
                    {
                        TypeCode.Single or TypeCode.Double or TypeCode.Decimal => true,

                        _ => false,
                    },

                    TypeCode.Single => toTypeCode switch
                    {
                        TypeCode.Double => true,

                        _ => false,
                    },

                    TypeCode.Decimal => false, // The linked table has conversions from decimal to float+double, but these are explicit

                    TypeCode.Double => false,
                    TypeCode.Boolean => false,

                    _ => from == typeof(IntPtr) || from == typeof(UIntPtr) || from.IsEnum ?
                        false : // All primitive/enum cases should be covered but never know
                        throw new NotSupportedException($"Got unhandled conversion from primitive/enum type {from} to primitive/enum type {to}!"),
                };
            }
            else
            {
                return GetUserDefinedConversion(from, to, implicitOnly, true) is not null;
            }
        }

        /// <summary>
        /// Gets the user defined conversion method for converting from this type to another type, if it exists.
        /// Searches conversions defined on both types. If no matching method is found, or if the two types are
        /// the same, returns null.
        /// </summary>
        /// <param name="from">The type casting from.</param>
        /// <param name="to">The type casting to.</param>
        /// <param name="implicitOnly">If true, will only look for implicit casts.
        ///     If false, explicit casts will be included as well.</param>
        /// <param name="flattenHierarchy"></param>
        /// <returns>The found conversion if any are found and null if not.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="from"/> or <paramref name="to"/> is null.</exception>
        public static MethodInfo? GetUserDefinedConversion(this Type from, Type to, bool implicitOnly = false, bool flattenHierarchy = true)
        {
            if (from is null)
                throw new ArgumentNullException(nameof(from));
            if (to is null)
                throw new ArgumentNullException(nameof(to));

            if (from == to)
                return null;

            var flags = BindingFlags.Public | BindingFlags.Static |
                (flattenHierarchy ? BindingFlags.FlattenHierarchy : BindingFlags.DeclaredOnly);

            MethodInfo? GetConversionDefinedIn(Type t) => t
                .GetMethods(flags)
                .Where(m =>
                {
                    if (m.Name != "op_Implicit" && (implicitOnly || m.Name != "op_Explicit"))
                        return false;

                    if (m.ReturnType != to)
                        return false;

                    var parameters = m.GetParameters();

                    return parameters.Length == 1 && parameters[0].ParameterType.IsAssignableFrom(from);
                })
                .SingleOrDefault();

            return GetConversionDefinedIn(from) ?? GetConversionDefinedIn(to);
        }
    }
}