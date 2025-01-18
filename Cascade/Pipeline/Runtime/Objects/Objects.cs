using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Frontend.Parser.Tools;
using Cascade.Pipeline.Runtime.Tools;
using Cascade.Pipeline.Runtime.Values;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Runtime.Objects
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
