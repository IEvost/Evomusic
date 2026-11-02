namespace Evomusic.App.Controls;

/// <summary>
/// Raises <see cref="LongPressed"/> on a press-and-hold. MAUI's ContextFlyout doesn't fire for
/// CollectionView rows on iOS, so a native long-press recognizer is attached instead (with haptic feedback).
/// </summary>
public sealed partial class LongPressBehavior : Behavior<View>
{
	private View? _view;

	public event EventHandler<View>? LongPressed;

	protected override void OnAttachedTo(View view)
	{
		base.OnAttachedTo(view);
		_view = view;
		view.HandlerChanged += OnHandlerChanged;
		Attach(view);
	}

	protected override void OnDetachingFrom(View view)
	{
		view.HandlerChanged -= OnHandlerChanged;
		Detach();
		_view = null;
		base.OnDetachingFrom(view);
	}

	private void OnHandlerChanged(object? sender, EventArgs e)
	{
		Detach();
		if (_view is not null)
			Attach(_view);
	}

	private void Raise()
	{
		if (_view is not null)
			LongPressed?.Invoke(this, _view);
	}

	partial void Attach(View view);

	partial void Detach();
}
