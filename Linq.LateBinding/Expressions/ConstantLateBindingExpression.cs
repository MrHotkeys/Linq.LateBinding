namespace MrHotkeys.Linq.LateBinding.Expressions
{
    public sealed class ConstantLateBindingExpression : ILateBindingExpression
    {
        public LateBindingExpressionType Type => LateBindingExpressionType.Constant;

        public object? Value { get; }

        public ConstantLateBindingExpression(object? value)
        {
            Value = value;
        }

        public override string ToString() =>
            Value?.ToString() ?? "<null>";
    }
}