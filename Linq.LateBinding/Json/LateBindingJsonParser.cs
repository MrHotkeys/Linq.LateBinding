using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace MrHotkeys.Linq.LateBinding.Json
{
    public sealed class LateBindingJsonParser
    {
        public LateBindingJsonParser()
        { }

        public JsonQuery ParseQuery(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Must be an object!", nameof(json));

            var query = new JsonQuery();

            if (json.TryGetProperty("select", StringComparer.OrdinalIgnoreCase, out var selectJson))
                query.Select = ParseQuerySelect(selectJson);
            if (json.TryGetProperty("where", StringComparer.OrdinalIgnoreCase, out var whereJson))
                query.Where = ParseQueryWhere(whereJson);
            if (json.TryGetProperty("orderBy", StringComparer.OrdinalIgnoreCase, out var orderByJson))
                query.OrderBy = ParseQueryOrderBy(orderByJson);
            if (json.TryGetProperty("skip", StringComparer.OrdinalIgnoreCase, out var skipJson))
                query.Skip = ParseQuerySkipTake(skipJson);
            if (json.TryGetProperty("take", StringComparer.OrdinalIgnoreCase, out var takeJson))
                query.Take = ParseQuerySkipTake(skipJson);

            return query;
        }

        public Dictionary<string, ILateBinding>? ParseQuerySelect(JsonElement selectJson)
        {
            if (selectJson.ValueKind == JsonValueKind.Null)
                return null;

            if (selectJson.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Must be an object, or null!", nameof(selectJson));

            var select = new Dictionary<string, ILateBinding>();
            foreach (var property in selectJson.EnumerateObject())
            {
                select[property.Name] = ParseLateBind(property.Value);
            }

            return select;
        }

        public List<ILateBinding>? ParseQueryWhere(JsonElement whereJson)
        {
            if (whereJson.ValueKind == JsonValueKind.Null)
                return null;

            if (whereJson.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Must be an array, or null!", nameof(whereJson));

            var where = new List<ILateBinding>();
            foreach (var itemJson in whereJson.EnumerateArray())
            {
                var expression = ParseLateBind(itemJson);
                where.Add(expression);
            }

            return where;
        }

        public List<LateBindingOrderBy>? ParseQueryOrderBy(JsonElement orderByJson)
        {
            if (orderByJson.ValueKind == JsonValueKind.Null)
                return null;

            if (orderByJson.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("Must be an array, or null!", nameof(orderByJson));

            var orderBys = new List<LateBindingOrderBy>();
            foreach (var itemJson in orderByJson.EnumerateArray())
            {
                var orderBy = ParseOrderBy(itemJson);
                orderBys.Add(orderBy);
            }

            return orderBys;
        }

        public int? ParseQuerySkipTake(JsonElement json)
        {
            if (json.ValueKind == JsonValueKind.Null)
                return null;

            if (json.ValueKind != JsonValueKind.Number)
                throw new ArgumentException("Must be a number, or null!", nameof(json));

            return json.GetInt32();
        }

        public ILateBinding ParseLateBind(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Must be an object!", nameof(json));
            if (!json.TryGetProperty("expressionType", StringComparer.OrdinalIgnoreCase, out var expressionTypeElement))
                throw new ArgumentException("Must contain an \"expressionType\" property to describe the type of late binding expression!", nameof(json));
            if (expressionTypeElement.ValueKind != JsonValueKind.String)
                throw new ArgumentException("\"expressionType\" must contain a string value!", nameof(json));

            var expressionTypeString = expressionTypeElement.GetString();
            if (!Enum.TryParse<LateBindingExpressionType>(expressionTypeString, true, out var expressionType))
                throw new ArgumentException($"Unrecognized expression type \"{expressionTypeString}\"!");

            switch (expressionType)
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

        private ILateBinding ParseConstantExpression(JsonElement json)
        {
            if (!json.TryGetProperty("value", StringComparer.OrdinalIgnoreCase, out var valueElement))
                throw new ArgumentException("Must contain a \"value\" property containing the value of the constant!", nameof(json));

            return new LateBindingToConstantJson(valueElement);
        }

        private ILateBinding ParseFieldExpression(JsonElement json)
        {
            if (!json.TryGetProperty("field", StringComparer.OrdinalIgnoreCase, out var fieldElement))
                throw new ArgumentException("Must contain a \"field\" property with the name of the target field on the entity!", nameof(json));
            if (fieldElement.ValueKind != JsonValueKind.String)
                throw new ArgumentException("\"field\" must contain a string value!", nameof(json));

            var field = fieldElement.GetString();

            if (field is null)
                throw new InvalidOperationException(); // This shouldn't happen since we checked the value kind

            return new LateBindingToField(field);
        }

        private ILateBinding ParseCalculateExpression(JsonElement json)
        {
            if (!json.TryGetProperty("method", StringComparer.OrdinalIgnoreCase, out var methodElement))
                throw new ArgumentException("Must contain a \"method\" property with the name of the target calculate method!", nameof(json));
            if (methodElement.ValueKind != JsonValueKind.String)
                throw new ArgumentException("\"method\" must contain a string value!", nameof(json));

            var method = methodElement.GetString();

            if (method is null)
                throw new InvalidOperationException(); // This shouldn't happen since we checked the value kind

            if (json.TryGetProperty("args", StringComparer.OrdinalIgnoreCase, out var argsElement))
            {
                var args = argsElement
                    .EnumerateArray()
                    .Select(ParseLateBind);

                return new LateBindingToCalculate(method, args);
            }
            else
            {
                return new LateBindingToCalculate(method, Enumerable.Empty<ILateBinding>());
            }
        }

        public LateBindingOrderBy ParseOrderBy(JsonElement orderByJson)
        {
            if (orderByJson.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Must be an object!", nameof(orderByJson));

            var ascending = true;
            if (!orderByJson.TryGetProperty("ascending", StringComparer.OrdinalIgnoreCase, out var ascendingJson))
            {
                ascending = ascendingJson.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => throw new ArgumentException("\"ascending\" must be either true or false!"),
                };
            }

            if (!orderByJson.TryGetProperty("expression", StringComparer.OrdinalIgnoreCase, out var expressionJson))
                throw new ArgumentException("Must contain a \"expression\" property with the late binding expression used to determine order!", nameof(orderByJson));
            if (expressionJson.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("\"expression\" must contain an object value!", nameof(orderByJson));

            var expression = ParseLateBind(expressionJson);

            return new LateBindingOrderBy(ascending, expression);
        }
    }
}