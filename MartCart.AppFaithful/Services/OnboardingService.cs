namespace MartCart.AppFaithful.Services;

public static class OnboardingService
{
    private const string Key = "martcart.onboarding.seen";

    public static bool Seen
    {
        get => Preferences.Default.Get(Key, false);
        set => Preferences.Default.Set(Key, value);
    }
}
