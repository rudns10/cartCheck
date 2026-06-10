using MartCart.AppFaithful.Controls;
using MartCart.AppFaithful.Services;
using Microsoft.Maui.Controls.Shapes;

namespace MartCart.AppFaithful.Pages;

public partial class StatsPage : ContentPage
{
    private string _period = "thismonth";

    public StatsPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Apply(_period);
    }

    private void OnPeriodTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Border tapped) return;
        var key = e.Parameter as string ?? "thismonth";
        _period = key;

        var chips = new[] { PeriodMonth, PeriodLast, Period3M };
        var primary = (Color)Application.Current!.Resources["PrimaryColor"];
        var divider = (Color)Application.Current!.Resources["DividerColor"];
        var muted = (Color)Application.Current!.Resources["MutedColor"];
        foreach (var c in chips)
        {
            c.Background = Colors.White;
            c.Stroke = divider;
            if (c.Content is Label l) l.TextColor = muted;
        }
        tapped.Background = primary;
        tapped.Stroke = primary;
        if (tapped.Content is Label tl) tl.TextColor = Colors.White;

        Apply(key);
    }

    private void Apply(string key)
    {
        var now = DateTimeOffset.Now;
        DateTimeOffset from, to;
        DateTimeOffset prevFrom, prevTo;

        switch (key)
        {
            case "thismonth":
                from = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
                to = from.AddMonths(1);
                prevFrom = from.AddMonths(-1); prevTo = from;
                break;
            case "lastmonth":
                to = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
                from = to.AddMonths(-1);
                prevFrom = from.AddMonths(-1); prevTo = from;
                break;
            case "3months":
                to = now;
                from = now.AddMonths(-3);
                prevFrom = from.AddMonths(-3); prevTo = from;
                break;
            default:
                from = to = prevFrom = prevTo = DateTimeOffset.MinValue;
                break;
        }

        var all = CartHistory.All();
        var period = all.Where(c => c.ClosedAt >= from && c.ClosedAt < to).ToList();
        var prev = all.Where(c => c.ClosedAt >= prevFrom && c.ClosedAt < prevTo).ToList();

        var spent = period.Sum(c => c.Sale);
        var prevSpent = prev.Sum(c => c.Sale);
        var saved = period.Sum(c => c.Saved);
        var prevSaved = prev.Sum(c => c.Saved);
        var origSum = period.Sum(c => c.Original);
        var savingsPct = origSum > 0 ? (double)saved / (double)origSum * 100.0 : 0;
        var count = period.Count;
        var avg = count > 0 ? spent / count : 0m;
        var reached = period.Count(c => c.ThresholdReached);
        var reachRate = count > 0 ? reached * 100 / count : 0;

        // Saved hero
        SavedAmountLabel.Text = saved.ToString("N0");
        SavedSubLabel.Text = saved > 0
            ? $"정상가 대비 {savingsPct:0.#}% 할인받으셨어요"
            : "아직 절약액이 없어요";
        if (saved > 0 && prevSaved > 0)
        {
            var diff = saved - prevSaved;
            SavedPillBorder.IsVisible = true;
            SavedPill.Text = diff > 0
                ? $"↗ 이전 대비 {diff:N0}원 더 절약"
                : diff < 0 ? $"↘ 이전 대비 {-diff:N0}원 감소" : "이전 대비 동일";
        }
        else SavedPillBorder.IsVisible = false;

        // Spent + trend vs previous period
        MetricSpentLabel.Text = spent.ToString("N0");
        if (prevSpent > 0)
        {
            var ratio = (spent - prevSpent) / prevSpent * 100;
            var danger = (Color)Application.Current!.Resources["DangerColor"];
            var accent = (Color)Application.Current!.Resources["AccentColor"];
            if (ratio > 0)
            {
                MetricSpentTrendLabel.Text = $"▲ {ratio:0.#}%";
                MetricSpentTrendLabel.TextColor = danger;
            }
            else if (ratio < 0)
            {
                MetricSpentTrendLabel.Text = $"▼ {-ratio:0.#}%";
                MetricSpentTrendLabel.TextColor = accent;
            }
            else
            {
                MetricSpentTrendLabel.Text = "동일";
                MetricSpentTrendLabel.TextColor = (Color)Application.Current!.Resources["MutedColor"];
            }
        }
        else
        {
            MetricSpentTrendLabel.Text = "—";
            MetricSpentTrendLabel.TextColor = (Color)Application.Current!.Resources["MutedColor"];
        }

        // Count + average gap
        MetricCountLabel.Text = count.ToString();
        if (count >= 2)
        {
            var ordered = period.OrderBy(c => c.ClosedAt).ToList();
            var gaps = new List<double>();
            for (int i = 1; i < ordered.Count; i++)
                gaps.Add((ordered[i].ClosedAt - ordered[i - 1].ClosedAt).TotalDays);
            var avgGap = gaps.Average();
            MetricGapLabel.FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "평균 ", TextColor = (Color)Application.Current!.Resources["MutedColor"] },
                    new Span { Text = $"{avgGap:0.#}일", FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["TextColor"] },
                    new Span { Text = " 간격", TextColor = (Color)Application.Current!.Resources["MutedColor"] },
                },
            };
        }
        else
        {
            MetricGapLabel.FormattedText = new FormattedString
            {
                Spans = { new Span { Text = count == 0 ? "기록 없음" : "1회 기록", TextColor = (Color)Application.Current!.Resources["MutedColor"] } },
            };
        }

        // Weekly trend (last 7 days)
        ApplyTrend(all);

        // Top 3 marts (within period)
        ApplyTop3(period);

        // Insight
        ApplyInsight(period, saved, savingsPct, count);
    }

    private void ClearTrend()
    {
        var bars = new[] { Bar0, Bar1, Bar2, Bar3, Bar4, Bar5, Bar6 };
        var labels = new[] { Day0Label, Day1Label, Day2Label, Day3Label, Day4Label, Day5Label, Day6Label };
        var divider = (Color)Application.Current!.Resources["DividerColor"];
        foreach (var b in bars) { b.HeightRequest = 6; b.Background = divider; }
        foreach (var l in labels) l.Text = "";
        TrendMaxLabel.Text = "";
        TrendAvgLabel.Text = "";
        TrendSubLabel.Text = "최근 7일";
    }

    private void ApplyTrend(IReadOnlyList<CompletedCart> all)
    {
        var today = DateTimeOffset.Now.Date;
        var bars = new[] { Bar0, Bar1, Bar2, Bar3, Bar4, Bar5, Bar6 };
        var labels = new[] { Day0Label, Day1Label, Day2Label, Day3Label, Day4Label, Day5Label, Day6Label };
        var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };

        // 7-day window ending today
        var sums = new decimal[7];
        for (int i = 0; i < 7; i++)
        {
            var day = today.AddDays(i - 6);
            sums[i] = all.Where(c => c.ClosedAt.Date == day).Sum(c => c.Sale);
            labels[i].Text = dayNames[(int)day.DayOfWeek];
        }
        // Make today's label bold/primary
        labels[6].FontAttributes = FontAttributes.Bold;
        labels[6].TextColor = (Color)Application.Current!.Resources["PrimaryColor"];

        var max = sums.Max();
        var muted = (Color)Application.Current!.Resources["Muted2Color"];
        var divider = (Color)Application.Current!.Resources["DividerColor"];
        var primaryBrush = (Brush)Application.Current!.Resources["PrimaryBrush"];
        var progressBrush = (Brush)Application.Current!.Resources["ProgressBrush"];

        for (int i = 0; i < 7; i++)
        {
            var pct = max > 0 ? (double)(sums[i] / max) : 0;
            bars[i].HeightRequest = sums[i] > 0 ? Math.Max(10, 90 * pct) : 6;
            if (sums[i] == 0) bars[i].Background = divider;
            else if (i == 6) bars[i].Background = primaryBrush;
            else bars[i].Background = progressBrush;
            // tap → toast
            bars[i].GestureRecognizers.Clear();
            var amt = sums[i];
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (amt > 0) await AppDialog.AlertAsync("지출", $"{amt:N0}원 지출");
                else await AppDialog.AlertAsync("지출 없음", "이 날은 장보지 않았어요.");
            };
            bars[i].GestureRecognizers.Add(tap);
        }

        var weekTotal = sums.Sum();
        var weekAvg = weekTotal / 7;
        TrendSubLabel.Text = $"최근 7일 · 합계 {weekTotal:N0}원";

        var maxIdx = Array.IndexOf(sums, max);
        if (max > 0)
        {
            TrendMaxLabel.FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = "최고: ", TextColor = (Color)Application.Current!.Resources["MutedColor"] },
                    new Span { Text = $"{dayNames[(int)today.AddDays(maxIdx - 6).DayOfWeek]}요일 {max:N0}원", FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["TextColor"] },
                },
            };
        }
        else TrendMaxLabel.Text = "이번 주 기록 없음";

        TrendAvgLabel.Text = $"평균 {weekAvg:N0}원/일";
    }

    private void ApplyTop3(IReadOnlyList<CompletedCart> period)
    {
        Top3Container.Children.Clear();
        var grouped = period
            .GroupBy(c => c.Mart)
            .Select(g => new
            {
                Mart = g.Key,
                Total = g.Sum(c => c.Sale),
                Count = g.Count(),
                Reached = g.Count(c => c.ThresholdReached),
            })
            .OrderByDescending(x => x.Total)
            .ToList();

        MartCountLabel.Text = grouped.Count > 0 ? $"전체 {grouped.Count}개 마트" : "";

        if (grouped.Count == 0)
        {
            var empty = new Label
            {
                Text = "기록이 없어요",
                FontSize = 13,
                TextColor = (Color)Application.Current!.Resources["MutedColor"],
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 12, 0, 12),
            };
            Top3Container.Children.Add(empty);
            return;
        }

        var top = grouped.Take(3).ToList();
        var maxTotal = top.Max(x => x.Total);
        var divider = (Color)Application.Current!.Resources["DividerColor"];

        for (int i = 0; i < top.Count; i++)
        {
            var entry = top[i];
            var rank = i + 1;

            var rankBg = rank switch
            {
                1 => (Brush)Application.Current!.Resources["PrimaryBrush"],
                2 => new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb("#94A3B8"), 0),
                        new GradientStop(Color.FromArgb("#6B7280"), 1),
                    }, new Point(0, 0), new Point(1, 1)),
                _ => new LinearGradientBrush(new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb("#CBD5E1"), 0),
                        new GradientStop(Color.FromArgb("#9CA3AF"), 1),
                    }, new Point(0, 0), new Point(1, 1)),
            };

            var row = new VerticalStackLayout { Spacing = 8 };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                },
                ColumnSpacing = 12,
            };
            var rankBorder = new Border
            {
                Background = rankBg,
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                WidthRequest = 32,
                HeightRequest = 32,
                Content = new Label { Text = rank.ToString(), FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
            };
            Grid.SetColumn(rankBorder, 0);

            var info = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
            info.Children.Add(new Label { Text = entry.Mart, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = (Color)Application.Current!.Resources["TextColor"] });
            var avg = entry.Count > 0 ? entry.Total / entry.Count : 0;
            var subText = entry.Reached > 0
                ? $"{entry.Count}회 · 평균 {avg:N0}원 · 목표 {entry.Reached}/{entry.Count}회 도달"
                : $"{entry.Count}회 · 평균 {avg:N0}원";
            info.Children.Add(new Label { Text = subText, FontSize = 11, TextColor = (Color)Application.Current!.Resources["MutedColor"] });
            Grid.SetColumn(info, 1);

            var price = new Label
            {
                Text = $"{entry.Total:N0}원",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = (Color)Application.Current!.Resources["TextColor"],
                VerticalOptions = LayoutOptions.Center,
            };
            Grid.SetColumn(price, 2);

            grid.Children.Add(rankBorder);
            grid.Children.Add(info);
            grid.Children.Add(price);

            // Bar
            var barTrack = new Border
            {
                Background = divider,
                Stroke = Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 2 },
                HeightRequest = 4,
            };
            var ratio = maxTotal > 0 ? (double)(entry.Total / maxTotal) : 0;
            var barFillColor = rank switch
            {
                1 => (Color)Application.Current!.Resources["PrimaryColor"],
                2 => Color.FromArgb("#94A3B8"),
                _ => Color.FromArgb("#CBD5E1"),
            };
            var barFillContainer = new Grid();
            var barFill = new BoxView { Color = barFillColor, HorizontalOptions = LayoutOptions.Start };
            barFillContainer.Children.Add(barFill);
            barTrack.Content = barFillContainer;
            // Set fill width via async ratio after layout
            barTrack.SizeChanged += (_, _) =>
            {
                if (barTrack.Width > 0) barFill.WidthRequest = barTrack.Width * ratio;
            };

            row.Children.Add(grid);
            row.Children.Add(barTrack);

            Top3Container.Children.Add(row);

            if (i < top.Count - 1)
            {
                Top3Container.Children.Add(new BoxView
                {
                    Color = (Color)Application.Current!.Resources["DividerSoftColor"],
                    HeightRequest = 1,
                });
            }
        }
    }

    private void ApplyInsight(IReadOnlyList<CompletedCart> period, decimal saved, double savingsPct, int count)
    {
        if (count == 0)
        {
            InsightBorder.IsVisible = false;
            return;
        }
        InsightBorder.IsVisible = true;
        InsightBodyLabel.Text = saved > 0
            ? $"이 기간 {saved:N0}원 절약 (정상가 대비 {savingsPct:0.#}%)"
            : $"이 기간 {count}회 장보기, 합계 {period.Sum(c => c.Sale):N0}원";
    }
}
