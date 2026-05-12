using System.ComponentModel;
using System.Windows;
using HandyControl.Controls;
using UnrealBinaryBuilder.ViewModels;

namespace UnrealBinaryBuilder.Views;

public partial class MainWindow
{
	private readonly MainViewModel _vm;

	public MainWindow()
	{
		InitializeComponent();
		_vm = new MainViewModel();
		DataContext = _vm;
	}

	private void OnClosing(object? sender, CancelEventArgs e)
	{
		if (_vm.Status.IsBusy)
		{
			var result = HandyControl.Controls.MessageBox.Show(
				"A build is still running. Stop it and exit?",
				"Build in progress",
				MessageBoxButton.YesNo,
				MessageBoxImage.Question);
			if (result != MessageBoxResult.Yes)
			{
				e.Cancel = true;
				return;
			}
		}

		_vm.SaveSettings();
		_vm.WriteLogToDisk();
	}

	private void OnAboutClicked(object sender, RoutedEventArgs e)
	{
		var dialog = new AboutDialog { Owner = this };
		dialog.ShowDialog();
	}
}
