using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace UnrealBinaryBuilder.Views;

public partial class AboutDialog
{
	public AboutDialog()
	{
		InitializeComponent();
	}

	private void OnClose(object sender, RoutedEventArgs e) => Close();

	private void OnNavigate(object sender, RequestNavigateEventArgs e)
	{
		Process.Start(new ProcessStartInfo
		{
			FileName = e.Uri.AbsoluteUri,
			UseShellExecute = true,
		});
		e.Handled = true;
	}
}
