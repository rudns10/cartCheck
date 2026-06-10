using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful.Pages;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        var all = CartHistory.All().OrderByDescending(c => c.ClosedAt).ToList();
        var now = DateTimeOffset.Now;
        var thisMonth = all.Where(c => c.ClosedAt.Year == now.Year && c.ClosedAt.Month == now.Month).ToList();

        PeriodTitleLabel.Text = $"{now.Year}년 {now.Month}월 · 실 구매 합계";
        SpentTotalLabel.Text = thisMonth.Sum(c => c.Sale).ToString("N0");
        CountLabel.Text = thisMonth.Count.ToString();
        var avg = thisMonth.Count > 0 ? thisMonth.Sum(c => c.Sale) / thisMonth.Count : 0;
        AvgLabel.Text = avg.ToString("N0");
        SavedTotalLabel.Text = thisMonth.Sum(c => c.Saved).ToString("N0");

        ListContainer.Children.Clear();

        if (all.Count == 0)
        {
            EmptyPanel.IsVisible = true;
            return;
        }
        EmptyPanel.IsVisible = false;

        // Group by year-month
        var groups = all.GroupBy(c => new { c.ClosedAt.Year, c.ClosedAt.Month })
                        .OrderByDescending(g => g.Key.Year).ThenByDescending(g => g.Key.Month);

        foreach (var g in groups)
        {
            var carts = g.OrderByDescending(c => c.ClosedAt).ToList();
            var total = carts.Sum(c => c.Sale);

            // Group header
            var header = new HorizontalStackLayout
            {
                Padding = new Thickness(24, 18, 24, 8),
                Spacing = 8,
                Children =
                {
                    new Label { Text = $"{g.Key.Month}월", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["TextColor"] },
                    new Label { Text = $"{carts.Count}건 · {total:N0}원", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["MutedColor"], VerticalOptions = LayoutOptions.Center },
                },
            };
            ListContainer.Children.Add(header);

            foreach (var cart in carts)
            {
                ListContainer.Children.Add(WrapWithSwipe(cart, BuildCartCard(cart)));
            }
        }
    }

    private Border BuildCartCard(CompletedCart cart)
    {
        // Outer Border with shadow
        var border = new Border
        {
            Margin = new Thickness(16, 0, 16, 0),
            Background = (Color)Application.Current!.Resources["SurfaceColor"],
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Padding = new Thickness(16),
            Shadow = new Shadow { Brush = new SolidColorBrush(Color.FromArgb("#0A1F33")), Offset = new Point(0, 2), Radius = 6, Opacity = 0.06f },
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 12,
        };

        // Mart icon
        var iconBorder = new Border
        {
            Background = Color.FromArgb("#EEF2F7"),
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            WidthRequest = 42,
            HeightRequest = 42,
            Content = new Label { Text = "🛒", FontSize = 20, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
        };
        Grid.SetColumn(iconBorder, 0);

        // Info
        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        var titleRow = new HorizontalStackLayout { Spacing = 6 };
        titleRow.Children.Add(new Label { Text = cart.Mart, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["TextColor"] });
        if (cart.ThresholdReached)
        {
            var pill = new Border
            {
                Background = (Color)Application.Current!.Resources["AccentBgColor"],
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 99 },
                Padding = new Thickness(7, 2),
                Content = new Label { Text = "✓ 도달", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["AccentColor"] },
            };
            titleRow.Children.Add(pill);
        }
        info.Children.Add(titleRow);
        info.Children.Add(new Label
        {
            Text = $"{cart.ClosedAt:M월 d일 (ddd) HH:mm} · {cart.ItemCount}개 항목",
            FontSize = 11,
            TextColor = (Color)Application.Current!.Resources["MutedColor"],
        });
        Grid.SetColumn(info, 1);

        // Price
        var priceStack = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        priceStack.Children.Add(new Label
        {
            Text = $"{cart.Sale:N0}원",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["TextColor"],
            HorizontalOptions = LayoutOptions.End,
        });
        if (cart.Saved > 0)
        {
            var savedPill = new Border
            {
                Background = (Color)Application.Current!.Resources["AccentBgColor"],
                Stroke = Colors.Transparent,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Padding = new Thickness(7, 2),
                HorizontalOptions = LayoutOptions.End,
                Content = new Label { Text = $"−{cart.Saved:N0}원", FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["AccentColor"] },
            };
            priceStack.Children.Add(savedPill);
        }
        Grid.SetColumn(priceStack, 2);

        grid.Children.Add(iconBorder);
        grid.Children.Add(info);
        grid.Children.Add(priceStack);

        border.Content = grid;

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Shell.Current.GoToAsync($"{nameof(HistoryDetailPage)}?id={cart.Id}");
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private SwipeView WrapWithSwipe(CompletedCart cart, View content)
    {
        var danger = (Color)Application.Current!.Resources["DangerColor"];

        var pill = new Border
        {
            Background = danger,
            Stroke = Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
            WidthRequest = 60,
            HeightRequest = 60,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 16, 0),
            Content = new Label
            {
                Text = "🗑",
                FontSize = 22,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
            },
        };

        var deleteItem = new SwipeItemView { Content = pill };
        deleteItem.Invoked += async (_, _) => await ConfirmDeleteAsync(cart);

        return new SwipeView
        {
            Threshold = 60,
            RightItems = new SwipeItems(new[] { deleteItem })
            {
                Mode = SwipeMode.Reveal,
            },
            Content = content,
        };
    }

    private async Task ConfirmDeleteAsync(CompletedCart cart)
    {
        var ok = await AppDialog.AlertAsync(
            "기록 삭제",
            $"'{cart.Mart}' · {cart.Sale:N0}원 기록을 삭제할까요?",
            "삭제",
            "취소");
        if (!ok) return;
        CartHistory.Delete(cart.Id);
        Render();
    }
}
