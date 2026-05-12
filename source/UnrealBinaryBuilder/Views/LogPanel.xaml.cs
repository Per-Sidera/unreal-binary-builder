using System.Windows.Controls;

namespace UnrealBinaryBuilder.Views;

public partial class LogPanel : UserControl
{
	private bool _autoScroll = true;

	public LogPanel()
	{
		InitializeComponent();
	}

	private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (e.ExtentHeightChange == 0)
		{
			_autoScroll = LogScroll.VerticalOffset >= LogScroll.ScrollableHeight - 1;
		}
		else if (_autoScroll)
		{
			LogScroll.ScrollToVerticalOffset(LogScroll.ExtentHeight);
		}
	}
}
