using System.Windows;
using System.Windows.Threading;
using UnrealBinaryBuilder.Views;

namespace UnrealBinaryBuilder;

public partial class App : Application
{
	public App()
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		var dialog = new CrashDialog(e.Exception)
		{
			Owner = Current.MainWindow
		};
		dialog.ShowDialog();
	}
}
