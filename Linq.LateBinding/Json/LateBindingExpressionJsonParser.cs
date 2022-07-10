using System;
using System.Linq;
using System.Text.Json;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding.Json
{
    public sealed class LateBindingExpressionJsonParser
    {
        public LateBindingExpressionJsonParser()
        { }

        public ILateBindingExpression Parse(JsonElement json)
        {
            if (!json.TryGetProperty("type", StringComparer.OrdinalIgnoreCase, out var typeElement))
                throw new ArgumentException();
            if (typeElement.ValueKind != JsonValueKind.String)
                throw new ArgumentException();
            if (!Enum.TryParse<LateBindingExpressionType>(typeElement.GetString(), true, out var type))
                throw new ArgumentException();

            switch (type)
            {
                case LateBindingExpressionType.Constant:
                    return ParseConstantExpression(json);
                case LateBindingExpressionType.Field:
                    return ParseFieldExpression(json);
                case LateBindingExpressionType.Calculate:
                    return ParseCalculateExpression(json);
                default:
                    throw new InvalidOperationException();
            }
        }

        private ILateBindingExpression ParseConstantExpression(JsonElement json)
        {
            if (!json.TryGetProperty("value", StringComparer.OrdinalIgnoreCase, out var valueElement))
                throw new ArgumentException();

            var value = valueElement.ValueKind switch
            {
                JsonValueKind.False => false,
                JsonValueKind.True => true,
                JsonValueKind.Number => valueElement.GetNumberBoxed(),
                JsonValueKind.String => valueElement.GetString(),
                _ => throw new ArgumentException(),
            };

            return new ConstantLateBindingExpression(value);
        }

        private ILateBindingExpression ParseFieldExpression(JsonElement json)
        {
            if (!json.TryGetProperty("field", StringComparer.OrdinalIgnoreCase, out var fieldElement))
                throw new ArgumentException();
            var field = fieldElement.GetString();

            if (field is null)
                throw new ArgumentException();

            return new FieldLateBindingExpression(field);
        }

        private ILateBindingExpression ParseCalculateExpression(JsonElement json)
        {
            if (!json.TryGetProperty("method", StringComparer.OrdinalIgnoreCase, out var methodElement))
                throw new ArgumentException();
            var method = methodElement.GetString();

            if (method is null)
                throw new ArgumentException();

            if (!json.TryGetProperty("args", StringComparer.OrdinalIgnoreCase, out var argsElement))
                throw new ArgumentException();
            var args = argsElement
                .EnumerateArray()
                .Select(Parse);

            return new CalculateLateBindingExpression(method, args);
        }
    }
}