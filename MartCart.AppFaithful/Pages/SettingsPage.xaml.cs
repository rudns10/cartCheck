using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful.Pages;

public partial class SettingsPage : ContentPage
{
    private static readonly string[] LockTimes = { "1분", "5분", "15분", "1시간", "안 함" };
    private int _lockIdx = 1;

    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RenderProfile();
        RenderPinStatus();
        if (BioToggle is not null)
        {
            _suppressBioToggle = true;
            BioToggle.IsToggled = BiometricService.Enabled;
            _suppressBioToggle = false;
        }
        if (MartCountLabel is not null)
            MartCountLabel.Text = $"{MartService.GetAll().Count}개";
        if (ThemeLabel is not null)
            ThemeLabel.Text = ThemeService.Label(ThemeService.Current);
    }

    private async void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        var choice = await AppDialog.ChoiceAsync("테마 선택", new[] { "시스템 설정", "라이트", "다크" });
        ThemeMode? mode = choice switch
        {
            "시스템 설정" => ThemeMode.System,
            "라이트" => ThemeMode.Light,
            "다크" => ThemeMode.Dark,
            _ => null,
        };
        if (mode is null) return;
        if (mode == ThemeService.Current) return;
        ThemeService.Set(mode.Value);

        // StaticResource bindings don't refresh; rebuild the shell to apply.
        if (Application.Current is App app)
        {
            var window = app.Windows.FirstOrDefault();
            if (window is not null)
            {
                var shell = new AppShell();
                window.Page = shell;
                await shell.GoToAsync("//SettingsPage");
            }
        }
    }

    private bool _suppressBioToggle;
    private async void OnBioToggled(object? sender, ToggledEventArgs e)
    {
        if (_suppressBioToggle) return;
        if (!e.Value)
        {
            BiometricService.SetEnabled(false);
            return;
        }

        // Enable flow: require PIN to be set first
        if (!PinService.IsSet)
        {
            await AppDialog.AlertAsync("PIN 먼저 설정", "생체 인증은 PIN을 먼저 설정한 뒤에 활성화할 수 있어요.");
            _suppressBioToggle = true; BioToggle.IsToggled = false; _suppressBioToggle = false;
            return;
        }

        var available = await BiometricService.IsAvailableAsync();
        if (!available)
        {
            await AppDialog.AlertAsync("사용 불가", "이 기기에서 생체 인증을 사용할 수 없거나 등록된 지문/Face ID가 없습니다. OS 설정에서 먼저 등록해주세요.");
            _suppressBioToggle = true; BioToggle.IsToggled = false; _suppressBioToggle = false;
            return;
        }

        var ok = await BiometricService.AuthenticateAsync("생체 인증을 등록합니다");
        if (!ok)
        {
            _suppressBioToggle = true; BioToggle.IsToggled = false; _suppressBioToggle = false;
            return;
        }

        BiometricService.SetEnabled(true);
        await AppDialog.AlertAsync("✓ 생체 인증 활성화", "이제 잠금화면에서 지문/Face ID로 풀 수 있어요.");
    }

    private void RenderProfile()
    {
        if (UserNameLabel is null) return;
        UserNameLabel.Text = UserProfile.Name;
        var since = UserProfile.Since.LocalDateTime;
        UserSinceLabel.Text = $"{since.Year}년 {since.Month}월 {since.Day}일부터 카트체크 사용 중";

        var history = CartHistory.All();
        UserCartCountSpan.Text = history.Count.ToString();
        var totalSaved = history.Sum(c => c.Saved);
        UserSavedSpan.Text = $"{totalSaved:N0}원";
    }

    private async void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        var newName = await AppDialog.PromptAsync("이름 변경", "표시할 이름을 입력하세요",
            initial: UserProfile.Name, maxLength: 20);
        if (string.IsNullOrWhiteSpace(newName)) return;
        UserProfile.Name = newName;
        RenderProfile();
    }

    private void RenderPinStatus()
    {
        if (PinStatusLabel is null) return;
        if (PinService.IsSet)
        {
            var when = PinService.LastSetAt?.LocalDateTime.ToString("M월 d일") ?? "?";
            PinStatusLabel.Text = $"설정됨 · 마지막 변경 {when}";
            PinTitleLabel.Text = "PIN 변경";
        }
        else
        {
            PinStatusLabel.Text = "아직 설정되지 않음 · 탭해서 설정";
            PinTitleLabel.Text = "PIN 설정";
        }
    }

    private async void OnPinChangeTapped(object? sender, TappedEventArgs e)
    {
        var isFirstTime = !PinService.IsSet;

        if (PinService.IsSet)
        {
            var current = await AppDialog.PromptAsync("기존 PIN 확인", "현재 PIN을 입력해주세요",
                placeholder: "4자리 숫자", ok: "확인", keyboard: Keyboard.Numeric, maxLength: 4);
            if (string.IsNullOrEmpty(current)) return;
            if (!PinService.Verify(current))
            {
                await AppDialog.AlertAsync("불일치", "기존 PIN이 일치하지 않습니다.");
                return;
            }
        }

        var newPin = await AppDialog.PromptAsync(
            isFirstTime ? "PIN 설정" : "새 PIN",
            "잠금 해제에 사용할 4자리 숫자를 입력하세요",
            placeholder: "예: 1234", ok: "다음", keyboard: Keyboard.Numeric, maxLength: 4);
        if (string.IsNullOrEmpty(newPin)) return;
        var digits = new string(newPin.Where(char.IsDigit).ToArray());
        if (digits.Length != 4)
        {
            await AppDialog.AlertAsync("값 오류", "PIN은 4자리 숫자여야 합니다.");
            return;
        }

        var confirm = await AppDialog.PromptAsync("확인", "같은 PIN을 한 번 더 입력하세요",
            placeholder: "다시 입력", ok: "저장", keyboard: Keyboard.Numeric, maxLength: 4);
        if (string.IsNullOrEmpty(confirm)) return;
        if (confirm != digits)
        {
            await AppDialog.AlertAsync("불일치", "두 번 입력한 PIN이 다릅니다.");
            return;
        }

        PinService.Set(digits);
        RenderPinStatus();

        // 첫 설정 시 — 생체 인증을 함께 등록 (PIN 분실 시 복구용)
        if (isFirstTime)
        {
            var available = await BiometricService.IsAvailableAsync();
            if (available)
            {
                await AppDialog.AlertAsync(
                    "생체 인증 등록",
                    "PIN을 잊어버렸을 때 데이터를 잃지 않고 재설정할 수 있도록 생체 인증을 함께 등록할게요.",
                    "다음");
                var bioOk = await BiometricService.AuthenticateAsync("PIN 복구용 생체 인증을 등록합니다");
                if (bioOk)
                {
                    BiometricService.SetEnabled(true);
                    if (BioToggle is not null)
                    {
                        _suppressBioToggle = true;
                        BioToggle.IsToggled = true;
                        _suppressBioToggle = false;
                    }
                    await AppDialog.AlertAsync("✓ 저장됨", "PIN과 생체 인증이 안전하게 저장되었어요.");
                    return;
                }
                await AppDialog.AlertAsync(
                    "⚠ 생체 인증 미등록",
                    "생체 인증이 등록되지 않았어요. PIN 분실 시 데이터 초기화만 가능합니다.\n나중에 설정에서 다시 활성화할 수 있어요.");
                return;
            }
            await AppDialog.AlertAsync(
                "✓ 저장됨",
                "PIN이 저장되었어요. 이 기기는 생체 인증을 지원하지 않아 PIN 분실 시 데이터 초기화만 가능합니다.");
            return;
        }

        await AppDialog.AlertAsync("✓ 저장됨", "PIN이 안전하게 저장되었어요.");
    }

    private async void OnLockTimeTapped(object? sender, TappedEventArgs e)
    {
        var pick = await AppDialog.ChoiceAsync("자동 잠금 시간", LockTimes);
        if (string.IsNullOrEmpty(pick)) return;
        var idx = Array.IndexOf(LockTimes, pick);
        if (idx < 0) return;
        _lockIdx = idx;
        LockTimeLabel.Text = LockTimes[_lockIdx];
    }

    private async void OnMartListTapped(object? sender, TappedEventArgs e)
        => await Navigation.PushAsync(new MartListPage());

    private async void OnMartAddTapped(object? sender, TappedEventArgs e)
    {
        var name = await AppDialog.PromptAsync("마트 추가", "새 마트 이름을 입력하세요",
            placeholder: "예: 이마트 트레이더스", ok: "다음", maxLength: 20);
        if (string.IsNullOrWhiteSpace(name)) return;
        var thrStr = await AppDialog.PromptAsync("목표 금액", "할인이 적용되는 기준 금액(원)을 입력하세요",
            placeholder: "예: 50000", ok: "다음", keyboard: Keyboard.Numeric, maxLength: 8);
        if (string.IsNullOrWhiteSpace(thrStr)) return;
        if (!int.TryParse(new string(thrStr.Where(char.IsDigit).ToArray()), out var threshold) || threshold <= 0)
        {
            await AppDialog.AlertAsync("값 오류", "목표 금액은 0보다 큰 숫자여야 합니다.");
            return;
        }
        var discStr = await AppDialog.PromptAsync("할인 금액", "목표 도달 시 받을 할인 금액(원). 없으면 0",
            placeholder: "예: 5000", ok: "저장", keyboard: Keyboard.Numeric, maxLength: 8);
        if (discStr is null) return;
        int.TryParse(new string(discStr.Where(char.IsDigit).ToArray()), out var discount);
        if (discount < 0) discount = 0;
        MartService.Add(name.Trim(), threshold, discount);
        if (MartCountLabel is not null) MartCountLabel.Text = $"{MartService.GetAll().Count}개";
        await AppDialog.AlertAsync("✓ 추가됨", $"'{name.Trim()}'이(가) 추가되었습니다.");
    }

    private async void OnPrivacyTapped(object? sender, TappedEventArgs e)
        => await Navigation.PushAsync(new PrivacyPage());

    private async void OnDeleteAllTapped(object? sender, TappedEventArgs e)
    {
        var ok = await AppDialog.AlertAsync("모든 데이터 삭제", "장바구니·기록·PIN·설정이 모두 영구 삭제됩니다. 계속할까요?", "삭제", "취소");
        if (!ok) return;
        CartHistory.Clear();
        PinService.Clear();
        Preferences.Default.Clear();
        RenderProfile();
        RenderPinStatus();
        await AppDialog.AlertAsync("완료", "모든 데이터가 삭제되었습니다.");
    }
}
