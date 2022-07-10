using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MrHotkeys.Linq.LateBinding.Json
{
    internal static class JsonElementExtensions
    {
        public static bool TryGetProperty(this JsonElement element, string propertyName, IEqualityComparer<string> stringComparer, out JsonElement value)
        {
            if (element.ValueKind != JsonValueKind.Object)
                throw new ArgumentException();

            foreach (var property in element.EnumerateObject())
            {
                if (stringComparer.Equals(property.Name, propertyName))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        public static object GetNumberBoxed(this JsonElement element)
        {
            var raw = element.GetRawText();
            var match = Regex.Match(raw, @"^(?'sign'\-)?(?'integer'\d+)(\.(?'fractional'\d+))?([Ee](?'expSign'[\+\-])?(?'exp'\d+))?$");
            if (!match.Success)
                throw new ArgumentException();

            var sign = match.Groups["sign"].Value;
            var integerStr = match.Groups["integer"].Value;
            var fractionalStr = match.Groups["fractional"].Value;
            var expSign = match.Groups["expsign"].Value;
            var expStr = match.Groups["exp"].Value;

            var fractionalLength = fractionalStr.Length;
            while (fractionalLength > 0 && fractionalStr[fractionalLength - 1] == '0')
                fractionalLength--;

            var exp = expStr switch
            {
                "" => 0,
                _ => int.TryParse(expStr, out var x) ? x : throw new ArgumentException(),
            };

            if (expSign == "-")
                exp *= -1;

            var decimalPlaces = fractionalLength - exp;

            // if ((fractionalStr == "" || fractional == 0) && (expSign == "" || expSign == "+" || exp == 0))
            if (decimalPlaces <= 0)
            {
                var x = BigInteger.Parse(sign + integerStr + fractionalStr.Substring(0, fractionalLength));
                var pow = exp - fractionalLength;
                if (pow != 0)
                    x *= BigInteger.Pow(10, pow);

                if (x <= byte.MaxValue && x >= byte.MinValue)
                    return (byte)x;
                if (x <= sbyte.MaxValue && x >= sbyte.MinValue)
                    return (sbyte)x;
                if (x <= short.MaxValue && x >= short.MinValue)
                    return (short)x;
                if (x <= ushort.MaxValue && x >= ushort.MinValue)
                    return (ushort)x;
                if (x <= int.MaxValue && x >= int.MinValue)
                    return (int)x;
                if (x <= uint.MaxValue && x >= uint.MinValue)
                    return (uint)x;
                if (x <= long.MaxValue && x >= long.MinValue)
                    return (long)x;
                if (x <= ulong.MaxValue && x >= ulong.MinValue)
                    return (ulong)x;

                return x;
            }
            else
            {
                // Start with a double since it has the greatest range (lowest min, highest max)
                var asDouble = element.GetDouble();
                if (double.IsNegativeInfinity(asDouble) || double.IsPositiveInfinity(asDouble))
                    throw new ArgumentException();

                // If the value is within range of a decimal, check if we'd benefit from its precision
                if (asDouble <= (double)decimal.MaxValue && asDouble >= (double)decimal.MinValue)
                {
                    var asDecimal = element.GetDecimal();
                    if ((decimal)asDouble != asDecimal)
                        return asDecimal;
                }

                // If the value is within range of a float, check if we lose any precision using it
                if (asDouble <= float.MaxValue && asDouble >= float.MinValue)
                {
                    var asFloat = element.GetSingle();

                    if (asFloat == 0f)
                    {
                        if (asFloat == asDouble)
                            return asFloat;
                    }
                    else
                    {
                        // Round and truncate the float to 7 significant digits
                        const int SignificantDigits = 7;
                        var power = Math.Floor(Math.Log10(Math.Abs(asFloat))) + 1;
                        var roundScale = Math.Pow(10, power);
                        var asFloatRounded = roundScale * Math.Round(asFloat / roundScale, SignificantDigits);
                        var truncateScale = Math.Pow(10, power - SignificantDigits);
                        var asFloatRoundedTruncated = truncateScale * Math.Truncate(asFloatRounded / truncateScale);

                        if (asFloatRoundedTruncated == asDouble)
                            return asFloat;
                    }
                }

                return asDouble;
            }

            throw new NotImplementedException();
        }
    }
}