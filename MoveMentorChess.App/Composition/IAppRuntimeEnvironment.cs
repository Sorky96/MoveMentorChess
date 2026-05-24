namespace MoveMentorChess.App.Composition;

internal interface IAppRuntimeEnvironment
{
    string BaseDirectory { get; }

    string CurrentDirectory { get; }

    bool FileExists(string path);
}
