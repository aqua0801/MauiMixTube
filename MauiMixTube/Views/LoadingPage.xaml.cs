using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Messages;

namespace MauiMixTube.Views;

public partial class LoadingPage : ContentPage
{
	public LoadingPage()
	{
		InitializeComponent();
        WeakReferenceMessenger.Default
            .Register<LoadingStatusMessage>(this, (r, m) =>
                MainThread.BeginInvokeOnMainThread(() =>
                    StatusLabel.Text = m.Status));
        WeakReferenceMessenger.Default
            .Register<LoadingProgressMessage>(this, async (r, m) =>
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    LoadingProgress.ProgressTo(m.Progress, 300, Easing.CubicOut));
            });
    }
}