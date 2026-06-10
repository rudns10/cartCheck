using CommunityToolkit.Maui;
using MartCart.Domain.Contracts;
using MartCart.Domain.Services;
using Microsoft.Extensions.Logging;

namespace MartCart.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitCamera()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // ===== Domain services =====
        builder.Services.AddSingleton<IPriceClassifier, HeuristicPriceClassifier>();
        builder.Services.AddSingleton<ICurrentCart, InMemoryCurrentCart>();

        // ===== Platform OCR =====
#if ANDROID
        builder.Services.AddSingleton<IPriceOcr, Platforms.Android.MlKitPriceOcr>();
#endif

        // ===== Pages / ViewModels =====
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddTransient<Pages.ScanPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
