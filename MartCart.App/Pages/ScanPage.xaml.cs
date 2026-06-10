using MartCart.Domain.Contracts;
using MartCart.Domain.Entities;
using MartCart.Domain.Services;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using System.Diagnostics;

namespace MartCart.App.Pages;

public partial class ScanPage : ContentPage
{
    private readonly IPriceOcr? _ocr;
    private readonly IPriceClassifier _classifier;
    private readonly ICurrentCart _currentCart;

    // Which entry the user last focused — candidate chip taps fill that one
    private Entry? _lastFocusedPriceEntry;

    public ScanPage(IPriceClassifier classifier, ICurrentCart currentCart, IServiceProvider services)
    {
        InitializeComponent();
        _classifier = classifier;
        _currentCart = currentCart;
        _ocr = services.GetService<IPriceOcr>();

        OriginalEntry.Focused += (_, _) => _lastFocusedPriceEntry = OriginalEntry;
        DiscountEntry.Focused += (_, _) => _lastFocusedPriceEntry = DiscountEntry;
        SaleEntry.Focused += (_, _) => _lastFocusedPriceEntry = SaleEntry;

        // Auto-recompute sale when orig/discount changes
        OriginalEntry.TextChanged += OnPriceTextChanged;
        DiscountEntry.TextChanged += OnPriceTextChanged;
    }

    private bool _suppressRecompute;
    private void OnPriceTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_suppressRecompute) return;
        var orig = ParseWon(OriginalEntry.Text);
        var disc = ParseWon(DiscountEntry.Text);
        if (orig > 0 && disc >= 0 && disc < orig)
        {
            _suppressRecompute = true;
            SaleEntry.Text = (orig - disc).ToString("N0");
            _suppressRecompute = false;
        }
    }

    private static decimal ParseWon(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
        => await Navigation.PopAsync();

    private void OnCloseResult(object? sender, EventArgs e)
    {
        ResultPanel.IsVisible = false;
    }

    private async void OnShutterClicked(object? sender, EventArgs e)
    {
        if (_ocr is null)
        {
            await DisplayAlert("OCR 미지원", "이 플랫폼에서는 OCR이 지원되지 않습니다.", "확인");
            return;
        }

        try
        {
            ShutterBtn.IsEnabled = false;
            GuideLabel.Text = "촬영 중…";
            await Camera.CaptureImage(CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScanPage.OnShutterClicked] {ex}");
            await DispatchUiAsync(async () =>
            {
                await DisplayAlert("오류", "촬영 실패: " + ex.Message, "확인");
                ShutterBtn.IsEnabled = true;
                GuideLabel.Text = "가격표를 사각형 안에 맞추고 셔터를 누르세요";
            });
        }
    }

    private async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
        Debug.WriteLine($"[ScanPage.OnMediaCaptured] thread={Environment.CurrentManagedThreadId} isMain={MainThread.IsMainThread}");

        if (_ocr is null) return;

        try
        {
            await DispatchUiAsync(() =>
            {
                LoadingPanel.IsVisible = true;
                GuideLabel.Text = "분석 중…";
            });

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await e.Media.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            OcrResult result;
            using (var input = new MemoryStream(bytes))
            {
                result = await _ocr.RecognizeAsync(input);
            }

            Debug.WriteLine($"[ScanPage] OCR done: text={result.FullText?.Length ?? 0} chars, prices={result.PriceCandidates.Count}");

            var classification = _classifier.Classify(result.PriceCandidates);

            await DispatchUiAsync(() =>
            {
                _suppressRecompute = true;
                NameEntry.Text = string.IsNullOrWhiteSpace(result.ProductName) ? "상품" : result.ProductName;

                // 검증식 통과 (3개 후보 + 정상가-할인=판매가 매칭)일 때만 정상가·할인 자동 채움
                // 그 외에는 판매가만 채우고 정상가·할인은 비워둔다 (강제 할인 표시 방지)
                var trustDiscount = classification.Confidence >= 0.80 && !classification.RequiresUserConfirmation;

                if (trustDiscount)
                {
                    OriginalEntry.Text = classification.Prices.OriginalPrice > 0 ? classification.Prices.OriginalPrice.ToString("N0") : "";
                    DiscountEntry.Text = classification.Prices.DiscountAmount > 0 ? classification.Prices.DiscountAmount.ToString("N0") : "";
                }
                else
                {
                    OriginalEntry.Text = "";
                    DiscountEntry.Text = "";
                }
                SaleEntry.Text = classification.Prices.SalePrice > 0 ? classification.Prices.SalePrice.ToString("N0") : "";
                _suppressRecompute = false;

                var pct = (int)(classification.Confidence * 100);
                if (result.PriceCandidates.Count == 0)
                    ConfidenceLabel.Text = "⚠ 가격 인식 실패 — 직접 입력해주세요";
                else if (trustDiscount)
                    ConfidenceLabel.Text = $"✓ 검증식 통과 · 신뢰도 {pct}% (값 틀리면 직접 고치세요)";
                else
                    ConfidenceLabel.Text = $"판매가만 자동 채움 · 후보 {result.PriceCandidates.Count}개 (할인이 있으면 직접 입력)";

                if (result.PriceCandidates.Count > 0)
                {
                    // Show distinct, sorted descending
                    var distinct = result.PriceCandidates.Distinct().OrderByDescending(p => p).ToList();
                    CandidatesCollection.ItemsSource = distinct;
                    CandidatesPanel.IsVisible = true;
                }
                else
                {
                    CandidatesPanel.IsVisible = false;
                }

                LoadingPanel.IsVisible = false;
                ResultPanel.IsVisible = true;
                ShutterBtn.IsEnabled = true;
                GuideLabel.Text = "가격표를 사각형 안에 맞추고 셔터를 누르세요";
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScanPage.OnMediaCaptured EXCEPTION] {ex}");
            await DispatchUiAsync(async () =>
            {
                LoadingPanel.IsVisible = false;
                ShutterBtn.IsEnabled = true;
                GuideLabel.Text = "가격표를 사각형 안에 맞추고 셔터를 누르세요";
                await DisplayAlert("OCR 실패", ex.Message + "\n\n[" + ex.GetType().Name + "]", "확인");
            });
        }
    }

    private void OnCandidateTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Label label) return;
        if (label.BindingContext is not decimal value) return;
        var target = _lastFocusedPriceEntry ?? SaleEntry;
        target.Text = value.ToString("N0");
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NameEntry.Text) ? "상품" : NameEntry.Text.Trim();
        var orig = ParseWon(OriginalEntry.Text);
        var disc = ParseWon(DiscountEntry.Text);
        var sale = ParseWon(SaleEntry.Text);

        if (sale <= 0)
        {
            await DisplayAlert("판매가 필요", "판매가를 입력해주세요.", "확인");
            SaleEntry.Focus();
            return;
        }

        // If original missing, treat as no-discount
        if (orig <= 0) orig = sale;
        if (disc < 0) disc = 0;
        // Sanity: discount can't be larger than original
        if (disc >= orig) disc = 0;

        var item = new CartItem
        {
            Name = name,
            OriginalPrice = orig,
            DiscountAmount = disc,
            SalePrice = sale,
            Quantity = 1,
            Source = ItemSource.Ocr,
        };
        _currentCart.AddItem(item);

        await DisplayAlert(
            "✓ 담았어요",
            $"{item.Name}\n{item.SalePrice:N0}원\n\n현재 합산: {_currentCart.Cart.OriginalSubtotal:N0}원 / {_currentCart.Cart.Threshold:N0}원",
            "확인");
        await Navigation.PopAsync();
    }

    private Task DispatchUiAsync(Action action)
    {
        if (MainThread.IsMainThread) { action(); return Task.CompletedTask; }
        var tcs = new TaskCompletionSource();
        Dispatcher.Dispatch(() =>
        {
            try { action(); tcs.TrySetResult(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        return tcs.Task;
    }

    private Task DispatchUiAsync(Func<Task> action)
    {
        if (MainThread.IsMainThread) return action();
        var tcs = new TaskCompletionSource();
        Dispatcher.Dispatch(async () =>
        {
            try { await action(); tcs.TrySetResult(); }
            catch (Exception ex) { tcs.TrySetException(ex); }
        });
        return tcs.Task;
    }
}
