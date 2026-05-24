using System.IO;

namespace MoveMentorChess.App.Composition;

internal sealed class SystemAppRuntimeEnvironment : IAppRuntimeEnvironment
{
    public static SystemAppRuntimeEnvironment Instance { get; } = new();

    private SystemAppRuntimeEnvironment()
    {
    }

    public string BaseDirectory => AppContext.BaseDirectory;

    public string CurrentDirectory => Environment.CurrentDirectory;

    public bool FileExists(string path) => File.Exists(path);
}
