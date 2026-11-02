using System.Collections.ObjectModel;
using System.Windows.Input;
using Evomusic.App.Services;
using Evomusic.Core.Models;

namespace Evomusic.App.Pages;

/// <summary>Phase 1 Home: proves the signed-in session by showing the account and the real YT Music home shelves.</summary>
public partial class HomePage : ContentPage
{
	private readonly SessionService _session;
	private readonly IServiceProvider _services;
	private bool _restored;

	public HomePage(SessionService session, IServiceProvider services)
	{
		InitializeComponent();
		_session = session;
		_services = services;
		_session.SessionChanged += async (_, _) => await LoadAsync();

		RefreshCommand = new Command(async () => await LoadAsync());
		BindingContext = this;
	}

	public ObservableCollection<Shelf> Shelves { get; } = [];

	public ICommand RefreshCommand { get; }

	public bool IsSignedIn => _session.IsSignedIn;

	public bool IsSignedOut => !_session.IsSignedIn;

	public string Greeting { get; private set; } = "";

	public string? ErrorText { get; private set; }

	public bool HasError => ErrorText is not null;

	public bool IsLoading { get; private set; }

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_restored)
			return;

		_restored = true;
		await _session.RestoreAsync();
		await LoadAsync();
	}

	private async Task LoadAsync()
	{
		Shelves.Clear();
		ErrorText = null;
		AccountToolbarItem.Text = _session.IsSignedIn ? "Sign out" : "";
		Notify(nameof(IsSignedIn), nameof(IsSignedOut), nameof(ErrorText), nameof(HasError));

		if (_session.Music is not { } music)
			return;

		SetLoading(true);
		try
		{
			var account = await music.GetAccountAsync();
			Greeting = account is null ? "Signed in" : $"Signed in as {account.Name}";
			Notify(nameof(Greeting));

			foreach (var shelf in await music.GetHomeAsync())
				Shelves.Add(shelf);

			if (Shelves.Count == 0)
				ErrorText = "Home feed loaded but no shelves were recognized (parser needs an update).";
		}
		catch (Exception ex)
		{
			ErrorText = ex.Message;
		}
		finally
		{
			SetLoading(false);
			Notify(nameof(ErrorText), nameof(HasError));
		}
	}

	private async void OnSignInClicked(object? sender, EventArgs e)
	{
		var login = _services.GetRequiredService<LoginPage>();
		await Navigation.PushModalAsync(new NavigationPage(login));
	}

	private async void OnAccountClicked(object? sender, EventArgs e)
	{
		if (!_session.IsSignedIn)
			return;

		if (await DisplayActionSheetAsync("Sign out of YouTube Music?", "Cancel", "Sign out") == "Sign out")
			await _session.SignOutAsync();
	}

	private void SetLoading(bool value)
	{
		IsLoading = value;
		Notify(nameof(IsLoading));
	}

	private void Notify(params string[] properties)
	{
		foreach (var property in properties)
			OnPropertyChanged(property);
	}
}
