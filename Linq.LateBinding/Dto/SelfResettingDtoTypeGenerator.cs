using System;
using System.Collections.Generic;

using Microsoft.Extensions.Logging;

namespace MrHotkeys.Linq.LateBinding.Dto
{
    public sealed class SelfResettingDtoTypeGenerator : IDtoTypeGenerator
    {
        private ILogger Logger { get; }

        public IDtoTypeGenerator Generator { get; }

        private int _dtoTypeCountThreshold = 100;
        public int DtoTypeCountThreshold
        {
            get => _dtoTypeCountThreshold;
            set
            {
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(DtoTypeCountThreshold), "Must be > 1!");
                _dtoTypeCountThreshold = value;

                CheckIfResetNeeded();
            }
        }

        public int DtoTypeCount { get; private set; } = 0;

        public SelfResettingDtoTypeGenerator(ILogger<SelfResettingDtoTypeGenerator> logger, IDtoTypeGenerator generator)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Generator = generator ?? throw new ArgumentNullException(nameof(generator));
        }

        public DtoTypeInfo Generate(IEnumerable<DtoPropertyDefinition> propertyDefintions)
        {
            CheckIfResetNeeded();

            var info = Generator.Generate(propertyDefintions);

            DtoTypeCount++;

            return info;
        }

        private void CheckIfResetNeeded()
        {
            if (DtoTypeCount >= DtoTypeCountThreshold)
                Reset();
        }

        public void Reset()
        {
            Generator.Reset();
            DtoTypeCount = 0;
        }
    }
}