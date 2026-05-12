using System;
using System.Windows.Controls;
using System.Windows.Media;
using HandyControl.Data;
using UnrealBinaryBuilder.ViewModels;

namespace UnrealBinaryBuilder.Views;

public partial class ColorRoleRow : UserControl
{
	public ColorRoleRow()
	{
		InitializeComponent();
	}

	private void OnPickerColorChanged(object sender, FunctionEventArgs<Color> e)
	{
		PushColor(e.Info);
	}

	private void OnPickerConfirmed(object sender, FunctionEventArgs<Color> e)
	{
		PushColor(e.Info);
		PickToggle.IsChecked = false;
	}

	private void OnPickerCanceled(object sender, EventArgs e)
	{
		PickToggle.IsChecked = false;
	}

	private void PushColor(Color color)
	{
		if (DataContext is ColorRoleViewModel role)
		{
			role.Hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
		}
	}
}
