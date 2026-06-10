using MartCart.AppFaithful.Pages;

namespace MartCart.AppFaithful;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Routes that are not part of the TabBar (pushed via Shell.Current.GoToAsync)
        Routing.RegisterRoute(nameof(CartDetailPage), typeof(CartDetailPage));
        Routing.RegisterRoute(nameof(ScanPage), typeof(ScanPage));
        Routing.RegisterRoute(nameof(HistoryDetailPage), typeof(HistoryDetailPage));
    }
}
