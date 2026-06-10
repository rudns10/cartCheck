using MartCart.AppFaithful.Pages;
using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        ThemeService.Apply();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // SplashPage가 잠시 보인 뒤 AppShell로 자동 전환됨 (SplashPage.OnAppearing 참고)
        return new Window(new SplashPage());
    }
}
