namespace MartCart.AppFaithful.Services.Ads;

/// <summary>
/// Google AdMob 배너 자리. 실 SDK 연결 전까지는 null을 반환해 슬롯을 비웁니다.
///
/// 연결 절차 (TODO):
///   1) NuGet 패키지 추가: Plugin.MauiMTAdmob 또는 Google.Android.Gms.Ads.Lite 바인딩
///   2) Platforms/Android/AndroidManifest.xml 에 AdMob APPLICATION_ID 메타데이터 등록
///   3) Platforms/Android/MainActivity.cs OnCreate 에서 MobileAds.Initialize 호출
///   4) 아래 CreateBannerAsync 에서 네이티브 AdView를 ContentView로 래핑해 반환
///   5) AdProvider.Current = new AdMobBannerAdProvider(unitId, testMode) 로 교체
/// </summary>
public sealed class AdMobBannerAdProvider : IBannerAdProvider
{
    public string AdUnitId { get; }
    public bool TestMode { get; }

    public AdMobBannerAdProvider(string adUnitId, bool testMode = false)
    {
        AdUnitId = adUnitId;
        TestMode = testMode;
    }

    public Task<View?> CreateBannerAsync(Page host)
    {
        // TODO: 실제 AdMob 배너로 교체. 지금은 슬롯 비움.
        return Task.FromResult<View?>(null);
    }
}
