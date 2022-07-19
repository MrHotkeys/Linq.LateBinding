using System;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeContainer
    {
        public Type DtoType { get; }

        public event EventHandler<EventArgs>? Finalizing;

        public DtoTypeContainer(Type dtoType)
        {
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
        }

        ~DtoTypeContainer()
        {
            Finalizing?.Invoke(this, EventArgs.Empty);
        }
    }
}