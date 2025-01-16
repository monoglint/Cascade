namespace Cascade2.Pipeline.Shared
{
    public enum CascadeDiagnosticType
    {
        ERROR,
        WARNING,
        INFO,
    }

    public class CascadeDiagnostic
    {
        public required string Message { get; set; }
        public required LocationInfo Location { get; set; }
        public CascadeDiagnosticType Type { get; set; } = CascadeDiagnosticType.ERROR;
    }

    public struct LocationInfo
    {
        public int Start { get; set; }
        public int End { get; set; }
        public int Line { get; set; }

        // Create a location with raw integers.
        public LocationInfo(int start = 0, int end = 0, int line = 0)
        {
            Start = start;
            End = end;
            Line = line;
        }

        // Create a location with two existing locations as a range.
        public LocationInfo(LocationInfo start, LocationInfo end)
        {
            Start = start.Start;
            End = end.End;
            Line = start.Line;
        }

        // Rely on the first location to get the line.
        public LocationInfo(LocationInfo start, int end)
        {
            Start = start.Start;
            End = end;
            Line = start.Line;
        }

        public override readonly string ToString()
        {
            return $"(Line {Line})";
        }
    }

    public static class LocationInfoList
    {
        public static readonly LocationInfo Empty = new();
    }
}
