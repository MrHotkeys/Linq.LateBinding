using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using MrHotkeys.Linq.LateBinding.Expressions;

namespace MrHotkeys.Linq.LateBinding.Json
{
    public sealed class LateBindingExpressionJsonParser
    {
        public LateBindingExpressionJsonParser()
        { }

        public JsonQuery ParseQuery(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
                throw new ArgumentException();

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
                throw new ArgumentException();

            var select = new Dictionary<string, ILateBinding>();
            foreach (var property in selectJson.EnumerateObject())
            {
                select[property.Name] = ParseExpression(property.Value);
            }

            return select;
        }

        public List<ILateBinding>? ParseQueryWhere(JsonElement whereJson)
        {
            if (whereJson.ValueKind == JsonValueKind.Null)
                return null;

            if (whereJson.ValueKind != JsonValueKind.Array)
                throw new ArgumentException();

            var where = new List<ILateBinding>();
            foreach (var itemJson in whereJson.EnumerateArray())
            {
                var expression = ParseExpression(itemJson);
                where.Add(expression);
            }

            return where;
        }

        public List<LateBindingOrderBy>? ParseQueryOrderBy(JsonElement orderByJson)
        {
            if (orderByJson.ValueKind == JsonValueKind.Null)
                return null;

            if (orderByJson.ValueKind != JsonValueKind.Array)
                throw new ArgumentException();

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
                throw new ArgumentException();

            return json.GetInt32();
        }

        public ILateBinding ParseExpression(JsonElement json)
        {
            if (json.ValueKind != JsonValueKind.Object)
                throw new ArgumentException();
            if (!json.TryGetProperty("type", StringComparer.OrdinalIgnoreCase, out var typeElement))
                throw new ArgumentException();
            if (typeElement.ValueKind != JsonValueKind.String)
                throw new ArgumentException();
            if (!Enum.TryParse<LateBindingTarget>(typeElement.GetString(), true, out var type))
                throw new ArgumentException();

            switch (type)
            {
                case LateBindingTarget.Constant:
                    return ParseConstantExpression(json);
                case LateBindingTarget.Field:
                    return ParseFieldExpression(json);
                case LateBindingTarget.Calculate:
                    return ParseCalculateExpression(json);
                default:
                    throw new InvalidOperationException();
            }
        }

        private ILateBinding ParseConstantExpression(JsonElement json)
        {
            if (!json.TryGetProperty("value", StringComparer.OrdinalIgnoreCase, out var valueElement))
                throw new ArgumentException();

            return new LateBindingToConstantJson(valueElement);
        }

        private ILateBinding ParseFieldExpression(JsonElement json)
        {
            if (!json.TryGetProperty("field", StringComparer.OrdinalIgnoreCase, out var fieldElement))
                throw new ArgumentException();
            var field = fieldElement.GetString();

            if (field is null)
                throw new ArgumentException();

            return new LateBindingToField(field);
        }

        private ILateBinding ParseCalculateExpression(JsonElement json)
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
                .Select(ParseExpression);

            return new LateBindingToCalculate(method, args);
        }

        public LateBindingOrderBy ParseOrderBy(JsonElement orderByJson)
        {
            if (orderByJson.ValueKind != JsonValueKind.Object)
                throw new ArgumentException();
            if (!orderByJson.TryGetProperty("ascending", StringComparer.OrdinalIgnoreCase, out var ascendingJson))
                throw new ArgumentException();

            var ascending = ascendingJson.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new ArgumentException(),
            };

            if (!orderByJson.TryGetProperty("expression", StringComparer.OrdinalIgnoreCase, out var expressionJson))
                throw new ArgumentException();
            if (expressionJson.ValueKind != JsonValueKind.Object)
                throw new ArgumentException();

            var expression = ParseExpression(expressionJson);

            return new LateBindingOrderBy(ascending, expression);
        }
    }
}