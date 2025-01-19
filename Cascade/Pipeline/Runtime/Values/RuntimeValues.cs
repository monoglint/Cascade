using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Frontend.Parser.Tools;
using Cascade.Pipeline.Runtime.Tools;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Runtime.Values
{
    public enum RuntimeValueKind
    {
        // Expressions
        E_MEMBER,

        E_CLASS,
        E_OBJECT,

        E_FUNCTION, // Constructs or normal methods.
        E_CS_FUNCTION, // C# native functions.

        // Literals
        L_FLOAT,
        L_DOUBLE,
        L_INTEGER,
        L_LONG,
        L_STRING,
        L_BOOLEAN,
        L_NULL,
    }


    public static class RuntimeValueCategories
    {
        // *LITERAL* Nodes that can be used as access keys for objects.
        // Identifiers are evaluated in their own way and do not require this list.
        public static readonly HashSet<RuntimeValueKind> MemberKeys = [
            RuntimeValueKind.L_STRING,
            RuntimeValueKind.L_INTEGER,
            RuntimeValueKind.L_LONG,
            RuntimeValueKind.L_FLOAT,
            RuntimeValueKind.L_DOUBLE,
        ];

        public static readonly HashSet<RuntimeValueKind> Functions = [
            RuntimeValueKind.E_CS_FUNCTION,
            RuntimeValueKind.E_FUNCTION,
        ];

        public static readonly HashSet<RuntimeValueKind> Numbers = [
            RuntimeValueKind.L_DOUBLE,
            RuntimeValueKind.L_INTEGER,
            RuntimeValueKind.L_LONG,
            RuntimeValueKind.L_FLOAT,
        ];
    }


    /* 
        =================
        LOW LEVEL VALUES 
        =================
    */


    public abstract class RuntimeValue
    {
        public abstract RuntimeValueKind Kind { get; }

        public bool IsMemberKey() => RuntimeValueCategories.MemberKeys.Contains(Kind);
        public bool IsFunction() => RuntimeValueCategories.Functions.Contains(Kind);
        public bool IsNumber() => RuntimeValueCategories.Numbers.Contains(Kind);
    }


    /* 
        =================
        MID LEVEL VALUES 
        =================
    */


    public abstract class FirstClassValue : RuntimeValue
    {
        public abstract TypeExpression Type { get; set; }

        public virtual int ResolveInteger() => int.Parse(ResolveString());
        public virtual long ResolveLong() => long.Parse(ResolveString());
        public virtual float ResolveFloat() => float.Parse(ResolveString());
        public virtual double ResolveDouble() => Convert.ToDouble(ResolveString());
        public virtual string ResolveString()
        {
            return this.Type.ToString();
        }
    }

    public abstract class LiteralValue<T> : FirstClassValue
    {
        public abstract T Value { get; protected set; }
    }

    public abstract class MemberContainerValue : FirstClassValue
    {
        public Dictionary<string, MemberExpressionValue> Members { get; protected set; } = [];

        public FirstClassValue DeclareMember(Interpreter interpreter, LocationInfo declarationLocation, List<MemberModifier> modifiers, TypeExpression type, string key, FirstClassValue value)
        {
            // If the member already exists, then the programmer is trying to reset the modifiers and type.
            if (Members.TryGetValue(key, out MemberExpressionValue? potentialExistingMember))
            {
                potentialExistingMember!.Modifiers = modifiers;
                potentialExistingMember!.Type = type;

                potentialExistingMember!.SetValue(interpreter, declarationLocation, value);

                return value;
            }

            Members.Add(key, new(interpreter, declarationLocation, modifiers, type, value));

            return value;
        }

        public void DeleteMember(Interpreter interpreter, LocationInfo deletionLocation, string key)
        {
            if (!Members.Remove(key))
            {
                interpreter.TerminateDiagnostic("Attempted to delete a non-existing or unreachable variable.", deletionLocation);
            }
        }

        public FirstClassValue AssignMember(Interpreter interpreter, LocationInfo assignmentLocation, string key, FirstClassValue value)
        {
            if (!Members.TryGetValue(key, out MemberExpressionValue? potentialExistingMember))
            {
                interpreter.TerminateDiagnostic($"Attempted to modify '{key}' that does not exist.", assignmentLocation); throw new Exception(); // !CALM
            }

            return potentialExistingMember.SetValue(interpreter, assignmentLocation, value);
        }

        public bool GetMember(string key, out MemberExpressionValue? memberExpression)
        {
            bool result = Members.TryGetValue(key, out MemberExpressionValue? value);

            memberExpression = value;

            return result;
        }
    }


    /* 
        =================
        HIGH LEVEL VALUES 
        =================
    */


    public sealed class CsFunctionExpressionValue(List<ParameterExpression> parameters, Func<Interpreter, Domain, LocationInfo, FirstClassValue> callMethod) : FirstClassValue
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.E_CS_FUNCTION;
        public override TypeExpression Type { get; set; } = new TypeExpression(true, StandardValueType.CS_FUNCTION);

        public Func<Interpreter, Domain, LocationInfo, FirstClassValue> CallMethod { get; set; } = callMethod;
        public List<ParameterExpression> Parameters { get; set; } = parameters;

        public FirstClassValue Call(Interpreter interpreter, LocationInfo callLocation, Domain parentDomain, List<FirstClassValue> arguments)
        {
            Domain localDomain = new(interpreter, parentDomain, DomainContext.FUNCTION);

            interpreter.VerifyAndLoadFunctionArguments(localDomain, callLocation, Parameters, arguments);

            return CallMethod(interpreter, localDomain, callLocation);
        }
    }

    public sealed class FunctionExpressionValue(TypeExpression returnType, List<StatementNode> body, List<ParameterExpression> parameters) : FirstClassValue
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.E_FUNCTION;
        public override TypeExpression Type { get; set; } = new TypeExpression(true, StandardValueType.FUNCTION);
        public TypeExpression ReturnType { get; set; } = returnType;
        public List<ParameterExpression> Parameters { get; set; } = parameters;
        public List<StatementNode> Body { get; set; } = body;
    }

    public sealed class ClassExpressionValue : MemberContainerValue
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.E_CLASS;
        public override TypeExpression Type { get; set; } = new TypeExpression(true, StandardValueType.CLASS);
        public ClassExpressionValue? Superclass { get; }

        public ClassExpressionValue(ClassExpressionValue? superClass, Dictionary<string, MemberExpressionValue>? members)
        {
            Superclass = superClass;

            if (members != null)
            {
                foreach (KeyValuePair<string, MemberExpressionValue> kvp in members)
                {
                    Members.Add(kvp.Key, kvp.Value);
                }
            }
        }
    }

    public sealed class ObjectExpressionValue : MemberContainerValue
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.E_OBJECT;
        public override TypeExpression Type { get; set; } = new TypeExpression(true, StandardValueType.OBJECT);
        public ClassExpressionValue? Class { get; } // If the class is constructed, have access to the class the object comes from.

        // Create a non constructed object.
        public ObjectExpressionValue(Dictionary<string, MemberExpressionValue>? members)
        {
            if (members != null)
            {
                foreach (KeyValuePair<string, MemberExpressionValue> kvp in members)
                {
                    Members.Add(kvp.Key, kvp.Value);
                }
            }
        }

        // Create an object constructed from a class.
        public ObjectExpressionValue(ClassExpressionValue inheritedClass)
        {
            Class = inheritedClass;

            // If there is a little bit of that polymorphism going on here...
            if (inheritedClass.Superclass != null)
            {
                Stack<ClassExpressionValue> classHierarchy = [];
                ClassExpressionValue? currentClass = inheritedClass;

                while (currentClass != null)
                {
                    classHierarchy.Push(currentClass);
                    currentClass = currentClass.Superclass;
                }

                while (classHierarchy.Count > 0)
                {
                    ClassExpressionValue selectedClass = classHierarchy.Pop();
                    foreach (KeyValuePair<string, MemberExpressionValue> kvp in selectedClass.Members)
                    {
                        Members[kvp.Key] = kvp.Value;
                    }
                }
            }

            foreach (KeyValuePair<string, MemberExpressionValue> kvp in inheritedClass.Members)
            {
                Members[kvp.Key] = kvp.Value;
            }
        }
    }

    // A high level value that contains a type, modifiers, and a value. Used for object members or variables.
    public sealed class MemberExpressionValue : RuntimeValue
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.E_MEMBER;
        public List<MemberModifier> Modifiers { get; set; }
        public TypeExpression Type { get; set; }
        public FirstClassValue Value { get; internal set; } = RuntimeValueList.NullLiteral;

        public MemberExpressionValue(Interpreter interpreter, LocationInfo assignmentLocation, List<MemberModifier> modifiers, TypeExpression type, FirstClassValue value)
        {
            Modifiers = modifiers;
            Type = type;

            SetValue(interpreter, assignmentLocation, value);
        }

        public FirstClassValue SetValue(Interpreter interpreter, LocationInfo assignmentLocation, FirstClassValue value)
        {
            if (!TypeComparator.TypesMatch(Type, value.Type))
            {
                interpreter.TerminateDiagnostic($"Attempted to assign a value with the type {value.Type} to {Type}.", assignmentLocation); throw new Exception(); // !OPTIMIZE
            }

            Value = value;

            return value;
        }
    }


    /* 
        =================
         LITERAL VALUES 
        =================
    */


    public sealed class FloatLiteralValue(float value) : LiteralValue<float>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_FLOAT;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardFloat;
        private float _value = value;
        public override float Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override string ResolveString()
        {
            return _value.ToString();
        }

        public override float ResolveFloat()
        {
            return _value;
        }
    }

    public sealed class IntegerLiteralValue(int value) : LiteralValue<int>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_INTEGER;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardInteger;
        private int _value = value;
        public override int Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override string ResolveString()
        {
            return _value.ToString();
        }

        public override int ResolveInteger()
        {
            return _value;
        }
    }

    public sealed class DoubleLiteralValue(double value) : LiteralValue<double>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_DOUBLE;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardDouble;
        private double _value = value;
        public override double Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override double ResolveDouble()
        {
            return _value;
        }

        public override string ResolveString()
        {
            return _value.ToString();
        }
    }

    public sealed class LongLiteralValue(long value) : LiteralValue<long>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_LONG;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardLong;
        private long _value = value;
        public override long Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override string ResolveString()
        {
            return _value.ToString();
        }

        public override long ResolveLong()
        {
            return _value;
        }
    }

    public sealed class StringLiteralValue(string value) : LiteralValue<string>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_STRING;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardString;
        private string _value = value;
        public override string Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override string ResolveString()
        {
            return _value;
        }
    }

    public sealed class BooleanLiteralValue(bool value) : LiteralValue<bool>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_BOOLEAN;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardBoolean;
        private bool _value = value;
        public override bool Value
        {
            get => _value;
            protected set => _value = value;
        }

        public override string ResolveString()
        {
            return _value.ToString();
        }
    }

    public sealed class NullLiteralValue : LiteralValue<bool?>
    {
        public override RuntimeValueKind Kind => RuntimeValueKind.L_NULL;
        public override TypeExpression Type { get; set; } = TypeExpressionList.StandardVoid;
        private bool? _value = null;
        public override bool? Value
        {
            get => null;
            protected set => _value = null;
        }

        public NullLiteralValue() { }

        public override string ResolveString()
        {
            return "null";
        }
    }
}