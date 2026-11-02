using Evomusic.App.Controls;
using Evomusic.App.Pages;
using Evomusic.App.Services;
using Evomusic.Core.Auth;
using Evomusic.Core.Streams;
using Microsoft.Extensions.Logging;

namespace Evomusic.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.ConfigureImageSources(sources => sources.AddService<ISymbolImageSource, SymbolImageSourceService>());

		builder.Services.AddSingleton<ICookieStore, SecureCookieStore>();
		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddSingleton<IStreamResolver>(services =>
		{
			var session = services.GetRequiredService<SessionService>();
			return new InnerTubeStreamResolver(session.GetVisitorDataAsync);
		});
#if IOS
		builder.Services.AddSingleton<IAudioPlayer, AvAudioPlayer>();
#elif WINDOWS
		builder.Services.AddSingleton<IAudioPlayer, WindowsAudioPlayer>();
#endif
		builder.Services.AddSingleton<PlaybackService>();
		builder.Services.AddSingleton<HomePage>();
		builder.Services.AddSingleton<SearchPage>();
		builder.Services.AddSingleton<LibraryPage>();
		builder.Services.AddSingleton<SettingsPage>();
		builder.Services.AddTransient<LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
