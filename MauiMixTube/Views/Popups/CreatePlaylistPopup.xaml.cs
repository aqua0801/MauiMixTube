using CommunityToolkit.Maui.Views;

namespace MauiMixTube.Views.Popups;

public partial class CreatePlaylistPopup : Popup<string>
{
	public CreatePlaylistPopup()
	{
		InitializeComponent();
	}

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        NameEntry.Focus();
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        var len = e.NewTextValue?.Length ?? 0;
        CharCountLabel.Text = $"{len} / 50";
        CreateButton.IsEnabled = len > 0;
    }

    private void OnEntryCompleted(object sender, EventArgs e)
    => OnCreateClicked(sender, e);

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameEntry.Text)) return;
        await CloseAsync(NameEntry.Text.Trim());
    }

    private async void OnCancelClicked(object sender, EventArgs e)
        => await CloseAsync(null);

}