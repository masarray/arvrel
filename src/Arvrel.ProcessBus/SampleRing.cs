namespace Arvrel.ProcessBus;

internal sealed class SampleRing
{
    private readonly double[] _buffer;
    private int _writeIndex;
    private int _count;
    private long _totalSamples;

    public SampleRing(int capacity)
    {
        if (capacity < 2)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _buffer = new double[capacity];
    }

    public int Count => _count;
    public int Capacity => _buffer.Length;

    public void Add(double value)
    {
        _buffer[_writeIndex] = value;
        _writeIndex = (_writeIndex + 1) % _buffer.Length;
        if (_count < _buffer.Length)
            _count++;
        _totalSamples++;
    }

    /// <summary>
    /// Returns the newest samples. Measurement and phasor algorithms use this path.
    /// </summary>
    public double[] Last(int count) => Latest(count);

    public double[] Latest(int count)
    {
        count = Math.Clamp(count, 0, _count);
        if (count == 0)
            return Array.Empty<double>();
        return Copy(Wrap(_writeIndex - count), count);
    }

    /// <summary>
    /// Returns the newest complete display window. Partial samples from the next
    /// window do not move the oscilloscope horizontally.
    /// </summary>
    public double[] DisplayWindow(int count)
    {
        count = Math.Clamp(count, 0, _count);
        if (count == 0)
            return Array.Empty<double>();

        var lagToCompletedWindow = _count >= count
            ? (int)(_totalSamples % count)
            : 0;
        var start = Wrap(_writeIndex - lagToCompletedWindow - count);
        return Copy(start, count);
    }

    public double RmsLast(int count)
    {
        count = Math.Clamp(count, 0, _count);
        if (count == 0)
            return 0;

        var start = Wrap(_writeIndex - count);
        var sum = 0.0;
        for (var index = 0; index < count; index++)
        {
            var value = _buffer[(start + index) % _buffer.Length];
            sum += value * value;
        }
        return Math.Sqrt(sum / count);
    }

    public void Clear()
    {
        Array.Clear(_buffer);
        _writeIndex = 0;
        _count = 0;
        _totalSamples = 0;
    }

    private double[] Copy(int start, int count)
    {
        var result = new double[count];
        for (var index = 0; index < count; index++)
            result[index] = _buffer[(start + index) % _buffer.Length];
        return result;
    }

    private int Wrap(int index)
    {
        index %= _buffer.Length;
        return index < 0 ? index + _buffer.Length : index;
    }
}
