namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class FieldLateBindingExpression : ILateBindingExpression
    {
        public LateBindingExpressionType Type => LateBindingExpressionType.Field;

        public string Field { get; }

        public FieldLateBindingExpression(string field)
        {
            Field = field;
        }

        public override string ToString() =>
            $"[{Field}]";
    }
}