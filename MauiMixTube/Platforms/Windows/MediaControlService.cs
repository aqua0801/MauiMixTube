using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Messages;
using MauiMixTube.Models;
using Windows.Media;
using Windows.Storage.Streams;

namespace MauiMixTube.Managers.Media
{
    public class MediaControlsService : IMediaControlsService
    {
        private SystemMediaTransportControls? _smtc;
        private Windows.Media.Playback.MediaPlayer _mediaPlayer;

        public void Initialize()
        {
            _mediaPlayer = new Windows.Media.Playback.MediaPlayer();
            _mediaPlayer.CommandManager.IsEnabled = false;
            _smtc = _mediaPlayer?.SystemMediaTransportControls;

            if(_smtc is null) return;

            _smtc.IsEnabled = true;

            _smtc.IsPlayEnabled = true;
            _smtc.IsPauseEnabled = true;
            _smtc.IsNextEnabled = true;
            _smtc.IsPreviousEnabled = true;
            _smtc.ButtonPressed += OnButtonPressed;
        }

        public void UpdateNowPlaying(AudioInfo? info)
        {
            var updater = _smtc?.DisplayUpdater;

            if (updater is null) return;

            updater.Type = MediaPlaybackType.Music;
            updater.MusicProperties.Title = info?.Title ?? string.Empty;
            updater.MusicProperties.Artist = info?.Artist ?? string.Empty;
            updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(
                new Uri(info?.Fetch.ThumbnailUrl ?? string.Empty));
            updater.Update();
        }

        public void SetPlaybackStatus(bool isPlaying)
            => _smtc?.PlaybackStatus = isPlaying
                ? MediaPlaybackStatus.Playing
                : MediaPlaybackStatus.Paused;

        private void OnButtonPressed(
            SystemMediaTransportControls sender,
            SystemMediaTransportControlsButtonPressedEventArgs e)
        {
            var key = e.Button switch
            {
                SystemMediaTransportControlsButton.Play or
                SystemMediaTransportControlsButton.Pause => MediaKey.PlayPause,
                SystemMediaTransportControlsButton.Next => MediaKey.Next,
                SystemMediaTransportControlsButton.Previous => MediaKey.Previous,
                _ => (MediaKey?)null
            };

            if (key is not null)
                WeakReferenceMessenger.Default.Send(new MediaKeyMessage(key.Value));
        }
    }
}
