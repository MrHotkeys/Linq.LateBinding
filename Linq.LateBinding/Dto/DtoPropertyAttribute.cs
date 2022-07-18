using System;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class DtoPropertyAttribute : Attribute
    {
        public string SelectName { get; }

        public DtoPropertyAttribute(string selectName)
        {
            SelectName = selectName ?? throw new ArgumentNullException(nameof(selectName));
        }
    }
}