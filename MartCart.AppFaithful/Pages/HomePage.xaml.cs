using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;
using MartCart.AppFaithful.Services.Ads;
using MartCart.Domain.Services;

namespace MartCart.AppFaithful.Pages;

public partial class HomePage : ContentPage
{
    private const string ThresholdKey = "martcart.threshold";
    private const string DiscountKey = "martcart.discount";
    private readonly ICurrentCart _currentCart;

    public HomePage(ICurrentCart currentCart)
    {
        InitializeComponent();
        _currentCart = currentCart;
        _currentCart.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Sync threshold / discount from preferences into cart
        var thr = (decimal)Preferences.Default.Get(ThresholdKey, 50_000.0);
        if (_currentCart.Cart.Threshold != thr) _currentCart.SetThreshold(thr);
        var disc = (decimal)Preferences.Default.Get(DiscountKey, 5_000.0);
        if (_currentCart.Cart.DiscountAmount != disc) _currentCart.SetDiscount(disc);
        Render();
        await LoadAdAsync();
    }

    private void Render()
    {
        var cart = _currentCart.Cart;

        // Big amount: 실 구매가 (Sale)
        HomeAmountLabel.Text = cart.SaleSubtotal.ToString("N0");

        // Original (할인 전) + saved
        HomeSaleLabel.Text = $"{cart.OriginalSubtotal:N0}원";
        if (cart.TotalSaved > 0)
        {
            HomeSavedPill.IsVisible = true;
            HomeSavedLabel.Text = $"−{cart.TotalSaved:N0}원 절약";
        }
        else
        {
            HomeSavedPill.IsVisible = false;
        }

        // Threshold
        var threshold = cart.Threshold;
        var discount = cart.DiscountAmount;
        if (threshold <= 0)
        {
            HomeThresholdLabel.Text = "목표 금액 미설정";
            HomeRemainingLabel.Text = "";
            HomeProgressFill.WidthRequest = 0;
            HomeDiscountLabel.IsVisible = false;
        }
        else
        {
            HomeThresholdLabel.Text = $"목표 금액 {threshold:N0}원까지";
            var remaining = Math.Max(0, threshold - cart.SaleSubtotal);
            var reached = remaining <= 0;
            HomeRemainingLabel.Text = reached ? "✓ 도달" : $"{remaining:N0}원 남음";

            var ratio = Math.Min(1.0, (double)(cart.SaleSubtotal / threshold));
            HomeProgressFill.WidthRequest = 280 * ratio;

            if (discount > 0)
            {
                HomeDiscountLabel.IsVisible = true;
                HomeDiscountLabel.Text = reached
                    ? $"🎉 {discount:N0}원 할인 받을 수 있어요"
                    : $"도달 시 {discount:N0}원 할인";
            }
            else
            {
                HomeDiscountLabel.IsVisible = false;
            }
        }

        // Mart
        var mart = Preferences.Default.Get(CartDetailPage.MartKey, "");
        HomeMartLabel.Text = string.IsNullOrEmpty(mart) ? "마트 선택 필요" : mart;

        // Recent carts
        RenderRecent();

        // Month summary
        RenderMonthSummary();
    }

    private void RenderMonthSummary()
    {
        var now = DateTimeOffset.Now;
        var thisMonth = CartHistory.All()
            .Where(c => c.ClosedAt.Year == now.Year && c.ClosedAt.Month == now.Month)
            .ToList();

        var spent = thisMonth.Sum(c => c.Sale);
        var count = thisMonth.Count;
        var saved = thisMonth.Sum(c => c.Saved);

        MonthSummaryTitle.FormattedText = new FormattedString
        {
            Spans =
            {
                new Span { Text = "이번 달", TextColor = (Color)Application.Current!.Resources["TextColor"], FontAttributes = FontAttributes.Bold },
                new Span { Text = $" ({now.Month}월) 요약" },
            },
        };

        MonthSpentLabel.Text = spent.ToString("N0");
        MonthCountLabel.Text = count.ToString();
        MonthSavedLabel.Text = saved.ToString("N0");
    }

    private void RenderRecent()
    {
        var history = CartHistory.All();
        var view = history.Take(3).Select(c => new
        {
            c.Id,
            c.Mart,
            c.ThresholdReached,
            HasSaved = c.Saved > 0,
            Subtitle = c.ThresholdReached
                ? $"{c.ClosedAt:M월 d일} · 목표 금액 도달"
                : $"{c.ClosedAt:M월 d일} · {c.ItemCount}개 항목",
            SaleText = $"{c.Sale:N0}원",
            SavedText = $"−{c.Saved:N0}원",
        }).ToList();

        if (view.Count == 0)
        {
            RecentEmpty.IsVisible = true;
            RecentList.IsVisible = false;
        }
        else
        {
            RecentEmpty.IsVisible = false;
            RecentList.IsVisible = true;
            RecentCollection.ItemsSource = view;
        }
    }

    private async void OnActiveCartTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync(nameof(CartDetailPage));

    private async Task LoadAdAsync()
    {
        try
        {
            var view = await AdProvider.Current.CreateBannerAsync(this);
            AdSlot.Content = view;
            AdSlot.IsVisible = view is not null;
        }
        catch
        {
            AdSlot.Content = null;
            AdSlot.IsVisible = false;
        }
    }

    private async void OnStatsTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//StatsPage");

    private async void OnHistoryTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//HistoryPage");

    private async void OnRecentCartTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid grid) return;
        var ctx = grid.BindingContext;
        if (ctx is null) return;
        var idProp = ctx.GetType().GetProperty("Id");
        var id = idProp?.GetValue(ctx) as string;
        if (string.IsNullOrEmpty(id)) return;
        await Shell.Current.GoToAsync($"{nameof(HistoryDetailPage)}?id={id}");
    }
}
