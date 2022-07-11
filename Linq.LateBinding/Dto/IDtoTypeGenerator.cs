using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public interface IDtoTypeGenerator
    {
        public Type Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions);
    }
}