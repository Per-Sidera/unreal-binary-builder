using System;
using System.Windows;

namespace UnrealBinaryBuilder.Views;

public partial class CrashDialog
{
	public CrashDialog(Exception exception)
	{
		InitializeComponent();
		TraceText.Text =
			$"Type:    {exception.GetType().FullName}\n" +
			$"Source:  {exception.Source}\n" +
			$"Message: {exception.Message}\n" +
			$"Target:  {exception.TargetSite}\n\n" +
			$"Stack:\n{exception.StackTrace}";
	}

	private void OnCopy(object sender, RoutedEventArgs e) => Clipboard.SetText(TraceText.Text);
	private void OnClose(object sender, RoutedEventArgs e) => Close();
}
