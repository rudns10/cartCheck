namespace MartCart.AppFaithful.Services.Ads;

/// <summary>
/// 홈 화면 배너 광고 슬롯에 표시할 콘텐츠를 제공하는 추상화.
/// 현재는 MockBannerAdProvider(하드코딩)를 사용하고, 추후 Google AdMob 등으로 교체.
/// </summary>
public interface IBannerAdProvider
{
    /// <summary>
    /// 배너 슬롯에 들어갈 View. 반환된 View는 호스트의 ContentView.Content로 설정됨.
    /// null 반환 시 슬롯이 비워짐(광고 미표시).
    /// </summary>
    /// <param name="host">광고가 표시될 페이지 — 클릭 핸들러에서 네비게이션·다이얼로그 호출 시 사용.</param>
    Task<View?> CreateBannerAsync(Page host);
}
