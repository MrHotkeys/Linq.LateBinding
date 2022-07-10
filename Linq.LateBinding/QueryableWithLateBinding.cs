using System;

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
                    _dtoGenerator = new DtoTypeGenerator();

                return _dtoGenerator;
            }
            set => _dtoGenerator = value ?? throw new ArgumentNullException();
        }

        private static ICalculateExpressionManager? _calculateExpressionManager;
        public static ICalculateExpressionManager CalculateExpressionManager
        {
            get
            {
                if (_calculateExpressionManager is null)
                    _calculateExpressionManager = new CalculateExpressionManager();

                return _calculateExpressionManager;
            }
            set => _calculateExpressionManager = value ?? throw new ArgumentNullException();
        }

        private static ILateBindingExpressionTreeBuilder? _expressionTreeBuilder;
        public static ILateBindingExpressionTreeBuilder ExpressionTreeBuilder
        {
            get
            {
                if (_expressionTreeBuilder is null)
                    _expressionTreeBuilder = new LateBindingExpressionTreeBuilder(CalculateExpressionManager);

                return _expressionTreeBuilder;
            }
            set => _expressionTreeBuilder = value ?? throw new ArgumentNullException();
        }
    }
}
