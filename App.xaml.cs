using System.Windows;
using System.Threading;
using System.Globalization;

namespace MabiLauncher
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		private void Application_Startup(object sender, StartupEventArgs e)
		{
			// Normally UI language determined by windows, but we can change this behavior
			bool override_current_ui_language = false;

			if (override_current_ui_language)
			{
				string locale = "en";
				Thread.CurrentThread.CurrentUICulture = new CultureInfo(locale);
				Thread.CurrentThread.CurrentCulture = new CultureInfo(locale);
			}

			MainWindow mainWindow = new MainWindow();
			mainWindow.Show();
		}
	}
}
