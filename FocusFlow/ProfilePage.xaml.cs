using FocusFlow.ViewModels;

namespace FocusFlow;

public partial class ProfilePage : ContentPage
{
    public ProfilePage(ProfileViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await ((ProfileViewModel)BindingContext).LoadProfileDataAsync();
    }
}
