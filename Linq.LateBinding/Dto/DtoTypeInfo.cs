using System;
using System.Collections.Generic;
using System.Reflection;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class DtoTypeInfo
    {
        public Type DtoType { get; }

        public IReadOnlyDictionary<string, PropertyInfo> SelectPropertyMap { get; }

        public DtoTypeInfo(Type dtoType, IReadOnlyDictionary<string, PropertyInfo> selectPropertyMap)
        {
            DtoType = dtoType ?? throw new ArgumentNullException(nameof(dtoType));
            SelectPropertyMap = selectPropertyMap ?? throw new ArgumentNullException(nameof(selectPropertyMap));
        }
    }
}