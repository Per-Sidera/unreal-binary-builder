using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using UnrealBinaryBuilder.Views;

namespace UnrealBinaryBuilder;

public partial class App : Application
{
	public App()
	{
		// HandyControl ships Chinese as its default locale. Pin everything to
		// en-US before any HC resource is resolved, otherwise built-in strings
		// (ColorPicker buttons, MessageBox, validation hints, etc.) render in
		// Chinese on machines whose system locale doesn't match.
		var en = new CultureInfo("en-US");
		Thread.CurrentThread.CurrentCulture = en;
		Thread.CurrentThread.CurrentUICulture = en;
		CultureInfo.DefaultThreadCurrentCulture = en;
		CultureInfo.DefaultThreadCurrentUICulture = en;
		FrameworkElement.LanguageProperty.OverrideMetadata(
			typeof(FrameworkElement),
			new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(en.IetfLanguageTag)));

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
