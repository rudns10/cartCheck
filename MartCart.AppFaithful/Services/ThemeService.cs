namespace MartCart.AppFaithful.Services;

public enum ThemeMode { System, Light, Dark }

public static class ThemeService
{
    private const string Key = "martcart.theme";
    private static bool _wired;
    private static Dictionary<string, object>? _lightSnapshot;

    private static readonly Dictionary<string, Color> DarkColors = new()
    {
        ["BgColor"]          = Color.FromArgb("#0B1220"),
        ["SurfaceColor"]     = Color.FromArgb("#111827"),
        ["Surface2Color"]    = Color.FromArgb("#0F172A"),
        ["PrimaryColor"]     = Color.FromArgb("#3B82F6"),
        ["Primary2Color"]    = Color.FromArgb("#60A5FA"),
        ["PrimaryDeepColor"] = Color.FromArgb("#1E3A5F"),
        ["AccentColor"]      = Color.FromArgb("#10B981"),
        ["Accent2Color"]     = Color.FromArgb("#34D399"),
        ["Accent3Color"]     = Color.FromArgb("#6EE7B7"),
        ["AccentBgColor"]    = Color.FromArgb("#0E2E22"),
        ["AccentBg2Color"]   = Color.FromArgb("#0F3A2A"),
        ["AccentDarkColor"]  = Color.FromArgb("#34D399"),
        ["TextColor"]        = Color.FromArgb("#F1F5F9"),
        ["MutedColor"]       = Color.FromArgb("#9CA3AF"),
        ["Muted2Color"]      = Color.FromArgb("#6B7280"),
        ["DividerColor"]     = Color.FromArgb("#1F2937"),
        ["DividerSoftColor"] = Color.FromArgb("#1F2937"),
        ["DangerColor"]      = Color.FromArgb("#F87171"),
        ["DangerBgColor"]    = Color.FromArgb("#3F1212"),
        ["WarnColor"]        = Color.FromArgb("#FBBF24"),
        ["WarnBgColor"]      = Color.FromArgb("#3F2D08"),
        ["WarnBg2Color"]     = Color.FromArgb("#2F2107"),
        ["WarnTextColor"]    = Color.FromArgb("#FCD34D"),
        ["ThumbFruitColor"]  = Color.FromArgb("#3F1212"),
        ["ThumbGrainColor"]  = Color.FromArgb("#3F2D08"),
        ["ThumbDairyColor"]  = Color.FromArgb("#0F2540"),
        ["ThumbDefaultColor"]= Color.FromArgb("#1F2937"),
    };

    private static readonly Dictionary<string, Brush> DarkBrushes = new()
    {
        ["HeroBrush"] = MakeBrush(0, 0, 0, 1,
            ("#1F2937", 0), ("#111827", 1)),
        ["PrimaryBrush"] = MakeBrush(0, 0, 0, 1,
            ("#1E3A5F", 0), ("#0F2540", 1)),
        ["ActiveCartBrush"] = MakeBrush(0, 0, 1, 1,
            ("#1E3A5F", 0), ("#15294A", 0.6), ("#0B1220", 1)),
        ["ProgressBrush"] = MakeBrush(0, 0, 1, 0,
            ("#10B981", 0), ("#34D399", 1)),
        ["ProgressLightBrush"] = MakeBrush(0, 0, 1, 0,
            ("#34D399", 0), ("#6EE7B7", 1)),
        ["BannerBrush"] = MakeBrush(0, 0, 0, 1,
            ("#2F2107", 0), ("#3F2D08", 1)),
        ["FabBrush"] = MakeBrush(0, 0, 0, 1,
            ("#34D399", 0), ("#10B981", 1)),
    };

    private static LinearGradientBrush MakeBrush(double sx, double sy, double ex, double ey, params (string hex, double offset)[] stops)
    {
        var b = new LinearGradientBrush { StartPoint = new Point(sx, sy), EndPoint = new Point(ex, ey) };
        foreach (var (hex, offset) in stops)
            b.GradientStops.Add(new GradientStop { Color = Color.FromArgb(hex), Offset = (float)offset });
        return b;
    }

    public static ThemeMode Current
    {
        get
        {
            var s = Preferences.Default.Get(Key, "system");
            return s switch
            {
                "light" => ThemeMode.Light,
                "dark" => ThemeMode.Dark,
                _ => ThemeMode.System,
            };
        }
    }

    public static void Set(ThemeMode mode)
    {
        Preferences.Default.Set(Key, mode switch
        {
            ThemeMode.Light => "light",
            ThemeMode.Dark => "dark",
            _ => "system",
        });
        Apply(mode);
    }

    public static void Apply() => Apply(Current);

    private static void Apply(ThemeMode mode)
    {
        var app = Application.Current;
        if (app is null) return;

        app.UserAppTheme = mode switch
        {
            ThemeMode.Light => AppTheme.Light,
            ThemeMode.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        if (!_wired)
        {
            _wired = true;
            app.RequestedThemeChanged += (_, _) => ApplyOverlay();
        }
        ApplyOverlay();
    }

    private static void ApplyOverlay()
    {
        var app = Application.Current;
        if (app?.Resources is null) return;

        SnapshotLightOnce(app);

        var isDark = ResolveEffective() == AppTheme.Dark;
        if (isDark)
        {
            foreach (var kv in DarkColors)  app.Resources[kv.Key] = kv.Value;
            foreach (var kv in DarkBrushes) app.Resources[kv.Key] = kv.Value;
        }
        else if (_lightSnapshot is not null)
        {
            foreach (var kv in _lightSnapshot) app.Resources[kv.Key] = kv.Value;
        }
    }

    private static void SnapshotLightOnce(Application app)
    {
        if (_lightSnapshot is not null) return;
        _lightSnapshot = new Dictionary<string, object>();
        foreach (var key in DarkColors.Keys)
            if (TryFind(app, key, out var v)) _lightSnapshot[key] = v!;
        foreach (var key in DarkBrushes.Keys)
            if (TryFind(app, key, out var v)) _lightSnapshot[key] = v!;
    }

    private static bool TryFind(Application app, string key, out object? value)
    {
        if (app.Resources.TryGetValue(key, out value)) return true;
        foreach (var d in app.Resources.MergedDictionaries)
            if (d.TryGetValue(key, out value)) return true;
        value = null;
        return false;
    }

    private static AppTheme ResolveEffective()
    {
        var app = Application.Current;
        if (app is null) return AppTheme.Light;
        if (app.UserAppTheme == AppTheme.Light) return AppTheme.Light;
        if (app.UserAppTheme == AppTheme.Dark) return AppTheme.Dark;
        return app.RequestedTheme == AppTheme.Dark ? AppTheme.Dark : AppTheme.Light;
    }

    public static string Label(ThemeMode mode) => mode switch
    {
        ThemeMode.Light => "라이트",
        ThemeMode.Dark => "다크",
        _ => "시스템 설정",
    };
}
