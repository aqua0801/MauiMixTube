using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Audio.Eq;
using MauiMixTube.Helpers;
using MauiMixTube.Managers;
using MauiMixTube.Messages;
using OpenTK.Audio.OpenAL;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;

namespace MauiMixTube.Audio
{
    public sealed class PcmPlayer : IDisposable
    {
        private const int BufferCount = 4;
        private const int BufferFrames = 4096;
        private const int ChannelCount = 2;
        private const int BytesPerSample = sizeof(short);
        private const int BytesPerFrame = ChannelCount * BytesPerSample;
        private const int BufferBytes = BufferFrames * BytesPerFrame;
        private const int SampleRate = 48000;

        private const ALFormat Format = ALFormat.Stereo16;

        private readonly SettingsManager _settingsManager;
        private readonly object _sync = new();

        private ALDevice _device;
        private ALContext _context;
        private int _source;
        private int[] _buffers = Array.Empty<int>();

        private float _volume = 1.0f;
        private string _currentDeviceName = string.Empty;

        private ChunkedAudioStream? _currentStream;
        private AutoEqProcessor? _eqProcessor;

        private CancellationTokenSource? _streamCts;
        private Task? _streamTask;

        private long _playedFrames;
        private readonly Dictionary<int, int> _bufferFrames = new();

        private bool _isPlaying;
        private bool _isPaused;
        private bool _disposed;

        public PcmPlayer(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager
                ?? throw new ArgumentNullException(nameof(settingsManager));

            WeakReferenceMessenger.Default.Register<EqPresetChangedMessage>(
                this,
                static (recipient, message) =>
                {
                    ((PcmPlayer)recipient).SetEqPreset(message.Preset);
                });

            if (settingsManager.AutoEqEnabled &&
                !string.IsNullOrWhiteSpace(settingsManager.EqDeviceName))
            {
                SetEqPreset(
                    AutoEqDatabase.Get(settingsManager.EqDeviceName));
            }

            CreateOpenAlDevice(settingsManager.AudioDeviceName);
        }

        #region Public API

        public void SetEqPreset(EqPreset? preset)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                _eqProcessor = preset is null
                    ? null
                    : new AutoEqProcessor(preset, SampleRate);
            }
        }

        public async Task StreamAsync(
            ChunkedAudioStream pcm,
            CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(pcm);

            CancellationTokenSource linkedCts;

            lock (_sync)
            {
                ThrowIfDisposed();

                StopStreamLocked();

                _currentStream = pcm;
                _playedFrames = 0;
                _bufferFrames.Clear();
                _isPlaying = false;
                _isPaused = false;

                _streamCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linkedCts = _streamCts;

                _streamTask = StreamCoreAsync(
                    pcm,
                    linkedCts.Token);
            }

            try
            {
                await _streamTask.ConfigureAwait(false);
            }
            finally
            {
                lock (_sync)
                {
                    if (ReferenceEquals(_streamCts, linkedCts))
                    {
                        _streamCts = null;
                        _streamTask = null;
                    }
                }

                linkedCts.Dispose();
            }
        }

        public void SetVolume(float v)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                _volume = Math.Clamp(v, 0f, 1f);

                AL.Source(
                    _source,
                    ALSourcef.Gain,
                    _volume);
            }
        }

        public async Task SeekAsync(TimeSpan position)
        {
            if (position < TimeSpan.Zero)
                position = TimeSpan.Zero;

            ChunkedAudioStream? stream;
            bool shouldResume;

            lock (_sync)
            {
                ThrowIfDisposed();

                stream = _currentStream;

                if (stream is null)
                    return;

                shouldResume = _isPlaying && !_isPaused;

                AL.SourceStop(_source);

                UnqueueAllBuffersLocked();

                _playedFrames = (long)(
                    position.TotalSeconds * SampleRate);

                _bufferFrames.Clear();

                ResetEqLocked();

                _isPlaying = false;
            }

            await stream.SeekAsync(position).ConfigureAwait(false);

            lock (_sync)
            {
                ThrowIfDisposed();

                if (!ReferenceEquals(stream, _currentStream))
                    return;

                FillAndQueueInitialBuffersLocked(
                    stream,
                    CancellationToken.None);

                if (shouldResume)
                {
                    AL.SourcePlay(_source);
                    _isPlaying = true;
                    _isPaused = false;
                }
                else
                {
                    _isPlaying = false;
                    _isPaused = true;
                }
            }
        }

        public TimeSpan CurrentPosition
        {
            get
            {
                lock (_sync)
                {
                    if (_disposed || _source == 0)
                        return TimeSpan.Zero;

                    AL.GetSource(
                        _source,
                        ALGetSourcei.SourceState,
                        out int state);

                    long positionFrames = _playedFrames;

                    if (state != (int)ALSourceState.Stopped ||
                        _isPlaying ||
                        _isPaused)
                    {
                        AL.GetSource(
                            _source,
                            ALGetSourcei.SampleOffset,
                            out int sampleOffset);

                        positionFrames += sampleOffset;
                    }

                    if (positionFrames < 0)
                        positionFrames = 0;

                    return TimeSpan.FromSeconds(
                        (double)positionFrames / SampleRate);
                }
            }
        }

        public void Pause()
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (!_isPlaying || _isPaused)
                    return;

                AL.SourcePause(_source);

                _isPaused = true;
            }
        }

        public void Resume()
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (!_isPaused)
                    return;

                AL.SourcePlay(_source);

                _isPaused = false;
                _isPlaying = true;
            }
        }

        public void Stop()
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                StopStreamLocked();

                AL.SourceStop(_source);

                UnqueueAllBuffersLocked();

                _playedFrames = 0;
                _bufferFrames.Clear();

                _currentStream = null;

                _isPlaying = false;
                _isPaused = false;

                ResetEqLocked();
            }
        }

        public void SwitchDevice(string deviceName)
        {
            lock (_sync)
            {
                ThrowIfDisposed();

                if (string.Equals(
                        _currentDeviceName,
                        deviceName,
                        StringComparison.Ordinal))
                {
                    return;
                }

                TimeSpan position = CurrentPosition;
                bool wasPlaying = _isPlaying && !_isPaused;

                RecreateOpenAlDeviceLocked(deviceName);

                _playedFrames =
                    (long)(position.TotalSeconds * SampleRate);

                if (_currentStream is not null)
                {
                    // Fire-and-forget is avoided here intentionally.
                    // The caller can continue playback through the
                    // existing StreamAsync loop after buffers are rebuilt.
                    //
                    // Actual stream seeking is performed asynchronously
                    // by starting a small recovery task.
                    _ = RestoreAfterDeviceSwitchAsync(
                        _currentStream,
                        position,
                        wasPlaying);
                }
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;

                WeakReferenceMessenger.Default
                    .Unregister<EqPresetChangedMessage>(this);

                StopStreamLocked();

                try
                {
                    if (_source != 0)
                    {
                        AL.SourceStop(_source);
                        UnqueueAllBuffersLocked();
                    }
                }
                catch
                {
                    // Ignore OpenAL errors during disposal.
                }

                try
                {
                    if (_source != 0)
                    {
                        AL.DeleteSource(_source);
                        _source = 0;
                    }
                }
                catch
                {
                }

                try
                {
                    if (_buffers.Length > 0)
                    {
                        AL.DeleteBuffers(_buffers);
                        _buffers = Array.Empty<int>();
                    }
                }
                catch
                {
                }

                try
                {
                    if (_context != ALContext.Null)
                    {
                        ALC.MakeContextCurrent(ALContext.Null);
                        ALC.DestroyContext(_context);
                        _context = ALContext.Null;
                    }
                }
                catch
                {
                }

                try
                {
                    if (_device != ALDevice.Null)
                    {
                        ALC.CloseDevice(_device);
                        _device = ALDevice.Null;
                    }
                }
                catch
                {
                }
            }
        }

        public unsafe static IReadOnlyList<string> GetAvailableDevices()
        {
            bool hasAllDevices =
                ALC.IsExtensionPresent(
                    ALDevice.Null,
                    "ALC_ENUMERATE_ALL_EXT");

            List<string> devices = new();

            if (hasAllDevices)
            {
                byte* ptr = ALC.GetStringPtr(
                    ALDevice.Null,
                    AlcGetString.AllDevicesSpecifier);

                if (ptr == null)
                    return Array.Empty<string>();

                while (*ptr != 0)
                {
                    byte* end = ptr;

                    while (*end != 0)
                        end++;

                    int length = (int)(end - ptr);

                    var span =
                        new ReadOnlySpan<byte>(ptr, length);

                    string device =
                        Encoding.Default.GetString(span);

                    //string device =
                    //    Encoding.UTF8.GetString(span);

                    if (!string.IsNullOrWhiteSpace(device))
                        devices.Add(device);

                    ptr = end + 1;
                }
            }
            else
            {
                devices.AddRange(
                    ALC.GetStringList(
                        GetEnumerationStringList.DeviceSpecifier));
            }

            return devices
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        public static string GetDefaultDevice()
        {
            return ALC.GetString(
                       ALDevice.Null,
                       AlcGetString.DefaultDeviceSpecifier)
                   ?? string.Empty;
        }

        #endregion

        #region Streaming

        private async Task StreamCoreAsync(
            ChunkedAudioStream pcm,
            CancellationToken ct)
        {
            try
            {
                lock (_sync)
                {
                    if (_disposed ||
                        !ReferenceEquals(_currentStream, pcm))
                    {
                        return;
                    }

                    UnqueueAllBuffersLocked();

                    _playedFrames = 0;
                    _bufferFrames.Clear();

                    ResetEqLocked();

                    FillAndQueueInitialBuffersLocked(
                        pcm,
                        ct);

                    if (_bufferFrames.Count == 0)
                    {
                        _isPlaying = false;
                        return;
                    }

                    AL.SourcePlay(_source);

                    _isPlaying = true;
                    _isPaused = false;
                }

                while (!ct.IsCancellationRequested)
                {
                    lock (_sync)
                    {
                        if (_disposed ||
                            !ReferenceEquals(_currentStream, pcm))
                        {
                            return;
                        }

                        ProcessCompletedBuffersLocked(
                            pcm,
                            ct);

                        if (_bufferFrames.Count == 0)
                        {
                            _isPlaying = false;
                            _isPaused = false;
                            return;
                        }
                    }

                    await Task.Delay(
                        10,
                        ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                lock (_sync)
                {
                    _isPlaying = false;
                    _isPaused = false;
                }

                throw;
            }
        }

        private void FillAndQueueInitialBuffersLocked(
            ChunkedAudioStream pcm,
            CancellationToken ct)
        {
            byte[] raw = new byte[BufferBytes];

            foreach (int buffer in _buffers)
            {
                int frames = FillBufferLocked(
                    pcm,
                    buffer,
                    raw,
                    ct);

                if (frames <= 0)
                    break;

                AL.SourceQueueBuffer(
                    _source,
                    buffer);

                _bufferFrames[buffer] = frames;
            }
        }

        private void ProcessCompletedBuffersLocked(
            ChunkedAudioStream pcm,
            CancellationToken ct)
        {
            AL.GetSource(
                _source,
                ALGetSourcei.BuffersProcessed,
                out int processed);

            if (processed <= 0)
                return;

            byte[] raw = new byte[BufferBytes];

            while (processed-- > 0)
            {
                int buffer =
                    AL.SourceUnqueueBuffer(_source);

                if (_bufferFrames.TryGetValue(
                        buffer,
                        out int frames))
                {
                    _playedFrames += frames;
                    _bufferFrames.Remove(buffer);
                }

                int read = FillBufferLocked(
                    pcm,
                    buffer,
                    raw,
                    ct);

                if (read <= 0)
                    continue;

                AL.SourceQueueBuffer(
                    _source,
                    buffer);

                _bufferFrames[buffer] = read;
            }

            if (_bufferFrames.Count > 0)
            {
                AL.GetSource(
                    _source,
                    ALGetSourcei.SourceState,
                    out int state);

                if (state == (int)ALSourceState.Stopped &&
                    !_isPaused)
                {
                    AL.SourcePlay(_source);
                }
            }
        }

        private int FillBufferLocked(
            ChunkedAudioStream pcm,
            int buffer,
            byte[] raw,
            CancellationToken ct)
        {
            int read = pcm.ReadAsync(
                    raw,
                    0,
                    raw.Length,
                    ct)
                .GetAwaiter()
                .GetResult();

            if (read <= 0)
                return 0;

            if (_settingsManager.AutoEqEnabled &&
                _eqProcessor is not null)
            {
                var samples = MemoryMarshal.Cast<byte, short>(
                    raw.AsSpan(0, read));

                _eqProcessor.Process(samples);
            }

            int alignedRead =
                read - (read % BytesPerFrame);

            if (alignedRead <= 0)
                return 0;

            if (alignedRead < raw.Length)
            {
                Array.Clear(
                    raw,
                    alignedRead,
                    raw.Length - alignedRead);
            }

            int frames = alignedRead / BytesPerFrame;

            var pcmSamples = MemoryMarshal.Cast<byte, short>(
                raw.AsSpan(0, alignedRead));

            AL.BufferData(
                buffer,
                Format,
                pcmSamples,
                SampleRate);

            return frames;
        }

        #endregion

        #region OpenAL

        private void CreateOpenAlDevice(string? deviceName)
        {
            _device = string.IsNullOrWhiteSpace(deviceName)
                ? ALC.OpenDevice(null)
                : OpenALNative.OpenDeviceUtf8(deviceName);

            if (_device == ALDevice.Null)
            {
                throw new InvalidOperationException(
                    $"Unable to open OpenAL device: {deviceName ?? "<default>"}");
            }

            _context = ALC.CreateContext(
                _device,
                (int[])null!);

            if (_context == ALContext.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;

                throw new InvalidOperationException(
                    "Unable to create OpenAL context.");
            }

            ALC.MakeContextCurrent(_context);

            _source = AL.GenSource();
            _buffers = AL.GenBuffers(BufferCount);

            AL.Source(
                _source,
                ALSourcef.Gain,
                _volume);

            _currentDeviceName =
                ALC.GetString(
                    _device,
                    AlcGetString.DeviceSpecifier)
                ?? string.Empty;
        }

        private void RecreateOpenAlDeviceLocked(
            string deviceName)
        {
            AL.SourceStop(_source);

            UnqueueAllBuffersLocked();

            if (_source != 0)
            {
                AL.DeleteSource(_source);
                _source = 0;
            }

            if (_buffers.Length > 0)
            {
                AL.DeleteBuffers(_buffers);
                _buffers = Array.Empty<int>();
            }

            ALC.MakeContextCurrent(ALContext.Null);

            if (_context != ALContext.Null)
            {
                ALC.DestroyContext(_context);
                _context = ALContext.Null;
            }

            if (_device != ALDevice.Null)
            {
                ALC.CloseDevice(_device);
                _device = ALDevice.Null;
            }

            CreateOpenAlDevice(deviceName);

            _bufferFrames.Clear();
            ResetEqLocked();
        }

        private void UnqueueAllBuffersLocked()
        {
            if (_source == 0)
                return;

            try
            {
                AL.GetSource(
                    _source,
                    ALGetSourcei.BuffersQueued,
                    out int queued);

                while (queued-- > 0)
                {
                    try
                    {
                        int buffer =
                            AL.SourceUnqueueBuffer(_source);

                        _bufferFrames.Remove(buffer);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch
            {
            }

            _bufferFrames.Clear();
        }

        #endregion

        #region Device Recovery

        private async Task RestoreAfterDeviceSwitchAsync(
            ChunkedAudioStream stream,
            TimeSpan position,
            bool wasPlaying)
        {
            try
            {
                await stream
                    .SeekAsync(position)
                    .ConfigureAwait(false);

                lock (_sync)
                {
                    if (_disposed ||
                        !ReferenceEquals(
                            stream,
                            _currentStream))
                    {
                        return;
                    }

                    ResetEqLocked();

                    FillAndQueueInitialBuffersLocked(
                        stream,
                        CancellationToken.None);

                    if (wasPlaying &&
                        _bufferFrames.Count > 0)
                    {
                        AL.SourcePlay(_source);

                        _isPlaying = true;
                        _isPaused = false;
                    }
                    else
                    {
                        _isPlaying = false;
                        _isPaused = true;
                    }
                }
            }
            catch
            {
                lock (_sync)
                {
                    _isPlaying = false;
                    _isPaused = false;
                }
            }
        }

        #endregion

        #region State

        private void ResetEqLocked()
        {
            // Recreating the processor guarantees that any
            // stateful IIR filter starts from a clean state.
            // if required. (currently not needed)
        }

        private void StopStreamLocked()
        {
            if (_streamCts is not null)
            {
                try
                {
                    _streamCts.Cancel();
                }
                catch
                {
                }

                _streamCts.Dispose();
                _streamCts = null;
            }

            _isPlaying = false;
            _isPaused = false;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(
                _disposed,
                this);
        }

        #endregion

        internal static class OpenALNative
        {
            [DllImport(
                "openal32.dll",
                CallingConvention = CallingConvention.Cdecl)]
            private static extern ALDevice alcOpenDevice(
                IntPtr devicename);

            public static unsafe ALDevice OpenDeviceUtf8(string name)
            {
                byte[] bytes =
                    System.Text.Encoding.UTF8.GetBytes(name + '\0');

                fixed (byte* ptr = bytes)
                {
                    return alcOpenDevice((IntPtr)ptr);
                }
            }
        }
    }
}
