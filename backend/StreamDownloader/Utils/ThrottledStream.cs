namespace StreamDownloader.Utils;

/// <summary>
/// 带速率控制的节流流
/// </summary>
public class ThrottledStream : Stream
{
    private readonly Stream _innerStream;
    private readonly long _maxBytesPerSecond;
    private readonly object _lock = new();

    private long _totalBytesRead;
    private DateTime _startTime;

    public ThrottledStream(Stream innerStream, long maxBytesPerSecond)
    {
        _innerStream = innerStream;
        _maxBytesPerSecond = maxBytesPerSecond;
        _startTime = DateTime.Now;
        _totalBytesRead = 0;
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

    public override void Flush()
    {
        _innerStream.Flush();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _innerStream.Read(buffer, offset, count);
        Throttle(bytesRead);
        return bytesRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var bytesRead = await _innerStream.ReadAsync(buffer, offset, count, cancellationToken);
        Throttle(bytesRead);
        return bytesRead;
    }

    public override int ReadByte()
    {
        var result = _innerStream.ReadByte();
        if (result != -1)
        {
            Throttle(1);
        }
        return result;
    }

    private void Throttle(int bytesRead)
    {
        if (_maxBytesPerSecond <= 0 || bytesRead <= 0)
        {
            return;
        }

        lock (_lock)
        {
            _totalBytesRead += bytesRead;
            var elapsed = DateTime.Now - _startTime;

            if (elapsed.TotalSeconds > 0)
            {
                var currentRate = _totalBytesRead / elapsed.TotalSeconds;

                if (currentRate > _maxBytesPerSecond)
                {
                    var expectedTime = _totalBytesRead / (double)_maxBytesPerSecond;
                    var delay = TimeSpan.FromSeconds(expectedTime - elapsed.TotalSeconds);

                    if (delay.TotalMilliseconds > 1)
                    {
                        Thread.Sleep(delay);
                    }
                }
            }
        }
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        return _innerStream.Seek(offset, origin);
    }

    public override void SetLength(long value)
    {
        _innerStream.SetLength(value);
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        _innerStream.Write(buffer, offset, count);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _innerStream.Dispose();
        }
        base.Dispose(disposing);
    }
}
