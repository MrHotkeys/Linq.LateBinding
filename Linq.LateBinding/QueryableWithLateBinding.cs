using System;

namespace MrHotkeys.Linq.LateBinding
{
    public static class QueryableWithLateBinding
    {
        private static IDtoTypeGenerator? _dtoGenerator;
        public static IDtoTypeGenerator DtoGenerator
        {
            get
            {
                if (_dtoGenerator is null)
                    _dtoGenerator = new DtoTypeGenerator();

                return _dtoGenerator;
            }
            set => _dtoGenerator = value ?? throw new ArgumentNullException();
        }
    }
}
