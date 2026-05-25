using System.ComponentModel;
using System.Diagnostics;

namespace MoveMentorChess.Analysis;

public static class LlamaCppProcessCleaner
{
    private static readonly LlamaManagedProcessRootResolver RootResolver = new();
    private static readonly string[] ManagedExecutableNames =
    [
        "llama-server",
        "llama-cli"
    ];

    public static void CleanupOrphanedProcesses()
        => CleanupOrphanedProcesses(SystemLlamaRuntimeEnvironment.Instance);

    private static void CleanupOrphanedProcesses(ILlamaRuntimeEnvironment environment)
    {
        IReadOnlySet<string> managedRoots = RootResolver.Resolve(environment);
        if (managedRoots.Count == 0)
        {
            return;
        }

        foreach (string processName in ManagedExecutableNames)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(processName);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
            {
                continue;
            }

            foreach (Process process in processes)
            {
                try
                {
                    if (process.HasExited)
                    {
                        continue;
                    }

                    string? executablePath = TryGetExecutablePath(process);
                    if (string.IsNullOrWhiteSpace(executablePath))
                    {
                        continue;
                    }

                    string normalizedPath = NormalizeDirectorySeparator(Path.GetFullPath(executablePath));
                    if (!IsUnderManagedRoot(normalizedPath, managedRoots))
                    {
                        continue;
                    }

                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Best effort orphan cleanup only.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }

    private static bool IsUnderManagedRoot(string executablePath, IReadOnlySet<string> managedRoots)
    {
        foreach (string root in managedRoots)
        {
            if (executablePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? TryGetExecutablePath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static string NormalizeDirectorySeparator(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
    }
}
