namespace Cascade2.Pipeline.Shared
{
    public class PipelineAlgorithm
    {
        protected List<CascadeDiagnostic> _diagnostics = [];
        public IEnumerable<CascadeDiagnostic> Diagnostics => _diagnostics;

        public void AddDiagnostic(string message, LocationInfo location, CascadeDiagnosticType type = CascadeDiagnosticType.ERROR)
        {
            _diagnostics.Add(new CascadeDiagnostic
            {
                Location = location,
                Type = type,
                Message = message,
            });
        }

        public void TerminateDiagnostic(string message, LocationInfo location)
        {
            AddDiagnostic(message, location);

            throw new Exception();
        }
    }
}