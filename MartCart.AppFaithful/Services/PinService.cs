using MartCart.Infrastructure.Security;

namespace MartCart.AppFaithful.Services;

public static class PinService
{
    private const string HashKey = "martcart.pin.hash";
    private const string SaltKey = "martcart.pin.salt";
    private const string SetAtKey = "martcart.pin.setAt";
    private const string LengthKey = "martcart.pin.length";

    public static int Length => Preferences.Default.Get(LengthKey, 0);

    public static bool IsSet =>
        !string.IsNullOrEmpty(Preferences.Default.Get(HashKey, "")) &&
        !string.IsNullOrEmpty(Preferences.Default.Get(SaltKey, ""));

    public static DateTimeOffset? LastSetAt
    {
        get
        {
            var ts = Preferences.Default.Get(SetAtKey, 0L);
            return ts > 0 ? DateTimeOffset.FromUnixTimeSeconds(ts) : null;
        }
    }

    public static void Set(string pin)
    {
        if (string.IsNullOrEmpty(pin)) throw new ArgumentException("PIN cannot be empty");
        var (salt, hash) = PinHasher.Hash(pin);
        Preferences.Default.Set(SaltKey, salt);
        Preferences.Default.Set(HashKey, hash);
        Preferences.Default.Set(SetAtKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Preferences.Default.Set(LengthKey, pin.Length);
    }

    public static bool Verify(string pin)
    {
        var salt = Preferences.Default.Get(SaltKey, "");
        var hash = Preferences.Default.Get(HashKey, "");
        if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(hash)) return false;
        return PinHasher.Verify(pin, salt, hash);
    }

    public static void Clear()
    {
        Preferences.Default.Remove(HashKey);
        Preferences.Default.Remove(SaltKey);
        Preferences.Default.Remove(SetAtKey);
        Preferences.Default.Remove(LengthKey);
        ResetFailures();
    }

    public static Task<bool> VerifyAsync(string pin) => Task.Run(() => Verify(pin));

    // ===== Failure tracking + lockout (§10.1) =====
    private const string FailCountKey = "martcart.pin.failCount";
    private const string LockoutUntilKey = "martcart.pin.lockoutUntil";
    private const int MaxFailures = 5;
    private const int LockoutSeconds = 30;

    public static int FailureCount => Preferences.Default.Get(FailCountKey, 0);

    public static DateTimeOffset? LockoutUntil
    {
        get
        {
            var ts = Preferences.Default.Get(LockoutUntilKey, 0L);
            if (ts == 0) return null;
            var until = DateTimeOffset.FromUnixTimeSeconds(ts);
            return until > DateTimeOffset.UtcNow ? until : null;
        }
    }

    public static void RecordFailure()
    {
        var n = FailureCount + 1;
        Preferences.Default.Set(FailCountKey, n);
        if (n >= MaxFailures)
        {
            var until = DateTimeOffset.UtcNow.AddSeconds(LockoutSeconds);
            Preferences.Default.Set(LockoutUntilKey, until.ToUnixTimeSeconds());
        }
    }

    public static void ResetFailures()
    {
        Preferences.Default.Remove(FailCountKey);
        Preferences.Default.Remove(LockoutUntilKey);
    }
}
