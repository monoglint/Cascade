using Cascade.Pipeline.Frontend.Parser.Tools;
using Cascade.Pipeline.Runtime.Values;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Runtime.Tools
{
    public static class Builtins
    {
        public static readonly LocationInfo LocationConstant = LocationInfoList.Empty;
        public static readonly List<MemberModifier> MemberModifierConstant = [MemberModifier.CONSTANT];
        public static readonly TypeExpression TypeExpressionFunctionConstant = new(false, StandardValueType.CS_FUNCTION);

        public static class FileObject
        {
            public static readonly string Identifier = "File";

            private static StringLiteralValue Read(Interpreter interpreter, Domain domain, LocationInfo callLocation, List<FirstClassValue> arguments)
            {
                string filePath = arguments[0].ResolveString();

                interpreter.TerminateDiagnostic($"\"{filePath}\" is not a valid file path.", callLocation);

                string result = File.ReadAllText(filePath);

                return new StringLiteralValue(result);
            }

            private static NullLiteralValue Write(Interpreter interpreter, Domain domain, LocationInfo callLocation, List<FirstClassValue> arguments)
            {
                string filePath = arguments[0].ResolveString();

                if (!File.Exists(filePath))
                {
                    interpreter.TerminateDiagnostic($"\"{filePath}\" is not a valid file path.", callLocation);
                }

                File.WriteAllText(filePath, arguments[1].ResolveString());

                return new NullLiteralValue();
            }

            public static void Insert(Interpreter interpreter, Domain domain)
            {
                ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
                {
                    {"Read", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
                        new CsFunctionExpressionValue(
                            [
                                new(
                                    new TypeExpression(false, StandardValueType.STRING),
                                    "filePath",
                                    RuntimeValueList.NullLiteral
                                )
                            ],
                            Read
                        )
                    )},

                    {"Write", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
                        new CsFunctionExpressionValue(
                            [
                                new(
                                    new TypeExpression(false, StandardValueType.STRING),
                                    "filePath",
                                    RuntimeValueList.NullLiteral
                                ),

                                new(
                                    new TypeExpression(false, StandardValueType.STRING),
                                    "contents",
                                    RuntimeValueList.NullLiteral
                                ),
                            ],
                            Write
                        )
                    )},
                });

                domain.DeclareVariable(interpreter, LocationConstant, MemberModifierConstant, new TypeExpression(false, StandardValueType.OBJECT), Identifier, obj);
            }
        }

        public static class ConsoleObject
        {
            public static readonly string Identifier = "Console";

            private static NullLiteralValue Write(Interpreter interpreter, Domain domain, LocationInfo callLocation, List<FirstClassValue> arguments)
            {
                Console.WriteLine(arguments[0].ResolveString());

                return RuntimeValueList.NullLiteral;
            }

            public static StringLiteralValue Read(Interpreter interpreter, Domain domain, LocationInfo callLocation, List<FirstClassValue> arguments)
            {
                string? result = Console.ReadLine();

                return new StringLiteralValue(result != null ? result : string.Empty);
            }

            public static void Insert(Interpreter interpreter, Domain domain)
            {
                ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
                {
                    {"Write", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
                        new CsFunctionExpressionValue(
                            [
                                new(
                                    new TypeExpression(false, StandardValueType.DYNAMIC),
                                    "text",
                                    RuntimeValueList.NullLiteral
                                )
                            ],
                            Write
                        )
                    )},

                    {"Read", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
                        new CsFunctionExpressionValue(
                            [],
                            Read
                        )
                    )},
                });

                domain.DeclareVariable(interpreter, LocationConstant, MemberModifierConstant, new TypeExpression(false, StandardValueType.OBJECT), Identifier, obj);
            } 
        }
    }
}
