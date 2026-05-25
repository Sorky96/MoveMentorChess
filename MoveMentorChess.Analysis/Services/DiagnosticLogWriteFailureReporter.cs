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
        catch (InvalidOperationException)
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
        catch (IOException)
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
        catch (UnauthorizedAccessException)
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
        catch (NotSupportedException)
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
        catch (ApplicationException)
        {
            // Diagnostic reporting must not make the original fallback path fail.
        }
    }
}
