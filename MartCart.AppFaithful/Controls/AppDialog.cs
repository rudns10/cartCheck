using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Maui.Views;
using Microsoft.Maui.Controls.Shapes;

namespace MartCart.AppFaithful.Controls;

public static class AppDialog
{
    public static Task<bool> AlertAsync(string title, string message, string ok = "확인", string? cancel = null)
        => ShowAsync<bool>(page => new ConfirmPopup(title, message, ok, cancel), page: null);

    public static Task<string?> PromptAsync(string title, string message, string? initial = null,
        string placeholder = "", string ok = "저장", string cancel = "취소",
        Keyboard? keyboard = null, int maxLength = 100)
        => ShowAsync<string?>(page => new PromptPopup(title, message, initial, placeholder, ok, cancel, keyboard ?? Keyboard.Default, maxLength), page: null);

    public static Task<string?> ChoiceAsync(string title, IReadOnlyList<string> options, string cancel = "취소", string? destructive = null)
        => ShowAsync<string?>(page => new ChoicePopup(title, options, cancel, destructive), page: null);

    private static async Task<T> ShowAsync<T>(Func<Page, Popup> factory, Page? page)
    {
        page ??= GetCurrentPage();
        if (page is null) return default!;
        var popup = factory(page);
        var result = await page.ShowPopupAsync(popup);
        if (result is T t) return t;
        return default!;
    }

    private static Page? GetCurrentPage()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        var page = window?.Page;
        while (page is NavigationPage np && np.CurrentPage is not null) page = np.CurrentPage;
        if (page is Shell shell && shell.CurrentPage is not null) return shell.CurrentPage;
        return page;
    }

    // ====================================================================
    // Base styled popup
    // ====================================================================
    private abstract class BasePopup : Popup
    {
        protected BasePopup()
        {
            Color = Colors.Transparent;
            CanBeDismissedByTappingOutsideOfPopup = true;
        }

        protected static Color Res(string key)
            => Application.Current!.Resources.TryGetValue(key, out var v) && v is Color c ? c : Colors.Gray;

        protected Border BuildCard(View content)
        {
            return new Border
            {
                Background = new SolidColorBrush(Res("SurfaceColor")),
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(20) },
                Padding = new Thickness(22, 20),
                WidthRequest = 320,
                Shadow = new Shadow { Brush = Color.FromArgb("#000000"), Offset = new Point(0, 12), Radius = 28, Opacity = 0.25f },
                Content = content,
            };
        }

        protected static Label TitleLabel(string text) => new()
        {
            Text = text,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Res("TextColor"),
        };

        protected static Label BodyLabel(string text) => new()
        {
            Text = text,
            FontSize = 13,
            TextColor = Res("MutedColor"),
            LineHeight = 1.45,
        };

        protected Border PrimaryButton(string text, EventHandler<TappedEventArgs> tapped)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            };
            var border = new Border
            {
                Background = (Brush)Application.Current!.Resources["PrimaryBrush"],
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                HeightRequest = 46,
                Content = label,
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += tapped;
            border.GestureRecognizers.Add(tap);
            return border;
        }

        protected Border DestructiveButton(string text, EventHandler<TappedEventArgs> tapped)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            };
            var border = new Border
            {
                Background = new SolidColorBrush(Res("DangerColor")),
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                HeightRequest = 46,
                Content = label,
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += tapped;
            border.GestureRecognizers.Add(tap);
            return border;
        }

        protected Border SecondaryButton(string text, EventHandler<TappedEventArgs> tapped)
        {
            var label = new Label
            {
                Text = text,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Res("TextColor"),
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                InputTransparent = true,
            };
            var border = new Border
            {
                Background = new SolidColorBrush(Res("DividerSoftColor")),
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
                HeightRequest = 46,
                Content = label,
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += tapped;
            border.GestureRecognizers.Add(tap);
            return border;
        }
    }

    // ====================================================================
    // Confirm popup
    // ====================================================================
    private sealed class ConfirmPopup : BasePopup
    {
        public ConfirmPopup(string title, string message, string ok, string? cancel)
        {
            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(TitleLabel(title));
            if (!string.IsNullOrEmpty(message)) stack.Children.Add(BodyLabel(message));

            var buttons = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 16, 0, 0) };
            if (cancel is not null)
            {
                buttons.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) };
                var cancelBtn = SecondaryButton(cancel, (_, _) => CloseAsync(false));
                Grid.SetColumn(cancelBtn, 0);
                buttons.Children.Add(cancelBtn);
                var okBtn = PrimaryButton(ok, (_, _) => CloseAsync(true));
                Grid.SetColumn(okBtn, 1);
                buttons.Children.Add(okBtn);
            }
            else
            {
                buttons.ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star) };
                var okBtn = PrimaryButton(ok, (_, _) => CloseAsync(true));
                Grid.SetColumn(okBtn, 0);
                buttons.Children.Add(okBtn);
            }
            stack.Children.Add(buttons);
            Content = BuildCard(stack);
        }
    }

    // ====================================================================
    // Prompt popup
    // ====================================================================
    private sealed class PromptPopup : BasePopup
    {
        public PromptPopup(string title, string message, string? initial, string placeholder, string ok, string cancel, Keyboard keyboard, int maxLength)
        {
            var entry = new Entry
            {
                Text = initial ?? "",
                Placeholder = placeholder,
                Keyboard = keyboard,
                MaxLength = maxLength,
                FontSize = 16,
                TextColor = Res("TextColor"),
                PlaceholderColor = Res("Muted2Color"),
                BackgroundColor = Colors.Transparent,
            };
            var inputBorder = new Border
            {
                Background = new SolidColorBrush(Res("DividerSoftColor")),
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
                Padding = new Thickness(12, 4),
                Content = entry,
            };

            var stack = new VerticalStackLayout { Spacing = 10 };
            stack.Children.Add(TitleLabel(title));
            if (!string.IsNullOrEmpty(message)) stack.Children.Add(BodyLabel(message));
            stack.Children.Add(inputBorder);

            var buttons = new Grid
            {
                ColumnSpacing = 10,
                Margin = new Thickness(0, 14, 0, 0),
                ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) },
            };
            var cancelBtn = SecondaryButton(cancel, (_, _) => CloseAsync((string?)null));
            Grid.SetColumn(cancelBtn, 0);
            buttons.Children.Add(cancelBtn);
            var okBtn = PrimaryButton(ok, (_, _) => CloseAsync(entry.Text ?? ""));
            Grid.SetColumn(okBtn, 1);
            buttons.Children.Add(okBtn);
            stack.Children.Add(buttons);

            entry.Completed += (_, _) => CloseAsync(entry.Text ?? "");

            Content = BuildCard(stack);
        }
    }

    // ====================================================================
    // Choice popup (action sheet replacement)
    // ====================================================================
    private sealed class ChoicePopup : BasePopup
    {
        public ChoicePopup(string title, IReadOnlyList<string> options, string cancel, string? destructive)
        {
            var stack = new VerticalStackLayout { Spacing = 8 };
            stack.Children.Add(TitleLabel(title));

            foreach (var opt in options)
            {
                var row = SecondaryButton(opt, (_, _) => CloseAsync((string?)opt));
                stack.Children.Add(row);
            }
            if (!string.IsNullOrEmpty(destructive))
            {
                var row = DestructiveButton(destructive, (_, _) => CloseAsync((string?)destructive));
                stack.Children.Add(row);
            }
            var cancelBtn = SecondaryButton(cancel, (_, _) => CloseAsync((string?)null));
            cancelBtn.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(cancelBtn);

            Content = BuildCard(stack);
        }
    }
}
