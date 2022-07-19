using System;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    internal sealed class WeakReferenceContainer<T>
    {
        public T Value { get; }

        public event EventHandler<EventArgs>? Finalizing;

        public WeakReferenceContainer(T value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        ~WeakReferenceContainer()
        {
            Finalizing?.Invoke(this, EventArgs.Empty);
        }
    }
}