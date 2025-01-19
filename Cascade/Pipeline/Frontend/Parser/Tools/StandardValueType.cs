using Cascade.Pipeline.Frontend.Lexer;

namespace Cascade.Pipeline.Frontend.Parser.Tools
{
    public static class StandardValueTypeCreator
    {
        public static StandardValueType FromToken(Token token)
        {
            switch (token.Kind)
            {
                case TokenKind.K_STRING:
                {
                    return StandardValueType.STRING;
                }
                case TokenKind.K_INTEGER:
                {
                    return StandardValueType.INTEGER;
                }
                case TokenKind.K_FLOAT:
                {
                    return StandardValueType.FLOAT;
                }
                case TokenKind.K_DOUBLE:
                {
                    return StandardValueType.DOUBLE;
                }
                case TokenKind.K_LONG:
                {
                    return StandardValueType.LONG;
                }
                case TokenKind.K_BOOLEAN:
                {
                    return StandardValueType.BOOLEAN;
                }
                case TokenKind.K_VOID:
                {
                    return StandardValueType.VOID;
                }
                case TokenKind.K_OBJECT:
                {
                    return StandardValueType.OBJECT;
                }
                case TokenKind.K_CLASS:
                {
                    return StandardValueType.CLASS;
                }
                case TokenKind.K_CONSTRUCT:
                {
                    return StandardValueType.CONSTRUCT;
                }
                case TokenKind.K_FUNCTION:
                {
                    return StandardValueType.FUNCTION;
                }
                case TokenKind.K_DYNAMIC:
                {
                    return StandardValueType.DYNAMIC;
                }
                default:
                {
                    return StandardValueType.VOID;
                }
            }
        }
    }

    public enum StandardValueType
    {
        // UNKEYWORDED EXPRESSIONS - There is not a direct token keyword that leads to the following expressions.
        FUNCTION,
        CS_FUNCTION,

        // EXPRESSIONS
        OBJECT,
        CLASS,

        CONSTRUCT, // Derived from functions

        DYNAMIC, // Represents any type.

        // LITERALS
        FLOAT,
        DOUBLE,
        INTEGER,
        LONG,
        STRING,
        BOOLEAN,
        VOID,
    }
}
