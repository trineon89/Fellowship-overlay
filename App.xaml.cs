using System.Configuration;
using System.Data;
using System.Windows;

namespace Fellowship_overlay;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		var sparkle = Fellowship_overlay.Services.Updater.Create();
		_ = sparkle.CheckForUpdatesAtUserRequest();  // or StartLoop(true)
	}
}

