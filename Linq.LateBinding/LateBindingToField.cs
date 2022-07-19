namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingToField : ILateBindingToField
    {
        public LateBindingExpressionType ExpressionType => LateBindingExpressionType.Field;

        public string Field { get; }

        public LateBindingToField(string field)
        {
            Field = field;
        }

        public override string ToString() =>
            $"[{Field}]";
    }
}