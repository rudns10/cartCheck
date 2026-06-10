using MartCart.App.Pages;
using MartCart.Domain.Entities;
using MartCart.Domain.Services;

namespace MartCart.App;

public partial class MainPage : ContentPage
{
    private readonly IServiceProvider _services;
    private readonly ICurrentCart _currentCart;
    private double _progressBarMax;

    public MainPage(IServiceProvider services, ICurrentCart currentCart)
    {
        InitializeComponent();
        _services = services;
        _currentCart = currentCart;

#if ANDROID
        StatusLabel.Text = "OCR: ML Kit Korean (온디바이스)";
#elif IOS
        StatusLabel.Text = "OCR: Vision Framework (한글 미지원)";
#else
        StatusLabel.Text = "OCR: 이 플랫폼에서는 미지원";
#endif

        _currentCart.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Render);

        // Capture progress bar's available width once the layout is ready
        SizeChanged += (_, _) =>
        {
            _progressBarMax = Width - (16 + 16 + 22 + 22); // page padding + hero padding
            Render();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Render();
    }

    private void Render()
    {
        var cart = _currentCart.Cart;
        var items = cart.Items.ToList();

        OriginalSubtotalLabel.Text = cart.OriginalSubtotal.ToString("N0");

        ThresholdLabel.Text = cart.Threshold > 0
            ? $"임계치 {cart.Threshold:N0}원까지"
            : "임계치 미설정 (탭해서 설정)";
        RemainingLabel.Text = cart.Threshold == 0
            ? ""
            : cart.Remaining > 0 ? $"{cart.Remaining:N0}원 남음" : "✓ 도달";

        var progress = cart.Threshold > 0
            ? Math.Min(1.0, (double)(cart.OriginalSubtotal / cart.Threshold))
            : 0;
        ProgressFill.WidthRequest = Math.Max(0, _progressBarMax) * progress;
        ProgressPctLabel.Text = cart.Threshold > 0 ? $"{progress * 100:0.#}%" : "—";

        SaleSubtotalLabel.Text = $"{cart.SaleSubtotal:N0}원";
        if (cart.TotalSaved > 0)
        {
            SavedBadge.Text = $"−{cart.TotalSaved:N0}원 절약";
            SavedBadgeBorder.IsVisible = true;
        }
        else
        {
            SavedBadgeBorder.IsVisible = false;
        }

        ItemCountBadge.Text = items.Count.ToString();
        ItemsCollection.ItemsSource = items;

        var hasItems = items.Count > 0;
        EmptyPanel.IsVisible = !hasItems;
        ItemsSection.IsVisible = hasItems;
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlert("권한 필요", "카메라 권한이 필요합니다.", "확인");
            return;
        }

        var page = _services.GetRequiredService<ScanPage>();
        await Navigation.PushAsync(page);
    }

    private async void OnManualClicked(object? sender, EventArgs e)
    {
        // Simple 3-step prompt — name, sale price, quantity
        var name = await DisplayPromptAsync("직접 입력 1/3", "상품명을 입력하세요", "다음", "취소", placeholder: "예: 서울우유 1L");
        if (string.IsNullOrWhiteSpace(name)) return;

        var saleText = await DisplayPromptAsync("직접 입력 2/3", "판매가(원)를 입력하세요", "다음", "취소", placeholder: "예: 3500", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(saleText)) return;
        var sale = ParseWon(saleText);
        if (sale <= 0)
        {
            await DisplayAlert("값 오류", "판매가는 양수여야 합니다.", "확인");
            return;
        }

        var qtyText = await DisplayPromptAsync("직접 입력 3/3", "수량을 입력하세요", "추가", "취소", initialValue: "1", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(qtyText)) return;
        if (!int.TryParse(qtyText, out var qty) || qty <= 0) qty = 1;

        var item = new CartItem
        {
            Name = name.Trim(),
            OriginalPrice = sale,
            DiscountAmount = 0,
            SalePrice = sale,
            Quantity = qty,
            Source = ItemSource.Manual,
        };
        _currentCart.AddItem(item);
    }

    private async void OnThresholdTapped(object? sender, TappedEventArgs e)
    {
        var current = _currentCart.Cart.Threshold;
        var input = await DisplayPromptAsync(
            title: "임계치 변경",
            message: "할인 받을 금액(원)을 입력하세요. 0 입력 시 비활성화.",
            accept: "변경",
            cancel: "취소",
            placeholder: "예: 50000",
            initialValue: current.ToString("0"),
            keyboard: Keyboard.Numeric);
        if (input is null) return;
        var newVal = ParseWon(input);
        if (newVal < 0)
        {
            await DisplayAlert("값 오류", "0 이상의 숫자를 입력해주세요.", "확인");
            return;
        }
        _currentCart.SetThreshold(newVal);
    }

    private async void OnClearClicked(object? sender, TappedEventArgs e)
    {
        var ok = await DisplayAlert("장바구니 비우기", "담은 항목을 모두 지울까요?", "비우기", "취소");
        if (ok) _currentCart.Clear();
    }

    private async void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        var action = await DisplayActionSheet("메뉴", "취소", null, "임계치 변경", "장바구니 비우기");
        if (action == "임계치 변경") OnThresholdTapped(sender, e);
        else if (action == "장바구니 비우기") OnClearClicked(sender, e);
    }

    private static decimal ParseWon(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }
}
