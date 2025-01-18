using Cascade.Pipeline.Frontend.Parser.Tools;
using Cascade.Pipeline.Runtime.Values;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Runtime.Tools
{
    public static class Objects
    {
        public static LocationInfo LocationConstant = LocationInfoList.Empty;
        public static List<MemberModifier> MemberModifierConstant = [MemberModifier.CONSTANT];
        public static TypeExpression TypeExpressionConstant = new TypeExpression(false, StandardValueType.CS_FUNCTION);

        public static class RuntimeConsoleObject
        {
            public static readonly string Identifier = "Console";
            public static void Insert(Interpreter interpreter, Domain domain)
            {
                ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
                {
                    {"Write", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionConstant,
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

                    {"Read", new MemberExpressionValue(interpreter, LocationConstant, MemberModifierConstant, TypeExpressionConstant,
                        new CsFunctionExpressionValue(
                            [],
                            Read
                        )
                    )},
                }
                );

                domain.DeclareVariable(interpreter, LocationInfoList.Empty, [MemberModifier.CONSTANT], new TypeExpression(false, StandardValueType.OBJECT), Identifier, obj);
            }

            private static NullLiteralValue Write(Domain domain, List<FirstClassValue> arguments)
            {
                Console.WriteLine(arguments[0].ResolveString());

                return RuntimeValueList.NullLiteral;
            }

            public static StringLiteralValue Read(Domain domain, List<FirstClassValue> arguments)
            {
                string? result = Console.ReadLine();

                return new StringLiteralValue(result != null ? result : string.Empty);
            }
        }
    }
}
