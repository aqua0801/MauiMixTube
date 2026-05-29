using CommunityToolkit.Maui.Views;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Models;
using MauiMixTube.Models.Playlist;
using Microsoft.Maui.Controls.Shapes;

namespace MauiMixTube.Views.Popups;

public partial class AddSourcePopup : Popup<PlaylistSource>
{
    private WebTag? _selectedTag;
    private readonly List<Border> _chips = new();
    private readonly FetchManager _fetchManager;

    public AddSourcePopup(FetchManager fetchManager)
    {
        InitializeComponent();
        _fetchManager = fetchManager;
        BuildTagChips();
    }

    private void BuildTagChips()
    {
        foreach (var tag in WebTagRegistry.GetAll())
        {
            var chip = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                StrokeThickness = 1,
                Padding = new Thickness(16, 8),
                Content = new Label { Text = tag.Value }
            };

            chip.GestureRecognizers.Add(new TapGestureRecognizer
            {
                Command = new Command(() => SelectTag(tag, chip))
            });

            _chips.Add(chip);
            TagChips.Children.Add(chip);
        }

        if (_chips.Count > 0)
            SelectTag(WebTagRegistry.GetAll()[0], _chips[0]);
    }

    private void SelectTag(WebTag tag, Border chip)
    {
        _selectedTag = tag;

        foreach (var c in _chips)
        {
            c.SetAppThemeColor(Border.BackgroundColorProperty,
                (Color)Application.Current!.Resources["LightSidebarItem"],
                (Color)Application.Current!.Resources["DarkSidebarItem"]);
        }

        chip.SetAppThemeColor(Border.BackgroundColorProperty,
            (Color)Application.Current!.Resources["LightPrimary"],
            (Color)Application.Current!.Resources["DarkPrimary"]);
    }

    private void OnUrlChanged(object sender, TextChangedEventArgs e)
    {
        var url = e.NewTextValue;
        if (string.IsNullOrWhiteSpace(url)) return;

        var tag = _fetchManager.DetectTagFromUrl(url);
        if (tag is null) return;

        var chip = _chips.FirstOrDefault(c =>
            (c.Content as Label)?.Text == tag.Value);

        if (chip is not null)
            SelectTag(tag, chip);
    }

    private async void OnAddClicked(object sender, EventArgs e)
    {
        if (_selectedTag is null || string.IsNullOrWhiteSpace(UrlEntry.Text))
            return;

        var result = new PlaylistSource
        {
            Tag = _selectedTag,
            Url = UrlEntry.Text.Trim(),
            IsPlaylist = RadioPlaylist.IsChecked
        };

        await CloseAsync(result); 
    }
}