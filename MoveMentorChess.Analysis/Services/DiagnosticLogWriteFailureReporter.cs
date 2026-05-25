using System.Diagnostics;

namespace MoveMentorChess.Analysis;

internal static class DiagnosticLogWriteFailureReporter
{
    public static void TraceWarning(string componentName, string filePath, Exception exception)
    {
        Trace.TraceWarning(
            "{0}: failed to write diagnostic log '{1}' ({2}: {3})",
            componentName,
            filePath,
            exception.GetType().Name,
            exception.Message);
    }
}
