using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Audio.Eq;
using MauiMixTube.Managers;
using MauiMixTube.Messages;
using OpenTK.Audio.OpenAL;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MauiMixTube.Audio
{
    public sealed class PcmPlayer : IDisposable
    {
        private readonly ALDevice _device;
        private readonly ALContext _context;
        private readonly int _source;
        private readonly int[] _buffers;
        private readonly SettingsManager _settingsManager;
        private AutoEqProcessor? _eqProcessor;
        private const int BufferCount = 4;
        private const int BufferSamples = 4096;     
        private const int SampleRate = 48000;
        private const ALFormat Format = ALFormat.Stereo16;

        private ChunkedAudioStream? _currentStream;
        private float _seekOffsetSeconds;

        private long _totalSamplesPlayed;

        public PcmPlayer(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager;

            WeakReferenceMessenger.Default.Register<EqPresetChangedMessage>(this, (r, m) =>
            {
                SetEqPreset(m.Preset);
            });

            if(settingsManager.AutoEqEnabled && !String.IsNullOrEmpty(settingsManager.DeviceName))
            {
                SetEqPreset(AutoEqDatabase.Get(settingsManager.DeviceName));
            }

            _device = ALC.OpenDevice(null);
            _context = ALC.CreateContext(_device, (int[])null!);
            ALC.MakeContextCurrent(_context);

            _source = AL.GenSource();
            _buffers = AL.GenBuffers(BufferCount);
        }

        public void SetEqPreset(EqPreset? preset)
        {
            _eqProcessor = preset is not null
                ? new AutoEqProcessor(preset, SampleRate)
                : null;
        }

        public async Task StreamAsync(ChunkedAudioStream pcm, CancellationToken ct)
        {
            try
            {
                _totalSamplesPlayed = 0;
                var raw = new byte[BufferSamples * 4];

                AL.SourceStop(_source);
                AL.GetSource(_source, ALGetSourcei.BuffersQueued, out int queued);
                if (queued > 0)
                    AL.SourceUnqueueBuffers(_source, queued);

                var silence = new byte[BufferSamples * 4];
                foreach (var buf in _buffers)
                {
                    AL.BufferData(buf, Format, silence, SampleRate);
                    AL.SourceQueueBuffer(_source, buf);
                }
                AL.SourcePlay(_source);

                foreach (var buf in _buffers)
                {
                    AL.SourceUnqueueBuffer(_source);
                    await FillBufferAsync(pcm, buf, raw, ct);
                    AL.SourceQueueBuffer(_source, buf);
                }

                _currentStream = pcm;

                while (!ct.IsCancellationRequested)
                {
                    AL.GetSource(_source, ALGetSourcei.BuffersProcessed, out int processed);
                    AL.GetSource(_source, ALGetSourcei.BuffersQueued, out int queued2);

                    if (processed == _buffers.Length && queued2 == 0)
                    {
                        AL.SourcePause(_source);
                        WeakReferenceMessenger.Default.Send(new BufferingStartedMessage());

                        while (!ct.IsCancellationRequested)
                        {
                            int read = await pcm.ReadAsync(raw, 0, raw.Length, ct);
                            if (read > 0)
                            {
                                ApplyEq(raw, read);
                                if (read < raw.Length) Array.Clear(raw, read, raw.Length - read);

                                int buf = _buffers[0];
                                AL.BufferData(buf, Format, raw, SampleRate);
                                AL.SourceQueueBuffer(_source, buf);
                                break;
                            }
                            await Task.Delay(50, ct);
                        }

                        AL.SourcePlay(_source);
                        WeakReferenceMessenger.Default.Send(new BufferingEndedMessage());
                    }

                    while (processed-- > 0)
                    {
                        int buf = AL.SourceUnqueueBuffer(_source);
                        _totalSamplesPlayed += BufferSamples;

                        int read = await pcm.ReadAsync(raw, 0, raw.Length, ct);
                        if (read == 0) return;

                        ApplyEq(raw, read);

                        if (read < raw.Length)
                            Array.Clear(raw, read, raw.Length - read);

                        AL.BufferData(buf, Format, raw, SampleRate);
                        AL.SourceQueueBuffer(_source, buf);
                    }

                    await Task.Delay(10, ct);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ApplyEq(byte[] raw, int length)
        {
            if (!_settingsManager.AutoEqEnabled || _eqProcessor is null) return;
            var samples = MemoryMarshal.Cast<byte, short>(raw.AsSpan(0, length));
            _eqProcessor.Process(samples);
        }

        private async Task FillBufferAsync(
            ChunkedAudioStream pcm, int buf, byte[] raw, CancellationToken ct)
        {
            int read = await pcm.ReadAsync(raw, 0, raw.Length, ct);
            if (read < raw.Length) Array.Clear(raw, read, raw.Length - read);
            AL.BufferData(buf, Format, raw, SampleRate);
        }

        public void SetVolume(float v)
        {
            v = Math.Clamp(v, 0f, 1f);
            AL.Source(_source, ALSourcef.Gain, v);
        }

        public async Task SeekAsync(TimeSpan position)
        {
            if (_currentStream is null) return;

            AL.SourceStop(_source);
            AL.GetSource(_source, ALGetSourcei.BuffersQueued, out int queued);
            if (queued > 0) AL.SourceUnqueueBuffers(_source, queued);

            await _currentStream.SeekAsync(position);

            _totalSamplesPlayed = (long)(position.TotalSeconds * SampleRate);
            _seekOffsetSeconds = 0;

            var raw = new byte[BufferSamples * 4];
            foreach (var buf in _buffers)
                await FillBufferAsync(_currentStream, buf, raw, CancellationToken.None);

            AL.SourceQueueBuffers(_source, _buffers);
            AL.SourcePlay(_source);
        }

        public TimeSpan CurrentPosition
        {
            get
            {
                //AL.GetSource(_source, ALGetSourcei.SampleOffset, out int sampleOffset);
                //var totalSamples = _totalSamplesPlayed + sampleOffset + (long)(_seekOffsetSeconds * SampleRate);
                //return TimeSpan.FromSeconds((double)totalSamples / SampleRate);

                AL.GetSource(_source, ALGetSourcei.SampleOffset, out int sampleOffset);
                var totalSamples = _totalSamplesPlayed + sampleOffset;
                return TimeSpan.FromSeconds((double)totalSamples / SampleRate);
            }
        }

        public void Pause()
        {
            AL.SourcePause(_source);
        }

        public void Resume()
        {
            AL.SourcePlay(_source);
        }

        public void Stop()
        {
            AL.SourceStop(_source); 

            AL.GetSource(_source, ALGetSourcei.BuffersQueued, out int queued);
            while (queued-- > 0)
            {
                AL.SourceUnqueueBuffer(_source);
            }
        }

        public void Dispose()
        {
            AL.DeleteSource(_source);
            AL.DeleteBuffers(_buffers);
            ALC.DestroyContext(_context);
            ALC.CloseDevice(_device);
        }
    }
}
