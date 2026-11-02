using Evomusic.Core;
using Evomusic.Core.Auth;
using Evomusic.Core.InnerTube;
using Microsoft.Extensions.Logging;

namespace Evomusic.App.Services;

/// <summary>Owns the signed-in YT Music session: restores it on launch and persists cookie rotations.</summary>
public sealed class SessionService(ICookieStore store, ILogger<SessionService> logger)
{
	private InnerTubeClient? _client;

	public YouTubeMusicService? Music { get; private set; }

	public bool IsSignedIn => Music is not null;

	public event EventHandler? SessionChanged;

	/// <summary>Visitor ID of the web session; stream requests send it so they look like the same visitor.</summary>
	public async Task<string?> GetVisitorDataAsync(CancellationToken cancellationToken)
	{
		if (_client is null)
			return null;

		return (_client.Context ?? await _client.InitializeAsync(cancellationToken)).VisitorData;
	}

	public async Task<bool> RestoreAsync()
	{
		var cookies = await store.LoadAsync();
		if (!CookieHeaderParser.HasAuthCookie(cookies))
			return false;

		Start(cookies);
		return true;
	}

	public async Task SignInAsync(IReadOnlyList<StoredCookie> cookies)
	{
		await store.SaveAsync(cookies);
		Start(cookies);
		SessionChanged?.Invoke(this, EventArgs.Empty);
	}

	public async Task SignOutAsync()
	{
		_client?.Dispose();
		_client = null;
		Music = null;
		await store.ClearAsync();
		await WebViewCookies.ClearAsync();
		SessionChanged?.Invoke(this, EventArgs.Empty);
	}

	private void Start(IReadOnlyList<StoredCookie> cookies)
	{
		_client?.Dispose();
		_client = new InnerTubeClient(cookies);
		_client.CookiesChanged += OnCookiesChanged;
		Music = new YouTubeMusicService(_client);
	}

	private async void OnCookiesChanged(object? sender, EventArgs e)
	{
		try
		{
			if (sender is InnerTubeClient client)
				await store.SaveAsync(client.ExportCookies());
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Failed to persist rotated cookies");
		}
	}
}
