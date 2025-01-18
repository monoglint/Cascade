using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Frontend.Lexer
{
    public static class Symbols
    {
        public const char _newLine = '\n';
        public const char _underscore = '_';
        public const char _point = '.';
        public const char _quote = '"';
        public const char _apostrophe = '\'';
        public const char _null = '\0';
    }

    public class Lexer(string code) : PipelineAlgorithm
    {
        private readonly string _code = code;
        private int _lastSelection = 0;
        private int _position = 0;

        private int _line = 0;

        public List<Token> ParseToEnd()
        {
            try
            {
                List<Token> tokenList = [];

                while (!EOF())
                {
                    char now = Now;

                    if (char.IsWhiteSpace(now))
                    {
                        if (now == Symbols._newLine)
                        {
                            _line++;
                        }

                        _position++;
                    }

                    // Parse string literals.
                    else if (now == Symbols._quote)
                    {
                        tokenList.Add(ParseString());
                    }

                    // Parse number literals.
                    else if (now == Symbols._point && char.IsNumber(Next) || char.IsNumber(now))
                    {
                        tokenList.Add(ParseNumber());
                    }

                    // Search up multi character tokens.
                    else if (_position + 1 != _code.Length && TokenSymbolLookup.LookupDouble(now, _code[_position + 1], out TokenKind doubleTokenKind))
                    {
                        tokenList.Add(new Token(doubleTokenKind, string.Empty, _position, _line));

                        _position += 2;
                    }

                    // Search up single character tokens.
                    else if (TokenSymbolLookup.LookupChar(now, out TokenKind charTokenKind))
                    {
                        tokenList.Add(new Token(charTokenKind, string.Empty, _position, _line));

                        _position++;
                    }

                    // Identifiers can only start with underscores or letters.
                    else if (now == Symbols._underscore || char.IsLetterOrDigit(now))
                    {
                        tokenList.Add(ParseKeyword());
                    }

                    else
                    {
                        AddDiagnostic($"Unexpected character found: {now}", new LocationInfo(_position, _position, _line));

                        _position++;
                    }
                }

                tokenList.Add(new Token(TokenKind.EOF, string.Empty, _position - 1, _position - 1, _line));

                return tokenList;
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("LEXER ERROR(S)");
                Console.ForegroundColor = ConsoleColor.Gray;
                return [];
            }
            
        }


        /* 
            =================
             LEXER FUNCTIONS 
            =================
        */


        Token ParseString() // NOTE: Shortcut created by GitHub Autopilot. Thanks!
        {
            Select();

            _position++;

            while (!EOF() && Now != Symbols._quote)
            {
                _position++;
            }

            if (EOF())
            {
                TerminateDiagnostic("Unterminated string literal.", new LocationInfo(Now));
            }

            _position++;

            return MakeToken(TokenKind.L_STRING);
        }

        Token ParseKeyword()
        {
            Select();

            while (!EOF() && (char.IsLetterOrDigit(Now) || Now == Symbols._underscore))
            {
                _position++;
            }

            // Check for any existing keywords.
            if (TokenSymbolLookup.LookupString(GetValue(), out TokenKind stringTokenKind))
            {
                return MakeToken(stringTokenKind);
            }

            // Otherwise, its a plain old identifier!
            return MakeToken(TokenKind.IDENTIFIER);
        }

        Token ParseNumber()
        {
            Select();

            bool decimalUsed = false;

            while (!EOF())
            {
                if (Now == Symbols._point)
                {
                    if (decimalUsed)
                    {
                        throw new Exception("Invalid number format.");
                    }

                    decimalUsed = true;
                }
                else if (!char.IsNumber(Now))
                {
                    break;
                }

                _position++;
            }

            return MakeToken(TokenKind.L_NUMBER);
        }


        /* 
            =================
            POINTER FUNCTIONS 
            =================
        */


        // Create a selection point.
        private void Select()
        {
            _lastSelection = _position;
        }

        // Return a string between the previous selection point and the current _position.
        private string GetValue()
        {
            return _code[_lastSelection.._position];
        }

        // Returns whether or not we're at the EOF.
        private bool EOF()
        {
            return _position >= _code.Length;
        }

        // Make a token based on the last selection point and the current _position.
        private Token MakeToken(TokenKind kind)
        {
            string tokenValue = kind == TokenKind.IDENTIFIER || kind == TokenKind.L_BOOLEAN || kind == TokenKind.L_NUMBER || kind == TokenKind.L_STRING ? GetValue() : string.Empty;

            // Use (position - 1) because we are assuming that the _position++ has been ran before the condition has been checked one last time.
            return new Token(kind, tokenValue, _lastSelection, _position - 1, _line);
        }

        private char Peek(int offset)
        {
            int pos = _position + offset;

            if (pos >= _code.Length)
            {
                return _code[^1];
            }

            return _code[pos];
        }

        private char Peek()
        {
            if (_position >= _code.Length)
            {
                return _code[^1];
            }

            return _code[_position];
        }

        private char Next => Peek(1);
        private char Now => Peek();
    }
}
