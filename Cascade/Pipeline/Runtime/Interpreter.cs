using Cascade.Pipeline.Runtime.Values;
using Cascade.Pipeline.Runtime.Tools;
using Cascade.Pipeline.Shared;
using Cascade.Pipeline.Frontend.Lexer;
using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Frontend.Parser.Tools;
using System.Text;
using System.Diagnostics;

namespace Cascade.Pipeline.Runtime
{
    public class Interpreter : PipelineAlgorithm
    {
        public RuntimeValue EvaluateAst(ProgramStatementNode node)
        {
            Domain globalDomain = new(this, null, DomainContext.PROGRAM);

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                FirstClassValue result = EvaluateProgramStatement(globalDomain, node);

                sw.Stop();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Actual program evaluation time: {sw.ElapsedTicks / 10000d} milliseconds");
                Console.ForegroundColor = ConsoleColor.Gray;

                return result;
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("RUNTIME ERROR(S)");
                Console.ForegroundColor = ConsoleColor.Gray;

                return new NullLiteralValue();
            }
        }

        public RuntimeValue Evaluate(Domain domain, AstNode node)
        {
            switch (node)
            {
                // Note: Programs are resolved initially.
                case VariableDeclarationStatementNode variableDeclarationStatement:
                {
                    EvaluateVariableDeclarationStatement(domain, variableDeclarationStatement);

                    return RuntimeValueList.NullLiteral;
                }
                case ExitStatementNode exitStatement:
                {
                    return EvaluateExitStatement(domain, exitStatement);
                }
                case IfStatementNode ifStatement:
                {
                    return EvaluateIfStatement(domain, ifStatement);
                }
                case DeleteStatementNode deleteStatement:
                {
                    return EvaluateDeleteStatement(domain, deleteStatement);
                }
                case WhileLoopStatementNode whileStatement:
                {
                    return EvaluateWhileLoopStatement(domain, whileStatement);
                }
                case PostWhileLoopStatementNode postWhileStatement:
                {
                    return EvaluatePostWhileLoopStatement(domain, postWhileStatement);
                }
                case ForLoopStatementNode forLoopStatement:
                {
                    return EvaluateForLoopStatement(domain, forLoopStatement);
                }

                case AccessMemberExpressionNode accessMemberExpression:
                {
                    return EvaluateAccessMemberExpression(domain, accessMemberExpression);
                }
                case IdentifierExpressionNode identifierExpression:
                {
                    return EvaluateIdentifierExpression(domain, identifierExpression);
                }

                case ClassExpressionNode classExpression:
                {
                    return EvaluateClassExpression(domain, classExpression);
                }
                case ObjectExpressionNode objectExpression:
                {
                    return EvaluateObjectExpression(domain, objectExpression);
                }

                case CallExpressionNode callExpression:
                {
                    return EvaluateCallExpression(domain, callExpression);
                }

                case FunctionExpressionNode functionExpression:
                {
                    return EvaluateFunctionExpression(domain, functionExpression);
                }
                case ConstructExpressionNode constructExpression:
                {
                    return EvaluateConstructExpression(domain, constructExpression);
                }
                case AssignmentExpressionNode assignmentExpression:
                {
                    return EvaluateAssignmentExpression(domain, assignmentExpression);
                }

                case TernaryExpressionNode ternaryExpression:
                {
                    return EvaluateTernaryExpression(domain, ternaryExpression);
                }
                case BinaryExpressionNode binaryExpression:
                {
                    return EvaluateBinaryExpression(domain, binaryExpression);
                }
                case UnaryExpressionNode unaryExpression:
                {
                    return EvaluateUnaryExpression(domain, unaryExpression);
                }

                case IntegerLiteralNode integerLiteral:
                {
                    return EvaluateIntegerLiteral(integerLiteral);
                }
                case LongLiteralNode longLiteral:
                {
                    return EvaluateLongLiteral(longLiteral);
                }
                case FloatLiteralNode floatLiteral:
                {
                    return EvaluateFloatLiteral(floatLiteral);
                }
                case DoubleLiteralNode doubleLiteral:
                {
                    return EvaluateDoubleLiteral(doubleLiteral);
                }
                case BooleanLiteralNode booleanLiteral:
                {
                    return EvaluateBooleanLiteral(booleanLiteral);
                }
                case StringLiteralNode stringLiteral:
                {
                    return EvaluateStringLiteral(stringLiteral);
                }
                case NullLiteralNode:
                {
                    return RuntimeValueList.NullLiteral;
                }
            }

            TerminateDiagnostic($"This node has not been implemented yet: {node.Kind}", node.Location); throw new Exception();
        }

        public FirstClassValue EvaluateFirstClass(Domain domain, AstNode node)
        {
            RuntimeValue evaluation = Evaluate(domain, node);

            if (evaluation is not FirstClassValue)
            {
                TerminateDiagnostic("A first class value was expected.", node.Location); throw new Exception(); // !CALM
            }

            return (FirstClassValue)evaluation;
        }

        public RuntimeValue EvaluateExpectedKind(Domain domain, AstNode node, RuntimeValueKind kind)
        {
            RuntimeValue evaluation = Evaluate(domain, node);

            if (evaluation.Kind != kind)
            {
                TerminateDiagnostic($"A {kind} value was expected. Got {node.Kind}", node.Location); throw new Exception(); // !CALM
            }

            return evaluation;
        }

        public RuntimeValue EvaluateExpectedKind(Domain domain, AstNode node, HashSet<RuntimeValueKind> kinds)
        {
            RuntimeValue evaluation = Evaluate(domain, node);

            if (!kinds.Contains(evaluation.Kind))
            {
                StringBuilder buffer = new();

                foreach (RuntimeValueKind kind in kinds)
                {
                    buffer.Append(kind.ToString() + " ");
                }

                TerminateDiagnostic($"{buffer}values was expected. Got {node.Kind}", node.Location); throw new Exception(); // !CALM
            }

            return evaluation;
        }


        /* 
            =================
            UTILITY FUNCTIONS 
            =================
        */


        public enum ArithmeticBinaryResolveMode
        {
            None,
            Concat,
            Math,
        }

        // This function will throw an exception if anything is invalid.
        public ArithmeticBinaryResolveMode GetArithmeticBinaryResolveMode(LocationInfo location, FirstClassValue left, FirstClassValue right)
        {
            RuntimeValueKind leftKind = left.Kind;
            RuntimeValueKind rightKind = right.Kind;

            bool leftIsNumber = left.IsNumber();
            bool rightIsNumber = right.IsNumber();

            if (leftKind == RuntimeValueKind.L_STRING || rightKind == RuntimeValueKind.L_STRING)
            {
                return ArithmeticBinaryResolveMode.Concat;
            }

            else if (leftIsNumber || rightIsNumber)
            {
                if (!leftIsNumber || !rightIsNumber)
                {
                    TerminateDiagnostic($"Arithmetic can not be performed on {leftKind} and {rightKind}.", location); throw new Exception(); // !CALM
                };

                return ArithmeticBinaryResolveMode.Math;
            }

            TerminateDiagnostic($"Failed to perform a binary expression on {leftKind} and {rightKind}.", location); throw new Exception(); // !CALM
        }

        // Confirm arguments by making sure they are in line with the parameters of the function to call.
        public void VerifyAndLoadFunctionArguments(Domain domain, LocationInfo callLocation, List<ParameterExpression> parameters, List<FirstClassValue> arguments)
        {
            int pointer = 0;

            while (pointer < parameters.Count)
            {
                // Retrieve parameter data.
                ParameterExpression parameter = parameters[pointer];
                TypeExpression variableType = parameter.Type;

                // Retrieve parameter value data through the potential existence of a matching argument.

                // Basically, just see if a given argument can be assigned to a parameter, or just try to use the parameter's default value.
                FirstClassValue variableValue = pointer < arguments.Count && arguments[pointer].Kind != RuntimeValueKind.L_NULL
                    ? arguments[pointer]
                    : parameter.DefaultValue;

                if (!TypeComparator.TypesMatch(variableType, variableValue.Type))
                {
                    TerminateDiagnostic($"Expected parameter type of {variableType}. Got {variableValue.Type}. (Param {pointer + 1})", callLocation);
                }

                domain.DeclareVariable(this, callLocation, [], variableType, parameter.Identifier, variableValue, true);

                pointer++;
            }
        }

        public FirstClassValue CallFunctionExpression(Domain parentDomain, LocationInfo callLocation, FunctionExpressionValue function, List<FirstClassValue> arguments, Dictionary<string, MemberExpressionValue>? defaultVariables = null)
        {
            if (parentDomain.Context == DomainContext.FUNCTION)
            {
                parentDomain = parentDomain.Parent!;
            }

            Domain localDomain = new(this, parentDomain, DomainContext.FUNCTION);

            VerifyAndLoadFunctionArguments(localDomain, callLocation, function.Parameters, arguments);

            // Add in custom variables.
            if (defaultVariables != null)
            {
                foreach (KeyValuePair<string, MemberExpressionValue> pair in defaultVariables)
                {
                    localDomain.DeclareVariable(this, callLocation, pair.Value.Modifiers, pair.Value.Type, pair.Key, pair.Value.Value, true);
                }
            }

            FirstClassValue functionResult = EvaluateStatementList(localDomain, function.Body);

            if (!TypeComparator.TypesMatch(function.ReturnType, functionResult.Type))
            {
                TerminateDiagnostic($"Function expected to return {function.ReturnType}, instead returned {functionResult.Type}", callLocation);
            }

            return functionResult;
        }

        public List<FirstClassValue> EvaluateArguments(Domain domain, List<ExpressionNode> arguments)
        {
            return arguments.Select(arg => EvaluateFirstClass(domain, arg)).ToList();
        }

        public FirstClassValue EvaluateStatementList(Domain domain, List<StatementNode> statements)
        {
            int pointer = 0;

            while (pointer < statements.Count)
            {
                StatementNode statement = statements[pointer];
                FirstClassValue result = EvaluateFirstClass(domain, statement);

                // Check exit statements.
                if (statement.Kind == AstNodeKind.S_EXIT)
                {
                    ExitStatementNode exitStatement = (ExitStatementNode)statement;

                    if (!domain.HasContext(exitStatement.Context))
                    {
                        TerminateDiagnostic($"Attempted to execute an 'exit {exitStatement.Context}' statement outside of a {exitStatement.Context}.", exitStatement.Location); throw new Exception(); // !CALM
                    }

                    domain.Exit(exitStatement.Context, result);

                    return result;
                }

                if (!domain.IsActive)
                {
                    return domain.ExitContent ?? RuntimeValueList.NullLiteral;
                }

                pointer++;
            }

            // Return nothing if there is not exit content.
            return RuntimeValueList.NullLiteral;
        }

        public (MemberContainerValue Object, string MemberName) GetMemberExpressionInfo(Domain domain, AccessMemberExpressionNode node)
        {
            // Evaluate the object.
            RuntimeValue obj = Evaluate(domain, node.Object);

            if (obj is not MemberContainerValue)
            {
                TerminateDiagnostic("The given object does not contain members.", node.Location); throw new Exception(); // !CALM
            }

            MemberContainerValue verifiedObject = (MemberContainerValue)obj;

            // Evaluate the method finding the property.
            if (node.Computed)
            {
                RuntimeValue keyRuntimeValue = Evaluate(domain, node.Member);

                if (!keyRuntimeValue.IsMemberKey())
                {
                    TerminateDiagnostic($"A {node.Member.Kind} can not be used as a member accessor.", node.Location);
                }

                string memberKey = RuntimeValuerResolver.ResolveToString(keyRuntimeValue);

                return (verifiedObject, memberKey);
            }

            IdentifierExpressionNode propertyNameIdentifier = (IdentifierExpressionNode)node.Member;

            return (verifiedObject, propertyNameIdentifier.Value);
        }

        public Dictionary<string, MemberExpressionValue> EvaluateMemberList(Domain domain, List<MemberExpressionNode> members)
        {
            int pointer = 0;

            Dictionary<string, MemberExpressionValue> evaluatedMembers = [];

            while (pointer < members.Count)
            {
                MemberExpressionNode selected = members[pointer];

                string key = selected.Computed ? EvaluateFirstClass(domain, selected.Key).ResolveString() : ((IdentifierExpressionNode)selected.Key).Value;

                if (evaluatedMembers.ContainsKey(key))
                {
                    TerminateDiagnostic("Members can not be overwritten during member container initialization.", selected.Location); throw new Exception(); // !CALM
                }

                TypeExpression evaluatedType = EvaluateTypeExpression(domain, selected.Type.TypeExpression);
                FirstClassValue evaluatedValue = EvaluateFirstClass(domain, selected.Value);

                MemberExpressionValue newMemberValue = new(this, selected.Location, selected.Modifiers, evaluatedType, evaluatedValue);

                evaluatedMembers.Add(key, newMemberValue);

                pointer++;
            }

            return evaluatedMembers;
        }


        /* 
            =================
                STATEMENTS 
            =================
        */


        public NullLiteralValue EvaluateForLoopStatement(Domain domain, ForLoopStatementNode forLoopStatement)
        {
            Domain localDomain = new(this, domain, DomainContext.LOOP);

            MemberExpressionValue memberExpression = EvaluateVariableDeclarationStatement(localDomain, forLoopStatement.Variable);

            throw new NotImplementedException();
        }

        public NullLiteralValue EvaluateWhileLoopStatement(Domain domain, WhileLoopStatementNode whileLoopStatement)
        {
            Domain localDomain = new(this, domain, DomainContext.LOOP);

            while (true)
            {
                if (!((BooleanLiteralValue)EvaluateExpectedKind(domain, whileLoopStatement.Condition, RuntimeValueKind.L_BOOLEAN)).Value)
                {
                    break;
                }

                EvaluateStatementList(localDomain, whileLoopStatement.Body);
            }

            return RuntimeValueList.NullLiteral;
        }

        public NullLiteralValue EvaluatePostWhileLoopStatement(Domain domain, PostWhileLoopStatementNode postWhileLoopStatement)
        {
            Domain localDomain = new(this, domain, DomainContext.LOOP);

            while (true)
            {
                EvaluateStatementList(localDomain, postWhileLoopStatement.Body);

                if (!((BooleanLiteralValue)EvaluateExpectedKind(domain, postWhileLoopStatement.Condition, RuntimeValueKind.L_BOOLEAN)).Value)
                {
                    break;
                }
            }

            return RuntimeValueList.NullLiteral;
        }

        public NullLiteralValue EvaluateDeleteStatement(Domain domain, DeleteStatementNode deleteStatement)
        {
            if (deleteStatement.AccessPoint.Kind == AstNodeKind.E_ACCESS_MEMBER)
            {
                var memberExpressionInfo = GetMemberExpressionInfo(domain, (AccessMemberExpressionNode)deleteStatement.AccessPoint);
                string key = memberExpressionInfo.MemberName;

                memberExpressionInfo.Object.DeleteMember(this, deleteStatement.Location, key);
            }
            else if (deleteStatement.AccessPoint.Kind == AstNodeKind.IDENTIFIER)
            {
                string key = ((IdentifierExpressionNode)deleteStatement.AccessPoint).Value;

                domain.DeleteVariable(this, deleteStatement.Location, key);
            }

            return RuntimeValueList.NullLiteral;
        }

        public NullLiteralValue EvaluateIfStatement(Domain domain, IfStatementNode ifStatement)
        {
            int pointer = 0;

            while (pointer < ifStatement.IfClauses.Count)
            {
                IfExpressionNode clause = ifStatement.IfClauses[pointer];
                BooleanLiteralValue conditionEvaluation = (BooleanLiteralValue)EvaluateExpectedKind(domain, clause.Condition, RuntimeValueKind.L_BOOLEAN);

                if (conditionEvaluation.Value)
                {
                    Domain localIfClauseDomain = new(this, domain, DomainContext.IF_STATEMENT_CLAUSE);
                    EvaluateStatementList(localIfClauseDomain, clause.Body);

                    return RuntimeValueList.NullLiteral;
                }

                pointer++;
            }

            if (ifStatement.ElseClause != null)
            {
                Domain localElseClauseDomain = new(this, domain, DomainContext.IF_STATEMENT_CLAUSE);
                EvaluateStatementList(localElseClauseDomain, ifStatement.ElseClause.Body);

                return RuntimeValueList.NullLiteral;
            }

            return RuntimeValueList.NullLiteral;
        }

        public FirstClassValue EvaluateExitStatement(Domain domain, ExitStatementNode exitStatement)
        {
            return EvaluateFirstClass(domain, exitStatement.Content);
        }

        public FirstClassValue EvaluateProgramStatement(Domain domain, ProgramStatementNode program)
        {
            return EvaluateStatementList(domain, program.Body);
        }

        public MemberExpressionValue EvaluateVariableDeclarationStatement(Domain domain, VariableDeclarationStatementNode variableDeclaration)
        {
            // Get the value of the variable.
            FirstClassValue value = EvaluateFirstClass(domain, variableDeclaration.Value);

            TypeExpression evaluatedTypeExpression = EvaluateTypeExpression(domain, variableDeclaration.Type.TypeExpression);

            if (variableDeclaration.AccessPoint.Kind == AstNodeKind.E_ACCESS_MEMBER)
            {
                var memberExpressionInfo = GetMemberExpressionInfo(domain, (AccessMemberExpressionNode)variableDeclaration.AccessPoint);
                string key = memberExpressionInfo.MemberName;

                memberExpressionInfo.Object.DeclareMember(this, variableDeclaration.Location, variableDeclaration.Modifiers, evaluatedTypeExpression, key, value);

                return memberExpressionInfo.Object.Members[key];
            }
            else if (variableDeclaration.AccessPoint.Kind == AstNodeKind.IDENTIFIER)
            {
                string key = ((IdentifierExpressionNode)variableDeclaration.AccessPoint).Value;

                domain.DeclareVariable(this, variableDeclaration.Location, variableDeclaration.Modifiers, evaluatedTypeExpression, key, value);

                return domain.Members[key];
            }
            else if (variableDeclaration.AccessPoint.IsLiteral())
            {
                string key = EvaluateFirstClass(domain, variableDeclaration.AccessPoint).ResolveString();
                
                domain.DeclareVariable(this, variableDeclaration.Location, variableDeclaration.Modifiers, evaluatedTypeExpression, key, value);

                return domain.Members[key];
            }

            TerminateDiagnostic("Could not properly declare the variable. Please access a member, use an identifier, or use a bracketed literal to name your variable.", variableDeclaration.Location); throw new Exception(); // !CALM
        }


        /* 
            =================
               EXPRESSIONS 
            =================
        */


        public List<ParameterExpression> EvaluateParameterExpressionList(Domain domain, List<ParameterExpressionNode> parameterExpressionList)
        {
            int pointer = 0;

            List<ParameterExpression> list = [];

            while (pointer < parameterExpressionList.Count)
            {
                list.Add(EvaluateParameterExpression(domain, parameterExpressionList[pointer]));

                pointer++;
            }

            return list;
        }

        public ParameterExpression EvaluateParameterExpression(Domain domain, ParameterExpressionNode parameterExpression)
        {
            TypeExpression evaluatedTypeExpression = EvaluateTypeExpression(domain, parameterExpression.Type.TypeExpression);
            FirstClassValue evaluatedDefaultValue = EvaluateFirstClass(domain, parameterExpression.DefaultValue);

            return new ParameterExpression(evaluatedTypeExpression, parameterExpression.Identifier.Value, evaluatedDefaultValue);
        }

        public FirstClassValue EvaluateCallExpression(Domain domain, CallExpressionNode callExpression)
        {
            List<FirstClassValue> arguments = EvaluateArguments(domain, callExpression.Arguments);
            FirstClassValue function = (FirstClassValue)EvaluateExpectedKind(domain, callExpression.Function, RuntimeValueCategories.Functions);

            if (function.Kind == RuntimeValueKind.E_CS_FUNCTION)
            {
                return ((CsFunctionExpressionValue)function).Call(this, callExpression.Location, domain, arguments);
            }

            return CallFunctionExpression(domain, callExpression.Location, (FunctionExpressionValue)function, arguments);
        }

        public FunctionExpressionValue EvaluateFunctionExpression(Domain domain, FunctionExpressionNode functionExpression)
        {
            TypeExpression returnType = EvaluateTypeExpression(domain, functionExpression.ReturnType.TypeExpression);

            return new FunctionExpressionValue(returnType, functionExpression.Body, EvaluateParameterExpressionList(domain, functionExpression.Parameters));
        }

        public ObjectExpressionValue EvaluateConstructExpression(Domain domain, ConstructExpressionNode constructExpression)
        {
            ClassExpressionValue parentClass = (ClassExpressionValue)EvaluateExpectedKind(domain, constructExpression.Class, RuntimeValueKind.E_CLASS);
            List<FirstClassValue> arguments = EvaluateArguments(domain, constructExpression.Arguments);

            string evaluatedConstructorName = constructExpression.Computed ? EvaluateFirstClass(domain, constructExpression.Constructor).ResolveString() : ((IdentifierExpressionNode)constructExpression.Constructor).Value;

            if (!parentClass.GetMember(evaluatedConstructorName, out MemberExpressionValue? constructor))
            {
                TerminateDiagnostic($"The given class does not have the constructor: {evaluatedConstructorName}", constructExpression.Location); throw new Exception(); // !CALM
            }
            else if (constructor!.Value.Kind != RuntimeValueKind.E_FUNCTION)
            {
                TerminateDiagnostic($"The referenced object, {evaluatedConstructorName}, is not a constructor.", constructExpression.Location); throw new Exception(); // !CALM
            }

            // Create the new object.
            ObjectExpressionValue newObject = new(parentClass);

            // The created object's type meta references to the initial class that was created.
            newObject.Type.SetMeta([parentClass]);

            Dictionary<string, MemberExpressionValue> defaultVariables = new() {{
                "self",
                    new MemberExpressionValue(
                        this,
                        constructExpression.Location,
                        [MemberModifier.CONSTANT],
                        newObject.Type,

                    newObject)
            }};

            CallFunctionExpression(domain, constructExpression.Location, (FunctionExpressionValue)constructor.Value, arguments, defaultVariables);

            return newObject;
        }

        public ObjectExpressionValue EvaluateObjectExpression(Domain domain, ObjectExpressionNode objectExpression)
        {
            return new ObjectExpressionValue(EvaluateMemberList(domain, objectExpression.Members));
        }

        public ClassExpressionValue EvaluateClassExpression(Domain domain, ClassExpressionNode classExpression)
        {
            ClassExpressionValue? superClass = classExpression.Superclass != null ? (ClassExpressionValue)EvaluateFirstClass(domain, classExpression.Superclass) : null;

            return new ClassExpressionValue(superClass, EvaluateMemberList(domain, classExpression.Members));
        }

        public TypeExpression EvaluateTypeExpression(Domain domain, UnevaluatedTypeExpression typeExpression)
        {
            List<RuntimeValue> newMeta = [];

            if (typeExpression.HasMeta())
            {
                int pointer = 0;

                while (pointer < typeExpression.Meta!.Count)
                {
                    ExpressionNode metaNode = typeExpression.Meta[pointer++];

                    newMeta.Add(Evaluate(domain, metaNode));
                }
            }

            return new(typeExpression.Mutable, typeExpression.Standard, newMeta, typeExpression.Nullable);
        }

        public FirstClassValue EvaluateTernaryExpression(Domain domain, TernaryExpressionNode ternaryExpression)
        {
            BooleanLiteralValue condition = (BooleanLiteralValue)EvaluateExpectedKind(domain, ternaryExpression.Condition, RuntimeValueKind.L_BOOLEAN);

            if (condition.Value)
            {
                return EvaluateFirstClass(domain, ternaryExpression.TrueBranch);
            }

            return EvaluateFirstClass(domain, ternaryExpression.FalseBranch);
        }

        public FirstClassValue EvaluateBinaryExpression(Domain domain, BinaryExpressionNode binaryExpression)
        {
            FirstClassValue left = EvaluateFirstClass(domain, binaryExpression.Left);
            FirstClassValue right = EvaluateFirstClass(domain, binaryExpression.Right);
            TokenKind binaryOperator = binaryExpression.Operator;

            if (TokenCategories.ArithmeticOperators.Contains(binaryOperator))
            {
                return EvaluateArithmeticBinaryExpression(binaryExpression.Location, binaryOperator, left, right);
            }
            else if (TokenCategories.LogicalOperators.Contains(binaryOperator))
            {
                return EvaluateLogicalBinaryExpression(binaryExpression.Location, binaryOperator, left, right);
            }

            else if (TokenCategories.DirectComparativeOperators.Contains(binaryOperator))
            {
                return EvaluateDirectComparativeBinaryExpression(binaryOperator, left, right);
            }
            else if (TokenCategories.NumericComparativeOperators.Contains(binaryOperator))
            {
                return EvaluateNumericComparativeBinaryExpression(binaryOperator, left, right);
            }

            TerminateDiagnostic($"This operator has not been implemented yet: {binaryOperator}", binaryExpression.Location); throw new Exception(); // !CALM
        }

        public FirstClassValue EvaluateNumericComparativeBinaryExpression(TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            RuntimeValueKind arithmeticResult = NumericResolver.DetermineProperArithmeticMethod(left.Kind, right.Kind);

            bool result = arithmeticResult switch
            {
                RuntimeValueKind.L_INTEGER => NumericResolver.SolveIntegerComparison(binaryOperator, left.ResolveInteger(), right.ResolveInteger()),
                RuntimeValueKind.L_LONG => NumericResolver.SolveLongComparison(binaryOperator, left.ResolveLong(), right.ResolveLong()),
                RuntimeValueKind.L_FLOAT => NumericResolver.SolveFloatComparison(binaryOperator, left.ResolveFloat(), right.ResolveFloat()),
                RuntimeValueKind.L_DOUBLE => NumericResolver.SolveDoubleComparison(binaryOperator, left.ResolveDouble(), right.ResolveDouble()),
                _ => throw new NotImplementedException() // !CALM
            };

            return result ? RuntimeValueList.Bool_True : RuntimeValueList.Bool_False;
        }

        public FirstClassValue EvaluateDirectComparativeBinaryExpression(TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            bool result = binaryOperator switch
            {
                TokenKind.S_EQUAL_TO => left.Kind == right.Kind && left.ResolveString() == right.ResolveString(),
                TokenKind.S_NOT_EQUAL_TO => left.Kind != right.Kind || left.ResolveString() != right.ResolveString(),
                _ => throw new NotImplementedException() // !CALM
            };

            return result ? RuntimeValueList.Bool_True : RuntimeValueList.Bool_False;
        }

        public FirstClassValue EvaluateLogicalBinaryExpression(LocationInfo binaryExpressionLocation, TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            if (left.Kind != RuntimeValueKind.L_BOOLEAN || right.Kind != RuntimeValueKind.L_BOOLEAN)
            {
                TerminateDiagnostic($"Can not perform the operation '{binaryOperator}' on {left.Kind} and {right.Kind}", binaryExpressionLocation); throw new Exception(); // !CALM
            }

            bool result = binaryOperator switch
            {
                TokenKind.S_AND => ((BooleanLiteralValue)left).Value && ((BooleanLiteralValue)right).Value,
                TokenKind.S_OR => ((BooleanLiteralValue)left).Value || ((BooleanLiteralValue)right).Value,
                _ => throw new NotImplementedException() // !CALM
            };

            return result ? RuntimeValueList.Bool_True : RuntimeValueList.Bool_False;
        }

        public FirstClassValue EvaluateArithmeticBinaryExpression(LocationInfo binaryExpressionLocation, TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            ArithmeticBinaryResolveMode resolveMode = GetArithmeticBinaryResolveMode(binaryExpressionLocation, left, right);

            return resolveMode switch
            {
                ArithmeticBinaryResolveMode.Concat => EvaluateConcatenationArithmeticBinaryExpression(binaryExpressionLocation, binaryOperator, left, right),
                ArithmeticBinaryResolveMode.Math => EvaluateMathArithmeticBinaryExpression(binaryOperator, left, right),
                _ => throw new NotImplementedException() // !CALM
            };
        }

        public FirstClassValue EvaluateConcatenationArithmeticBinaryExpression(LocationInfo binaryExpressionLocation, TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            if (binaryOperator != TokenKind.S_PLUS)
            {
                TerminateDiagnostic($"Can not perform the operation '{binaryOperator}' on {left.Kind} and {right.Kind}", binaryExpressionLocation); throw new Exception(); // !CALM
            }

            return new StringLiteralValue(string.Concat(left.ResolveString(), right.ResolveString()));
        }

        public FirstClassValue EvaluateMathArithmeticBinaryExpression(TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            RuntimeValueKind arithmeticResult = NumericResolver.DetermineProperArithmeticMethod(left.Kind, right.Kind);

            return arithmeticResult switch
            {
                RuntimeValueKind.L_INTEGER => new IntegerLiteralValue(NumericResolver.SolveIntegerArithmetic(binaryOperator, left.ResolveInteger(), right.ResolveInteger())),
                RuntimeValueKind.L_LONG => new LongLiteralValue(NumericResolver.SolveLongArithmetic(binaryOperator, left.ResolveLong(), right.ResolveLong())),
                RuntimeValueKind.L_FLOAT => new FloatLiteralValue(NumericResolver.SolveFloatArithmetic(binaryOperator, left.ResolveFloat(), right.ResolveFloat())),
                RuntimeValueKind.L_DOUBLE => new DoubleLiteralValue(NumericResolver.SolveDoubleArithmetic(binaryOperator, left.ResolveDouble(), right.ResolveDouble())),
                _ => throw new NotImplementedException() // !CALM
            };
        }

        public FirstClassValue EvaluateUnaryExpression(Domain domain, UnaryExpressionNode unaryExpression)
        {
            FirstClassValue value = EvaluateFirstClass(domain, unaryExpression.Value);

            return unaryExpression.Operator switch
            {
                TokenKind.S_EXCLAMATION => EvaluateNotUnaryExpression(unaryExpression.Location, value),
                _ => throw new NotImplementedException() // !CALM
            };
        }

        public FirstClassValue EvaluateNotUnaryExpression(LocationInfo unaryExpressionLocation, FirstClassValue value)
        {
            if (value.Kind != RuntimeValueKind.L_BOOLEAN)
            {
                TerminateDiagnostic($"The 'NOT' operator can not be applied to {value.Kind} literals.", unaryExpressionLocation);
            }

            return ((BooleanLiteralValue)value).Value ? RuntimeValueList.Bool_False : RuntimeValueList.Bool_True;
        }

        public FirstClassValue EvaluateAssignmentExpression(Domain domain, AssignmentExpressionNode node)
        {
            FirstClassValue value = EvaluateFirstClass(domain, node.Value);

            bool operatorIsIrregular = node.Operator != TokenKind.S_ASSIGN;

            if (node.AccessPoint.Kind == AstNodeKind.E_ACCESS_MEMBER)
            {
                var memberExpressionInfo = GetMemberExpressionInfo(domain, (AccessMemberExpressionNode)node.AccessPoint);

                if (operatorIsIrregular)
                {
                    TokenKind arithmeticOperator = TokenLibrary.AssignmentToArithmeticOperator(node.Operator);

                    value = EvaluateArithmeticBinaryExpression(node.Location, arithmeticOperator, memberExpressionInfo.Object, value);
                }

                return memberExpressionInfo.Object.AssignMember(this, node.Location, memberExpressionInfo.MemberName, value);
            }

            string key = ((IdentifierExpressionNode)node.AccessPoint).Value;

            if (operatorIsIrregular)
            {
                MemberExpressionValue? existingValue = domain.LookUp(this, node.AccessPoint.Location, key);

                if (existingValue == null)
                {
                    TerminateDiagnostic("Attempt to perform an irregular assignment operation on a value that does not exist.", node.Location);
                }

                TokenKind arithmeticOperator = TokenLibrary.AssignmentToArithmeticOperator(node.Operator);
                value = EvaluateArithmeticBinaryExpression(node.Location, arithmeticOperator, existingValue!.Value, value);
            }

            return domain.AssignVariable(this, node.Location, key, value);
        }

        public FirstClassValue EvaluateAccessMemberExpression(Domain domain, AccessMemberExpressionNode node)
        {
            var memberExpressionInfo = GetMemberExpressionInfo(domain, node);

            memberExpressionInfo.Object.Members.TryGetValue(memberExpressionInfo.MemberName, out MemberExpressionValue? memberValue);

            if (memberValue == null)
            {
                TerminateDiagnostic($"{memberExpressionInfo.MemberName} does not exist.", node.Location); throw new Exception(); // !CALM
            }

            return memberValue.Value;
        }

        public RuntimeValue EvaluateIdentifierExpression(Domain domain, IdentifierExpressionNode node)
        {
            // The object the identifier is referencing.
            MemberExpressionValue? reference = domain.LookUp(this, node.Location, node.Value);

            if (reference == null)
            {
                TerminateDiagnostic($"Variable '{node.Value}' does not exist.", node.Location); throw new Exception(); //!CALM
            }

            return reference.Value;
        }


        /* 
            =================
                LITERALS 
            =================
        */


        public static IntegerLiteralValue EvaluateIntegerLiteral(IntegerLiteralNode expression) => new(expression.Value);
        public static LongLiteralValue EvaluateLongLiteral(LongLiteralNode expression) => new(expression.Value);
        public static FloatLiteralValue EvaluateFloatLiteral(FloatLiteralNode expression) => new(expression.Value);
        public static DoubleLiteralValue EvaluateDoubleLiteral(DoubleLiteralNode expression) => new(expression.Value);
        public static StringLiteralValue EvaluateStringLiteral(StringLiteralNode expression) => new(expression.Value);
        public static BooleanLiteralValue EvaluateBooleanLiteral(BooleanLiteralNode expression) => expression.Value ? RuntimeValueList.Bool_True : RuntimeValueList.Bool_False;
    }
}