using CommunityToolkit.Maui;
using MartCart.AppFaithful.Pages;
using MartCart.Domain.Contracts;
using MartCart.Domain.Services;
using Microsoft.Extensions.Logging;

namespace MartCart.AppFaithful;

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

        // Domain
        builder.Services.AddSingleton<IPriceClassifier, HeuristicPriceClassifier>();
        builder.Services.AddSingleton<ICurrentCart, InMemoryCurrentCart>();

        // Platform OCR
#if ANDROID
        builder.Services.AddSingleton<IPriceOcr, Platforms.Android.MlKitPriceOcr>();
#endif

        // Pages
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<ScanPage>();
        builder.Services.AddTransient<CartDetailPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
