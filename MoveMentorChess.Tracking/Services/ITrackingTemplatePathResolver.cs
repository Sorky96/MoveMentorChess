namespace MoveMentorChess.Tracking;

public interface ITrackingTemplatePathResolver
{
    string? Resolve(string fileName);
}
