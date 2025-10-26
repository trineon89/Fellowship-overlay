using System.Windows;
using Fellowship_overlay.Services;

namespace Fellowship_overlay;

public partial class App : System.Windows.Application
{
    public AppController? Controller { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
		
        Controller = new AppController();
        var settingsWindow = new SettingsWindow(Controller);
        settingsWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Controller?.SaveSettings();
        Controller?.Dispose();
        base.OnExit(e);
    }
}