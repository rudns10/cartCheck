using Plugin.Fingerprint;
using Plugin.Fingerprint.Abstractions;

namespace MartCart.AppFaithful.Services;

public static class BiometricService
{
    private const string EnabledKey = "martcart.biometric.enabled";

    public static bool Enabled => Preferences.Default.Get(EnabledKey, false);

    public static void SetEnabled(bool value) => Preferences.Default.Set(EnabledKey, value);

    public static async Task<bool> IsAvailableAsync()
    {
        try
        {
            var availability = await CrossFingerprint.Current.GetAvailabilityAsync(allowAlternativeAuthentication: false);
            return availability == FingerprintAvailability.Available;
        }
        catch { return false; }
    }

    public static async Task<bool> AuthenticateAsync(string reason)
    {
        var result = await AuthenticateDetailedAsync(reason);
        return result == BiometricResult.Success;
    }

    public static async Task<BiometricResult> AuthenticateDetailedAsync(string reason)
    {
        try
        {
            var req = new AuthenticationRequestConfiguration("카트체크 잠금 해제", reason)
            {
                CancelTitle = "PIN 사용",
                AllowAlternativeAuthentication = false,
            };
            var result = await CrossFingerprint.Current.AuthenticateAsync(req);
            if (result.Authenticated) return BiometricResult.Success;
            if (result.Status == FingerprintAuthenticationResultStatus.Canceled
                || result.Status == FingerprintAuthenticationResultStatus.FallbackRequested)
                return BiometricResult.Fallback;
            return BiometricResult.Failed;
        }
        catch { return BiometricResult.Failed; }
    }
}

public enum BiometricResult { Success, Fallback, Failed }
