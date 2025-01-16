using Cascade2.Pipeline.Frontend.Parser.Tools;
using Cascade2.Pipeline.Frontend.Parser.AST;

namespace Cascade2.Pipeline.Runtime.Values
{
    public static class RuntimeValuerResolver
    {
        public static string ResolveToString(RuntimeValue runtimeValue)
        {
            return runtimeValue switch
            {
                FloatLiteralValue value => value.Value.ToString(),
                DoubleLiteralValue value => value.Value.ToString(),
                IntegerLiteralValue value => value.Value.ToString(),
                LongLiteralValue value => value.Value.ToString(),
                BooleanLiteralValue value => value.Value == true ? "True" : "False",
                StringLiteralValue value => value.Value,
                NullLiteralValue => "Null",
                _ => throw new NotImplementedException()
            };
        }
    }

    public static class TypeComparator
    {
        public static bool TypesMatch(TypeExpression baseType, TypeExpression type)
        {
            if (baseType.Nullable && type.Standard == StandardValueType.VOID)
            {
                return true;
            }

            return StandardsMatch(baseType, type) && MetasMatch(baseType, type);
        }

        public static bool StandardsMatch(TypeExpression baseType, TypeExpression type)
        {
            // If the types match directly.
            if (baseType.Standard == type.Standard)
            {
                return true;
            }

            // If the base type is dynamic.
            else if (baseType.Standard == StandardValueType.DYNAMIC)
            {
                return true;
            }

            // If we're assigning a function to a construct.
            else if (baseType.Standard == StandardValueType.CONSTRUCT && type.Standard == StandardValueType.FUNCTION)
            {
                return true;
            }

            return false;
        }

        public static bool MetasMatch(TypeExpression baseType, TypeExpression type)
        {
            // Ensure both types have metas.
            bool baseHasMeta = baseType.HasMeta();
            bool typeHasMeta = type.HasMeta();

            if (!baseHasMeta || !typeHasMeta)
            {
                // If both metas do not exist, then the types are matching.
                // OTHERWISE, if one meta exists while the other doesn't, they are not matching.
                return !baseHasMeta && !typeHasMeta;
            }

            // First check if there are a different amount of entries in both lists to save power.
            if (baseType.Meta!.Count != type.Meta!.Count)
            {
                return false;
            }

            int pointer = 0;

            while (pointer < baseType.Meta.Count)
            {
                if (baseType.Meta[pointer].Kind != type.Meta[pointer].Kind)
                {
                    return false;
                }

                pointer++;
            }

            return true;
        }
    }
}
