using UIKit;

namespace Evomusic.App.Controls;

public sealed partial class LongPressBehavior
{
	private UIView? _nativeView;
	private UILongPressGestureRecognizer? _recognizer;

	partial void Attach(View view)
	{
		if (view.Handler?.PlatformView is not UIView native)
			return;

		_recognizer = new UILongPressGestureRecognizer(recognizer =>
		{
			if (recognizer.State != UIGestureRecognizerState.Began)
				return;

			new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Medium).ImpactOccurred();
			Raise();
		})
		{
			MinimumPressDuration = 0.45,
		};

		_nativeView = native;
		native.AddGestureRecognizer(_recognizer);
	}

	partial void Detach()
	{
		if (_nativeView is not null && _recognizer is not null)
			_nativeView.RemoveGestureRecognizer(_recognizer);

		_recognizer = null;
		_nativeView = null;
	}
}
