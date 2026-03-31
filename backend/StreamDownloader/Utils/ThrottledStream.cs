namespace StreamDownloader.Utils;

public class ThrottledStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maximumBytesPerSecond;
    private readonly int _maxChunkSize;
    private long _totalBytesRead;
    private long _startTime;
    private readonly object _lock = new();

    public ThrottledStream(Stream innerStream, long maximumBytesPerSecond)
    {
        _innerStream = innerStream ?? throw new ArgumentNullException(nameof(innerStream));
        _maximumBytesPerSecond = maximumBytesPerSecond;
        _maxChunkSize = (int)Math.Min(maximumBytesPerSecond / 4, 1024 * 1024);
        _startTime = DateTime.UtcNow.Ticks;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        return ReadAsync(buffer, offset, count).GetAwaiter().GetResult();
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (_maximumBytesPerSecond <= 0)
        {
            return await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        }

        var bytesToRead = Math.Min(count, _maxChunkSize);
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, bytesToRead, cancellationToken);

        if (bytesRead > 0)
        {
            lock (_lock)
            {
                _totalBytesRead += bytesRead;
            }

            await ThrottleAsync(cancellationToken);
        }

        return bytesRead;
    }

    public override int Read(Span<byte> buffer)
    {
        return ReadAsync(buffer.ToArray(), 0, buffer.Length).GetAwaiter().GetResult();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_maximumBytesPerSecond <= 0)
        {
            return await _innerStream.ReadAsync(buffer, cancellationToken);
        }

        var bytesToRead = Math.Min(buffer.Length, _maxChunkSize);
        var slice = buffer.Slice(0, bytesToRead);
        var bytesRead = await _innerStream.ReadAsync(slice, cancellationToken);

        if (bytesRead > 0)
        {
            lock (_lock)
            {
                _totalBytesRead += bytesRead;
            }

            await ThrottleAsync(cancellationToken);
        }

        return bytesRead;
    }

    private async Task ThrottleAsync(CancellationToken cancellationToken)
    {
        var elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - _startTime).TotalSeconds;
        if (elapsed <= 0) return;

        var currentSpeed = _totalBytesRead / elapsed;
        if (currentSpeed > _maximumBytesPerSecond)
        {
            var expectedTime = _totalBytesRead / (double)_maximumBytesPerSecond;
            var delay = (int)((expectedTime - elapsed) * 1000);
            if (delay > 0)
            {
                await Task.Delay(Math.Min(delay, 100), cancellationToken);
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }

    public override bool CanRead => _innerStream.CanRead;
    public override bool CanSeek => _innerStream.CanSeek;
    public override bool CanWrite => _innerStream.CanWrite;
    public override long Length => _innerStream.Length;
    public override long Position
    {
        get => _innerStream.Position;
        set => _innerStream.Position = value;
    }
    public override void Flush() => _innerStream.Flush();
    public override long Seek(long offset, SeekOrigin origin) => _innerStream.Seek(offset, origin);
    public override void SetLength(long value) => _innerStream.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _innerStream.Write(buffer, offset, count);
}
