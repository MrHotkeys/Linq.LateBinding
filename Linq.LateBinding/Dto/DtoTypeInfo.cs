using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeInfo
    {
        public Type DtoType { get; }

        public IReadOnlyDictionary<string, PropertyInfo> SelectPropertyMap { get; }

        public IReadOnlyCollection<DtoPropertyDefinition> PropertyDefinitions { get; }

        public DtoTypeInfo(Type dtoType, IReadOnlyDictionary<string, PropertyInfo> selectPropertyMap,
            IReadOnlyCollection<DtoPropertyDefinition> propertyDefinitions)
        {
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            SelectPropertyMap = selectPropertyMap ?? throw new ArgumentNullException(nameof(selectPropertyMap));
            PropertyDefinitions = propertyDefinitions ?? throw new ArgumentNullException(nameof(propertyDefinitions));
        }

        public Weak ToWeak()
        {
            return new Weak(DtoType, SelectPropertyMap, PropertyDefinitions);
        }

        public sealed class Weak
        {
            private WeakReference<DtoTypeContainer> DtoTypeContainerReference { get; }

            public string DtoTypeFullName { get; }

            public IReadOnlyDictionary<string, PropertyInfo> SelectPropertyMap { get; }

            public IReadOnlyCollection<DtoPropertyDefinition> PropertyDefinitions { get; }

            public event EventHandler<EventArgs>? DtoTypeFinalizing;

            public Weak(Type dtoType, IReadOnlyDictionary<string, PropertyInfo> selectPropertyMap,
                IReadOnlyCollection<DtoPropertyDefinition> propertyDefinitions)
            {
                if (dtoType is null)
                    throw new ArgumentNullException(nameof(dtoType));
                var container = new DtoTypeContainer(dtoType);
                DtoTypeContainerReference = new WeakReference<DtoTypeContainer>(container);
                container.Finalizing += HandleDtoTypeContainerFinalizing;

                DtoTypeFullName = dtoType.FullName;

                SelectPropertyMap = selectPropertyMap ?? throw new ArgumentNullException(nameof(selectPropertyMap));
                PropertyDefinitions = propertyDefinitions ?? throw new ArgumentNullException(nameof(propertyDefinitions));
            }

            private void HandleDtoTypeContainerFinalizing(object sender, EventArgs args)
            {
                DtoTypeFinalizing?.Invoke(this, EventArgs.Empty);
            }

            public bool TryGetDtoType([NotNullWhen(true)] out Type? dtoType)
            {
                if (DtoTypeContainerReference.TryGetTarget(out var container))
                {
                    dtoType = container.DtoType;
                    return true;
                }
                else
                {
                    dtoType = default;
                    return false;
                }
            }

            public bool TryGetDtoTypeInfo([NotNullWhen(true)] out DtoTypeInfo? dtoTypeInfo)
            {
                if (TryGetDtoType(out var dtoType))
                {
                    dtoTypeInfo = new DtoTypeInfo(dtoType, SelectPropertyMap, PropertyDefinitions);
                    return true;
                }
                else
                {
                    dtoTypeInfo = default;
                    return false;
                }
            }
        }
    }
}