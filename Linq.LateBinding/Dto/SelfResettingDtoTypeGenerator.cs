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
                var startValue = _dtoTypeCountThreshold;
                if (value <= 0)
                    throw new ArgumentOutOfRangeException(nameof(DtoTypeCountThreshold), "Must be > 1!");
                _dtoTypeCountThreshold = value;

                if (Logger.IsEnabled(LogLevel.Trace))
                {
                    Logger.LogDebug($"{nameof(DtoTypeCountThreshold)} updated from {{oldDtoTypeCountThreshold}} to {{newDtoTypeCountThreshold}}",
                        startValue, DtoTypeCountThreshold);
                }

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
                Reset(false);
        }

        public void Reset() =>
            Reset(true);

        private void Reset(bool manual)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                if (manual)
                    Logger.LogDebug($"Resetting inner DTO generator: {nameof(Reset)} called.");
                else
                    Logger.LogDebug("Resetting inner DTO generator: {dtoTypeCountThreshold} count threshold hit.", DtoTypeCountThreshold);
            }

            Generator.Reset();
            DtoTypeCount = 0;
        }
    }
}