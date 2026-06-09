namespace MoveMentorChess.Analysis;

public interface IRuntimeSettingsEnvironment
{
    string LocalApplicationDataDirectory { get; }

    string BaseDirectory { get; }

    bool FileExists(string path);

    string ReadAllText(string path);

    void CreateDirectory(string path);

    void WriteAllText(string path, string contents);

    void ReplaceFile(string sourcePath, string destinationPath);
}

public sealed class SystemRuntimeSettingsEnvironment : IRuntimeSettingsEnvironment
{
    public static SystemRuntimeSettingsEnvironment Instance { get; } = new();

    private SystemRuntimeSettingsEnvironment()
    {
    }

    public string LocalApplicationDataDirectory => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string BaseDirectory => AppContext.BaseDirectory;

    public bool FileExists(string path) => File.Exists(path);

    public string ReadAllText(string path) => File.ReadAllText(path);

    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    public void ReplaceFile(string sourcePath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(sourcePath, destinationPath, null);
            return;
        }

        File.Move(sourcePath, destinationPath);
    }
}
