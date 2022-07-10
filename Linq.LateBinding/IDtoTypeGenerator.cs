﻿using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding
{
    public interface IDtoTypeGenerator
    {
        public Type Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions);

        public Type Generate<TSource, TDto>(ICollection<string> propertyNames);
    }
}