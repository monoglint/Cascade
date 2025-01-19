using Cascade.Pipeline.Frontend.Parser.AST;
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
            public static readonly string Identifier = "file";

            private static StringLiteralValue Read(Interpreter interpreter, Domain domain, LocationInfo callLocation)
            {
                string filePath = domain.Members["filePath"].Value.ResolveString();

                if (!File.Exists(filePath))
                {
                    interpreter.TerminateDiagnostic($"'{filePath}' is not a valid file path.", callLocation);
                }

                string result = File.ReadAllText(filePath);

                return new StringLiteralValue(result);
            }

            private static NullLiteralValue Write(Interpreter interpreter, Domain domain, LocationInfo callLocation)
            {
                string filePath = domain.Members["filePath"].Value.ResolveString();

                if (!File.Exists(filePath))
                {
                    interpreter.TerminateDiagnostic($"'{filePath}' is not a valid file path.", callLocation);
                }

                File.WriteAllText(filePath, domain.Members["contents"].Value.ResolveString());

                return new NullLiteralValue();
            }

            public static void Insert(Interpreter interpreter, Domain domain)
            {
                ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
                {
                    {"read", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
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

                    {"write", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
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

        public static class InOutObject
        {
            public static readonly string Identifier = "io";

            private static NullLiteralValue Write(Interpreter interpreter, Domain domain, LocationInfo callLocation)
            {
                bool newline = ((BooleanLiteralValue)domain.Members["newLine"].Value).Value;
                string text = domain.Members["text"].Value.ResolveString();

                Console.Write(text);

                if (newline)
                {
                    Console.Write('\n');
                }

                return RuntimeValueList.NullLiteral;
            }

            public static StringLiteralValue Read(Interpreter interpreter, Domain domain, LocationInfo callLocation)
            {
                string? result = Console.ReadLine();

                return new StringLiteralValue(result ?? string.Empty);
            }

            public static void Insert(Interpreter interpreter, Domain domain)
            {
                ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
                {
                    {"write", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
                        new CsFunctionExpressionValue(
                            [
                                new(
                                    new TypeExpression(false, StandardValueType.DYNAMIC),
                                    "text",
                                    RuntimeValueList.NullLiteral
                                ),

                                new(
                                    new TypeExpression(false, StandardValueType.BOOLEAN),
                                    "newLine",
                                    RuntimeValueList.Bool_False
                                )
                            ],
                            Write
                        )
                    )},

                    {"read", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionFunctionConstant,
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
