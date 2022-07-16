namespace MrHotkeys.Linq.LateBinding
{
    public sealed class LateBindingToField : ILateBindingToField
    {
        public LateBindingTarget Target => LateBindingTarget.Field;

        public string Field { get; }

        public LateBindingToField(string field)
        {
            Field = field;
        }

        public override string ToString() =>
            $"[{Field}]";
    }
}