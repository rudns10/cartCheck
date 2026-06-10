using MartCart.AppFaithful.Controls;
using Microsoft.Maui.Controls.Shapes;

namespace MartCart.AppFaithful.Services.Ads;

/// <summary>
/// 실제 광고 SDK가 연결되기 전 사용하는 더미 배너. 디자인 mockup을 그대로 보여줌.
/// </summary>
public sealed class MockBannerAdProvider : IBannerAdProvider
{
    public Task<View?> CreateBannerAsync(Page host)
    {
        var res = Application.Current!.Resources;
        var bannerBrush = (Brush)res["BannerBrush"];
        var warnColor = (Color)res["WarnColor"];
        var warnText = (Color)res["WarnTextColor"];
        var textColor = (Color)res["TextColor"];
        var mutedColor = (Color)res["MutedColor"];

        var grid = new Grid
        {
            ColumnSpacing = 12,
            Padding = new Thickness(0, 0, 16, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 6 },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var bar = new BoxView { Color = warnColor };
        Grid.SetColumn(bar, 0); grid.Children.Add(bar);

        var adChip = new Border
        {
            Stroke = Colors.Transparent,
            Background = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(9) },
            Padding = new Thickness(9, 3),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(10, 0, 0, 0),
            Content = new Label { Text = "광고", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = warnText },
        };
        Grid.SetColumn(adChip, 1); grid.Children.Add(adChip);

        var icon = new Border
        {
            Stroke = Colors.Transparent,
            Background = Colors.White,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            WidthRequest = 38,
            HeightRequest = 38,
            Content = new Label { Text = "🛒", FontSize = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        };
        Grid.SetColumn(icon, 2); grid.Children.Add(icon);

        var copy = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        copy.Children.Add(new Label { Text = "이마트 · 이번 주말 한정", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = textColor });
        copy.Children.Add(new Label { Text = "5만원 이상 구매 시 5,000원 즉시 할인 쿠폰", FontSize = 11, TextColor = mutedColor, LineBreakMode = LineBreakMode.TailTruncation });
        Grid.SetColumn(copy, 3); grid.Children.Add(copy);

        var close = new Label { Text = "✕", FontSize = 12, TextColor = warnText, VerticalOptions = LayoutOptions.Center };
        Grid.SetColumn(close, 4); grid.Children.Add(close);

        var border = new Border
        {
            Background = bannerBrush,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Padding = new Thickness(0, 14),
            Content = grid,
            Shadow = new Shadow { Brush = Color.FromArgb("#0A1F33"), Offset = new Point(0, 2), Radius = 6, Opacity = 0.06f },
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await AppDialog.AlertAsync(
            "이마트 프로모션",
            "5만원 이상 구매 시 5,000원 즉시 할인 쿠폰\n\n자세한 내용은 마트 앱에서 확인하세요.");
        border.GestureRecognizers.Add(tap);

        return Task.FromResult<View?>(border);
    }
}
