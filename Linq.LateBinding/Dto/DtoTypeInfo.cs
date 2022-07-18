using System;
using System.Collections.Generic;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeInfo
    {
        public Type DtoType { get; }

        public IReadOnlyDictionary<string, PropertyInfo> SelectPropertyMap { get; }

        public IReadOnlyCollection<DtoPropertyDefinition> PropertyDefinitions { get; }

        public event EventHandler<EventArgs>? Finalizing;

        public DtoTypeInfo(Type dtoType, IReadOnlyDictionary<string, PropertyInfo> selectPropertyMap, IReadOnlyCollection<DtoPropertyDefinition> propertyDefinitions)
        {
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            SelectPropertyMap = selectPropertyMap ?? throw new ArgumentNullException(nameof(selectPropertyMap));
            PropertyDefinitions = propertyDefinitions ?? throw new ArgumentNullException(nameof(propertyDefinitions));
        }

        ~DtoTypeInfo()
        {
            Finalizing?.Invoke(this, EventArgs.Empty);
        }
    }
}