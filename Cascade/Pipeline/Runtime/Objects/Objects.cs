using Cascade2.Pipeline.Frontend.Parser.AST;
using Cascade2.Pipeline.Frontend.Parser.Tools;
using Cascade2.Pipeline.Runtime.Tools;
using Cascade2.Pipeline.Runtime.Values;
using Cascade2.Pipeline.Shared;

namespace Cascade2.Pipeline.Runtime.Objects
{
    
public static class RuntimeConsoleObject
    {
        public static readonly string Identifier = "Console";
        public static void Insert(Interpreter interpreter, Domain domain)
        {
            ObjectExpressionValue obj = new(new Dictionary<string, MemberExpressionValue>
            {
                {
                    "Write", new MemberExpressionValue(
                        interpreter,
                        LocationInfoList.Empty,
                        [MemberModifier.CONSTANT],
                        new TypeExpression(false, StandardValueType.CS_FUNCTION),
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
                    )
                }
            });

            domain.DeclareVariable(interpreter, LocationInfoList.Empty, [MemberModifier.CONSTANT], new TypeExpression(false, StandardValueType.OBJECT), Identifier, obj);
        }

        private static NullLiteralValue Write(Domain domain, List<FirstClassValue> arguments)
        {
            Console.WriteLine(arguments[0].ResolveString());

            return RuntimeValueList.NullLiteral;
        }
    }
}
