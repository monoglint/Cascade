namespace Cascade.Pipeline.Runtime.Values
{
    // A list of immutable starter values.
    public readonly struct RuntimeValueList
    {
        public static readonly NullLiteralValue NullLiteral = new();

        public static readonly BooleanLiteralValue Bool_True = new(true);
        public static readonly BooleanLiteralValue Bool_False = new(false);
    }
}
