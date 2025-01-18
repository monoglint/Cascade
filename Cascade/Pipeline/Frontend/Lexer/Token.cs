using System.Runtime.InteropServices;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Frontend.Lexer
{
    public enum TokenKind
    {
        EOF,

        // Identifier
        IDENTIFIER,

        // Keywords
        K_IF,
        K_ELSEIF,
        K_ELSE,

        K_WHILE,
        K_FOR,
        K_POST,

        K_RETURN,   // Break from functions.
        K_BREAK,    // Break from loops.
        K_CONTINUE, // Advance a loop.
        K_EXIT,     // Break from a program.

        K_NEW,      // Create a new instance of a class.

        K_OF,       // Used for inheritance.

        // Type definitions
        K_STRING,
        K_INTEGER,
        K_FLOAT,
        K_DOUBLE,
        K_LONG,
        K_VOID,
        K_BOOLEAN,

        K_CLASS,
        K_OBJECT,

        K_CONSTRUCT,
        K_DYNAMIC,

        K_DELETE,
        K_ENSURE,

        // Data modifiers
        K_CONSTANT,
        K_STATIC,
        K_PUBLIC,
        K_PRIVATE,
        K_PROTECTED,

        // Literals
        L_STRING,
        L_NUMBER,
        L_BOOLEAN,
        L_NULL,

        // Symbols
        S_FLOAT,
        S_DOUBLE,
        S_LONG,
        S_INTEGER,

        S_LPAREN,
        S_RPAREN,
        S_LBRACE,
        S_RBRACE,
        S_LBRACKET,
        S_RBRACKET,
        S_UNDERSCORE,
        S_QUESTION,

        S_SLASH,
        S_PIPE,
        S_BACKSLASH,

        S_COMMA,
        S_DOT,
        S_COLON,
        S_SEMICOLON,

        S_PLUS,
        S_ASTERISK,
        S_MINUS,
        S_CARET,
        S_PERCENT,
        S_AMPERSAND,

        S_RARROW,
        S_LARROW,

        S_ASSIGN,                //  =
        S_ASSIGN_ADD,
        S_ASSIGN_SUB,
        S_ASSIGN_MUL,
        S_ASSIGN_DIV,
        S_ASSIGN_MOD,
        S_ASSIGN_EXP,

        S_AND,
        S_OR,
        S_XOR,
        S_EXCLAMATION,

        S_EQUAL,              //  ==
        S_NOT_EQUAL,          //  !=
        S_GREATER_OR_EQUALS,   //  >=
        S_LESS_OR_EQUALS      //  <=
    }

    public static class TokenCategories
    {
        public static readonly HashSet<TokenKind> ModifierTokens = [
            TokenKind.K_CONSTANT,
            TokenKind.K_STATIC,
            TokenKind.K_PUBLIC,
            TokenKind.K_PRIVATE,
            TokenKind.K_PROTECTED
        ];

        public static readonly HashSet<TokenKind> ExitTokens = [
            TokenKind.K_RETURN,
            TokenKind.K_BREAK,
            TokenKind.K_CONTINUE,
            TokenKind.K_EXIT
        ];

        public static readonly HashSet<TokenKind> TypeTokens =
        [
            TokenKind.K_STRING,
            TokenKind.K_INTEGER,
            TokenKind.K_FLOAT,
            TokenKind.K_DOUBLE,
            TokenKind.K_LONG,
            TokenKind.K_VOID,
            TokenKind.K_BOOLEAN,
            TokenKind.K_CLASS,
            TokenKind.K_OBJECT,
            TokenKind.K_CONSTRUCT,
            TokenKind.K_DYNAMIC,
        ];

        public static readonly HashSet<TokenKind> ArithmeticOperators = 
        [
            TokenKind.S_PLUS, TokenKind.S_MINUS,
            TokenKind.S_ASTERISK, TokenKind.S_SLASH,
            TokenKind.S_CARET,
        ];

        public static readonly HashSet<TokenKind> LogicalOperators =
        [
            TokenKind.S_AND, TokenKind.S_OR,
        ];

        public static readonly HashSet<TokenKind> LogicalAndOperators = [TokenKind.S_AND];
        public static readonly HashSet<TokenKind> LogicalOrOperators = [TokenKind.S_OR];

        public static readonly HashSet<TokenKind> DirectComparisonOperators = [TokenKind.S_EQUAL, TokenKind.S_NOT_EQUAL];
        public static readonly HashSet<TokenKind> NumericComparisonOperators = [TokenKind.S_LARROW, TokenKind.S_LESS_OR_EQUALS, TokenKind.S_RARROW, TokenKind.S_GREATER_OR_EQUALS];
        public static readonly HashSet<TokenKind> AdditiveOperators = [TokenKind.S_PLUS, TokenKind.S_MINUS];
        public static readonly HashSet<TokenKind> MultiplicativeOperators = [TokenKind.S_ASTERISK, TokenKind.S_SLASH];
        public static readonly HashSet<TokenKind> ExponentialOperators = [TokenKind.S_CARET];
        public static readonly HashSet<TokenKind> UnaryOperators = [TokenKind.S_EXCLAMATION, TokenKind.S_MINUS];
        public static readonly HashSet<TokenKind> AssignmentOperators = [TokenKind.S_ASSIGN, TokenKind.S_ASSIGN_ADD, TokenKind.S_ASSIGN_DIV, TokenKind.S_ASSIGN_EXP, TokenKind.S_ASSIGN_MOD, TokenKind.S_ASSIGN_MUL, TokenKind.S_ASSIGN_SUB];

        public static readonly HashSet<TokenKind> NumericSpecifiers = [TokenKind.S_FLOAT, TokenKind.S_DOUBLE, TokenKind.S_LONG, TokenKind.S_INTEGER];
    };

    public static class TokenLibrary
    {
        public static TokenKind AssignmentToArithmeticOperator(TokenKind kind)
        {
            return kind switch
            {
                TokenKind.S_ASSIGN_ADD => TokenKind.S_PLUS,
                TokenKind.S_ASSIGN_DIV => TokenKind.S_SLASH,
                TokenKind.S_ASSIGN_EXP => TokenKind.S_CARET,
                TokenKind.S_ASSIGN_MOD => TokenKind.S_PERCENT,
                TokenKind.S_ASSIGN_MUL => TokenKind.S_ASTERISK,
                TokenKind.S_ASSIGN_SUB => TokenKind.S_MINUS,
                _ => throw new NotImplementedException()
            };
        }
    }

    public class Token
    {
        public readonly TokenKind Kind;
        public readonly string Value;
        public readonly LocationInfo Location;

        public Token(TokenKind kind, string value, int start, int end, int line)
        {
            Kind = kind;
            Value = value;

            Location.Start = start;
            Location.End = end;
            Location.Line = line;
        }

        public Token(TokenKind kind, string value, int start, int line)
        {
            Kind = kind;
            Value = value;

            Location.Start = start;
            Location.End = start;
            Location.Line = line;
        }

        public bool IsModifier() => TokenCategories.ModifierTokens.Contains(Kind);
        public bool IsExitStatement() => TokenCategories.ExitTokens.Contains(Kind);
        public bool IsType() => TokenCategories.TypeTokens.Contains(Kind);
        public bool IsLogicalOperator() => TokenCategories.LogicalOperators.Contains(Kind);
        public bool IsLogicalAndOperator() => TokenCategories.LogicalAndOperators.Contains(Kind);
        public bool IsLogicalOrOperator() => TokenCategories.LogicalOrOperators.Contains(Kind);
        public bool IsDirectComparisonOperator() => TokenCategories.DirectComparisonOperators.Contains(Kind);
        public bool IsNumericComparisonOperator() => TokenCategories.NumericComparisonOperators.Contains(Kind);
        public bool IsAdditiveOperator() => TokenCategories.AdditiveOperators.Contains(Kind);
        public bool IsMultiplicativeOperator() => TokenCategories.MultiplicativeOperators.Contains(Kind);
        public bool IsExponentialOperator() => TokenCategories.ExponentialOperators.Contains(Kind);
        public bool IsUnaryOperator() => TokenCategories.UnaryOperators.Contains(Kind);
        public bool IsNumericSpecifier() => TokenCategories.NumericSpecifiers.Contains(Kind);
        public bool IsAssignmentOperator() => TokenCategories.AssignmentOperators.Contains(Kind);
        public bool IsArithmeticOperator() => TokenCategories.ArithmeticOperators.Contains(Kind);
        public string GetCodeDiagnostic(string sourceCode)
        {
            return sourceCode.Substring(Location.Start, Location.End - Location.Start + 1);
        }
    }

    public class TokenSymbolLookup
    {
        static readonly Dictionary<char, TokenKind> Single = new()
        {
            { '(', TokenKind.S_LPAREN },
            { ')', TokenKind.S_RPAREN },
            { '{', TokenKind.S_LBRACE },
            { '}', TokenKind.S_RBRACE },
            { '[', TokenKind.S_LBRACKET },
            { ']', TokenKind.S_RBRACKET },
            { '_', TokenKind.S_UNDERSCORE },

            { '+', TokenKind.S_PLUS },
            { '*', TokenKind.S_ASTERISK },
            { '-', TokenKind.S_MINUS },
            { '^', TokenKind.S_CARET },
            { '%', TokenKind.S_PERCENT },
            { '/', TokenKind.S_SLASH },
            { '?', TokenKind.S_QUESTION },

            { '&', TokenKind.S_AMPERSAND },
            { '|', TokenKind.S_PIPE },
            { '\\', TokenKind.S_BACKSLASH },

            { ',', TokenKind.S_COMMA },
            { '.', TokenKind.S_DOT },
            { ':', TokenKind.S_COLON },
            { ';', TokenKind.S_SEMICOLON },
            { '=', TokenKind.S_ASSIGN },

            { '<', TokenKind.S_LARROW },
            { '>', TokenKind.S_RARROW },

            { '!', TokenKind.S_EXCLAMATION },

            { 'I', TokenKind.S_INTEGER },
            { 'L', TokenKind.S_LONG },
            { 'F', TokenKind.S_FLOAT },
            { 'D', TokenKind.S_DOUBLE },
        };

        static readonly Dictionary<string, TokenKind> Double = new()
        {
            { "==", TokenKind.S_EQUAL },
            { "!=", TokenKind.S_NOT_EQUAL },
            { ">=", TokenKind.S_GREATER_OR_EQUALS },
            { "<=", TokenKind.S_LESS_OR_EQUALS },

            { "+=", TokenKind.S_ASSIGN_ADD },
            { "-=", TokenKind.S_ASSIGN_SUB },
            { "*=", TokenKind.S_ASSIGN_MUL },
            { "/=", TokenKind.S_ASSIGN_DIV },
            { "^=", TokenKind.S_ASSIGN_EXP },
            { "%=", TokenKind.S_ASSIGN_MOD },

            { "&&", TokenKind.S_AND },
            { "||", TokenKind.S_OR },
            { "^^", TokenKind.S_XOR },

            { "->", TokenKind.S_RARROW },
            { "<-", TokenKind.S_LARROW },
        };

        static readonly Dictionary<string, TokenKind> String = new()
        {
            { "if", TokenKind.K_IF },
            { "elseif", TokenKind.K_ELSEIF },
            { "else", TokenKind.K_ELSE },
            { "while", TokenKind.K_WHILE },
            { "for", TokenKind.K_FOR },
            { "post", TokenKind.K_POST },
            { "return", TokenKind.K_RETURN },
            { "break", TokenKind.K_BREAK },
            { "continue", TokenKind.K_CONTINUE },
            { "exit", TokenKind.K_EXIT },
            { "new", TokenKind.K_NEW },

            { "of", TokenKind.K_OF },

            { "true", TokenKind.L_BOOLEAN },
            { "false", TokenKind.L_BOOLEAN },
            { "null", TokenKind.L_NULL },

            { "class", TokenKind.K_CLASS },
            { "object", TokenKind.K_OBJECT },

            { "ensure", TokenKind.K_ENSURE },
            { "delete", TokenKind.K_DELETE },

            { "string", TokenKind.K_STRING },
            { "float", TokenKind.K_FLOAT },
            { "double", TokenKind.K_DOUBLE },
            { "bool", TokenKind.K_BOOLEAN },
            { "int", TokenKind.K_INTEGER },
            { "long", TokenKind.K_LONG },
            { "void", TokenKind.K_VOID },
            { "construct", TokenKind.K_CONSTRUCT },
            { "dynamic", TokenKind.K_DYNAMIC },

            { "const", TokenKind.K_CONSTANT },
            { "static", TokenKind.K_STATIC },
            { "public", TokenKind.K_PUBLIC },
            { "private", TokenKind.K_PRIVATE },
            { "protected", TokenKind.K_PROTECTED },
        };

        public static bool LookupChar(char now, out TokenKind charTokenKind)
        {
            return Single.TryGetValue(now, out charTokenKind);
        }

        public static bool LookupDouble(char char1, char char2, out TokenKind doubleTokenKind)
        {
            string key = string.Concat(char1, char2);

            return Double.TryGetValue(key, out doubleTokenKind);
        }

        public static bool LookupString(string str, out TokenKind stringTokenKind)
        {
            return String.TryGetValue(str, out stringTokenKind);
        }
    }
}