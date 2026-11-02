using Evomusic.App.Controls;
using Microsoft.Maui.Platform;
using UIKit;

namespace Evomusic.App.Services;

public sealed class SymbolImageSourceService : ImageSourceService, IImageSourceService<ISymbolImageSource>
{
	public override Task<IImageSourceServiceResult<UIImage>?> GetImageAsync(IImageSource imageSource, float scale = 1, CancellationToken cancellationToken = default)
	{
		if (imageSource is not ISymbolImageSource symbol || imageSource.IsEmpty)
			return Task.FromResult<IImageSourceServiceResult<UIImage>?>(null);

		var configuration = UIImageSymbolConfiguration.Create((nfloat)symbol.PointSize, UIImageSymbolWeight.Medium);
		var image = UIImage.GetSystemImage(symbol.Name, configuration);
		if (image is not null && symbol.Color is { } color)
			image = image.ApplyTintColor(color.ToPlatform(), UIImageRenderingMode.AlwaysOriginal);

		return Task.FromResult<IImageSourceServiceResult<UIImage>?>(image is null ? null : new ImageSourceServiceResult(image));
	}
}
