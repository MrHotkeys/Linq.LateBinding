using System;

using MrHotkeys.Linq.LateBinding.Dto;
using MrHotkeys.Linq.LateBinding.Expressions;

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
                {
                    var actualGenerator = new DtoTypeGenerator();
                    var resettingWrapper = new SelfResettingDtoTypeGenerator(actualGenerator, 100);
                    var cachingWrapper = new CachingDtoTypeGenerator(resettingWrapper); // This needs to come after the resetting wrapper!

                    _dtoGenerator = cachingWrapper;
                }

                return _dtoGenerator;
            }
            set => _dtoGenerator = value ?? throw new ArgumentNullException();
        }

        private static ILateBindingCalculateMethodManager? _calculateMethodManager;
        public static ILateBindingCalculateMethodManager CalculateMethodManager
        {
            get
            {
                if (_calculateMethodManager is null)
                    _calculateMethodManager = new LateBindingCalculateMethodManager();

                return _calculateMethodManager;
            }
            set => _calculateMethodManager = value ?? throw new ArgumentNullException();
        }

        private static ILateBindingExpressionTreeBuilder? _expressionTreeBuilder;
        public static ILateBindingExpressionTreeBuilder ExpressionTreeBuilder
        {
            get
            {
                if (_expressionTreeBuilder is null)
                    _expressionTreeBuilder = new LateBindingExpressionTreeBuilder(CalculateMethodManager);

                return _expressionTreeBuilder;
            }
            set => _expressionTreeBuilder = value ?? throw new ArgumentNullException();
        }
    }
}
