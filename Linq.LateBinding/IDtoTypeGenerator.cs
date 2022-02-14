using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding
{
    public interface IDtoTypeGenerator
    {
        Type Generate<TSource, TDto>(ICollection<string> propertyNames);
    }
}