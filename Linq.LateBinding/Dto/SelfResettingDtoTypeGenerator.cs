using System;
using System.Collections.Generic;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class SelfResettingDtoTypeGenerator : IDtoTypeGenerator
    {
        public IDtoTypeGenerator Generator { get; }

        public int DtoTypeCountThreshold { get; }

        public int DtoTypeCount { get; private set; } = 0;

        public SelfResettingDtoTypeGenerator(IDtoTypeGenerator generator, int dtoTypeCountThreshold)
        {
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
            DtoTypeCountThreshold = dtoTypeCountThreshold > 0 ?
                dtoTypeCountThreshold :
                throw new ArgumentOutOfRangeException(nameof(dtoTypeCountThreshold), dtoTypeCountThreshold, "Must be > 0!");
        }

        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            if (DtoTypeCount >= DtoTypeCountThreshold)
                Reset();

            var info = Generator.Generate(propertyDefintions);

            DtoTypeCount++;

            return info;
        }

        public void Reset()
        {
            Generator.Reset();
            DtoTypeCount = 0;
        }
    }
}