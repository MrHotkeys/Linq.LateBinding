using System;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MrHotkeys.Linq.LateBinding.Json
{
    public sealed class LateBindingToConstantJson : ILateBindingToConstant
    {
        public LateBindingForm Form => LateBindingForm.Const;

        private JsonElement Json { get; }

        public LateBindingToConstantJson(JsonElement json)
        {
            Json = json;
        }

        public object? GetValue()
        {
            return TryGetValue(out var value) ?
                value :
                throw new InvalidOperationException();
        }

        public bool TryGetValue(out object? value)
        {
            switch (Json.ValueKind)
            {
                case JsonValueKind.False:
                case JsonValueKind.True:
                    {
                        value = Json.GetBoolean();
                        return true;
                    }

                case JsonValueKind.Number:
                    {
                        value = Json.GetNumberBoxed();
                        return true;
                    }

                case JsonValueKind.String:
                    {
                        value = Json.GetString();
                        return true;
                    }

                case JsonValueKind.Null:
                    {
                        value = null;
                        return true;
                    }

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    {
                        value = default;
                        return true;
                    }

                case JsonValueKind.Undefined:
                    throw new InvalidOperationException();

                default:
                    throw new InvalidOperationException();
            }
        }

        public bool TryGetValueAs(Type type, out object? value)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            if (type == typeof(object))
                return TryGetValue(out value);

            switch (Json.ValueKind)
            {
                case JsonValueKind.False:
                case JsonValueKind.True:
                    {
                        if (type == typeof(bool))
                        {
                            value = Json.GetBoolean();
                            return true;
                        }
                        else
                        {
                            value = default;
                            return false;
                        }
                    }

                case JsonValueKind.Number:
                    {
                        if (type == typeof(sbyte))
                        {
                            if (Json.TryGetSByte(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(byte))
                        {
                            if (Json.TryGetByte(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(short))
                        {
                            if (Json.TryGetInt16(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(ushort))
                        {
                            if (Json.TryGetUInt16(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(int))
                        {
                            if (Json.TryGetInt32(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(uint))
                        {
                            if (Json.TryGetUInt32(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(long))
                        {
                            if (Json.TryGetInt64(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(ulong))
                        {
                            if (Json.TryGetUInt64(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(float))
                        {
                            if (Json.TryGetSingle(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(double))
                        {
                            if (Json.TryGetDouble(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else if (type == typeof(decimal))
                        {
                            if (Json.TryGetDecimal(out var valueUnboxed))
                            {
                                value = valueUnboxed;
                                return true;
                            }
                            else
                            {
                                value = default;
                                return false;
                            }
                        }
                        else
                        {
                            value = default;
                            return false;
                        }
                    }

                case JsonValueKind.String:
                    {
                        var str = Json.GetString()!; // We know it's not null else the JsonValueKind wouldn't be string

                        if (type == typeof(string))
                        {
                            value = str;
                            return true;
                        }
                        else if (type == typeof(DateTime))
                        {
                            value = ParseDateTime(str);
                            return true;
                        }
                        else
                        {
                            value = default;
                            return false;
                        }
                    }

                case JsonValueKind.Null:
                    {
                        if (type.CanBeSetToNull())
                        {
                            value = null;
                            return true;
                        }
                        else
                        {
                            value = default;
                            return false;
                        }
                    }

                case JsonValueKind.Object:
                case JsonValueKind.Array:
                    {
                        value = default;
                        return false;
                    }

                case JsonValueKind.Undefined:
                    throw new InvalidOperationException();

                default:
                    throw new InvalidOperationException();
            }
        }

        private static DateTime ParseDateTime(string str)
        {
            // Ex 2022-01-01 01:02:03.456
            // Seconds and ms are optional (but seconds required if ms given)
            // Date can be given without time (but date required if time given)
            var regex = @"^\s*" +
                        @"(?'year'\d{4})" + @"\-" +
                        @"(?'month'\d{1,2})" + @"\-" +
                        @"(?'day'\d{1,2})" +
                        @"(\s(?'hour'\d\d)" + ":" +
                         @"(?'minute'\d\d)" +
                         @"(:(?'second'\d\d)" +
                          @"(.(?'millisecond'\d{1,3}))?" +
                         @")?" +
                        @")?\s*$";
            var match = Regex.Match(str, regex);

            if (!match.Success)
                throw new InvalidOperationException();

            var hourStr = match.Groups["hour"].Value;
            var minuteStr = match.Groups["minute"].Value;
            var secondStr = match.Groups["second"].Value;
            var millisecondStr = match.Groups["millisecond"].Value;

            var hour = 0;
            var minute = 0;
            var second = 0;
            var millisecond = 0;
            if (hourStr != "")
            {
                hour = int.Parse(hourStr);
                minute = int.Parse(minuteStr);

                if (secondStr != "")
                {
                    second = int.Parse(secondStr);

                    if (millisecondStr != "")
                        millisecond = int.Parse(millisecondStr);
                }
            }

            return new DateTime(
                year: int.Parse(match.Groups["year"].Value),
                month: int.Parse(match.Groups["month"].Value),
                day: int.Parse(match.Groups["day"].Value),
                hour: hour,
                minute: minute,
                second: second,
                millisecond: millisecond
            );
        }

        public override string ToString() => Json.ToString();
    }
}