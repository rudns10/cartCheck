namespace MartCart.AppFaithful.Services;

public static class UserProfile
{
    private const string NameKey = "martcart.user.name";
    private const string SinceKey = "martcart.user.since";

    public static string Name
    {
        get => Preferences.Default.Get(NameKey, "로컬 사용자");
        set => Preferences.Default.Set(NameKey, string.IsNullOrWhiteSpace(value) ? "로컬 사용자" : value.Trim());
    }

    public static DateTimeOffset Since
    {
        get
        {
            var stored = Preferences.Default.Get(SinceKey, 0L);
            if (stored > 0) return DateTimeOffset.FromUnixTimeSeconds(stored);
            var now = DateTimeOffset.UtcNow;
            Preferences.Default.Set(SinceKey, now.ToUnixTimeSeconds());
            return now;
        }
    }

    public static void Clear()
    {
        Preferences.Default.Remove(NameKey);
        Preferences.Default.Remove(SinceKey);
    }
}
