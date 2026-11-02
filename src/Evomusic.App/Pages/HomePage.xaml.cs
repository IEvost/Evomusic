using System.Collections.ObjectModel;
using System.Windows.Input;
using Evomusic.App.Services;
using Evomusic.App.ViewModels;
using Evomusic.Core;

namespace Evomusic.App.Pages;

/// <summary>The signed-in YT Music Home feed (same shelves as music.youtube.com), or the sign-in prompt.</summary>
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

	public ObservableCollection<Row> Rows { get; } = [];

	public ICommand RefreshCommand { get; }

	public bool IsSignedIn => _session.IsSignedIn;

	public bool IsSignedOut => !_session.IsSignedIn;

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
		Rows.Clear();
		ErrorText = null;
		Notify(nameof(IsSignedIn), nameof(IsSignedOut), nameof(ErrorText), nameof(HasError));

		if (_session.Music is not { } music)
			return;

		SetLoading(true);
		try
		{
			var page = await music.BrowseAsync(YouTubeMusicService.HomeId);
			foreach (var row in RowBuilder.Build(page))
				Rows.Add(row);

			if (Rows.Count == 0)
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
