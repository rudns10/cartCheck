using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful.Pages;

public partial class SplashPage : ContentPage
{
    public SplashPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Task.Delay(1600);
        if (Application.Current is not App app) return;
        var window = app.Windows.FirstOrDefault();
        if (window is null) return;

        if (!OnboardingService.Seen)
        {
            window.Page = new OnboardingPage();
            return;
        }

        if (PinService.IsSet)
        {
            window.Page = new PinLockPage(onSuccess: () =>
            {
                window.Page = new AppShell();
            });
        }
        else
        {
            window.Page = new AppShell();
        }
    }
}
