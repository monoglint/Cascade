using Cascade2.Pipeline.Runtime.Tools;
using Cascade2.Pipeline.Shared;
using Cascade2.Pipeline.Frontend.Lexer;
using Cascade2.Pipeline.Frontend.Parser.Tools;

namespace Cascade2.Pipeline.Frontend.Parser.AST
{
    public enum AstNodeKind
    {
        /*
            ===============
            STATEMENT NODES
            ===============      
        */

        S_PROGRAM,
        S_EXIT,

        S_VARIABLE_DECLARATION,

        S_IF,
        S_WHILE_LOOP,
        S_POST_WHILE_LOOP,
        S_FOR_LOOP,

        S_ENSURE,
        S_DELETE,

        /*
            ================
            EXPRESSION NODES
            ================
        */

        IDENTIFIER,

        E_FUNCTION, // Exactly what you'd assume it would be.
        E_PARAMETER,

        E_CALL,
        E_CONSTRUCT, // Derived from functions. Has a definite return type, only called after a new object is created.

        E_UNARY,
        E_BINARY,
        E_TERNARY,

        E_CLASS, // Objects are created from classes. Do not need type containers.
        E_OBJECT, // Holds keys and values.
        E_MEMBER,
        E_ACCESS_MEMBER,

        E_ASSIGNMENT,

        E_TYPE, // Type expressions.

        // CONTROL FLOW
        E_IF,
        E_ELSE,

        /*
            =============
            LITERAL NODES
            =============
        */

        L_FLOAT,
        L_DOUBLE,
        L_INTEGER,
        L_LONG,
        L_STRING,
        L_BOOLEAN,
        L_NULL,
    }


    public static class NodeCategories
    {
        // Nodes that arithmetic operations can be performed on.
        public static readonly HashSet<AstNodeKind> ArithmeticOperands = [
            AstNodeKind.L_STRING,
            AstNodeKind.L_INTEGER,
            AstNodeKind.L_LONG,
            AstNodeKind.L_FLOAT,
            AstNodeKind.L_DOUBLE,
        ];

        // Nodes that can be used for computing keys for member containers.
        public static readonly HashSet<AstNodeKind> ComputableKeys = [
            AstNodeKind.L_FLOAT,
            AstNodeKind.L_DOUBLE,
            AstNodeKind.L_INTEGER,
            AstNodeKind.L_LONG,
            AstNodeKind.L_STRING,
            AstNodeKind.L_BOOLEAN,
        ];
    }


    /* 
        ===============
        LOW LEVEL NODES 
        ===============
    */


    public abstract class AstNode
    {
        public abstract AstNodeKind Kind { get; }

        public LocationInfo Location { get; set; }

        public bool IsArithmeticOperand() => NodeCategories.ArithmeticOperands.Contains(Kind);
        public bool IsComputableKey() => NodeCategories.ComputableKeys.Contains(Kind);
    }

    public abstract class StatementNode : AstNode
    {

    }

    public abstract class DeclarationStatementNode : StatementNode
    {
        public abstract List<MemberModifier>? Modifiers { get; }
        public abstract TypeExpressionNode Type { get; }
        public abstract ExpressionNode AccessPoint { get; }
    }

    public abstract class ExpressionNode : StatementNode
    {

    }

    public abstract class LiteralExpressionNode<T> : ExpressionNode
    {
        public abstract T Value { get; protected set; }
    }

    // Classes, dictionaries, etc.
    public abstract class MemberContainerExpressionNode : ExpressionNode
    {
        public abstract List<MemberExpressionNode> Members { get; }
    }


    /* 
        =================
         MID LEVEL NODES 
        =================
    */


    public sealed class EnsureStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_ENSURE;
        public ExpressionNode AccessPoint { get; }

        public EnsureStatementNode(LocationInfo location, ExpressionNode accessPoint)
        {
            AccessPoint = accessPoint;

            Location = location;
        }
    }

    public sealed class DeleteStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_DELETE;
        public ExpressionNode AccessPoint { get; }

        public DeleteStatementNode(LocationInfo location, ExpressionNode accessPoint)
        {
            AccessPoint = accessPoint;

            Location = location;
        }
    }

    // Note: Access points can be identifiers or AccessMemberExpressions.
    public sealed class VariableDeclarationStatementNode : DeclarationStatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_VARIABLE_DECLARATION;
        public override List<MemberModifier> Modifiers { get; }
        public override TypeExpressionNode Type { get; }
        public override ExpressionNode AccessPoint { get; }
        public ExpressionNode Value { get; }

        public VariableDeclarationStatementNode(LocationInfo location, List<MemberModifier> modifiers, TypeExpressionNode type, ExpressionNode accessPoint, ExpressionNode value)
        {
            Modifiers = modifiers;
            Type = type;
            AccessPoint = accessPoint;
            Value = value;

            Location = location;
        }
    }

    public sealed class ProgramStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_PROGRAM;
        public List<StatementNode> Body { get; set; }

        public ProgramStatementNode(LocationInfo location, List<StatementNode> body)
        {
            Body = body;

            Location = location;
        }
    }

    public sealed class ExitStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_EXIT;
        public DomainContext Context { get; }
        public ExpressionNode Content { get; }

        public ExitStatementNode(LocationInfo location, DomainContext exitContext, ExpressionNode exitContent)
        {
            Context = exitContext;
            Content = exitContent;
            Location = location;
        }
    }

    public sealed class IfStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_IF;
        public List<IfExpressionNode> IfClauses { get; }
        public ElseExpressionNode? ElseClause { get; }

        public IfStatementNode(LocationInfo location, List<IfExpressionNode> ifClauses, ElseExpressionNode? elseClause)
        {
            IfClauses = ifClauses;
            ElseClause = elseClause;
            Location = location;
        }
    }

    public sealed class WhileLoopStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_WHILE_LOOP;
        public ExpressionNode Condition { get; }
        public List<StatementNode> Body { get; }

        public WhileLoopStatementNode(LocationInfo location, ExpressionNode condition, List<StatementNode> body)
        {
            Condition = condition;
            Body = body;
            Location = location;
        }
    }

    public sealed class PostWhileLoopStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_POST_WHILE_LOOP;
        public ExpressionNode Condition { get; }
        public List<StatementNode> Body { get; }

        public PostWhileLoopStatementNode(LocationInfo location, ExpressionNode condition, List<StatementNode> body)
        {
            Condition = condition;
            Body = body;
            Location = location;
        }
    }

    public sealed class ForLoopStatementNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.S_FOR_LOOP;
        public VariableDeclarationStatementNode Variable { get; } // The variable declaration manages the first expression
        public ExpressionNode SecondExpression { get; } // The target expression the variable should be equal to eventually.
        public ExpressionNode Incrementation { get; } // A reference to a numeric literal that shares the type of the variable.
        public List<StatementNode> Body { get; }

        public ForLoopStatementNode(LocationInfo location, VariableDeclarationStatementNode variable, ExpressionNode secondExpression, ExpressionNode incrementation, List<StatementNode> body)
        {
            SecondExpression = secondExpression;
            Variable = variable;
            Incrementation = incrementation;
            Body = body;
            Location = location;
        }
    }

    public sealed class AccessMemberExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_ACCESS_MEMBER;

        public ExpressionNode Object { get; }       // The object we have selected.
        public ExpressionNode Member { get; }       // The reference to the member (A literal or an identifier depending on whether or not it is computed).
        public bool Computed { get; }               // Whether or not the 'Member' attribute is an identifier or literal. (Computed = Literal)

        public AccessMemberExpressionNode(LocationInfo location, ExpressionNode obj, ExpressionNode member, bool computed)
        {
            Object = obj;
            Member = member;
            Computed = computed;

            Location = location;
        }
    }

    public sealed class AssignmentExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_ASSIGNMENT;
        public ExpressionNode AccessPoint { get; }
        public ExpressionNode Value { get; }
        public TokenKind Operator { get; }

        public AssignmentExpressionNode(LocationInfo location, ExpressionNode accessPoint, TokenKind opr, ExpressionNode value)
        {
            AccessPoint = accessPoint;
            Operator = opr;
            Value = value;

            Location = location;
        }
    }

    public sealed class IfExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_IF;
        public ExpressionNode Condition { get; }
        public List<StatementNode> Body { get; }

        public IfExpressionNode(LocationInfo location, ExpressionNode condition, List<StatementNode> body)
        {
            Condition = condition;
            Body = body;

            Location = location;
        }
    }

    public sealed class ElseExpressionNode : StatementNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_ELSE;
        public List<StatementNode> Body { get; }

        public ElseExpressionNode(LocationInfo location, List<StatementNode> body)
        {
            Body = body;

            Location = location;
        }
    }

    public sealed class UnaryExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_UNARY;
        public ExpressionNode Value { get; }
        public TokenKind Operator { get; }

        public UnaryExpressionNode(LocationInfo location, ExpressionNode value, TokenKind opr)
        {
            Value = value;
            Operator = opr;

            Location = location;
        }
    }

    public sealed class BinaryExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_BINARY;
        public ExpressionNode Left { get; }
        public ExpressionNode Right { get; }
        public TokenKind Operator { get; }

        public BinaryExpressionNode(ExpressionNode left, ExpressionNode right, TokenKind opr)
        {
            Left = left;
            Right = right;
            Operator = opr;

            Location = new LocationInfo
            {
                Start = left.Location.Start,
                End = right.Location.End,
                Line = left.Location.Line
            };
        }
    }

    public sealed class TernaryExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_TERNARY;
        public ExpressionNode Condition { get; }
        public ExpressionNode TrueBranch { get; }
        public ExpressionNode FalseBranch { get; }

        public TernaryExpressionNode(ExpressionNode condition, ExpressionNode trueBranch, ExpressionNode falseBranch)
        {
            Condition = condition;
            TrueBranch = trueBranch;
            FalseBranch = falseBranch;

            Location = new LocationInfo
            {
                Start = condition.Location.Start,
                End = falseBranch.Location.End,
                Line = condition.Location.Line
            };
        }
    }

    public sealed class IdentifierExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.IDENTIFIER;
        public string Value { get; }

        public IdentifierExpressionNode(LocationInfo location, string value)
        {
            Value = value;

            Location = location;
        }
    }

    public sealed class FunctionExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_FUNCTION;
        public List<ParameterExpressionNode> Parameters { get; }
        public TypeExpressionNode ReturnType { get; }
        public List<StatementNode> Body { get; }

        public FunctionExpressionNode(LocationInfo location, TypeExpressionNode returnType, List<ParameterExpressionNode> parameters, List<StatementNode> body)
        {
            Parameters = parameters;
            ReturnType = returnType;
            Body = body;

            Location = location;
        }
    }

    public sealed class ParameterExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_PARAMETER;
        public TypeExpressionNode Type { get; }
        public IdentifierExpressionNode Identifier { get; }
        public ExpressionNode DefaultValue { get; }

        public ParameterExpressionNode(TypeExpressionNode type, IdentifierExpressionNode identifier, ExpressionNode defaultValue)
        {
            Type = type;
            Identifier = identifier;
            DefaultValue = defaultValue;

            Location = new LocationInfo(type.Location.Start, defaultValue != null ? defaultValue.Location.End : identifier.Location.End);
        }
    }

    public sealed class CallExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_CALL;
        public ExpressionNode Function { get; } // End result should be a function value, but in the meantime it could possibly be an access-member-expression.
        public List<ExpressionNode> Arguments { get; }

        public CallExpressionNode(LocationInfo location, ExpressionNode function, List<ExpressionNode> arguments)
        {
            Function = function;
            Arguments = arguments;

            Location = location;
        }
    }

    public sealed class ConstructExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_CONSTRUCT;
        public ExpressionNode Class { get; }
        public ExpressionNode Constructor { get; }
        public bool Computed { get; }
        public List<ExpressionNode> Arguments { get; }

        public ConstructExpressionNode(LocationInfo location, ExpressionNode classReference, ExpressionNode constructorReference, bool computed, List<ExpressionNode> arguments)
        {
            Class = classReference;
            Constructor = constructorReference;
            Computed = computed;
            Arguments = arguments;

            Location = location;
        }
    }

    // Describe the creation of a class.
    public sealed class ClassExpressionNode : MemberContainerExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_CLASS;
        public override List<MemberExpressionNode> Members { get; }
        public ExpressionNode? Superclass { get; } // Reference to a class the current one could be inheriting from.

        public ClassExpressionNode(LocationInfo location, List<MemberExpressionNode> members, ExpressionNode? superClass = null)
        {
            Members = members;
            Superclass = superClass;

            Location = location;
        }
    }

    // Describe the creation of a non constructed object.
    public sealed class ObjectExpressionNode : MemberContainerExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_OBJECT;
        public override List<MemberExpressionNode> Members { get; }

        public ObjectExpressionNode(LocationInfo location, List<MemberExpressionNode> members)
        {
            Members = members;

            Location = location;
        }
    }

    // Define the member of a class or object.
    // In the case of a class, the value would be the default property.
    public sealed class MemberExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_MEMBER;
        public List<MemberModifier> Modifiers { get; }
        public TypeExpressionNode Type { get; }
        public bool Computed { get; }
        public ExpressionNode Key { get; }
        public ExpressionNode Value { get; }

        public MemberExpressionNode(LocationInfo location, List<MemberModifier> modifiers, TypeExpressionNode type, ExpressionNode key, bool computed, ExpressionNode value)
        {
            Modifiers = modifiers;
            Type = type;
            Key = key;
            Value = value;

            Computed = computed;

            Location = location;
        }
    }

    // Define the type of an expression. Beit a literal, a class, an object or a member.
    public sealed class TypeExpressionNode : ExpressionNode
    {
        public override AstNodeKind Kind => AstNodeKind.E_TYPE;
        public UnevaluatedTypeExpression TypeExpression { get; }

        public TypeExpressionNode(LocationInfo location, StandardValueType standard, List<ExpressionNode>? meta = null, bool? nullable = false)
        {
            TypeExpression = new(true, standard, meta, nullable != null && (bool)nullable);

            Location = location;
        }

        public TypeExpressionNode(Token standard, List<ExpressionNode>? meta = null, bool? nullable = false)
        {
            TypeExpression = new(true, StandardValueTypeCreator.FromToken(standard), meta, nullable != null && (bool)nullable);

            Location = standard.Location;

            if (meta != null)
            {
                var location = Location;
                location.End = meta[^1].Location.End;
                Location = location;
            }
        }
    }


    /* 
        =================
          LITERAL NODES 
        =================
    */


    public sealed class BooleanLiteralNode : LiteralExpressionNode<bool>
    {
        public override AstNodeKind Kind => AstNodeKind.L_BOOLEAN;
        private bool _value;

        public override bool Value
        {
            get => _value;
            protected set => _value = value;
        }

        public BooleanLiteralNode(LocationInfo location, bool value)
        {
            _value = value;

            Location = location;
        }
    }

    public sealed class StringLiteralNode : LiteralExpressionNode<string>
    {
        public override AstNodeKind Kind => AstNodeKind.L_STRING;
        private string _value;
        public override string Value
        {
            get => _value;
            protected set => _value = value;
        }

        public StringLiteralNode(LocationInfo location, string result)
        {
            _value = result;

            Location = location;
        }
    }

    public sealed class FloatLiteralNode : LiteralExpressionNode<float>
    {
        public override AstNodeKind Kind => AstNodeKind.L_FLOAT;
        private float _value;
        public override float Value
        {
            get => _value;
            protected set => _value = value;
        }

        public FloatLiteralNode(LocationInfo location, float value)
        {
            _value = value;

            Location = location;
        }
    }

    public sealed class IntegerLiteralNode : LiteralExpressionNode<int>
    {
        public override AstNodeKind Kind => AstNodeKind.L_INTEGER;
        private int _value;
        public override int Value
        {
            get => _value;
            protected set => _value = value;
        }

        public IntegerLiteralNode(LocationInfo location, int value)
        {
            _value = value;

            Location = location;
        }
    }

    public sealed class LongLiteralNode : LiteralExpressionNode<long>
    {
        public override AstNodeKind Kind => AstNodeKind.L_LONG;
        private long _value;
        public override long Value
        {
            get => _value;
            protected set => _value = value;
        }

        public LongLiteralNode(LocationInfo location, long value)
        {
            _value = value;

            Location = location;
        }
    }

    public sealed class DoubleLiteralNode : LiteralExpressionNode<double>
    {
        public override AstNodeKind Kind => AstNodeKind.L_DOUBLE;
        private double _value;
        public override double Value
        {
            get => _value;
            protected set => _value = value;
        }

        public DoubleLiteralNode(LocationInfo location, double value)
        {
            _value = value;

            Location = location;
        }
    }

    public sealed class NullLiteralNode : LiteralExpressionNode<bool?>
    {
        public override AstNodeKind Kind => AstNodeKind.L_NULL;
        public override bool? Value
        {
            get => null;
            protected set => CompilerScreamStopper.Null = null;
        }

        public NullLiteralNode(LocationInfo location)
        {
            Location = location;
        }
    }

    static class CompilerScreamStopper
    {
        public static bool? Null = null;
    }
}
