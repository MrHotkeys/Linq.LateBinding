namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingToField : ILateBinding
    {
        public string Field { get; }
    }
}