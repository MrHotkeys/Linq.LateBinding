using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public interface IDtoTypeGenerator
    {
        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions);
        public void Reset();
    }
}