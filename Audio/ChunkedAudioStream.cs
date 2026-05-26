using MauiMixTube.Helpers;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using MauiMixTube.Models;

namespace MauiMixTube.Audio
{
    public sealed class ChunkedAudioStream : Stream
    {
        private const int ChunkSize = 4 * 1024 * 1024; 
        private const int BytesPerSecond = 48000 * 2 * 2;   

        private sealed class Chunk : IDisposable
        {
            public readonly byte[] Buffer;
            public int Length;
            public readonly TaskCompletionSource<bool> Ready =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Chunk() => Buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);

            public void Dispose() => ArrayPool<byte>.Shared.Return(Buffer);
        }

        private readonly ConcurrentDictionary<int, Chunk> _chunks = new();

        private long _position;
        private int _currentChunkIndex;

        private Process? _process;
        private FileStream? _cachedStream;
        private CancellationTokenSource _cts = new();
        private Task? _fillTask;

        private readonly AudioInfo _info;
        private bool _isEndOfStream;

        public event Action<AudioInfo>? OnCacheReady;

        public ChunkedAudioStream(AudioInfo info )
        {
            _info = info;
            StartFeed(seekSeconds: 0);
        }

        /// Starts the appropriate feed source from seekSeconds.
        /// Always fire-and-forget; ReadAsync waits on chunk.Ready.
        private void StartFeed(double seekSeconds)
        {
            StartFfmpeg(seekSeconds);
            _fillTask = Task.Run(() =>
                FillFromStreamAsync(_process!.StandardOutput.BaseStream, _cts.Token));
        }

        private void StartFfmpeg(double seekSeconds)
        {
            _process?.Kill(entireProcessTree: true);
            _process?.Dispose();
            _process = null;

            var args = _info.Fetch.GetCacheArgsOrDefault(seekSeconds);

            var startInfo = new ProcessStartInfo
            {
                FileName = AppPaths.FfmpegBinary,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            _process = Process.Start(startInfo)
                ?? throw new InvalidOperationException(
                    $"FFmpeg failed to start : {AppPaths.FfmpegBinary}");

            _process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    Console.WriteLine($"[FFmpeg] {e.Data}");
            };
            _process.BeginErrorReadLine();
        }


        private async Task FillFromStreamAsync(Stream source, CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            int offset = 0;
            int chunkIndex = _currentChunkIndex;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int read = await source.ReadAsync(
                        buffer.AsMemory(offset, ChunkSize - offset), ct);

                    if (read <= 0) break;

                    offset += read;

                    if (offset >= ChunkSize)
                    {
                        PushToChunk(buffer.AsSpan(0, ChunkSize), _currentChunkIndex++);
                        offset = 0;
                    }
                }

                if (offset > 0 && !ct.IsCancellationRequested)
                {
                    PushToChunk(buffer.AsSpan(0, offset), _currentChunkIndex++);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);

                if (!ct.IsCancellationRequested && _process is not null)
                {
                    await _process.WaitForExitAsync(ct);

                    if (_process.ExitCode == 0)
                        OnCacheReady?.Invoke(_info);
                    else
                        Console.WriteLine($"[FFmpeg] exited with code {_process.ExitCode}, cache not saved");
                }

                _isEndOfStream = true;

                foreach (var chunk in _chunks.Values)
                    chunk.Ready.TrySetResult(false);
            }
        }

        private void PushToChunk(ReadOnlySpan<byte> data, int index)
        {
            var chunk = GetOrCreateChunk(index);
            data.CopyTo(chunk.Buffer);
            chunk.Length = data.Length;
            chunk.Ready.TrySetResult(true);
        }

        public async Task SeekAsync(TimeSpan time)
        {
            // Cancel current fill and wait for it to exit cleanly
            _cts.Cancel();
            if (_fillTask is not null)
            {
                try { await _fillTask; }
                catch { /* ignore cancellation */ }
            }

            _cts = new CancellationTokenSource();
            _position = (long)(time.TotalSeconds * BytesPerSecond);
            _currentChunkIndex = (int)(_position / ChunkSize);

            ClearChunks();
            _isEndOfStream = false;
            StartFeed(time.TotalSeconds);
        }


        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalRead = 0;

            while (buffer.Length > 0)
            {
                int chunkIndex = (int)(_position / ChunkSize);
                int offsetInChunk = (int)(_position % ChunkSize);

                var chunk = GetOrCreateChunk(chunkIndex);

                if (!chunk.Ready.Task.IsCompleted)
                {
                    if (_isEndOfStream) break;

                    try
                    {
                        await chunk.Ready.Task.WaitAsync(cancellationToken);
                    }
                    catch (OperationCanceledException) { break; }
                }

                int available = chunk.Length - offsetInChunk;

                if (available <= 0) break; // end of stream

                int toCopy = Math.Min(buffer.Length, available);
                chunk.Buffer.AsSpan(offsetInChunk, toCopy).CopyTo(buffer.Span);

                buffer = buffer[toCopy..];
                _position += toCopy;
                totalRead += toCopy;

                if (offsetInChunk + toCopy >= chunk.Length)
                {
                    if (_chunks.TryRemove(chunkIndex, out var consumed))
                        consumed.Dispose();
                }
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
            => ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();

        private Chunk GetOrCreateChunk(int index)
            => _chunks.GetOrAdd(index, _ => new Chunk());

        private void ClearChunks()
        {
            foreach (var chunk in _chunks.Values)
                chunk.Dispose();
            _chunks.Clear();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (!_cts.IsCancellationRequested)
                        _cts.Cancel();
                }
                catch (ObjectDisposedException) { }

                ClearChunks();

                try { _process?.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FFmpeg] Kill failed: {ex.Message}");
                }

                _process?.Dispose();
                _cachedStream?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}