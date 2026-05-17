using MoveMentorChess.App.ViewModels;
using MoveMentorChess.App.Views;

namespace MoveMentorChess.App.Composition;

public interface IProfilesWindowFactory
{
    ProfilesWindow Create(ProfilesWindowRequest request);
}
