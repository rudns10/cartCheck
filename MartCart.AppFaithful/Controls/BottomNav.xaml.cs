using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace MartCart.AppFaithful.Controls;

public partial class BottomNav : ContentView
{
    public static readonly BindableProperty ActiveProperty =
        BindableProperty.Create(nameof(Active), typeof(string), typeof(BottomNav), "home",
            propertyChanged: (b, _, _) => ((BottomNav)b).Apply());

    public string Active
    {
        get => (string)GetValue(ActiveProperty);
        set => SetValue(ActiveProperty, value);
    }

    public BottomNav()
    {
        InitializeComponent();
        Apply();
    }

    private void Apply()
    {
        SetTabState(HomePill, HomeIcon, HomeLabel, Active == "home");
        SetTabState(HistoryPill, HistoryIcon, HistoryLabel, Active == "history");
        SetTabState(StatsPill, StatsIcon, StatsLabel, Active == "stats");
        SetTabState(SettingsPill, SettingsIcon, SettingsLabel, Active == "settings");
    }

    private static void SetTabState(Border pill, Path icon, Label label, bool active)
    {
        var primary = (Color)Application.Current!.Resources["PrimaryColor"];
        var muted = (Color)Application.Current!.Resources["MutedColor"];
        var accentBg = (Color)Application.Current!.Resources["AccentBgColor"];

        if (active)
        {
            // Material 3 style — pill background around icon, label color matches
            pill.Background = new SolidColorBrush(accentBg);
            icon.Fill = new SolidColorBrush(primary);
            label.TextColor = primary;
        }
        else
        {
            pill.Background = Colors.Transparent;
            icon.Fill = new SolidColorBrush(muted);
            label.TextColor = muted;
        }
    }

    private async void OnHomeTapped(object? sender, TappedEventArgs e)
    {
        if (Active == "home") return;
        await Shell.Current.GoToAsync("//HomePage");
    }
    private async void OnHistoryTapped(object? sender, TappedEventArgs e)
    {
        if (Active == "history") return;
        await Shell.Current.GoToAsync("//HistoryPage");
    }
    private async void OnStatsTapped(object? sender, TappedEventArgs e)
    {
        if (Active == "stats") return;
        await Shell.Current.GoToAsync("//StatsPage");
    }
    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
    {
        if (Active == "settings") return;
        await Shell.Current.GoToAsync("//SettingsPage");
    }
}
