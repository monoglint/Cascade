using Cascade2.Pipeline.Runtime.Values;
using Cascade2.Pipeline.Runtime.Tools;
using Cascade2.Pipeline.Shared;
using Cascade2.Pipeline.Frontend.Lexer;
using Cascade2.Pipeline.Frontend.Parser.AST;
using Cascade2.Pipeline.Frontend.Parser.Tools;
using System.Text;
using System.Diagnostics;

namespace Cascade2.Pipeline.Runtime
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
                    return EvaluateVariableDeclarationStatement(domain, variableDeclarationStatement);
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
                case BinaryExpressionNode binaryExpression:
                {
                    return EvaluateBinaryExpression(domain, binaryExpression);
                }

                case IntegerLiteralNode integerLiteral:
                {
                    return EvaluateIntegerLiteral(integerLiteral);
                }
                case FloatLiteralNode floatLiteral:
                {
                    return EvaluateFloatLiteral(floatLiteral);
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


        public enum BinaryResolveMode
        {
            None,
            Concat,
            Arithmetic,
        }

        // This function will throw an exception if anything is invalid.
        public BinaryResolveMode GetBinaryResolveMode(LocationInfo location, FirstClassValue left, FirstClassValue right)
        {
            RuntimeValueKind leftKind = left.Kind;
            RuntimeValueKind rightKind = right.Kind;

            bool leftIsNumber = left.IsNumber();
            bool rightIsNumber = right.IsNumber();

            if (leftKind == RuntimeValueKind.L_STRING || rightKind == RuntimeValueKind.L_STRING)
            {
                return BinaryResolveMode.Concat;
            }

            else if (leftIsNumber || rightIsNumber)
            {
                if (!leftIsNumber || !rightIsNumber)
                {
                    TerminateDiagnostic($"Arithmetic can not be performed on {leftKind} and {rightKind}.", location); throw new Exception(); // !CALM
                };

                return BinaryResolveMode.Arithmetic;
            }

            TerminateDiagnostic($"Failed to perform a binary expression on {leftKind} and {rightKind}.", location); throw new Exception(); // !CALM
        }

        // Confirm arguments by making sure they are in line with the parameters of the function to call.
        public void VerifyAndLoadFunctionArguments(Domain parentDomain, Domain domain, LocationInfo callLocation, List<ParameterExpression> parameters, List<FirstClassValue> arguments)
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

                domain.DeclareVariable(this, callLocation, [], variableType, parameter.Identifier, variableValue);

                pointer++;
            }
        }

        public FirstClassValue CallFunctionExpression(Domain parentDomain, LocationInfo callLocation, FunctionExpressionValue function, List<FirstClassValue> arguments, Dictionary<string, MemberExpressionValue>? defaultVariables = null)
        {
            Domain localDomain = new(this, parentDomain, DomainContext.FUNCTION);

            VerifyAndLoadFunctionArguments(parentDomain, localDomain, callLocation, function.Parameters, arguments);

            // Add in custom variables.
            if (defaultVariables != null)
            {
                foreach (KeyValuePair<string, MemberExpressionValue> pair in defaultVariables)
                {
                    localDomain.DeclareVariable(this, callLocation, pair.Value.Modifiers, pair.Value.Type, pair.Key, pair.Value.Value);
                }
            }

            FirstClassValue functionExport = EvaluateStatementList(localDomain, function.Body);

            if (!TypeComparator.TypesMatch(function.ReturnType, functionExport.Type))
            {
                TerminateDiagnostic($"Function expected to return {function.ReturnType}, instead returned {functionExport.Type}", callLocation);
            }

            return functionExport;
        }

        public List<FirstClassValue> EvaluateArguments(Domain domain, List<ExpressionNode> arguments)
        {
            return arguments.Select(arg => EvaluateFirstClass(domain, arg)).ToList();
        }

        public FirstClassValue EvaluateStatementList(Domain domain, List<StatementNode> statements)
        {
            foreach (StatementNode statement in statements)
            {
                FirstClassValue result = EvaluateFirstClass(domain, statement);

                // Check exit statements.
                if (statement.Kind == AstNodeKind.S_EXIT)
                {
                    ExitStatementNode exitStatement = (ExitStatementNode)statement;

                    if (!domain.HasContext(exitStatement.Context))
                    {
                        TerminateDiagnostic($"Attempted to execute an 'exit {exitStatement.Context}' statement outside of a {exitStatement.Context}.", exitStatement.Location);

                        return RuntimeValueList.NullLiteral;
                    }

                    domain.Exit(exitStatement.Context);

                    return result;
                }
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


        public NullLiteralValue EvaluateWhileLoopStatement(Domain domain, WhileLoopStatementNode whileLoopStatement)
        {
            throw new NotImplementedException();
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
            bool clauseEvaluated = false;
            int pointer = 0;

            while (pointer < ifStatement.IfClauses.Count)
            {
                IfExpressionNode clause = ifStatement.IfClauses[pointer];
                BooleanLiteralValue conditionEvaluation = (BooleanLiteralValue)EvaluateExpectedKind(domain, clause.Condition, RuntimeValueKind.L_BOOLEAN);

                if (conditionEvaluation.Value)
                {
                    Domain localIfClauseDomain = new(this, domain, DomainContext.IF_STATEMENT_CLAUSE);
                    EvaluateStatementList(localIfClauseDomain, clause.Body);

                    clauseEvaluated = true;

                    break;
                }

                pointer++;
            }

            if (!clauseEvaluated && ifStatement.ElseClause != null)
            {
                Domain localElseClauseDomain = new(this, domain, DomainContext.IF_STATEMENT_CLAUSE);
                EvaluateStatementList(localElseClauseDomain, ifStatement.ElseClause.Body);
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

        public NullLiteralValue EvaluateVariableDeclarationStatement(Domain domain, VariableDeclarationStatementNode variableDeclaration)
        {
            // Get the value of the variable.
            FirstClassValue value = EvaluateFirstClass(domain, variableDeclaration.Value);

            TypeExpression evaluatedTypeExpression = EvaluateTypeExpression(domain, variableDeclaration.Type.TypeExpression);

            if (variableDeclaration.AccessPoint.Kind == AstNodeKind.E_ACCESS_MEMBER)
            {
                var memberExpressionInfo = GetMemberExpressionInfo(domain, (AccessMemberExpressionNode)variableDeclaration.AccessPoint);
                string key = memberExpressionInfo.MemberName;

                memberExpressionInfo.Object.DeclareMember(this, variableDeclaration.Location, variableDeclaration.Modifiers, evaluatedTypeExpression, key, (FirstClassValue)value);
            }
            else if (variableDeclaration.AccessPoint.Kind == AstNodeKind.IDENTIFIER)
            {
                string key = ((IdentifierExpressionNode)variableDeclaration.AccessPoint).Value;

                domain.DeclareVariable(this, variableDeclaration.Location, variableDeclaration.Modifiers, evaluatedTypeExpression, key, (FirstClassValue)value);
            }

            return RuntimeValueList.NullLiteral;
        }

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


        /* 
            =================
               EXPRESSIONS 
            =================
        */


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
            List<FirstClassValue> newMeta = [];

            if (typeExpression.HasMeta())
            {
                int pointer = 0;

                while (pointer < typeExpression.Meta!.Count)
                {
                    ExpressionNode metaNode = typeExpression.Meta[pointer++];

                    newMeta.Add((FirstClassValue)Evaluate(domain, metaNode));
                }
            }

            return new(typeExpression.Mutable, typeExpression.Standard, newMeta, typeExpression.Nullable);
        }

        public FirstClassValue EvaluateBinaryExpression(Domain domain, BinaryExpressionNode binaryExpression)
        {
            FirstClassValue left = EvaluateFirstClass(domain, binaryExpression.Left);
            FirstClassValue right = EvaluateFirstClass(domain, binaryExpression.Right);
            TokenKind binaryOperator = binaryExpression.Operator;

            if (TokenCategories.ArithmeticOperators.Contains(binaryOperator))
            {
                return EvaluateArithmeticBinaryExpression(domain, binaryExpression.Location, binaryOperator, left, right);
            }

            TerminateDiagnostic($"This operator has not been implemented yet: {binaryOperator}", binaryExpression.Location); throw new Exception(); // !CALM
        }

        public FirstClassValue EvaluateArithmeticBinaryExpression(Domain domain, LocationInfo binaryExpressionLocation, TokenKind binaryOperator, FirstClassValue left, FirstClassValue right)
        {
            BinaryResolveMode resolveMode = GetBinaryResolveMode(binaryExpressionLocation, left, right);

            // Concatenation.
            if (binaryOperator == TokenKind.S_PLUS)
            {
                switch (resolveMode)
                {
                    case BinaryResolveMode.Concat:
                    {
                        return new StringLiteralValue(string.Concat(left.ResolveString(), right.ResolveString()));
                    }
                    case BinaryResolveMode.Arithmetic:
                    {
                        return new DoubleLiteralValue(left.ResolveDouble() + right.ResolveDouble());
                    }
                }
            }
            else if (binaryOperator == TokenKind.S_MINUS || binaryOperator == TokenKind.S_ASTERISK || binaryOperator == TokenKind.S_SLASH || binaryOperator == TokenKind.S_CARET)
            {
                if (resolveMode != BinaryResolveMode.Arithmetic)
                {
                    TerminateDiagnostic($"Attempted to perform arithmetic on {left.Kind}, {right.Kind}", binaryExpressionLocation); throw new Exception(); // !CALM
                }

                return new DoubleLiteralValue(0d);
            }

            throw new Exception(); // !CALM
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

                    value = EvaluateArithmeticBinaryExpression(domain, node.Location, arithmeticOperator, memberExpressionInfo.Object, value);
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
                value = EvaluateArithmeticBinaryExpression(domain, node.Location, arithmeticOperator, existingValue!.Value, value);
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
        public static DoubleLiteralValue EvaluateFloatLiteral(DoubleLiteralNode expression) => new(expression.Value);
        public static StringLiteralValue EvaluateStringLiteral(StringLiteralNode expression) => new(expression.Value);
        public static BooleanLiteralValue EvaluateBooleanLiteral(BooleanLiteralNode expression) => expression.Value ? RuntimeValueList.Bool_True : RuntimeValueList.Bool_False;
    }
}