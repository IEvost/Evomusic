using Evomusic.App.Controls;
using WImageSource = Microsoft.UI.Xaml.Media.ImageSource;

namespace Evomusic.App.Services;

// SF Symbols don't exist on Windows; the development preview simply shows no icon.
public sealed class SymbolImageSourceService : ImageSourceService, IImageSourceService<ISymbolImageSource>
{
	public override Task<IImageSourceServiceResult<WImageSource>?> GetImageSourceAsync(IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default) =>
		Task.FromResult<IImageSourceServiceResult<WImageSource>?>(null);
}
