using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful.Pages;

[QueryProperty(nameof(CartId), "id")]
public partial class HistoryDetailPage : ContentPage
{
    public string CartId { get; set; } = "";

    public HistoryDetailPage()
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
        var cart = string.IsNullOrEmpty(CartId) ? null : CartHistory.Find(CartId);
        if (cart is null)
        {
            HeaderMartLabel.Text = "기록 없음";
            HeaderDateLabel.Text = "";
            NotFoundLabel.IsVisible = true;
            return;
        }

        HeaderMartLabel.Text = cart.Mart;
        HeaderDateLabel.Text = cart.ClosedAt.ToString("yyyy년 M월 d일 (ddd) HH:mm");

        SaleLabel.Text = cart.Sale.ToString("N0");
        OriginalLabel.Text = $"{cart.Original:N0}원";
        SavedLabel.Text = cart.Saved > 0 ? $"−{cart.Saved:N0}원" : "0원";
        ThresholdLabel.Text = cart.Threshold > 0 ? $"{cart.Threshold:N0}원" : "—";

        ReachedBadge.IsVisible = cart.ThresholdReached;

        ItemCountBadge.Text = cart.ItemCount.ToString();
        ItemsCollection.ItemsSource = cart.Items;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");
}
