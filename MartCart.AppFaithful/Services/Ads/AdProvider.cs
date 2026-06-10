namespace MartCart.AppFaithful.Services.Ads;

/// <summary>
/// 광고 제공자 전역 진입점. 앱 시작 시 한 줄로 교체 가능.
/// 예: AdProvider.Current = new AdMobBannerAdProvider("ca-app-pub-...", testMode: true);
/// </summary>
public static class AdProvider
{
    // 광고는 일시 비활성. AdMob 연결 시 아래 한 줄을 교체:
    //   new AdMobBannerAdProvider("ca-app-pub-...", testMode: true)
    public static IBannerAdProvider Current { get; set; } = new NullBannerAdProvider();
}

internal sealed class NullBannerAdProvider : IBannerAdProvider
{
    public Task<View?> CreateBannerAsync(Page host) => Task.FromResult<View?>(null);
}
