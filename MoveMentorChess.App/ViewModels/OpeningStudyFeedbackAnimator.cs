using System.Threading;
using Avalonia.Media;

namespace MoveMentorChess.App.ViewModels;

public sealed class OpeningStudyFeedbackAnimator
{
    private long version;

    public async Task AnimateAsync(OpeningTrainingAttemptResult result, Action<OpeningStudyFeedbackFrame> applyFrame)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(applyFrame);

        long currentVersion = Interlocked.Increment(ref version);
        (string Text, Color Fill, Color Border) feedback = result.Score switch
        {
            OpeningTrainingScore.Correct => ("Good move", Color.Parse("#5BE37A"), Color.Parse("#9EF5AE")),
            OpeningTrainingScore.Wrong => ("Needs review", Color.Parse("#EF5F5F"), Color.Parse("#FF9C9C")),
            _ => result.Status == OpeningTrainingAttemptStatus.TransposedToKnownPosition
                ? ("Known transposition", Color.Parse("#F2C94C"), Color.Parse("#FFE28A"))
                : ("Useful alternative", Color.Parse("#F2C94C"), Color.Parse("#FFE28A"))
        };

        IBrush fillBrush = new SolidColorBrush(Color.FromArgb(210, feedback.Fill.R, feedback.Fill.G, feedback.Fill.B));
        IBrush borderBrush = new SolidColorBrush(feedback.Border);
        applyFrame(new OpeningStudyFeedbackFrame(feedback.Text, fillBrush, borderBrush, 0.88));

        await Task.Delay(180);
        if (currentVersion != Volatile.Read(ref version))
        {
            return;
        }

        applyFrame(new OpeningStudyFeedbackFrame(feedback.Text, fillBrush, borderBrush, 0.36));

        await Task.Delay(420);
        if (currentVersion != Volatile.Read(ref version))
        {
            return;
        }

        applyFrame(new OpeningStudyFeedbackFrame(feedback.Text, fillBrush, borderBrush, 0));
    }
}

public sealed record OpeningStudyFeedbackFrame(
    string Text,
    IBrush Brush,
    IBrush BorderBrush,
    double Opacity);
