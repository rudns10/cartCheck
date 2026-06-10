using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MartCart.AppFaithful.Pages;

public partial class MartListPage : ContentPage
{
    public MartListPage()
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
        ListLayout.Children.Clear();
        var marts = MartService.GetAll();
        if (marts.Count == 0)
        {
            ListLayout.Children.Add(new Label
            {
                Text = "등록된 마트가 없습니다. 추가 버튼으로 등록하세요.",
                FontSize = 12,
                TextColor = (Color)Application.Current!.Resources["MutedColor"],
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0),
            });
            return;
        }

        foreach (var m in marts)
            ListLayout.Children.Add(BuildRow(m));
    }

    private Border BuildRow(Mart m)
    {
        var surface = (Color)Application.Current!.Resources["SurfaceColor"];
        var text = (Color)Application.Current!.Resources["TextColor"];
        var muted = (Color)Application.Current!.Resources["MutedColor"];
        var danger = (Color)Application.Current!.Resources["DangerColor"];

        var grid = new Grid
        {
            Padding = new Thickness(18, 14),
            ColumnSpacing = 12,
            MinimumHeightRequest = 60,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
            },
        };

        var info = new VerticalStackLayout { Spacing = 3, VerticalOptions = LayoutOptions.Center };
        info.Children.Add(new Label { Text = m.Name, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = text });
        var subText = m.DefaultDiscount > 0
            ? $"{m.DefaultThreshold:N0}원 달성 시 {m.DefaultDiscount:N0}원 할인"
            : $"목표 금액 {m.DefaultThreshold:N0}원";
        info.Children.Add(new Label { Text = subText, FontSize = 11, TextColor = muted });
        Grid.SetColumn(info, 0);
        grid.Children.Add(info);
        var infoTap = new TapGestureRecognizer();
        infoTap.Tapped += async (_, _) => await EditAsync(m);
        info.GestureRecognizers.Add(infoTap);

        var editLabel = new Label
        {
            Text = "수정",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = (Color)Application.Current!.Resources["PrimaryColor"],
            VerticalOptions = LayoutOptions.Center,
        };
        var editTap = new TapGestureRecognizer();
        editTap.Tapped += async (_, _) => await EditAsync(m);
        editLabel.GestureRecognizers.Add(editTap);
        Grid.SetColumn(editLabel, 1);
        grid.Children.Add(editLabel);

        var delLabel = new Label
        {
            Text = "삭제",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = danger,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        var delTap = new TapGestureRecognizer();
        delTap.Tapped += async (_, _) => await DeleteAsync(m);
        delLabel.GestureRecognizers.Add(delTap);
        Grid.SetColumn(delLabel, 2);
        grid.Children.Add(delLabel);

        return new Border
        {
            Background = new SolidColorBrush(surface),
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(14) },
            Content = grid,
            Shadow = new Shadow { Brush = Color.FromArgb("#0A1F33"), Offset = new Point(0, 2), Radius = 6, Opacity = 0.06f },
        };
    }

    private async Task EditAsync(Mart m)
    {
        var name = await AppDialog.PromptAsync("마트 이름", "마트 이름을 입력하세요", initial: m.Name, ok: "다음", maxLength: 20);
        if (string.IsNullOrWhiteSpace(name)) return;
        var thrStr = await AppDialog.PromptAsync("목표 금액", "할인이 적용되는 기준 금액(원)을 입력하세요",
            initial: m.DefaultThreshold.ToString(), ok: "다음", keyboard: Keyboard.Numeric, maxLength: 8);
        if (string.IsNullOrWhiteSpace(thrStr)) return;
        if (!int.TryParse(new string(thrStr.Where(char.IsDigit).ToArray()), out var threshold) || threshold <= 0)
        {
            await AppDialog.AlertAsync("값 오류", "목표 금액은 0보다 큰 숫자여야 합니다.");
            return;
        }
        var discStr = await AppDialog.PromptAsync("할인 금액", "목표 도달 시 받을 할인 금액(원). 없으면 0",
            initial: m.DefaultDiscount.ToString(), ok: "저장", keyboard: Keyboard.Numeric, maxLength: 8);
        if (discStr is null) return;
        int.TryParse(new string(discStr.Where(char.IsDigit).ToArray()), out var discount);
        if (discount < 0) discount = 0;
        MartService.Update(m.Id, name.Trim(), threshold, discount);
        Render();
    }

    private async Task DeleteAsync(Mart m)
    {
        var ok = await AppDialog.AlertAsync("마트 삭제", $"'{m.Name}'을(를) 삭제할까요? 기존 기록은 유지됩니다.", "삭제", "취소");
        if (!ok) return;
        MartService.Delete(m.Id);
        Render();
    }

    private async void OnAddTapped(object? sender, TappedEventArgs e)
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
        Render();
    }
}
