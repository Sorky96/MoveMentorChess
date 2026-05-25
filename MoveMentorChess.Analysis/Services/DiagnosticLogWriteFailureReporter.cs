using System.Diagnostics;

namespace MoveMentorChess.Analysis;

internal static class DiagnosticLogWriteFailureReporter
{
    public static void TraceWarning(string componentName, string filePath, Exception exception)
    {
        try
        {
            Trace.TraceWarning(
                "{0}: failed to write diagnostic log '{1}' ({2}: {3})",
                componentName,
                filePath,
                exception.GetType().Name,
                exception.Message);
        }
#pragma warning disable CA1031
        catch (Exception)
#pragma warning restore CA1031
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
    }
}
