using Cascade2.Pipeline.Runtime.Values;

namespace Cascade2.Pipeline.Frontend.Parser.Tools
{
    public class ParameterExpression(TypeExpression type, string identifier, FirstClassValue defaultValue)
    {
        public TypeExpression Type { get; set; } = type;
        public string Identifier { get; set; } = identifier;
        public FirstClassValue DefaultValue { get; set; } = defaultValue;
    }
}
