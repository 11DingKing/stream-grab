namespace StreamDownloader.Utils;

/// <summary>
/// 下载速度限制器
/// </summary>
public class SpeedLimiter
{
    private readonly long _bytesPerSecond;
    private long _totalBytesRead;
    private DateTime _startTime;
    private readonly object _lock = new();

    public SpeedLimiter(int speedLimitKbPerSecond)
    {
        _bytesPerSecond = speedLimitKbPerSecond * 1024L;
        _startTime = DateTime.Now;
        _totalBytesRead = 0;
    }

    public async Task<int> ReadWithLimitAsync(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        int bytesRead = await stream.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        if (bytesRead > 0 && _bytesPerSecond > 0)
        {
            lock (_lock)
            {
                _totalBytesRead += bytesRead;
                var elapsed = (DateTime.Now - _startTime).TotalSeconds;
                var expectedTime = (double)_totalBytesRead / _bytesPerSecond;

                if (elapsed < expectedTime)
                {
                    var delayMs = (int)((expectedTime - elapsed) * 1000);
                    if (delayMs > 0)
                    {
                        Task.Delay(delayMs, cancellationToken).Wait(cancellationToken);
                    }
                }
            }
        }

        return bytesRead;
    }

    public async Task<byte[]> ReadAllBytesWithLimitAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memoryStream = new MemoryStream();
        var buffer = new byte[81920];
        int bytesRead;

        while ((bytesRead = await ReadWithLimitAsync(stream, buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
        }

        return memoryStream.ToArray();
    }
}
