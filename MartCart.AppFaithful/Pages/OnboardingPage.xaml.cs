using MartCart.AppFaithful.Services;

namespace MartCart.AppFaithful.Pages;

public partial class OnboardingPage : ContentPage
{
    public sealed record Slide(string Emoji, string Title, string Subtitle);

    public OnboardingPage()
    {
        InitializeComponent();
        Carousel.ItemsSource = BuildSlides();
        Carousel.PositionChanged += (_, e) => UpdateButtons(e.CurrentPosition);
        UpdateButtons(0);
    }

    private static List<Slide> BuildSlides() => new()
    {
        new("🛒",
            "카트체크에 오신 걸 환영해요",
            "마트에서 장 볼 때 가격을 추적하고\n할인 목표 금액까지 얼마나 남았는지 알려줘요."),
        new("📷",
            "가격표를 그냥 찍기만 하면 끝",
            "카메라로 가격표를 비추면\n상품명과 가격을 자동으로 인식해줍니다."),
        new("🎯",
            "마트별 목표 금액 설정",
            "이마트 5만원, 코스트코 10만원 등\n할인이 적용되는 기준을 미리 정해두세요."),
        new("📊",
            "장보기 기록과 통계",
            "장보기를 마치면 자동으로 기록되고\n누적 절약 금액과 마트별 통계를 볼 수 있어요."),
    };

    private void UpdateButtons(int index)
    {
        var slides = (List<Slide>?)Carousel.ItemsSource;
        if (slides is null) return;
        var isLast = index >= slides.Count - 1;
        NextLabel.Text = isLast ? "시작하기" : "다음 →";
        SkipLabel.IsVisible = !isLast;
    }

    private void OnNextTapped(object? sender, TappedEventArgs e)
    {
        var slides = (List<Slide>?)Carousel.ItemsSource;
        if (slides is null) return;
        var pos = Carousel.Position;
        if (pos >= slides.Count - 1) { Finish(); return; }
        Carousel.Position = pos + 1;
    }

    private void OnSkipTapped(object? sender, TappedEventArgs e) => Finish();

    private void Finish()
    {
        OnboardingService.Seen = true;
        if (Application.Current is not App app) return;
        var window = app.Windows.FirstOrDefault();
        if (window is null) return;
        window.Page = PinService.IsSet
            ? new PinLockPage(onSuccess: () => window.Page = new AppShell())
            : new AppShell();
    }
}
