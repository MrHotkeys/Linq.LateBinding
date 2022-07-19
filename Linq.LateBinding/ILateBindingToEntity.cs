namespace MrHotkeys.Linq.LateBinding
{
    public interface ILateBindingToEntity : ILateBinding
    {
        public string Field { get; }
    }
}