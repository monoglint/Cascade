using Cascade.Pipeline.Frontend.Lexer;
using Cascade.Pipeline.Frontend.Parser.Tools;

namespace Cascade.Pipeline.Runtime.Values
{
    public static class RuntimeValuerResolver
    {
        public static string ResolveToString(RuntimeValue runtimeValue)
        {
            return runtimeValue switch
            {
                FloatLiteralValue value => value.Value.ToString(),
                DoubleLiteralValue value => value.Value.ToString(),
                IntegerLiteralValue value => value.Value.ToString(),
                LongLiteralValue value => value.Value.ToString(),
                BooleanLiteralValue value => value.Value == true ? "True" : "False",
                StringLiteralValue value => value.Value,
                NullLiteralValue => "Null",
                _ => throw new NotImplementedException()
            };
        }
    }

    public static class NumericResolver
    {
        public static bool SolveComparison<T>(TokenKind binaryOperator, T value0, T value1) where T : IComparable<T>
        {
            return binaryOperator switch
            {
                TokenKind.S_RARROW => value0.CompareTo(value1) > 0,
                TokenKind.S_LARROW => value0.CompareTo(value1) < 0,
                TokenKind.S_GREATER_OR_EQUAL_TO => value0.CompareTo(value1) >= 0,
                TokenKind.S_LESS_OR_EQUAL_TO => value0.CompareTo(value1) <= 0,
                _ => throw new NotImplementedException()
            };
        }

        public static bool SolveDoubleComparison(TokenKind binaryOperator, double value0, double value1) => SolveComparison(binaryOperator, value0, value1);
        public static bool SolveFloatComparison(TokenKind binaryOperator, float value0, float value1) => SolveComparison(binaryOperator, value0, value1);
        public static bool SolveIntegerComparison(TokenKind binaryOperator, int value0, int value1) => SolveComparison(binaryOperator, value0, value1);
        public static bool SolveLongComparison(TokenKind binaryOperator, long value0, long value1) => SolveComparison(binaryOperator, value0, value1);

        public static double SolveDoubleArithmetic(TokenKind binaryOperator, double value0, double value1) => binaryOperator switch
        {
            TokenKind.S_PLUS => value0 + value1,
            TokenKind.S_MINUS => value0 - value1,
            TokenKind.S_ASTERISK => value0 * value1,
            TokenKind.S_SLASH => value0 / value1,
            TokenKind.S_CARET => Math.Pow(value0, value1),
            _ => throw new NotImplementedException()
        };

        public static float SolveFloatArithmetic(TokenKind binaryOperator, float value0, float value1) => binaryOperator switch
        {
            TokenKind.S_PLUS => value0 + value1,
            TokenKind.S_MINUS => value0 - value1,
            TokenKind.S_ASTERISK => value0 * value1,
            TokenKind.S_SLASH => value0 / value1,
            TokenKind.S_CARET => MathF.Pow(value0, value1),
            _ => throw new NotImplementedException()
        };
        public static int SolveIntegerArithmetic(TokenKind binaryOperator, int value0, int value1) => binaryOperator switch
        {
            TokenKind.S_PLUS => value0 + value1,
            TokenKind.S_MINUS => value0 - value1,
            TokenKind.S_ASTERISK => value0 * value1,
            TokenKind.S_SLASH => value0 / value1,
            TokenKind.S_CARET => Convert.ToInt32(Math.Pow(value0, value1)),
            _ => throw new NotImplementedException()
        };
        public static long SolveLongArithmetic(TokenKind binaryOperator, long value0, long value1) => binaryOperator switch
        {
            TokenKind.S_PLUS => value0 + value1,
            TokenKind.S_MINUS => value0 - value1,
            TokenKind.S_ASTERISK => value0 * value1,
            TokenKind.S_SLASH => value0 / value1,
            TokenKind.S_CARET => Convert.ToInt32(Math.Pow(value0, value1)),
            _ => throw new NotImplementedException()
        };

        public static RuntimeValueKind DetermineProperArithmeticMethod(RuntimeValueKind operand0, RuntimeValueKind operand1)
        {
            if (operand0 == RuntimeValueKind.L_INTEGER)
            {
                return operand1 == RuntimeValueKind.L_INTEGER ? RuntimeValueKind.L_INTEGER : operand1;
            }

            if (operand0 == RuntimeValueKind.L_LONG)
            {
                return operand1 == RuntimeValueKind.L_LONG ? RuntimeValueKind.L_LONG : operand1;
            }

            if (operand0 == RuntimeValueKind.L_FLOAT)
            {
                return operand1 == RuntimeValueKind.L_DOUBLE ? RuntimeValueKind.L_DOUBLE : operand1;
            }

            return RuntimeValueKind.L_DOUBLE;
        }
    }

    public static class TypeComparator
    {
        public static bool TypesMatch(TypeExpression baseType, TypeExpression type)
        {
            if (baseType.Nullable && type.Standard == StandardValueType.VOID)
            {
                return true;
            }

            return StandardsMatch(baseType, type) && MetasMatch(baseType, type);
        }

        public static bool StandardsMatch(TypeExpression baseType, TypeExpression type)
        {
            // If the types match directly.
            if (baseType.Standard == type.Standard)
            {
                return true;
            }

            // If the base type is dynamic.
            else if (baseType.Standard == StandardValueType.DYNAMIC)
            {
                return true;
            }

            // If we're assigning a function to a construct.
            else if (baseType.Standard == StandardValueType.CONSTRUCT && type.Standard == StandardValueType.FUNCTION)
            {
                return true;
            }

            return false;
        }

        public static bool MetasMatch(TypeExpression baseType, TypeExpression type)
        {
            // Ensure both types have metas.
            bool baseHasMeta = baseType.HasMeta();
            bool typeHasMeta = type.HasMeta();

            if (!baseHasMeta || !typeHasMeta)
            {
                // If both metas do not exist, then the types are matching.
                // OTHERWISE, if one meta exists while the other doesn't, they are not matching.
                return !baseHasMeta && !typeHasMeta;
            }

            // First check if there are a different amount of entries in both lists to save power.
            if (baseType.Meta!.Count != type.Meta!.Count)
            {
                return false;
            }

            int pointer = 0;

            while (pointer < baseType.Meta.Count)
            {
                if (baseType.Meta[pointer].Kind != type.Meta[pointer].Kind)
                {
                    return false;
                }

                pointer++;
            }

            return true;
        }
    }
}
