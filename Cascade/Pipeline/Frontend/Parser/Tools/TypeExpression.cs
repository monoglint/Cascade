using System.Text;
using Cascade.Pipeline.Frontend.Parser.AST;
using Cascade.Pipeline.Runtime.Values;

namespace Cascade.Pipeline.Frontend.Parser.Tools
{
    public static class TypeExpressionList
    {
        public static readonly TypeExpression StandardInteger = new(false, StandardValueType.INTEGER);
        public static readonly TypeExpression StandardLong = new(false, StandardValueType.LONG);
        public static readonly TypeExpression StandardBoolean = new(false, StandardValueType.BOOLEAN);
        public static readonly TypeExpression StandardString = new(false, StandardValueType.STRING);
        public static readonly TypeExpression StandardFloat = new(false, StandardValueType.FLOAT);
        public static readonly TypeExpression StandardDouble = new(false, StandardValueType.DOUBLE);
        public static readonly TypeExpression StandardVoid = new(false, StandardValueType.VOID);
    }

    public class UnevaluatedTypeExpression(bool mutable, StandardValueType standard, List<ExpressionNode>? meta = null, bool? nullable = false)
    {
        public bool Mutable { set; get; } = mutable;
        public bool Nullable { get; set; } = nullable != null && (bool)nullable;
        public StandardValueType Standard { get; set; } = standard;
        public List<ExpressionNode>? Meta { get; set; } = meta;

        public virtual bool HasMeta()
        {
            return Meta != null && Meta.Count > 0;
        }

        public override string ToString()
        {
            StringBuilder meta = new();

            if (HasMeta())
            {
                int pointer = 0;

                while (pointer < Meta!.Count)
                {
                    meta.Append(Meta[pointer++]);
                }
            }

            return $"{Standard}{meta}{(Nullable ? "?" : string.Empty)}";
        }
    }

    public class TypeExpression(bool mutable, StandardValueType standard, List<FirstClassValue>? meta = null, bool? nullable = false)
    {
        public bool Mutable { get; } = mutable;
        public bool Nullable { get; internal set; } = nullable != null && (bool)nullable;
        public StandardValueType Standard { get; internal set; } = standard;
        public List<FirstClassValue>? Meta { get; internal set; } = meta;

        public void SetNullable(bool nullable)
        {
            if (Mutable)
            {
                Nullable = nullable;
            }
        }

        public void SetStandard(StandardValueType standard)
        {
            if (Mutable)
            {
                Standard = standard;
            }
        }

        public void SetMeta(List<FirstClassValue>? meta = null)
        {
            if (Mutable)
            {
                Meta = meta;
            }
        }

        public virtual bool HasMeta()
        {
            return Meta != null && Meta.Count > 0;
        }

        public override string ToString()
        {
            StringBuilder meta = new();

            if (HasMeta())
            {
                int pointer = 0;

                while (pointer < Meta!.Count)
                {
                    meta.Append(Meta[pointer++].ToString());
                }
            }

            return $"{Standard}{meta}{(Nullable ? "?" : string.Empty)}";
        }
    }
}
