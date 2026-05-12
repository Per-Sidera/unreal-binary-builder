using System.Globalization;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Threading;
using HandyControl.Tools;
using UnrealBinaryBuilder.Theming;
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

		// HandyControl resolves its own string resources through ConfigHelper —
		// pinning Thread culture isn't enough. Force HC's internal Lang to "en".
		ConfigHelper.Instance.SetLang("en");

		// HC 3.5.1 ships only a Chinese resource file, so SetLang alone leaves
		// strings like '取消'/'确定' on the ColorPicker buttons. Wrap HC's
		// ResourceManager to override the handful of keys we care about.
		HandyControlEnglishStrings.Install();

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
