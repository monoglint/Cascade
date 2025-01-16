using Cascade2.Pipeline.Frontend.Lexer;
using Cascade2.Pipeline.Frontend.Parser.AST;
using Cascade2.Pipeline.Frontend.Parser.Tools;
using Cascade2.Pipeline.Runtime.Tools;
using Cascade2.Pipeline.Shared;

namespace Cascade2.Pipeline.Frontend.Parser
{
    public class Parser(List<Token> tokenList, string code) : PipelineAlgorithm
    {
        private readonly Token[] _tokens = [.. tokenList];
        private int _position = 0;

        // Used for diagnostic purposes
        private readonly string _code = code;

        public ProgramStatementNode ParseToEnd()
        {
            try
            {
                return ParseProgramStatement();
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("PARSER ERROR(S)");
                Console.ForegroundColor = ConsoleColor.Gray;
                return new ProgramStatementNode(LocationInfoList.Empty, []);
            }
        }


        /* 
            =================
          PARSER UTIL FUNCTIONS 
            =================

    * Either lambda functions or functions that do not belong directly to the parser pipeline. 
    
        */


        private ExpressionNode ParseBinaryExpression(Func<ExpressionNode> parseLowerPrecedence, HashSet<TokenKind> operatorKinds)
        {
            ExpressionNode left = parseLowerPrecedence();

            while (operatorKinds.Contains(Now.Kind))
            {
                Token op = Now;
                _position++;

                ExpressionNode right = parseLowerPrecedence();

                left = new BinaryExpressionNode(left, right, op.Kind);
            }

            return left;
        }

        // Note: This function may return null if no modifiers are specified for the statement.
        private List<MemberModifier> ParseGetModifiers()
        {
            if (!Now.IsModifier())
            {
                return [];
            }

            List<MemberModifier> modifiers = [];

            while (Now.IsModifier())
            {
                switch (Now.Kind)
                {
                    case TokenKind.K_PUBLIC:
                    {
                        modifiers.Add(MemberModifier.PUBLIC);
                        break;
                    }
                    case TokenKind.K_PRIVATE:
                    {
                        modifiers.Add(MemberModifier.PRIVATE);
                        break;
                    }
                    case TokenKind.K_PROTECTED:
                    {
                        modifiers.Add(MemberModifier.PROTECTED);
                        break;
                    }
                    case TokenKind.K_STATIC:
                    {
                        modifiers.Add(MemberModifier.STATIC);
                        break;
                    }
                    case TokenKind.K_CONSTANT:
                    {
                        modifiers.Add(MemberModifier.CONSTANT);
                        break;
                    }
                }

                _position++;
            }

            return modifiers;
        }

        private TypeExpressionNode ParseGetType()
        {
            // Get the standard for the type. We will use this token and pass it to the type constructor to transform it for us.
            Token standardTypeToken = Now;

            if (!standardTypeToken.IsType())
            {
                TerminateDiagnostic($"Expected a type identifier, got {Now.GetCodeDiagnostic(_code)}", Now.Location);
            }

            _position++;

            // If there is no meta type, then just return.
            if (Now.Kind != TokenKind.S_COLON)
            {
                return new TypeExpressionNode(standardTypeToken, null, CheckAndAdvanceIfNullable());
            }

            // Classes can not have meta types.
            if (standardTypeToken.Kind == TokenKind.K_CLASS)
            {
                TerminateDiagnostic("Classes can not have meta types.", Now.Location);
            }

            _position++;

            // If the meta is not a list, then expect just one identifier in the meta.
            if (Now.Kind != TokenKind.S_LBRACKET)
            {
                return new TypeExpressionNode(standardTypeToken, [ParseAccessMemberExpression()], CheckAndAdvanceIfNullable());
            }

            List<ExpressionNode> meta = [];

            // Otherwise, expect a list of meta types.
            do
            {
                _position++;

                meta.Add(ParseAccessMemberExpression());
            } while (Now.Kind == TokenKind.S_COMMA);

            Expect(TokenKind.S_RBRACKET);

            return new TypeExpressionNode(standardTypeToken, meta, CheckAndAdvanceIfNullable());
        }

        private bool CheckAndAdvanceIfNullable()
        {
            if (Now.Kind == TokenKind.S_QUESTION)
            {
                _position++;

                return true;
            }

            return false;
        }

        // NewType is used to make the variable type a function and pass the function return data over to the expression.
        public enum BlockDeclaration { BLOCK_FUNCTIONS, BLOCK_ASSIGNMENTS, BLOCK_CLASSES, BLOCK_NONE }
        private (ExpressionNode Value, TypeExpressionNode NewType) ParseGetValueToSet(TypeExpressionNode existingVariableType, BlockDeclaration blockDeclaration = BlockDeclaration.BLOCK_NONE)
        {
            Token firstToken = Now;

            // Declaring any other expression, literally
            if (firstToken.Kind == TokenKind.S_ASSIGN)
            {
                if (blockDeclaration == BlockDeclaration.BLOCK_ASSIGNMENTS)
                {
                    TerminateDiagnostic("A function or class was expected to be declared.", firstToken.Location);
                }

                Expect(TokenKind.S_ASSIGN);

                return (ParseExpression(), existingVariableType);
            }

            // Declaring functions
            else if (firstToken.Kind == TokenKind.S_LPAREN)
            {
                if (blockDeclaration == BlockDeclaration.BLOCK_FUNCTIONS)
                {
                    TerminateDiagnostic("A function or construct can not be declared.", firstToken.Location);
                }

                // Check if the user is creating a construct!
                if (existingVariableType.TypeExpression.Standard == StandardValueType.CONSTRUCT)
                {
                    if (existingVariableType.TypeExpression.HasMeta())
                    {
                        TerminateDiagnostic("Constructs can not have a meta return type.", firstToken.Location);
                    }

                    // !OPTIMIZE - The new type expression node may be able to be reused later.
                    // Note: The meta references "VOID" since the constructor only references the function within it. It doesn't actually return anything.
                    return (ParseFunctionExpression(new TypeExpressionNode(Now.Location, StandardValueType.VOID)), new TypeExpressionNode(Now.Location, StandardValueType.CONSTRUCT));
                }

                // !OPTIMIZE Remember to optimize this by accessing a dictionary of default type expression nodes.
                return (ParseFunctionExpression(existingVariableType), new TypeExpressionNode(Now.Location, StandardValueType.FUNCTION));
            }

            // Declaring classes.
            else if (firstToken.Kind == TokenKind.S_LBRACE)
            {
                if (blockDeclaration == BlockDeclaration.BLOCK_CLASSES)
                {
                    TerminateDiagnostic($"A class can not be declared.", firstToken.Location);
                }

                if (existingVariableType.TypeExpression.Standard != StandardValueType.CLASS)
                {
                    TerminateDiagnostic($"Expected standard type: 'CLASS', got '{existingVariableType.TypeExpression.Standard}'", firstToken.Location);
                }

                if (existingVariableType.TypeExpression.HasMeta())
                {
                    TerminateDiagnostic($"Classes can not have a meta return type.", firstToken.Location);
                }

                List<MemberExpressionNode> members = ParseMemberList();
                ExpressionNode? superClass = null;

                // Check for inheritance.
                if (Now.Kind == TokenKind.K_OF)
                {
                    _position++;

                    superClass = ParseAccessMemberExpression(); // Expect an identifier or an access member expression.

                    if (superClass.Kind != AstNodeKind.IDENTIFIER && superClass.Kind != AstNodeKind.E_ACCESS_MEMBER)
                    {
                        TerminateDiagnostic("Expected an identifier or access member expression.", Now.Location);
                    }
                }

                return (new ClassExpressionNode(new LocationInfo(firstToken.Location, Now.Location), members, superClass), new TypeExpressionNode(LocationInfoList.Empty, StandardValueType.CLASS));
            }

            // Otherwise, assume nothing is being assigned. Set the value to null by default.
            return (new NullLiteralNode(Now.Location), existingVariableType);
        }

        private List<ParameterExpressionNode> ParseParameterExpressionList()
        {
            Expect(TokenKind.S_LPAREN);

            List<ParameterExpressionNode> parameters = [];

            while (Now.Kind != TokenKind.S_RPAREN)
            {
                parameters.Add(ParseParameterExpression());

                if (Now.Kind == TokenKind.S_COMMA)
                {
                    _position++;
                }
            }

            // Note: The right parenthesis is eaten by the loop on return.

            // Eat past the right parenthesis.
            _position++;

            return parameters;
        }

        // Gets one singular parameter (type identifier) in a list of parameters for a function.
        private ParameterExpressionNode ParseParameterExpression()
        {
            TypeExpressionNode parameterType = ParseGetType();

            ExpressionNode parameterName = ParsePrimaryExpression();

            if (parameterName.Kind != AstNodeKind.IDENTIFIER)
            {
                TerminateDiagnostic("Expected an identifier when adding a parameter.", parameterName.Location);
            }

            // If the user defines a proper definition to a parameter, that is the parameter's default value.
            ExpressionNode defaultValue = ParseGetValueToSet(parameterType).Value;

            return new ParameterExpressionNode(parameterType, (IdentifierExpressionNode)parameterName, defaultValue);
        }

        private List<StatementNode> ParseBody()
        {
            Expect(TokenKind.S_LBRACE);

            List<StatementNode> statements = [];

            while (!EOF() && Now.Kind != TokenKind.S_RBRACE)
            {
                statements.Add(ParseStatement());
            }

            Expect(TokenKind.S_RBRACE);

            return statements;
        }

        // Like ParseBody, but does not look out for braces and parses to the end of the program.
        private List<StatementNode> ParseStatementListToEnd()
        {
            List<StatementNode> statements = [];

            while (!EOF())
            {
                statements.Add(ParseStatement());
            }

            return statements;
        }

        public (ExpressionNode MemberName, bool Computed) ParseGetMemberName()
        {
            bool computed = Now.Kind == TokenKind.S_LBRACKET;

            if (computed)
            {
                // Eat past the bracket.
                _position++;
            }

            ExpressionNode memberName = ParsePrimaryExpression(); // Can be an identifier or literal.

            if (computed)
            {
                Expect(TokenKind.S_RBRACKET);
            }

            if (memberName.IsComputableKey() && !computed)
            {
                TerminateDiagnostic($"An expression with the type {memberName.Kind} must be surrounded by brackets.", memberName.Location); throw new Exception(); // !CALM
            }

            return (memberName, computed);
        }

        private ExpressionNode ParseGetAccessPoint()
        {
            ExpressionNode accessPoint = ParseAccessMemberExpression();

            if (accessPoint.Kind != AstNodeKind.E_ACCESS_MEMBER && accessPoint.Kind != AstNodeKind.IDENTIFIER)
            {
                TerminateDiagnostic($"Expected an E_ACCESS_MEMBER or IDENTIFIER, got {accessPoint.Kind}", accessPoint.Location);
            }

            return accessPoint;
        }

        // Create a list of members, full with keys, modifiers, types, and values.
        private List<MemberExpressionNode> ParseMemberList()
        {
            Expect(TokenKind.S_LBRACE);

            List<MemberExpressionNode> members = [];

            while (Now.Kind != TokenKind.S_RBRACE)
            {
                LocationInfo startLocation = Now.Location;

                List<MemberModifier> modifiers = ParseGetModifiers();
                TypeExpressionNode type = ParseGetType();

                var (MemberName, Computed) = ParseGetMemberName();
                var (Value, NewType) = ParseGetValueToSet(type);

                MemberExpressionNode memberExpressionNode = new(new LocationInfo(startLocation, _position), modifiers, NewType, MemberName, Computed, Value);

                members.Add(memberExpressionNode);

                TokenKind kind = Now.Kind;

                if (kind == TokenKind.S_RBRACE)
                {
                    break;
                }

                // If the next token is a comma, eat past it.
                // Note: Programmers should be allowed to not use commas, it may make their code less readable, though.
                else if (kind == TokenKind.S_COMMA || kind == TokenKind.S_SEMICOLON)
                {
                    _position++;
                }
            }

            // Eat past the right brace.
            _position++;

            return members;
        }


        /* 
            =================
                STATEMENTS
            =================
        */


        private StatementNode ParseStatement()
        {
            // Note, be able to add an incomplete statement error later.
            Token now = Now;

            if (now.Kind == TokenKind.K_IF)
            {
                return ParseIfStatement();
            }
            else if (now.Kind == TokenKind.K_WHILE)
            {
                return ParseWhileLoopStatement();
            }
            else if (now.Kind == TokenKind.K_POST)
            {
                return ParsePostWhileLoopStatement();
            }
            else if (now.Kind == TokenKind.K_FOR)
            {
                return ParseForLoopStatement();
            }

            else if (now.Kind == TokenKind.K_ENSURE)
            {
                return ParseEnsureStatement();
            }
            else if (now.Kind == TokenKind.K_DELETE)
            {
                return ParseDeleteStatement();
            }

            else if (now.IsExitStatement())
            {
                return ParseExitStatement();
            }
            else if (now.IsType() || now.IsModifier())
            {
                return ParseVariableDeclarationStatement();
            }

            return ParseExpression();
        }

        private ProgramStatementNode ParseProgramStatement()
        {
            LocationInfo startLocation = Now.Location;

            List<StatementNode> statements = ParseStatementListToEnd();

            return new(new LocationInfo(startLocation, _position), statements);
        }

        private EnsureStatementNode ParseEnsureStatement()
        {
            LocationInfo startLocation = Now.Location;

            // Eat past the ensure
            _position++;

            ExpressionNode accessPoint = ParseGetAccessPoint();

            return new(new LocationInfo(startLocation, _position), accessPoint);
        }

        private DeleteStatementNode ParseDeleteStatement()
        {
            LocationInfo startLocation = Now.Location;

            // Eat past the delete
            _position++;

            ExpressionNode accessPoint = ParseGetAccessPoint();

            return new(new LocationInfo(startLocation, _position), accessPoint);
        }

        private WhileLoopStatementNode ParseWhileLoopStatement()
        {
            LocationInfo startLocation = Now.Location;

            // Eat past the while
            _position++;

            ExpressionNode conditionExpression = ParseExpression();
            List<StatementNode> body = ParseBody();

            return new WhileLoopStatementNode(new LocationInfo(startLocation, _position), conditionExpression, body);
        }

        private PostWhileLoopStatementNode ParsePostWhileLoopStatement()
        {
            LocationInfo startLocation = Now.Location;

            // Eat past the post
            _position++;

            List<StatementNode> body = ParseBody();

            Expect(TokenKind.K_WHILE);

            ExpressionNode conditionExpression = ParseExpression();

            return new PostWhileLoopStatementNode(new LocationInfo(startLocation, _position), conditionExpression, body);
        }

        private ForLoopStatementNode ParseForLoopStatement()
        {
            LocationInfo startLocation = Now.Location;

            // Eat past the for.
            _position++;

            VariableDeclarationStatementNode variableDeclarationStatement = ParseVariableDeclarationStatement();

            Expect(TokenKind.S_RARROW);

            ExpressionNode secondExpression = ParseExpression();

            Expect(TokenKind.S_COMMA);

            ExpressionNode incrementationExpression = ParseExpression();
            List<StatementNode> body = ParseBody();

            return new ForLoopStatementNode(new LocationInfo(startLocation, _position), variableDeclarationStatement, secondExpression, incrementationExpression, body);
        }

        private VariableDeclarationStatementNode ParseVariableDeclarationStatement()
        {
            LocationInfo startLocation = Now.Location;

            List<MemberModifier> modifiers = ParseGetModifiers();
            TypeExpressionNode type = ParseGetType();

            ExpressionNode accessPoint = ParseGetAccessPoint(); // Should be an identifier or an access-member expression.

            var (Value, NewType) = ParseGetValueToSet(type);

            return new(new LocationInfo(startLocation, _position), modifiers, NewType, accessPoint, Value);
        }

        private IfStatementNode ParseIfStatement()
        {
            LocationInfo statementStartLocation = Now.Location;

            List<IfExpressionNode> ifClauses = [];

            do
            {
                LocationInfo ifExpressionStartLocation = Now.Location;

                // Eat past the if (or elseif).
                _position++;

                ExpressionNode conditionExpression = ParseExpression();
                List<StatementNode> body = ParseBody();

                ifClauses.Add(new(new LocationInfo(ifExpressionStartLocation, _position), conditionExpression, body));

            } while (Now.Kind == TokenKind.K_ELSEIF);

            // If we don't find an else statement.
            if (Now.Kind != TokenKind.K_ELSE)
            {
                return new IfStatementNode(new LocationInfo(statementStartLocation, _position), ifClauses, null);
            }

            LocationInfo elseExpressionStartLocation = Now.Location;

            // Eat past the "else".
            _position++;

            List<StatementNode> elseBody = ParseBody();

            ElseExpressionNode elseExpression = new(new LocationInfo(elseExpressionStartLocation, _position), elseBody);

            return new IfStatementNode(new LocationInfo(statementStartLocation, _position), ifClauses, elseExpression);
        }

        private ExitStatementNode ParseExitStatement()
        {
            Token startToken = Now;

            // Get the exit context.
            DomainContext exitContext = startToken.Kind switch
            {
                TokenKind.K_RETURN => DomainContext.FUNCTION,
                TokenKind.K_EXIT => DomainContext.PROGRAM,
                TokenKind.K_BREAK => DomainContext.LOOP,

                _ => DomainContext.NONE,
            };

            _position++;

            // Get the exit content.
            ExpressionNode exitContent = ParseExpression();

            return new ExitStatementNode(new LocationInfo(startToken.Location, _position), exitContext, exitContent);
        }


        /* 
            =================
               EXPRESSIONS
            =================
        */


        private ExpressionNode ParseExpression() => ParseAssignmentExpression();

        private ExpressionNode ParseAssignmentExpression()
        {
            ExpressionNode left = ParseConditionalTernaryExpression();

            Token opr = Now;

            if (opr.IsAssignmentOperator())
            {
                _position++;

                // Allow chaining
                ExpressionNode Value = ParseAssignmentExpression();

                return new AssignmentExpressionNode(Now.Location, left, opr.Kind, Value);
            }

            return left;
        }


        private FunctionExpressionNode ParseFunctionExpression(TypeExpressionNode returnType)
        {
            LocationInfo startLocation = Now.Location;

            List<ParameterExpressionNode> parameters = ParseParameterExpressionList();

            List<StatementNode> body = ParseBody();

            return new FunctionExpressionNode(new LocationInfo(startLocation, _position), returnType, parameters, body);
        }

        // Condition ? TrueBranch : FalseBranch
        private ExpressionNode ParseConditionalTernaryExpression()
        {
            ExpressionNode ternaryCondition = ParseLogicalOrBinaryExpression();

            if (Now.Kind != TokenKind.S_QUESTION)
            {
                return ternaryCondition;
            }

            // Eat past the question mark.
            _position++;

            ExpressionNode trueBranch = ParseExpression();

            Expect(TokenKind.S_COLON);

            ExpressionNode falseBranch = ParseExpression();

            return new TernaryExpressionNode(ternaryCondition, trueBranch, falseBranch);
        }


        private ExpressionNode ParseLogicalOrBinaryExpression() => ParseBinaryExpression(ParseLogicalAndBinaryExpression, TokenCategories.LogicalOrOperators);
        private ExpressionNode ParseLogicalAndBinaryExpression() => ParseBinaryExpression(ParseDirectComparisonBinaryExpression, TokenCategories.LogicalAndOperators);
        private ExpressionNode ParseDirectComparisonBinaryExpression() => ParseBinaryExpression(ParseNumericComparisonBinaryExpression, TokenCategories.DirectComparisonOperators);
        private ExpressionNode ParseNumericComparisonBinaryExpression() => ParseBinaryExpression(ParseAdditiveBinaryExpression, TokenCategories.NumericComparisonOperators);
        private ExpressionNode ParseAdditiveBinaryExpression() => ParseBinaryExpression(ParseMultiplicativeBinaryExpression, TokenCategories.AdditiveOperators);
        private ExpressionNode ParseMultiplicativeBinaryExpression() => ParseBinaryExpression(ParseExponentialBinaryExpression, TokenCategories.MultiplicativeOperators);
        private ExpressionNode ParseExponentialBinaryExpression() => ParseBinaryExpression(ParseUnaryExpression, TokenCategories.ExponentialOperators);
        private ExpressionNode ParseUnaryExpression()
        {
            Token startToken = Now;

            if (!startToken.IsUnaryOperator())
            {
                return ParseConstructExpression();
            }

            _position++;

            return new UnaryExpressionNode(new LocationInfo(startToken.Location, _position), ParseConstructExpression(), startToken.Kind);
        }

        private ExpressionNode ParseConstructExpression()
        {
            LocationInfo startLocation = Now.Location;

            if (Now.Kind != TokenKind.K_NEW)
            {
                return ParseCallMemberExpression();
            }

            // Eat past the new.
            _position++;

            ExpressionNode classReference = ParseCallMemberExpression();

            // Expect a comma to separate the class and the constructor.
            Expect(TokenKind.S_COMMA);

            var (MemberName, Computed) = ParseGetMemberName();
            List<ExpressionNode> arguments = ParseArguments();

            return new ConstructExpressionNode(new LocationInfo(startLocation, _position), classReference, MemberName, Computed, arguments);

        }

        private ExpressionNode ParseCallMemberExpression()
        {
            ExpressionNode member = ParseAccessMemberExpression();

            while (Now.Kind == TokenKind.S_LPAREN)
            {
                return ParseCallExpression(member);
            }

            return member;
        }

        private ExpressionNode ParseCallExpression(ExpressionNode caller)
        {
            LocationInfo startLocation = Now.Location;
            List<ExpressionNode> expressionNodes = ParseArguments();

            ExpressionNode callExpression = new CallExpressionNode(new LocationInfo(startLocation, _position), caller, expressionNodes);

            if (Now.Kind == TokenKind.S_LPAREN)
            {
                callExpression = ParseCallExpression(callExpression);
            }

            return callExpression;
        }

        private List<ExpressionNode> ParseArguments()
        {
            Expect(TokenKind.S_LPAREN);

            List<ExpressionNode> arguments = Now.Kind == TokenKind.S_RPAREN ? [] : ParseArgumentsList();

            Expect(TokenKind.S_RPAREN);

            return arguments;
        }

        private List<ExpressionNode> ParseArgumentsList()
        {
            List<ExpressionNode> arguments = [];

            do
            {
                arguments.Add(ParseExpression());

                if (!EOF() && Now.Kind == TokenKind.S_COMMA)
                {
                    _position++;
                }
            } while (Now.Kind != TokenKind.S_RPAREN);

            return arguments;
        }

        private ExpressionNode ParseAccessMemberExpression()
        {
            ExpressionNode topLevelExpression = ParseObjectExpression();

            while (Now.Kind == TokenKind.S_DOT || Now.Kind == TokenKind.S_LBRACKET)
            {
                LocationInfo startLocation = Now.Location;

                bool computed = Now.Kind == TokenKind.S_LBRACKET;
                ExpressionNode member;

                _position++;

                if (computed)
                {
                    member = ParseExpression();

                    Expect(TokenKind.S_RBRACKET);
                }
                else
                {
                    Token identifier = Expect(TokenKind.IDENTIFIER);

                    member = new IdentifierExpressionNode(identifier.Location, identifier.Value);
                }

                topLevelExpression = new AccessMemberExpressionNode(new LocationInfo(startLocation, _position), topLevelExpression, member, computed);
            }

            return topLevelExpression;
        }

        // Create a new object not constructed from a class.
        private ExpressionNode ParseObjectExpression()
        {
            if (Now.Kind != TokenKind.S_LBRACE)
            {
                return ParsePrimaryExpression();
            }

            LocationInfo startLocation = Now.Location;
            List<MemberExpressionNode> members = ParseMemberList();

            return new ObjectExpressionNode(new LocationInfo(startLocation, _position), members);
        }

        private ExpressionNode ParsePrimaryExpression()
        {
            Token now = Now;

            switch (now.Kind)
            {
                case TokenKind.IDENTIFIER:
                {
                    _position++;

                    return new IdentifierExpressionNode(now.Location, now.Value);
                }
                case TokenKind.L_NUMBER:
                {
                    _position++;

                    Token numericSpecifierToken = Now;

                    if (numericSpecifierToken.IsNumericSpecifier())
                    {
                        _position++;

                        switch (numericSpecifierToken.Kind)
                        {
                            case TokenKind.S_FLOAT:
                            {
                                return new FloatLiteralNode(now.Location, float.TryParse(now.Value, out float resultF) ? resultF : float.NaN);
                            }
                            case TokenKind.S_DOUBLE:
                            {
                                return new DoubleLiteralNode(now.Location, double.TryParse(now.Value, out double resultD) ? resultD : double.NaN);
                            }
                            case TokenKind.S_INTEGER:
                            {
                                return new IntegerLiteralNode(now.Location, int.TryParse(now.Value, out int resultI) ? resultI : 0);
                            }
                            case TokenKind.S_LONG:
                            {
                                return new LongLiteralNode(now.Location, long.TryParse(now.Value, out long resultL) ? resultL : 0);
                            }
                        }
                    }

                    return new FloatLiteralNode(now.Location, float.TryParse(now.Value, out float result) ? result : float.NaN);
                }
                case TokenKind.L_STRING:
                {
                    _position++;
                    return new StringLiteralNode(now.Location, now.Value[1..^1]);
                }
                case TokenKind.L_BOOLEAN:
                {
                    _position++;
                    return new BooleanLiteralNode(now.Location, now.Value == "true");
                }
                case TokenKind.L_NULL:
                {
                    _position++;
                    return new NullLiteralNode(now.Location);
                }
                case TokenKind.S_LPAREN:
                {
                    _position++;

                    ExpressionNode expression = ParseExpression();

                    Expect(TokenKind.S_RPAREN);

                    return expression;
                }

                // Nope, not a primary expression.
                default:
                {
                    TerminateDiagnostic($"Unexpected token: {now.GetCodeDiagnostic(_code)}", Now.Location);
                    _position++;

                    return new NullLiteralNode(now.Location);
                }
            }
        }


        /* 
            =================
            POINTER FUNCTIONS 
            =================
        */


        private bool EOF()
        {
            return _position >= _tokens.Length - 1;
        }

        private Token Expect(TokenKind kind)
        {
            Token now = Now;

            if (now.Kind != kind)
            {
                TerminateDiagnostic($"Expected token {kind}, got {now.Kind}.", Now.Location);
            }

            _position++;

            return now;
        }

        private Token Peek()
        {
            if (_position >= _tokens.Length)
            {
                return _tokens[^1];
            }

            return _tokens[_position];
        }

        private Token Now => Peek();
    }
}