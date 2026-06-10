using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;
using MartCart.Domain.Entities;
using MartCart.Domain.Services;

namespace MartCart.AppFaithful.Pages;

public partial class CartDetailPage : ContentPage
{
    private const string ThresholdKey = "martcart.threshold";
    private const string DiscountKey = "martcart.discount";
    public const string MartKey = "martcart.mart";
    private readonly ICurrentCart _currentCart;

    // 시각적 갱신을 강제하기 위한 row record (CartItem은 INotifyPropertyChanged 미구현)
    private sealed record ItemRow(Guid Id, string Name, decimal SalePrice, int Quantity, decimal SaleLineTotal);

    private static readonly string[] CommonMarts =
        { "이마트 트레이더스", "이마트", "코스트코", "홈플러스", "롯데마트", "노브랜드" };

    public CartDetailPage(ICurrentCart currentCart)
    {
        InitializeComponent();
        _currentCart = currentCart;
        _currentCart.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Sync threshold / discount from preferences into cart
        var thr = (decimal)Preferences.Default.Get(ThresholdKey, 50_000.0);
        if (_currentCart.Cart.Threshold != thr) _currentCart.SetThreshold(thr);
        var disc = (decimal)Preferences.Default.Get(DiscountKey, 5_000.0);
        if (_currentCart.Cart.DiscountAmount != disc) _currentCart.SetDiscount(disc);
        Render();
    }

    private void Render()
    {
        var cart = _currentCart.Cart;
        var items = cart.Items.ToList();

        // Mart name from Preferences (default empty)
        var martName = Preferences.Default.Get(MartKey, "");
        MartLabel.Text = string.IsNullOrEmpty(martName) ? "마트 선택" : martName;
        MartLabel.TextColor = string.IsNullOrEmpty(martName)
            ? (Color)Application.Current!.Resources["MutedColor"]
            : (Color)Application.Current!.Resources["TextColor"];

        // 합산·목표 금액·진행률은 실 구매가(SaleSubtotal) 기준
        OriginalSubtotalLabel.Text = cart.SaleSubtotal.ToString("N0");

        ThresholdLabel.Text = cart.Threshold > 0
            ? $"목표 금액 {cart.Threshold:N0}원까지"
            : "목표 금액 미설정 (탭해서 설정)";

        var saleRemaining = Math.Max(0m, cart.Threshold - cart.SaleSubtotal);
        RemainingLabel.Text = cart.Threshold == 0
            ? ""
            : saleRemaining > 0 ? $"{saleRemaining:N0}원 남음" : "✓ 도달";

        var ratio = cart.Threshold > 0
            ? Math.Min(1.0, (double)(cart.SaleSubtotal / cart.Threshold))
            : 0;
        ProgressFill.WidthRequest = 326 * ratio;
        ProgressPctLabel.Text = cart.Threshold > 0 ? $"{ratio * 100:0.#}%" : "—";

        OriginalAltLabel.Text = $"{cart.OriginalSubtotal:N0}원";
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
        // 새 record 인스턴스로 투영 → CollectionView가 새로 렌더링
        ItemsCollection.ItemsSource = items.Select(i => new ItemRow(
            i.Id,
            i.Name ?? "(이름 없음)",
            i.SalePrice,
            i.Quantity,
            i.SaleLineTotal)).ToList();

        var hasItems = items.Count > 0;
        EmptyPanel.IsVisible = !hasItems;
        ItemsSection.IsVisible = hasItems;
    }

    private async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private async void OnMartTapped(object? sender, TappedEventArgs e)
    {
        var marts = MartService.GetAll();
        var options = marts.Select(m => m.Name).Concat(new[] { "직접 입력…" }).ToList();
        var picked = await AppDialog.ChoiceAsync("어떤 마트인가요?", options);
        if (string.IsNullOrEmpty(picked)) return;

        string? value = picked;
        if (picked == "직접 입력…")
        {
            value = await AppDialog.PromptAsync("마트 이름", "마트 이름을 입력하세요",
                placeholder: "예: 농협하나로마트",
                initial: Preferences.Default.Get(MartKey, ""));
            if (string.IsNullOrWhiteSpace(value)) return;
            value = value.Trim();
        }

        Preferences.Default.Set(MartKey, value);

        // 등록된 마트면 목표 금액/할인 금액 자동 적용
        var mart = MartService.FindByName(value);
        if (mart is not null)
        {
            _currentCart.SetThreshold(mart.DefaultThreshold);
            _currentCart.SetDiscount(mart.DefaultDiscount);
            Preferences.Default.Set(ThresholdKey, (double)mart.DefaultThreshold);
            Preferences.Default.Set(DiscountKey, (double)mart.DefaultDiscount);
        }
        Render();
    }

    private async void OnScanClicked(object? sender, EventArgs e)
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
        {
            await AppDialog.AlertAsync("권한 필요", "카메라 권한이 필요합니다.");
            return;
        }
        await Shell.Current.GoToAsync(nameof(ScanPage));
    }

    private async void OnManualClicked(object? sender, EventArgs e)
    {
        var name = await AppDialog.PromptAsync("직접 입력 1/2", "상품명", placeholder: "예: 서울우유 1L", ok: "다음");
        if (string.IsNullOrWhiteSpace(name)) return;

        var saleText = await AppDialog.PromptAsync("직접 입력 2/2", "판매가(원)", placeholder: "예: 3500", ok: "추가", keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(saleText)) return;
        var sale = ParseWon(saleText);
        if (sale <= 0)
        {
            await AppDialog.AlertAsync("값 오류", "판매가는 양수여야 합니다.");
            return;
        }

        _currentCart.AddItem(new CartItem
        {
            Name = name.Trim(),
            OriginalPrice = sale,
            DiscountAmount = 0,
            SalePrice = sale,
            Quantity = 1,
            Source = ItemSource.Manual,
        });
    }

    private async void OnThresholdTapped(object? sender, TappedEventArgs e)
    {
        var currentThr = _currentCart.Cart.Threshold;
        var currentDisc = _currentCart.Cart.DiscountAmount;

        var input = await AppDialog.PromptAsync(
            "목표 금액",
            "할인이 적용되는 기준 금액(원). 0 입력 시 비활성화.",
            placeholder: "예: 50000",
            initial: currentThr.ToString("0"),
            ok: "다음",
            keyboard: Keyboard.Numeric);
        if (input is null) return;
        var newThr = ParseWon(input);
        if (newThr < 0)
        {
            await AppDialog.AlertAsync("값 오류", "0 이상의 숫자를 입력해주세요.");
            return;
        }

        var discInput = await AppDialog.PromptAsync(
            "할인 금액",
            "목표 도달 시 받을 할인 금액(원). 없으면 0",
            placeholder: "예: 5000",
            initial: currentDisc.ToString("0"),
            ok: "변경",
            keyboard: Keyboard.Numeric);
        if (discInput is null) return;
        var newDisc = ParseWon(discInput);
        if (newDisc < 0) newDisc = 0;

        Preferences.Default.Set(ThresholdKey, (double)newThr);
        Preferences.Default.Set(DiscountKey, (double)newDisc);
        _currentCart.SetThreshold(newThr);
        _currentCart.SetDiscount(newDisc);
    }

    private async void OnClearTapped(object? sender, TappedEventArgs e)
    {
        var ok = await AppDialog.AlertAsync("장바구니 비우기", "담은 항목을 모두 지울까요?", "비우기", "취소");
        if (ok) _currentCart.Clear();
    }

    private async void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        var action = await AppDialog.ChoiceAsync("메뉴", new[] { "목표 금액 변경", "장바구니 비우기" });
        if (action == "목표 금액 변경") OnThresholdTapped(sender, e);
        else if (action == "장바구니 비우기") OnClearTapped(sender, e);
    }

    private async void OnEndTapped(object? sender, TappedEventArgs e) => await EndShopping();

    private async void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Grid grid) return;
        if (grid.BindingContext is not ItemRow row) return;
        var item = _currentCart.Cart.Items.FirstOrDefault(i => i.Id == row.Id);
        if (item is null) return;

        var action = await AppDialog.ChoiceAsync(
            $"{item.Name}\n{item.SalePrice:N0}원 × {item.Quantity}개",
            new[] { "수량 +1", "수량 −1" },
            destructive: "삭제");

        if (string.IsNullOrEmpty(action)) return;

        if (action == "수량 +1") _currentCart.IncrementQuantity(item.Id);
        else if (action == "수량 −1") _currentCart.DecrementQuantity(item.Id);
        else if (action == "삭제")
        {
            var ok = await AppDialog.AlertAsync("삭제", $"'{item.Name}'을(를) 장바구니에서 지울까요?", "삭제", "취소");
            if (ok) _currentCart.RemoveItem(item.Id);
        }
    }

    private async Task EndShopping()
    {
        var cart = _currentCart.Cart;
        if (cart.Items.Count == 0)
        {
            await AppDialog.AlertAsync("빈 장바구니", "아직 담은 항목이 없어요.");
            return;
        }

        var mart = Preferences.Default.Get(MartKey, "");
        if (string.IsNullOrEmpty(mart))
        {
            var setMart = await AppDialog.AlertAsync("마트 선택 필요", "어느 마트에서 장보셨나요? 종료하기 전에 마트를 선택해주세요.", "선택", "취소");
            if (!setMart) return;
            OnMartTapped(null, new TappedEventArgs(null));
            return;
        }

        var ok = await AppDialog.AlertAsync(
            "장보기 종료",
            $"{mart}\n{cart.Items.Count}개 항목 · {cart.SaleSubtotal:N0}원\n\n종료하면 기록에 저장되고 장바구니가 비워집니다.",
            "종료", "취소");
        if (!ok) return;

        var items = cart.Items.Select(i => new CompletedCartItem(
            Name: i.Name ?? "(이름 없음)",
            OriginalPrice: i.OriginalPrice,
            DiscountAmount: i.DiscountAmount,
            SalePrice: i.SalePrice,
            Quantity: i.Quantity)).ToList();

        CartHistory.Add(new CompletedCart(
            Id: Guid.NewGuid().ToString("N"),
            Mart: mart,
            Original: cart.OriginalSubtotal,
            Sale: cart.SaleSubtotal,
            Saved: cart.TotalSaved,
            Threshold: cart.Threshold,
            ItemCount: cart.Items.Count,
            ThresholdReached: cart.IsThresholdReached,
            ClosedAt: DateTimeOffset.Now,
            Items: items));

        _currentCart.Clear();
        Preferences.Default.Remove(MartKey);  // 다음 장보기를 위해 마트도 초기화
        await AppDialog.AlertAsync("✓ 종료 완료", "기록에 저장되었습니다.");
        await Shell.Current.GoToAsync("..");
    }

    private static decimal ParseWon(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }
}
