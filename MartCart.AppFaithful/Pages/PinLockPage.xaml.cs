using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MartCart.AppFaithful.Pages;

public partial class PinLockPage : ContentPage
{
    private string _entered = "";
    private readonly Action _onSuccess;
    private System.Threading.CancellationTokenSource? _countdownCts;

    public PinLockPage(Action onSuccess)
    {
        InitializeComponent();
        _onSuccess = onSuccess;
    }

    private bool _biometricTried;
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        RedrawDots();
        CheckLockout();

        if (BiometricService.Enabled && !LockoutOverlay.IsVisible && !_biometricTried)
        {
            _biometricTried = true;
            await Task.Delay(300);
            await TryBiometric();
        }
    }

    private async Task TryBiometric()
    {
        if (!BiometricService.Enabled) return;
        var available = await BiometricService.IsAvailableAsync();
        if (!available) return;
        var result = await BiometricService.AuthenticateDetailedAsync("카트체크 잠금 해제");
        if (result == BiometricResult.Success)
        {
            PinService.ResetFailures();
            _onSuccess();
        }
        // Fallback or Failed: just let the PIN keypad stay visible
    }

    private const int PinLength = 4;
    private Ellipse[] Dots => new[] { D0, D1, D2, D3 };

    private void RedrawDots()
    {
        var dots = Dots;
        var divider = (Color)Application.Current!.Resources["DividerColor"];
        var primary = (Color)Application.Current!.Resources["PrimaryColor"];
        for (int i = 0; i < dots.Length; i++)
        {
            if (i < _entered.Length)
            {
                dots[i].Fill = new SolidColorBrush(primary);
                dots[i].Stroke = new SolidColorBrush(primary);
            }
            else
            {
                dots[i].Fill = Colors.Transparent;
                dots[i].Stroke = new SolidColorBrush(divider);
            }
        }
    }

    private bool _verifying;
    private async void OnKeyTapped(object? sender, TappedEventArgs e)
    {
        if (LockoutOverlay.IsVisible) return;
        if (_verifying) return;
        if (_entered.Length >= PinLength) return;

        _entered += e.Parameter as string ?? "";
        RedrawDots();

        if (_entered.Length == PinLength)
        {
            _verifying = true;
            var ok = await PinService.VerifyAsync(_entered);
            _verifying = false;

            if (ok)
            {
                PinService.ResetFailures();
                _onSuccess();
                return;
            }
            await OnWrongPin();
        }
    }

    private void OnDeleteTapped(object? sender, TappedEventArgs e)
    {
        if (_entered.Length == 0) return;
        _entered = _entered[..^1];
        RedrawDots();
        SubLabel.TextColor = (Color)Application.Current!.Resources["MutedColor"];
        TitleLabel.TextColor = (Color)Application.Current!.Resources["TextColor"];
    }

    private async Task OnWrongPin()
    {
        PinService.RecordFailure();
        // shake
        await DotsRow.TranslateTo(-10, 0, 60);
        await DotsRow.TranslateTo(10, 0, 60);
        await DotsRow.TranslateTo(-8, 0, 60);
        await DotsRow.TranslateTo(8, 0, 60);
        await DotsRow.TranslateTo(0, 0, 60);

        var remain = 5 - PinService.FailureCount;
        if (remain > 0)
        {
            TitleLabel.Text = $"PIN이 일치하지 않아요 ({PinService.FailureCount}/5)";
            TitleLabel.TextColor = (Color)Application.Current!.Resources["DangerColor"];
            SubLabel.Text = $"남은 시도 {remain}회 · 5회 실패 시 30초 잠금";
        }
        _entered = "";
        RedrawDots();
        CheckLockout();
    }

    private void CheckLockout()
    {
        var until = PinService.LockoutUntil;
        if (until is null)
        {
            LockoutOverlay.IsVisible = false;
            return;
        }
        LockoutOverlay.IsVisible = true;
        StartCountdown(until.Value);
    }

    private async void StartCountdown(DateTimeOffset until)
    {
        _countdownCts?.Cancel();
        _countdownCts = new System.Threading.CancellationTokenSource();
        var token = _countdownCts.Token;
        while (!token.IsCancellationRequested)
        {
            var remain = (int)Math.Max(0, (until - DateTimeOffset.UtcNow).TotalSeconds);
            CountdownLabel.Text = remain.ToString();
            if (remain <= 0)
            {
                PinService.ResetFailures();
                LockoutOverlay.IsVisible = false;
                TitleLabel.Text = "PIN을 입력하세요";
                TitleLabel.TextColor = (Color)Application.Current!.Resources["TextColor"];
                SubLabel.Text = "잠금 해제하려면 4~6자리 PIN을 입력하세요";
                return;
            }
            try { await Task.Delay(1000, token); }
            catch (TaskCanceledException) { return; }
        }
    }

    private async void OnForgotTapped(object? sender, TappedEventArgs e)
    {
        var bioEnabled = BiometricService.Enabled && await BiometricService.IsAvailableAsync();

        if (bioEnabled)
        {
            var proceed = await AppDialog.AlertAsync(
                "PIN을 잊으셨나요?",
                "생체 인증으로 본인 확인 후 PIN을 다시 설정할 수 있어요. 기존 데이터는 유지됩니다.",
                "생체 인증으로 재설정",
                "취소");
            if (!proceed) return;

            var ok = await BiometricService.AuthenticateAsync("PIN 재설정을 위한 본인 확인");
            if (!ok) return;

            var newPin = await AppDialog.PromptAsync(
                "새 PIN", "잠금 해제에 사용할 4자리 숫자를 입력하세요",
                placeholder: "예: 1234", ok: "다음", keyboard: Keyboard.Numeric, maxLength: 4);
            if (string.IsNullOrEmpty(newPin)) return;
            var digits = new string(newPin.Where(char.IsDigit).ToArray());
            if (digits.Length != 4)
            {
                await AppDialog.AlertAsync("값 오류", "PIN은 4자리 숫자여야 합니다.");
                return;
            }

            var confirm = await AppDialog.PromptAsync(
                "확인", "같은 PIN을 한 번 더 입력하세요",
                placeholder: "다시 입력", ok: "저장", keyboard: Keyboard.Numeric, maxLength: 4);
            if (string.IsNullOrEmpty(confirm) || confirm != digits)
            {
                await AppDialog.AlertAsync("불일치", "두 번 입력한 PIN이 다릅니다.");
                return;
            }

            PinService.Set(digits);
            PinService.ResetFailures();
            await AppDialog.AlertAsync("✓ 변경됨", "새 PIN으로 잠금 해제됩니다.");
            _onSuccess();
            return;
        }

        // 생체 인증 미등록 → 데이터 초기화 fallback
        var wipe = await AppDialog.AlertAsync(
            "PIN을 잊으셨나요?",
            "생체 인증이 등록되어 있지 않습니다. PIN을 재설정하려면 저장된 모든 데이터(장바구니·기록·설정)가 삭제됩니다. 계속할까요?",
            "PIN 초기화",
            "취소");
        if (!wipe) return;
        Services.CartHistory.Clear();
        PinService.Clear();
        Preferences.Default.Clear();
        _onSuccess();
    }
}
