namespace FlightClub.FsClient;

public partial class App : System.Windows.Application
{
    private static Mutex? _mutex;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _mutex = new Mutex(true, "FlightClub.FsClient.SingleInstance", out bool isNew);
        if (!isNew)
        {
            System.Windows.MessageBox.Show(
                "FlightClub is already running in the system tray.",
                "FlightClub",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }
}
