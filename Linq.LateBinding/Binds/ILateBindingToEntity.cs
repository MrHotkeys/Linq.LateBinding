namespace MrHotkeys.Linq.LateBinding.Binds
{
    public interface ILateBindingToEntity : ILateBinding
    {
        public string Field { get; }
    }
}