namespace MrHotkeys.Linq.LateBinding.Binds
{
    public sealed class LateBindingToEntity : ILateBindingToEntity
    {
        public LateBindingForm Form => LateBindingForm.Entity;

        public string Field { get; }

        public LateBindingToEntity(string field)
        {
            Field = field;
        }

        public override string ToString() =>
            $"Entity[Field]";
    }
}