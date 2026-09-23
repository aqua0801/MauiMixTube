using CommunityToolkit.Maui.Extensions;
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Extensions;
using MauiMixTube.Managers;
using MauiMixTube.Managers.Fetch;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using MauiMixTube.Models.Playlist;
using MauiMixTube.ViewModels;
using MauiMixTube.Views.Popups;

namespace MauiMixTube;

public partial class MainPage : ContentPage
{
    // ── State ────────────────────────────────────────────────────────────────
    private bool _sidebarOpen = false;

    private readonly FetchManager _fetchManager;
    private DateTime _lastDragCompletedTime = DateTime.MinValue;
    private const int DragThrottleIntervalMilliseconds = 100;

    // ── Init ─────────────────────────────────────────────────────────────────
    public MainPage(MainViewModel vm , FetchManager fetchManager)
    {
        InitializeComponent();
        BindingContext = vm;
        _fetchManager = fetchManager;

        WeakReferenceMessenger.Default
            .Register<OpenSidebarMessage>(this, async (r, m) =>
            await OpenSidebarAsync());

        WeakReferenceMessenger.Default
            .Register<CloseSidebarMessage>(this, async (r, m) =>
            await CloseSidebarAsync());

        WeakReferenceMessenger.Default
            .Register<ScrollToLyricsMessage>(this, async (r, m) =>
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var target = (LyricsPanel as IVisualTreeElement)
                        .GetVisualChildren()[0]
                        .GetVisualTreeDescendants()
                        .OfType<Label>()
                        .ElementAtOrDefault(m.Index);

                    if (target is null) return;

                    double targetAbsoluteTop = 0;
                    Element current = target;

                    while (current != null && current != LyricsPanel)
                    {
                        if (current is VisualElement ve)
                            targetAbsoluteTop += ve.Y;
                        current = current.Parent;
                    }

                    double targetAbsoluteBottom = targetAbsoluteTop + target.Height;
                    double viewportTop = LyricsPanel.ScrollY;
                    double viewportBottom = viewportTop + LyricsPanel.Height;

                    bool isVisible = targetAbsoluteTop >= viewportTop
                                  && targetAbsoluteBottom <= viewportBottom;

                    if (isVisible)
                        await LyricsPanel.ScrollToAsync(target, ScrollToPosition.Center, animated: true);
                });
            });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  SIDEBAR
    // ════════════════════════════════════════════════════════════════════════

    private async void OnSidebarToggle(object sender, EventArgs e)
    {
        if (_sidebarOpen)
            await CloseSidebarAsync();
        else
            await OpenSidebarAsync();
    }

    private async void OnDimmerTapped(object sender, TappedEventArgs e)
        => await CloseSidebarAsync();

    private async Task OpenSidebarAsync()
    {
        _sidebarOpen = true;
        SidebarDimmer.IsVisible = true;

        await Task.WhenAll(
            SidebarDimmer.FadeToAsync(1, 220, Easing.CubicOut),
            SidebarPanel.TranslateToAsync(0, 0, 260, Easing.CubicOut)
        );

        _ = (BindingContext as MainViewModel)?.LoadPlaylistsAsync();
    }

    private async Task CloseSidebarAsync()
    {
        _sidebarOpen = false;

        await Task.WhenAll(
            SidebarDimmer.FadeToAsync(0, 200, Easing.CubicIn),
            SidebarPanel.TranslateToAsync(-300, 0, 240, Easing.CubicIn)
        );

        SidebarDimmer.IsVisible = false;
    }

    // Slider
    private void OnSliderDragStarted(object sender , EventArgs e)
    {
        (BindingContext as MainViewModel)?.IsSliderDragging = true;
    }

    private void OnSliderDragCompleted(object sender, EventArgs e)
    {
        if ((DateTime.UtcNow - _lastDragCompletedTime).TotalMilliseconds < DragThrottleIntervalMilliseconds)
            return;
        
        if (BindingContext is not MainViewModel mv ||
            sender is not Slider slider)
            return;

        _lastDragCompletedTime = DateTime.UtcNow;
        mv.HandleSliderDrag(slider.Value);
        mv.IsSliderDragging = false;
    }

    private async void OnNewPlaylistClicked(object sender, EventArgs e)
    {
        var popup = new CreatePlaylistPopup();
        var result = await this.ShowPopupAsync<string>(popup);

        if(result.Result is string name)
        {
            (BindingContext as MainViewModel)?.HandleNewPlaylist(name);
        }
    }

    private async void OnMoreOptionsTapped(object sender, EventArgs e)
    {
        var playlist = (sender as View)?.BindingContext as UserPlaylist ?? null;
        if (BindingContext is not MainViewModel vm) return;

        if (playlist is null) return;

        var rename = LocalizationManager.Instance["Action_Rename"];
        var cancel = LocalizationManager.Instance["Action_Cancel"];
        var import = LocalizationManager.Instance["Action_Import"];
        var export = LocalizationManager.Instance["Action_Export"];
        var delete = LocalizationManager.Instance["Action_Delete"];

        var action = await Shell.Current.DisplayActionSheetAsync(
                    playlist.Name,
                    rename,
                    cancel,
                    import,
                    export,
                    delete);

        if (action == import)
        {
            vm.HandleImportPlaylist(playlist);
        }
        else if (action == export)
        {
            vm.HandleExportPlaylist(playlist);
        }
        else if (action == rename)
        {
            await RenameAsync(playlist);
        }
        else if (action == delete)
        {
            await DeleteAsync(playlist);
        }
    }

    private async void OnTrackMoreOptionsTapped(object sender, EventArgs e)
    {
        var track = (sender as View)?.BindingContext as TrackDisplayItem ?? null;
        var mv = BindingContext as MainViewModel;
        if (track is null || mv is null) return;

        var cancel = LocalizationManager.Instance["Action_Cancel"];
        var delete = LocalizationManager.Instance["Options_DeleteFromPlaylist"];
        var remove = LocalizationManager.Instance["Options_RemoveFromQueue"];

        var action = await Shell.Current.DisplayActionSheetAsync(
            track.Title,
            cancel,
            delete,
            remove);

        if (action == delete)
        {
            mv.HandleRemoveFromPlaylist(track);
        }
        else if (action == remove) 
        {
            mv.HandleRemoveFromQueue(track);
        }
    }

    private async Task RenameAsync(UserPlaylist playlist)
    {
        var newName = await Shell.Current.DisplayPromptAsync(
            "Rename Playlist",
            null,
            initialValue: playlist.Name,
            maxLength: 50,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(newName)) return;

        playlist.Name = newName;
        (BindingContext as MainViewModel)?.HandleUpdatePlaylist(playlist);
    }

    private async Task DeleteAsync(UserPlaylist playlist)
    {
        var confirm = await Shell.Current.DisplayAlertAsync(
            "Delete Playlist",
            $"Are you sure you want to delete \"{playlist.Name}\"?",
            "Delete",
            "Cancel");

        if (!confirm) return;

        (BindingContext as MainViewModel)?.HandleDeletePlaylist(playlist);
    }
    private void OnAlbumPanelScrolled(object sender, ScrolledEventArgs e)
    {
        if (sender is not ScrollView scrollView)
            return;

        const int scrollToEndThresholdPx = 100;

        var scrollingSpace = scrollView.ContentSize.Height - scrollView.Height;

        if (scrollingSpace - e.ScrollY < scrollToEndThresholdPx)
        {
            if (BindingContext is MainViewModel mv)
                mv.FetchNextPageCommand.Execute(null);
        }

    }

    // ════════════════════════════════════════════════════════════════════════
    //  SEARCH
    // ════════════════════════════════════════════════════════════════════════

    private async void OnAddToQueueClicked(object sender, EventArgs e)
    {
        var popup = new AddSourcePopup(_fetchManager);
        var result = await this.ShowPopupAsync<PlaylistSource>(popup);

        if (result.Result is PlaylistSource source)
        {
            (BindingContext as MainViewModel)?.HandleNewSource(source);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  BOTTOM EXPANDER — Album / Lyrics tabs
    // ════════════════════════════════════════════════════════════════════════

    private void OnExpanderChanged(object sender, CommunityToolkit.Maui.Core.ExpandedChangedEventArgs e)
    {
        ExpandChevron.Text = e.IsExpanded ? "⌄" : "⌃";
        ExpandChevron.Rotation = 0;
    }

    private void OnTabAlbumClicked(object sender, EventArgs e)
        => SwitchTab(albumTab: true);

    private void OnTabLyricsClicked(object sender, EventArgs e)
        => SwitchTab(albumTab: false);

    private void SwitchTab(bool albumTab)
    {
        BtnTabAlbum.SetThemeColorToggle(Button.TextColorProperty, albumTab ,
                "LightPrimary", "DarkPrimary",
                "LightTextSecondary", "DarkTextSecondary");

        BtnTabLyrics.SetThemeColorToggle(Button.TextColorProperty, !albumTab,
                "LightPrimary", "DarkPrimary",
                "LightTextSecondary", "DarkTextSecondary");

        Grid.SetColumn(TabIndicator, albumTab ? 0 : 1);
    }


    // ════════════════════════════════════════════════════════════════════════
    //  MISC
    // ════════════════════════════════════════════════════════════════════════

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("settings");
    }
}

