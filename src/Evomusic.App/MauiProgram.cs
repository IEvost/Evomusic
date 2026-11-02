using Evomusic.App.Pages;
using Evomusic.App.Services;
using Evomusic.Core.Auth;
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
			});

		builder.Services.AddSingleton<ICookieStore, SecureCookieStore>();
		builder.Services.AddSingleton<SessionService>();
		builder.Services.AddSingleton<HomePage>();
		builder.Services.AddTransient<LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
