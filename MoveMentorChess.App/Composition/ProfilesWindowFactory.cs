using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;
using MoveMentorChess.Persistence;
using MoveMentorChess.Profiles;

namespace MoveMentorChess.App.Composition;

public sealed class ProfilesWindowFactory : IProfilesWindowFactory
{
    private readonly Func<IAnalysisStore?> analysisStoreProvider;

    public ProfilesWindowFactory(Func<IAnalysisStore?> analysisStoreProvider)
    {
        this.analysisStoreProvider = analysisStoreProvider ?? throw new ArgumentNullException(nameof(analysisStoreProvider));
    }

    public ProfilesWindow Create(ProfilesWindowRequest request)
    {
        IAnalysisStore store = analysisStoreProvider()
            ?? throw new InvalidOperationException("Local analysis store is unavailable.");

        return new ProfilesWindow(
            new PlayerProfileService(store),
            request.NavigateToProfileExampleAsync,
            request.NavigateToOpeningExampleAsync,
            request.NavigateToOpeningPositionAsync);
    }
}
