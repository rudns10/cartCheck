using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using MartCart.AppFaithful.Controls;
using MartCart.Domain.Contracts;
using MartCart.Domain.Entities;
using MartCart.Domain.Services;
using System.Diagnostics;

namespace MartCart.AppFaithful.Pages;

public partial class ScanPage : ContentPage
{
    private readonly IPriceOcr? _ocr;
    private readonly IPriceClassifier _classifier;
    private readonly ICurrentCart _currentCart;

    public ScanPage(IPriceClassifier classifier, ICurrentCart currentCart, IServiceProvider services)
    {
        InitializeComponent();
        _classifier = classifier;
        _currentCart = currentCart;
        _ocr = services.GetService<IPriceOcr>();
        _currentCart.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(RenderStrip);
    }

    private int _quantity = 1;
    private void OnQtyPlus(object? sender, TappedEventArgs e)
    {
        _quantity++;
        QtyLabel.Text = _quantity.ToString();
    }
    private void OnQtyMinus(object? sender, TappedEventArgs e)
    {
        if (_quantity <= 1) return;
        _quantity--;
        QtyLabel.Text = _quantity.ToString();
    }
    private void ResetQuantity()
    {
        _quantity = 1;
        QtyLabel.Text = "1";
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderStrip();
    }

    private void RenderStrip()
    {
        var cart = _currentCart.Cart;
        if (cart.Threshold > 0)
        {
            StripAmountLabel.FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = $"{cart.SaleSubtotal:N0}원", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Span { Text = $" / {cart.Threshold:N0}원", FontSize = 10, TextColor = Colors.White },
                },
            };
            var remaining = Math.Max(0, cart.Threshold - cart.SaleSubtotal);
            StripRemainingLabel.Text = remaining > 0 ? $"{remaining:N0}원 남음" : "✓ 도달";
            var ratio = Math.Min(1.0, (double)(cart.SaleSubtotal / cart.Threshold));
            StripProgressFill.WidthRequest = 120 * ratio;
        }
        else
        {
            StripAmountLabel.Text = $"{cart.SaleSubtotal:N0}원";
            StripRemainingLabel.Text = "목표 금액 미설정";
            StripProgressFill.WidthRequest = 0;
        }
    }

    private async void OnCloseClicked(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    private void OnCloseResult(object? sender, EventArgs e) => ResultPanel.IsVisible = false;

    // ===== Keyboard helpers =====
    private void OnDoneTapped(object? sender, TappedEventArgs e) => UnfocusAll();
    private void OnNameCompleted(object? sender, EventArgs e) => OriginalEntry.Focus();
    private void OnOriginalCompleted(object? sender, EventArgs e) => DiscountEntry.Focus();
    private void OnDiscountCompleted(object? sender, EventArgs e) => SaleEntry.Focus();
    private void OnSaleCompleted(object? sender, EventArgs e) => UnfocusAll();

    private void UnfocusAll()
    {
        NameEntry.Unfocus();
        OriginalEntry.Unfocus();
        DiscountEntry.Unfocus();
        SaleEntry.Unfocus();
    }

    // Format comma only on focus loss (avoids EmojiCompat crash with live edits)
    private void OnPriceUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry) return;
        var digits = new string((entry.Text ?? "").Where(char.IsDigit).ToArray());
        if (digits.Length == 0) { entry.Text = ""; return; }
        if (decimal.TryParse(digits, out var n))
        {
            entry.Text = n.ToString("N0");
        }
    }

    private async void OnShutterClicked(object? sender, TappedEventArgs e)
    {
        if (_ocr is null)
        {
            await AppDialog.AlertAsync("OCR 미지원", "이 플랫폼에서는 OCR이 지원되지 않습니다.");
            return;
        }
        try
        {
            GuideLabel.Text = "촬영 중…";
            await Camera.CaptureImage(CancellationToken.None);
        }
        catch (Exception ex)
        {
            await DispatchUiAsync(async () =>
            {
                await AppDialog.AlertAsync("오류", "촬영 실패: " + ex.Message);
                GuideLabel.Text = "상품과 가격표가 함께 보이도록 맞춰주세요";
            });
        }
    }

    private async void OnMediaCaptured(object? sender, MediaCapturedEventArgs e)
    {
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

            // 가이드 사각형 안쪽만 잘라서 OCR에 보냄
            var croppedBytes = CropToGuide(bytes);

            OcrResult result;
            using (var input = new MemoryStream(croppedBytes))
            {
                result = await _ocr.RecognizeAsync(input);
            }

            var classification = _classifier.Classify(result.PriceCandidates);
            var trustDiscount = classification.Confidence >= 0.80 && !classification.RequiresUserConfirmation;

            await DispatchUiAsync(() =>
            {
                NameEntry.Text = string.IsNullOrWhiteSpace(result.ProductName) ? "상품" : result.ProductName;

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

                var pct = (int)(classification.Confidence * 100);
                if (result.PriceCandidates.Count == 0)
                    ConfidenceLabel.Text = "⚠ 가격 인식 실패 — 직접 입력해주세요";
                else if (trustDiscount)
                    ConfidenceLabel.Text = $"✓ 검증식 통과 · 신뢰도 {pct}%";
                else
                    ConfidenceLabel.Text = $"판매가만 자동 채움 · 후보 {result.PriceCandidates.Count}개 (할인 있으면 직접 입력)";

                LoadingPanel.IsVisible = false;
                ResultPanel.IsVisible = true;
                GuideLabel.Text = "상품과 가격표가 함께 보이도록 맞춰주세요";
                ResetQuantity();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ScanPage OCR error] {ex}");
            await DispatchUiAsync(async () =>
            {
                LoadingPanel.IsVisible = false;
                GuideLabel.Text = "상품과 가격표가 함께 보이도록 맞춰주세요";
                await AppDialog.AlertAsync("OCR 실패", ex.Message);
            });
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NameEntry.Text) ? "상품" : NameEntry.Text.Trim();
        var orig = ParseWon(OriginalEntry.Text);
        var disc = ParseWon(DiscountEntry.Text);
        var sale = ParseWon(SaleEntry.Text);

        if (sale <= 0)
        {
            await AppDialog.AlertAsync("판매가 필요", "판매가를 입력해주세요.");
            return;
        }
        if (orig <= 0) orig = sale;
        if (disc < 0) disc = 0;
        if (disc >= orig) disc = 0;

        // ===== Duplicate detection (§6.2 F-14) =====
        var dup = FindDuplicate(name, sale);
        if (dup is not null)
        {
            var choice = await AppDialog.ChoiceAsync(
                $"이미 담은 상품이에요\n{dup.Name} · {dup.SalePrice:N0}원 × {dup.Quantity}개",
                new[] { $"수량 +{_quantity}", "별도로 담기" });

            if (string.IsNullOrEmpty(choice)) return;

            if (choice.StartsWith("수량 +"))
            {
                for (int i = 0; i < _quantity; i++) _currentCart.IncrementQuantity(dup.Id);
                await AppDialog.AlertAsync(
                    "✓ 수량 추가",
                    $"{dup.Name}\n{dup.Quantity + _quantity}개로 변경됨\n\n현재 합산: {_currentCart.Cart.SaleSubtotal:N0}원");
                await Shell.Current.GoToAsync("..");
                return;
            }
            // "별도로 담기" → fall through to normal add
        }

        var item = new CartItem
        {
            Name = name,
            OriginalPrice = orig,
            DiscountAmount = disc,
            SalePrice = sale,
            Quantity = _quantity,
            Source = ItemSource.Ocr,
        };
        _currentCart.AddItem(item);

        await AppDialog.AlertAsync(
            "✓ 담았어요",
            $"{name} × {_quantity}개\n{(sale * _quantity):N0}원\n\n현재 합산: {_currentCart.Cart.SaleSubtotal:N0}원");
        await Shell.Current.GoToAsync("..");
    }

    private CartItem? FindDuplicate(string name, decimal sale)
    {
        var normalized = NormalizeName(name);
        return _currentCart.Cart.Items.FirstOrDefault(i =>
            NormalizeName(i.Name ?? "") == normalized
            && Math.Abs(i.SalePrice - sale) <= 10m);
    }

    private static string NormalizeName(string s)
        => new string(s.ToLowerInvariant().Where(c => !char.IsWhiteSpace(c)).ToArray());

    private static decimal ParseWon(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, out var v) ? v : 0;
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

    // 캡처된 이미지의 중앙 약 80% × 60% 영역을 잘라서 반환.
    // EXIF 회전 정보를 먼저 적용해서 사용자가 본 방향과 동일하게 만든 뒤 크롭.
    // 화면 가이드 사각형은 비율 기준이라 디바이스 픽셀과 무관하게 동작.
    private static byte[] CropToGuide(byte[] bytes)
    {
#if ANDROID
        try
        {
            // 1) EXIF 회전 정보 추출
            int rotation;
            using (var exifStream = new MemoryStream(bytes))
            {
                var exif = new AndroidX.ExifInterface.Media.ExifInterface(exifStream);
                var orientation = exif.GetAttributeInt(
                    AndroidX.ExifInterface.Media.ExifInterface.TagOrientation,
                    (int)AndroidX.ExifInterface.Media.ExifInterface.OrientationNormal);
                rotation = orientation switch
                {
                    (int)AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate90 => 90,
                    (int)AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate180 => 180,
                    (int)AndroidX.ExifInterface.Media.ExifInterface.OrientationRotate270 => 270,
                    _ => 0,
                };
            }

            // 2) 비트맵 디코드 + 회전 적용
            using var raw = Android.Graphics.BitmapFactory.DecodeByteArray(bytes, 0, bytes.Length);
            if (raw is null) return bytes;

            Android.Graphics.Bitmap upright;
            if (rotation != 0)
            {
                var matrix = new Android.Graphics.Matrix();
                matrix.PostRotate(rotation);
                upright = Android.Graphics.Bitmap.CreateBitmap(raw, 0, 0, raw.Width, raw.Height, matrix, true);
            }
            else
            {
                upright = raw;
            }

            try
            {
                // 3) 중앙 80% × 60% 크롭
                const double widthRatio = 0.80;
                const double heightRatio = 0.60;

                var cropW = (int)(upright.Width * widthRatio);
                var cropH = (int)(upright.Height * heightRatio);
                var cropX = (upright.Width - cropW) / 2;
                var cropY = (upright.Height - cropH) / 2;

                if (cropW < 64 || cropH < 64) return bytes;

                using var cropped = Android.Graphics.Bitmap.CreateBitmap(upright, cropX, cropY, cropW, cropH);
                using var output = new MemoryStream();
                cropped.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 92, output);
                return output.ToArray();
            }
            finally
            {
                if (!ReferenceEquals(upright, raw)) upright.Dispose();
            }
        }
        catch { return bytes; }
#else
        return bytes;
#endif
    }
}
