namespace Evomusic.App.Controls;

public interface ISymbolImageSource : IImageSource
{
	string Name { get; }

	double PointSize { get; }

	/// <summary>Fixed color; null renders a template image tinted by the host (tab bar, button).</summary>
	Color? Color { get; }
}

/// <summary>An SF Symbol by name (e.g. <c>play.fill</c>), rendered natively on iOS.</summary>
public sealed class SymbolImageSource : ImageSource, ISymbolImageSource
{
	public static readonly BindableProperty NameProperty =
		BindableProperty.Create(nameof(Name), typeof(string), typeof(SymbolImageSource), "", propertyChanged: OnSourceChanged);

	public static readonly BindableProperty PointSizeProperty =
		BindableProperty.Create(nameof(PointSize), typeof(double), typeof(SymbolImageSource), 17.0, propertyChanged: OnSourceChanged);

	public static readonly BindableProperty ColorProperty =
		BindableProperty.Create(nameof(Color), typeof(Color), typeof(SymbolImageSource), null, propertyChanged: OnSourceChanged);

	public string Name
	{
		get => (string)GetValue(NameProperty);
		set => SetValue(NameProperty, value);
	}

	public double PointSize
	{
		get => (double)GetValue(PointSizeProperty);
		set => SetValue(PointSizeProperty, value);
	}

	public Color? Color
	{
		get => (Color?)GetValue(ColorProperty);
		set => SetValue(ColorProperty, value);
	}

	public override bool IsEmpty => string.IsNullOrEmpty(Name);

	private static void OnSourceChanged(BindableObject bindable, object oldValue, object newValue) =>
		((SymbolImageSource)bindable).OnSourceChanged();
}
