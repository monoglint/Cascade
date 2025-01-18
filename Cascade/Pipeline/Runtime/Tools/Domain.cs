using Cascade.Pipeline.Frontend.Parser.Tools;
using Cascade.Pipeline.Runtime.Values;
using Cascade.Pipeline.Shared;

namespace Cascade.Pipeline.Runtime.Tools
{
    public enum DomainContext
    {
        PROGRAM,
        FUNCTION,
        LOOP,
        IF_STATEMENT_CLAUSE,
        NONE
    }

    public class Domain
    {
        public Domain? Parent { get; set; }
        public Dictionary<string, MemberExpressionValue> Members { get; set; } = [];
        public DomainContext Context { get; set; }

        // Whether or not the current domain is active.
        public bool IsActive { get; set; } = true;
        public bool IsGlobal { get { return Parent == null; } }

        public Domain(Interpreter interpreter, Domain? parent, DomainContext context)
        {
            Parent = parent;
            Context = context;

            if (IsGlobal)
            {
                Objects.RuntimeConsoleObject.Insert(interpreter, this);
            }
        }

        public void DeleteVariable(Interpreter interpreter, LocationInfo deletionLocation, string key)
        {
            if (!Members.Remove(key) && IsGlobal)
            {
                interpreter.TerminateDiagnostic("Attempted to delete a non-existing or unreachable variable.", deletionLocation);
            }

            Parent?.Resolve(interpreter, deletionLocation, key);
        }

        public FirstClassValue DeclareVariable(Interpreter interpreter, LocationInfo declarationLocation, List<MemberModifier> modifiers, TypeExpression type, string key, FirstClassValue value)
        {
            MemberExpressionValue? potentialExistingMember = LookUp(interpreter, declarationLocation, key);

            // If the member already exists, then the programmer is trying to reset the modifiers and type.
            if (potentialExistingMember != null)
            {
                potentialExistingMember.Modifiers = modifiers;
                potentialExistingMember.Type = type;

                potentialExistingMember.SetValue(interpreter, declarationLocation, value);

                return value;
            }

            MemberExpressionValue member = new(interpreter, declarationLocation, modifiers, type, value);

            Members.Add(key, member);

            return value;
        }

        public FirstClassValue AssignVariable(Interpreter interpreter, LocationInfo assignmentLocation, string key, FirstClassValue value)
        {
            MemberExpressionValue? potentialExistingMember = LookUp(interpreter, assignmentLocation, key);

            // If the member already exists, then the programmer is trying to reset the modifiers and type.
            if (potentialExistingMember == null)
            {
                interpreter.TerminateDiagnostic($"An assignment operation can only be applied to pre-existing members.", assignmentLocation); throw new Exception(); // !CALM
            }

            potentialExistingMember.SetValue(interpreter, assignmentLocation, value);

            return value;
        }

        // Locate a variable that is accessible within the current domain.
        public MemberExpressionValue? LookUp(Interpreter interpreter, LocationInfo lookupLocation, string variableName)
        {
            Domain? validDomain = Resolve(interpreter, lookupLocation, variableName);

            if (validDomain == null)
            {
                return null;
            }

            validDomain.Members.TryGetValue(variableName, out MemberExpressionValue? memberValue);

            return memberValue!;
        }

        public Domain? Resolve(Interpreter interpreter, LocationInfo resolveLocation, string variableName)
        {
            if (Members.ContainsKey(variableName))
            {
                return this;
            }
            else if (IsGlobal)
            {
                return null;
            }

            return Parent!.Resolve(interpreter, resolveLocation, variableName);
        }

        // Exit out of all parent domains up to one with the given exit context.
        // Returns whether or not the exit was a success.
        // USE "HasContext" TO ENSURE THIS FUNCTION WORKS!
        public void Exit(DomainContext exitContext)
        {
            Domain selectedDomain = this;

            while (selectedDomain.Parent != null && selectedDomain.Context != exitContext)
            {
                selectedDomain = selectedDomain.Parent;

                selectedDomain.IsActive = false;
            }
        }

        // Searches parent domains for a matching context.
        public bool HasContext(DomainContext context)
        {
            Domain selectedDomain = this;

            while (selectedDomain.Parent != null && selectedDomain.Context != context)
            {
                selectedDomain = selectedDomain.Parent;
            }

            return selectedDomain.Context == context;
        }
    }
}
