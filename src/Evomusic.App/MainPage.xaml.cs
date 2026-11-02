namespace Evomusic.App;

public partial class MainPage : ContentPage
{
	public MainPage()
	{
		InitializeComponent();

		BuildInfoLabel.Text = $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})\n{DeviceInfo.Current.Platform} {DeviceInfo.Current.VersionString}";
	}
}
